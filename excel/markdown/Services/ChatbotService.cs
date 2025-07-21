using Microsoft.Extensions.Logging;

namespace ExcelChatbot.Services;

public class ChatbotService
{
    private readonly ExcelToMarkdownConverter _excelConverter;
    private readonly AzureOpenAIService _openAIService;
    private readonly FileManager _fileManager;
    private readonly TokenManager _tokenManager;
    private readonly ILogger<ChatbotService> _logger;
    private readonly List<string> _conversationHistory;

    public ChatbotService(
        ExcelToMarkdownConverter excelConverter,
        AzureOpenAIService openAIService,
        FileManager fileManager,
        TokenManager tokenManager,
        ILogger<ChatbotService> logger)
    {
        _excelConverter = excelConverter;
        _openAIService = openAIService;
        _fileManager = fileManager;
        _tokenManager = tokenManager;
        _logger = logger;
        _conversationHistory = new List<string>();
    }

    public async Task ProcessExcelFileAsync(string filePath)
    {
        try
        {
            if (!_fileManager.IsValidExcelFile(filePath))
            {
                throw new ArgumentException($"Invalid Excel file: {filePath}");
            }

            _logger.LogInformation($"Processing Excel file: {Path.GetFileName(filePath)}");
            
            var markdownContent = _excelConverter.ConvertToMarkdown(filePath);
            _fileManager.AddProcessedFile(filePath, markdownContent);
            
            Console.WriteLine($"Successfully processed: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing file: {filePath}");
            Console.WriteLine($"Error processing {Path.GetFileName(filePath)}: {ex.Message}");
        }
    }

    public async Task<string> GetChatResponseAsync(string userInput)
    {
        try
        {
            if (!_fileManager.HasFiles())
            {
                return "Please upload at least one Excel file before asking questions.";
            }

            var excelContext = _fileManager.GetCombinedMarkdownContent();
            var response = await _openAIService.GetChatResponseAsync(userInput, excelContext, _conversationHistory);
            
            // Add to conversation history
            _conversationHistory.Add(userInput);
            _conversationHistory.Add(response);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response");
            return "I encountered an error while processing your question. Please try again.";
        }
    }

    public void ShowStatus()
    {
        Console.WriteLine("\nCurrent Status:");
        Console.WriteLine($"   Files loaded: {_fileManager.GetFileCount()}");
        
        if (_fileManager.HasFiles())
        {
            Console.WriteLine("   Loaded files:");
            foreach (var fileName in _fileManager.GetProcessedFileNames())
            {
                Console.WriteLine($"      - {fileName}");
            }
        }
        
        Console.WriteLine($"   Conversation history: {_conversationHistory.Count / 2} exchanges");
        Console.WriteLine();
    }

    public void ClearConversationHistory()
    {
        _conversationHistory.Clear();
        Console.WriteLine("Conversation history cleared.");
    }

    public void ClearAllData()
    {
        _fileManager.ClearAllFiles();
        _conversationHistory.Clear();
        Console.WriteLine("All data cleared.");
    }

    public async Task ShowTokenInfoAsync()
    {
        if (!_fileManager.HasFiles())
        {
            Console.WriteLine("No Excel files loaded. Upload files first to see token analysis.");
            return;
        }

        Console.WriteLine("\nToken Analysis:");
        
        var excelContext = _fileManager.GetCombinedMarkdownContent();
        var excelTokens = _tokenManager.EstimateTokenCount(excelContext);
        var historyTokens = _conversationHistory.Sum(_tokenManager.EstimateTokenCount);
        
        // Simulate a test query to get model info
        var testOptimization = _tokenManager.OptimizeContext(
            "What is the total revenue?", 
            excelContext, 
            _conversationHistory, 
            "gpt-4"); // This will be replaced with actual deployment name in real usage
            
        Console.WriteLine($"   Excel data: ~{excelTokens:N0} tokens");
        Console.WriteLine($"   Conversation history: ~{historyTokens:N0} tokens");
        Console.WriteLine($"   Model token limit: {testOptimization.TokenLimit:N0} tokens");
        Console.WriteLine($"   Estimated total for next query: ~{testOptimization.EstimatedTotalTokens:N0} tokens");
        
        if (testOptimization.CompressionApplied)
        {
            Console.WriteLine($"   WARNING: Context compression will be applied for queries");
            Console.WriteLine($"   Tip: Ask specific questions to get the most relevant data");
        }
        else
        {
            Console.WriteLine($"   All data fits within token limits");
        }
        
        Console.WriteLine();
        Console.WriteLine("Tips for managing large datasets:");
        Console.WriteLine("   - Ask specific questions about particular tables or metrics");
        Console.WriteLine("   - Use /clear to remove old conversation history");
        Console.WriteLine("   - Consider uploading smaller, focused Excel files");
        Console.WriteLine();
    }
} 