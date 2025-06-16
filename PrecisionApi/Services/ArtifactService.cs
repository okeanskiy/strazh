using System.Text.Json;
using PrecisionApi.Domain;

namespace PrecisionApi.Services;

public class ArtifactService : IDisposable
{
    private readonly AnalysisArtifact _artifact;
    public AnalysisArtifact Artifact => _artifact;

    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ArtifactService(string sourcePath)
    {
        _artifact = new AnalysisArtifact(sourcePath);
        Log("ArtifactService initialized.", new { RunId = _artifact.Metadata.RunId });
    }

    public void Log(string message, object? data = null)
    {
        _artifact.LogEntries.Add(new LogEntry(message, data));
    }

    private void FinalizeArtifact()
    {
        if (_artifact.Metadata.EndTime.HasValue) return; // Already finalized

        _artifact.Metadata.EndTime = DateTime.UtcNow;
        Log("Artifact finalization.");
    }

    public void Dispose()
    {
        FinalizeArtifact();
        GC.SuppressFinalize(this);
    }
} 