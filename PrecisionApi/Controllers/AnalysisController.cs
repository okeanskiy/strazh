using Microsoft.AspNetCore.Mvc;
using PrecisionApi.Services;
using System.IO.Compression;

namespace PrecisionApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly AnalysisService _analysisService;

    public AnalysisController(AnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(System.Text.Json.JsonDocument))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadAndAnalyzeCodebase(IFormFile zipFile)
    {
        if (zipFile == null || zipFile.Length == 0)
        {
            return BadRequest("No file uploaded or file is empty.");
        }

        if (Path.GetExtension(zipFile.FileName).ToLowerInvariant() != ".zip")
        {
            return BadRequest("Invalid file type. Only .zip files are allowed.");
        }

        var tempExtractPath = Path.Combine(Path.GetTempPath(), "PrecisionApi_Uploads", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempExtractPath);

        try
        {
            var zipFilePath = Path.Combine(tempExtractPath, zipFile.FileName);
            await using (var stream = new FileStream(zipFilePath, FileMode.Create))
            {
                await zipFile.CopyToAsync(stream);
            }

            ZipFile.ExtractToDirectory(zipFilePath, tempExtractPath);
            System.IO.File.Delete(zipFilePath); // Delete the zip file after extraction

            // Call the AnalysisService
            var graphJson = await _analysisService.AnalyzeCodebaseAsync(tempExtractPath);
            
            return Ok(graphJson);
        }
        catch (Exception ex)
        {
            // Log the exception (e.g., using ILogger if injected)
            Console.WriteLine($"Error during analysis: {ex}"); // Simple console log for now
            return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred during processing: {ex.Message}");
        }
        finally
        {
            // Temp directory cleanup strategy:
            // For robust cleanup, consider a background service or an IAsyncDisposable pattern on the service 
            // if it holds onto the path. For now, leaving it to be potentially inspected during development.
            // if (Directory.Exists(tempExtractPath))
            // {
            //     Directory.Delete(tempExtractPath, recursive: true);
            // }
        }
    }
} 