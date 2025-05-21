using System.Text.Json;
using Buildalyzer;
using System.Linq;
using Microsoft.CodeAnalysis; // Added for AdhocWorkspace, SolutionInfo, Project, etc.
using Microsoft.Build.Logging.StructuredLogger; // Required for AnalyzerManager with solution (might be indirect via Buildalyzer)
using Microsoft.CodeAnalysis.CSharp; // Potentially for CSharpCompilationOptions if needed later
using System.Collections.Concurrent; // For ConcurrentBag if adapting GetAnalysisContext closely
using System.Collections.Generic; // For List<T>
using System.IO; // For Path operations
using Buildalyzer.Workspaces; // Added for AddToWorkspace

// Domain and Analysis namespaces will be needed once we integrate Extractor and Triple generation
using PrecisionApi.Domain;
using PrecisionApi.Analysis;

namespace PrecisionApi.Services;

// Helper class to hold the Roslyn workspace and project analysis results
public class RoslynAnalysisContext : IDisposable
{
    public AdhocWorkspace Workspace { get; }
    public List<(Microsoft.CodeAnalysis.Project RoslynProject, IAnalyzerResult BuildalyzerResult)> Projects { get; }

    public RoslynAnalysisContext(AdhocWorkspace workspace, List<(Microsoft.CodeAnalysis.Project, IAnalyzerResult)> projects)
    {
        Workspace = workspace;
        Projects = projects;
    }

