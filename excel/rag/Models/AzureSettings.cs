namespace ExcelRAGChatbot.Models;

public class AzureOpenAISettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
}

public class AzureSearchSettings
{
    public string ServiceName { get; set; } = string.Empty;
    public string AdminKey { get; set; } = string.Empty;
    public string IndexPrefix { get; set; } = "excel-rag";
    
    public string ServiceUrl => $"https://{ServiceName}.search.windows.net";
} 