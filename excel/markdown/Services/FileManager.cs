using Microsoft.Extensions.Logging;

namespace ExcelChatbot.Services;

public class FileManager
{
    private readonly ILogger<FileManager> _logger;
    private readonly List<string> _processedFiles;
    private readonly Dictionary<string, string> _fileContents;

    public FileManager(ILogger<FileManager> logger)
    {
        _logger = logger;
        _processedFiles = new List<string>();
        _fileContents = new Dictionary<string, string>();
    }

    public bool IsValidExcelFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning($"File does not exist: {filePath}");
            return false;
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var validExtensions = new[] { ".xlsx", ".xls" };

        return validExtensions.Contains(extension);
    }

    public void AddProcessedFile(string filePath, string markdownContent)
    {
        var fileName = Path.GetFileName(filePath);
        
        if (!_processedFiles.Contains(filePath))
        {
            _processedFiles.Add(filePath);
            _fileContents[fileName] = markdownContent;
            _logger.LogInformation($"Added file to context: {fileName}");
        }
        else
        {
            _logger.LogInformation($"File already processed: {fileName}");
        }
    }

    public void RemoveFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        if (_processedFiles.Contains(filePath))
        {
            _processedFiles.Remove(filePath);
            _fileContents.Remove(fileName);
            _logger.LogInformation($"Removed file from context: {fileName}");
        }
    }

    public void ClearAllFiles()
    {
        _processedFiles.Clear();
        _fileContents.Clear();
        _logger.LogInformation("Cleared all files from context");
    }

    public string GetCombinedMarkdownContent()
    {
        string result;

        if (!_fileContents.Any())
        {
            return "No Excel files have been uploaded yet.";
        }
        else
        {
            /// Log markdwn content to a file
            /// 
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logDirectory);
            string logFileName = $"MarkdownLog_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            string logFilePath = Path.Combine(logDirectory, logFileName);
            result = string.Join("\n\n---\n\n", _fileContents.Values);
            File.WriteAllText(logFilePath, $"Timestamp: {DateTime.Now}\n\n{result}");
        }

        return result;
    }

    public List<string> GetProcessedFileNames()
    {
        return _processedFiles.Select(Path.GetFileName).ToList();
    }

    public int GetFileCount()
    {
        return _processedFiles.Count;
    }

    public bool HasFiles()
    {
        return _processedFiles.Any();
    }
} 