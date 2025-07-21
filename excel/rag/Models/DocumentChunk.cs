using System.Text.Json.Serialization;

namespace ExcelRAGChatbot.Models;

public class DocumentChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("tableName")]
    public string? TableName { get; set; }

    [JsonPropertyName("chunkIndex")]
    public int ChunkIndex { get; set; }

    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = "table";

    [JsonPropertyName("hasNumericData")]
    public bool HasNumericData { get; set; }

    [JsonPropertyName("hasCurrencyData")]
    public bool HasCurrencyData { get; set; }

    [JsonPropertyName("hasDateData")]
    public bool HasDateData { get; set; }

    [JsonPropertyName("columnHeaders")]
    public string[] ColumnHeaders { get; set; } = Array.Empty<string>();

    [JsonPropertyName("rowCount")]
    public int RowCount { get; set; }

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("uploadTimestamp")]
    public DateTime UploadTimestamp { get; set; } = DateTime.UtcNow;
} 