    public void Dispose()
    {
        Workspace?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class AnalysisService
{
    public async Task<JsonDocument> AnalyzeCodebaseAsync(string extractedCodebasePath)
    {
        List<string> projectFilePaths = new();
        string? solutionFilePath = null;

        // 1. Scan extractedCodebasePath for .sln or .csproj files.
        try
        {
            var solutionFiles = Directory.GetFiles(extractedCodebasePath, "*.sln", SearchOption.AllDirectories);
            if (solutionFiles.Any())
            {
                solutionFilePath = solutionFiles.OrderBy(f => f.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar))
                                                .ThenBy(f => f)
                                                .First();
                Console.WriteLine($"Found solution file: {solutionFilePath}");
            }
            else
            {
                var csharpProjectFiles = Directory.GetFiles(extractedCodebasePath, "*.csproj", SearchOption.AllDirectories);
                if (csharpProjectFiles.Any())
                {
                    projectFilePaths.AddRange(csharpProjectFiles);
                    Console.WriteLine($"Found project files: {string.Join(", ", projectFilePaths)}"); 
                }
                else
                {
                    return JsonDocument.Parse(JsonSerializer.Serialize(new { message = "No .sln or .csproj files found." }));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during file discovery: {ex.Message}");
            return JsonDocument.Parse(JsonSerializer.Serialize(new { message = $"Error during file discovery: {ex.Message}", error = ex.ToString() }));
        }

        // 2. Initialize Buildalyzer.AnalyzerManager & 3. Get Roslyn workspace.
        IAnalyzerManager analyzerManager;
        if (!string.IsNullOrEmpty(solutionFilePath))
        {
            analyzerManager = new AnalyzerManager(solutionFilePath);
        }
        else
        {
            analyzerManager = new AnalyzerManager(); 
            // If using an empty manager, projects need to be added individually if that's the intended workflow
            // For now, assuming if projectFilePaths is populated, they will be used directly by GetRoslynAnalysisContext
        }

        RoslynAnalysisContext? roslynContext = null;
        try
        {
            // If solutionFilePath is null, pass projectFilePaths to GetRoslynAnalysisContext
            roslynContext = GetRoslynAnalysisContext(analyzerManager, string.IsNullOrEmpty(solutionFilePath) ? projectFilePaths : null);
            if (roslynContext == null || !roslynContext.Projects.Any())
            {
                return JsonDocument.Parse(JsonSerializer.Serialize(new { message = "Failed to analyze projects or no projects found for analysis." }));
            }
            Console.WriteLine($"Roslyn context created with {roslynContext.Projects.Count} project(s).");

            // ADDED: Triple collection logic starts here
            var allTriples = new List<Triple>();
            Console.WriteLine($"Starting project analysis for {roslynContext.Projects.Count} project(s).");
            foreach (var (roslynProject, buildalyzerResult) in roslynContext.Projects)
            {
                Console.WriteLine($"Analyzing project: {roslynProject.Name} at {roslynProject.FilePath}");
                if (string.IsNullOrEmpty(roslynProject.FilePath))
                {
                    Console.WriteLine($"Skipping project with no FilePath: {roslynProject.Name}");
                    continue;
                }

                var projectRootPath = Path.GetDirectoryName(roslynProject.FilePath);
                if (string.IsNullOrEmpty(projectRootPath))
                {
                    Console.WriteLine($"Skipping project with invalid FilePath (no directory): {roslynProject.FilePath}");
                    continue;
                }

                var projectFolderNode = new FolderNode 
                {
                    FullName = projectRootPath, 
                    Name = Path.GetFileName(projectRootPath) ?? projectRootPath 
                };
                projectFolderNode.SetPrimaryKey(); // Call base SetPrimaryKey after properties are set

                var compilation = await roslynProject.GetCompilationAsync();
                if (compilation == null)
                {
                    Console.WriteLine($"Could not get compilation for project: {roslynProject.Name}");
                    continue;
                }

                Console.WriteLine($"Got compilation for {roslynProject.Name}. Analyzing {compilation.SyntaxTrees.Count()} syntax trees.");
                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    var semanticModel = compilation.GetSemanticModel(syntaxTree);
                    try
                    {
                        Extractor.AnalyzeTree(allTriples, syntaxTree, semanticModel, projectFolderNode);
                        Console.WriteLine($"Analyzed syntax tree: {syntaxTree.FilePath ?? "in-memory tree"} for project {roslynProject.Name}. Current triples count: {allTriples.Count}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error analyzing syntax tree {syntaxTree.FilePath ?? "in-memory tree"} in project {roslynProject.Name}: {ex.Message}\n{ex.StackTrace}");
                        // Continue with the next syntax tree
                    }
                }
            }
            Console.WriteLine($"Finished project analysis. Total triples collected: {allTriples.Count}");
            // ADDED: Triple collection logic ends here

            // Deduplicate Triples
            var deduplicatedTriples = DeduplicateTriples(allTriples);
            Console.WriteLine($"Triples count after deduplication: {deduplicatedTriples.Count}");

            // Transform Triples to JSON structure
            var (nodes, edges) = TransformTriplesToJsonObjects(deduplicatedTriples);
            Console.WriteLine($"Transformed to {nodes.Count} nodes and {edges.Count} edges.");

            await System.Threading.Tasks.Task.Delay(100); // Qualified Task.Delay

            var finalResult = new 
            {
                message = "Analysis complete. Triples extracted, deduplicated, and transformed.", // Updated message
                sourcePath = extractedCodebasePath,
                discoveredSolution = solutionFilePath,
                discoveredProjects = projectFilePaths,
                analyzedProjectCount = roslynContext?.Projects.Count ?? 0,
                collectedTriplesCount = allTriples.Count, 
                deduplicatedTriplesCount = deduplicatedTriples.Count, 
                nodes = nodes, // Use transformed nodes
                edges = edges  // Use transformed edges
            };
            
            return JsonDocument.Parse(JsonSerializer.Serialize(finalResult)); // Use finalResult
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating Roslyn context: {ex.Message}");
            return JsonDocument.Parse(JsonSerializer.Serialize(new { message = $"Error initializing Roslyn analysis: {ex.Message}", error = ex.ToString() }));
        }
        finally
        {
            roslynContext?.Dispose(); // Ensure workspace is disposed
        }
    }

    // Adapted from Strazh.Analysis.Analyzer.GetAnalysisContext
    private RoslynAnalysisContext? GetRoslynAnalysisContext(IAnalyzerManager manager, List<string>? projectPathsOverride = null)
    {
        if (manager == null) throw new ArgumentNullException(nameof(manager));

        var projectResults = new ConcurrentBag<(Microsoft.CodeAnalysis.Project, IAnalyzerResult)>();
        AdhocWorkspace workspace = new AdhocWorkspace();

        List<IAnalyzerResult> buildalyzerResults;

        if (projectPathsOverride != null && projectPathsOverride.Any())
        {
            // Analyze specified project files if no solution was found/used
            buildalyzerResults = new List<IAnalyzerResult>();
            foreach (var projectPath in projectPathsOverride)
            {
                Console.WriteLine($"Building project (override): {Path.GetFileName(projectPath)} - starting");
                var projectAnalyzer = manager.GetProject(projectPath);
                var buildResult = projectAnalyzer.Build().FirstOrDefault(r => r.Succeeded);
                if (buildResult != null)
                {
                    buildalyzerResults.Add(buildResult);
                    Console.WriteLine($"Building project (override): {Path.GetFileName(projectPath)} - finished (succeeded)");
                }
                else
                {
                    Console.WriteLine($"Building project (override): {Path.GetFileName(projectPath)} - finished (failed or no results)");
                }
            }
        }
        else if (!string.IsNullOrEmpty(manager.SolutionFilePath))
        {
            // Analyze all projects in the solution
            Console.WriteLine("Building projects from solution - starting");
            buildalyzerResults = manager.Projects.Values
                .Select(p =>
                {
                    Console.WriteLine($"Building project (solution): {p.ProjectFile.Name} - starting");
                    var result = p.Build().FirstOrDefault(); // Build() can return multiple results (e.g. for different targets)
                    Console.WriteLine($"Building project (solution): {p.ProjectFile.Name} - finished {(result == null ? "(failed)" : "(succeeded)")}");
                    return result;
                })
                .Where(x => x != null && x.Succeeded)
                .ToList();
            Console.WriteLine($"Building projects from solution - finished. {buildalyzerResults.Count} succeeded.");

            SolutionInfo solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default, manager.SolutionFilePath);
            workspace.AddSolution(solutionInfo);
            // Sort projects by order in solution file for consistency if needed, Buildalyzer might do this.
            // For simplicity, we'll rely on the order Buildalyzer provides or Roslyn's workspace handles.
        }
        else
        {
             Console.WriteLine("No solution or project overrides provided to GetRoslynAnalysisContext.");
            return null; // No projects to analyze
        }

        if (!buildalyzerResults.Any())
        {
            Console.WriteLine("No projects successfully built by Buildalyzer.");
            workspace.Dispose();
            return null;
        }

        // Add each built project to the workspace. If the project (by FilePath) is already present (e.g., added implicitly
        // when another project with a reference was loaded), reuse the existing instance instead of attempting to add it
        // again which would throw an exception.

        var processedProjectPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (IAnalyzerResult result in buildalyzerResults)
        {
            if (!processedProjectPaths.Add(result.ProjectFilePath))
            {
                // We already processed this path in a previous iteration.
                continue;
            }

            Microsoft.CodeAnalysis.Project? roslynProject = null;
            try
            {
                roslynProject = result.AddToWorkspace(workspace, true);
                Console.WriteLine($"Added project to Roslyn workspace: {roslynProject.Name}");
            }
            catch (Exception addEx)
            {
                // Adding failed (likely because the project already exists). Attempt to retrieve the existing project.
                roslynProject = workspace.CurrentSolution.Projects
                                       .FirstOrDefault(p => string.Equals(p.FilePath, result.ProjectFilePath, StringComparison.OrdinalIgnoreCase));

                if (roslynProject != null)
                {
                    Console.WriteLine($"Project already existed in workspace, using existing instance: {roslynProject.Name} ({result.ProjectFilePath})");
                }
                else
                {
                    Console.WriteLine($"Failed to add project {result.ProjectFilePath} and could not find existing instance: {addEx.Message}");
                    continue; // Skip this project entirely.
                }
            }

            projectResults.Add((roslynProject, result));
        }
        
        if (!projectResults.Any())
        {
            Console.WriteLine("No projects were added to the Roslyn workspace.");
            workspace.Dispose();
            return null;
        }

        return new RoslynAnalysisContext(workspace, projectResults.ToList());
    }

    private List<Triple> DeduplicateTriples(List<Triple> triples)
    {
        var uniqueTriples = new List<Triple>();
        var seenKeys = new HashSet<string>();

        if (triples == null) return uniqueTriples;

        foreach (var triple in triples)
        {
            if (triple?.NodeA?.Pk == null || triple?.NodeB?.Pk == null || string.IsNullOrEmpty(triple?.Relationship?.Type))
            {
                Console.WriteLine($"Skipping triple due to missing Pk or Type: NodeA Pk: {triple?.NodeA?.Pk}, NodeB Pk: {triple?.NodeB?.Pk}, Relationship Type: {triple?.Relationship?.Type}");
                continue;
            }

            // Ensure Pk values and Relationship Type are not empty strings either, though null check above is primary.
            if (string.IsNullOrEmpty(triple.NodeA.Pk) || string.IsNullOrEmpty(triple.NodeB.Pk))
            {
                 Console.WriteLine($"Skipping triple due to empty Pk: NodeA Pk: {triple.NodeA.Pk}, NodeB Pk: {triple.NodeB.Pk}, Relationship Type: {triple.Relationship.Type}");
                continue;
            }

            var key = $"{triple.NodeA.Pk}_{triple.Relationship.Type}_{triple.NodeB.Pk}";
            if (seenKeys.Add(key)) // .Add returns true if the item was added, false if it was already present
            {
                uniqueTriples.Add(triple);
            }
            else
            {
                // Optionally log duplicate detection if needed for debugging
                // Console.WriteLine($"Duplicate triple detected and skipped: {key}");
            }
        }
        return uniqueTriples;
    }

    private (List<object> nodes, List<object> edges) TransformTriplesToJsonObjects(List<Triple> triples)
    {
        var nodes = new List<object>();
        var edges = new List<object>();
        var seenNodePks = new HashSet<string>();

        if (triples == null) return (nodes, edges);

        foreach (var triple in triples)
        {
            if (triple?.NodeA?.Pk == null || triple?.NodeB?.Pk == null || string.IsNullOrEmpty(triple?.Relationship?.Type) || 
                string.IsNullOrEmpty(triple.NodeA.Pk) || string.IsNullOrEmpty(triple.NodeB.Pk) )
            {
                Console.WriteLine($"Skipping triple in transformation due to missing Pk or Type.");
                continue;
            }

            // Add NodeA if not already seen
            if (seenNodePks.Add(triple.NodeA.Pk))
            {
                nodes.Add(new 
                {
                    id = triple.NodeA.Pk,
                    label = triple.NodeA.Label,
                    name = triple.NodeA.Name,
                    fullName = triple.NodeA.FullName
                    // Add other Node properties as needed based on LLM_CONTEXT.txt or Domain.md, e.g., for PackageNode, MethodNode
                    // For now, keeping it to common properties specified in LLM_CONTEXT's example.
                });
            }

            // Add NodeB if not already seen
            if (seenNodePks.Add(triple.NodeB.Pk))
            {
                nodes.Add(new 
                {
                    id = triple.NodeB.Pk,
                    label = triple.NodeB.Label,
                    name = triple.NodeB.Name,
                    fullName = triple.NodeB.FullName
                    // Add other Node properties as needed
                });
            }

            // Add Edge
            edges.Add(new 
            {
                source = triple.NodeA.Pk,
                target = triple.NodeB.Pk,
                type = triple.Relationship.Type
            });
        }

        return (nodes, edges);
    }
} 