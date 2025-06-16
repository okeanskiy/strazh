using System.Text.Json.Serialization;

namespace PrecisionApi.Domain;

public class AnalysisArtifact
{
    [JsonPropertyName("metadata")]
    public ArtifactMetadata Metadata { get; set; }

    [JsonPropertyName("logEntries")]
    public List<LogEntry> LogEntries { get; set; }

    public AnalysisArtifact(string sourcePath)
    {
        Metadata = new ArtifactMetadata
        {
            RunId = Guid.NewGuid().ToString(),
            SourcePath = sourcePath,
            StartTime = DateTime.UtcNow
        };
        LogEntries = new List<LogEntry>();
    }
}

public class ArtifactMetadata
{
    [JsonPropertyName("runId")]
    public string RunId { get; set; }

    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }

    [JsonPropertyName("endTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? EndTime { get; set; }
}

public class LogEntry
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    public LogEntry(string message, object? data = null)
    {
        Message = message;
        Data = data;
    }
} 