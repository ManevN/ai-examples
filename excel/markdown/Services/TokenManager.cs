using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ExcelChatbot.Services;

public class TokenManager
{
    private readonly ILogger<TokenManager> _logger;
    private readonly ContextSummarizer _contextSummarizer;
    
    // Approximate token counts for different models
    private readonly Dictionary<string, int> _modelLimits = new()
    {
        { "gpt-4", 8192 },
        { "gpt-4-32k", 32768 },
        { "gpt-4o", 128000 },
        { "gpt-4o-mini", 128000 },
        { "gpt-35-turbo", 4096 },
        { "gpt-35-turbo-16k", 16384 }
    };

    public TokenManager(ILogger<TokenManager> logger, ContextSummarizer contextSummarizer)
    {
        _logger = logger;
        _contextSummarizer = contextSummarizer;
    }

    public int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Rough estimation: ~4 characters per token for English text
        // This is a simplified estimation - OpenAI's tiktoken would be more accurate
        var characterCount = text.Length;
        var estimatedTokens = (int)Math.Ceiling(characterCount / 4.0);
        
        return estimatedTokens;
    }

    public int GetModelTokenLimit(string deploymentName)
    {
        // Try to match deployment name with known models
        var lowerName = deploymentName.ToLowerInvariant();
        
        foreach (var model in _modelLimits)
        {
            if (lowerName.Contains(model.Key.Replace("-", "")))
            {
                return model.Value;
            }
        }
        
        // Default to GPT-4 limits if unknown
        _logger.LogWarning($"Unknown model '{deploymentName}', defaulting to GPT-4 token limit");
        return _modelLimits["gpt-4"];
    }

    public ContextOptimizationResult OptimizeContext(
        string userMessage, 
        string fullExcelContext, 
        List<string> conversationHistory, 
        string deploymentName,
        int reserveTokensForResponse = 1000)
    {
        var tokenLimit = GetModelTokenLimit(deploymentName);
        var availableTokens = tokenLimit - reserveTokensForResponse;
        
        var systemPromptBase = "You are a financial data analysis assistant..."; // Base prompt
        var systemPromptTokens = EstimateTokenCount(systemPromptBase);
        var userMessageTokens = EstimateTokenCount(userMessage);
        
        var remainingTokens = availableTokens - systemPromptTokens - userMessageTokens;
        
        _logger.LogInformation($"Token budget: {availableTokens}, System: {systemPromptTokens}, User: {userMessageTokens}, Remaining: {remainingTokens}");
        
        // Optimize conversation history first
        var optimizedHistory = OptimizeConversationHistory(conversationHistory, remainingTokens / 3); // Use 1/3 for history
        var historyTokens = optimizedHistory.Sum(EstimateTokenCount);
        
        remainingTokens -= historyTokens;
        
        // Optimize Excel context
        var optimizedContext = OptimizeExcelContext(fullExcelContext, userMessage, remainingTokens);
        var contextTokens = EstimateTokenCount(optimizedContext);
        
        var totalTokens = systemPromptTokens + userMessageTokens + historyTokens + contextTokens;
        
        return new ContextOptimizationResult
        {
            OptimizedExcelContext = optimizedContext,
            OptimizedConversationHistory = optimizedHistory,
            EstimatedTotalTokens = totalTokens,
            TokenLimit = tokenLimit,
            IsWithinLimit = totalTokens <= availableTokens,
            CompressionApplied = contextTokens < EstimateTokenCount(fullExcelContext)
        };
    }

    private List<string> OptimizeConversationHistory(List<string> history, int maxTokens)
    {
        if (!history.Any())
            return new List<string>();

        var optimizedHistory = new List<string>();
        var currentTokens = 0;
        
        // Start from the most recent messages and work backwards
        for (int i = history.Count - 1; i >= 0; i--)
        {
            var messageTokens = EstimateTokenCount(history[i]);
            if (currentTokens + messageTokens <= maxTokens)
            {
                optimizedHistory.Insert(0, history[i]);
                currentTokens += messageTokens;
            }
            else
            {
                break;
            }
        }
        
        _logger.LogInformation($"Conversation history optimized: {history.Count} -> {optimizedHistory.Count} messages, {currentTokens} tokens");
        return optimizedHistory;
    }

    private string OptimizeExcelContext(string fullContext, string userMessage, int maxTokens)
    {
        var fullContextTokens = EstimateTokenCount(fullContext);
        
        if (fullContextTokens <= maxTokens)
        {
            return fullContext; // No optimization needed
        }
        
        _logger.LogInformation($"Excel context optimization needed: {fullContextTokens} tokens -> {maxTokens} tokens max");
        
        // Strategy 1: Find relevant sections based on user query
        var relevantSections = FindRelevantSections(fullContext, userMessage);
        var relevantContext = string.Join("\n\n", relevantSections);
        
        if (EstimateTokenCount(relevantContext) <= maxTokens)
        {
            _logger.LogInformation("Using relevant sections strategy");
            return relevantContext;
        }
        
        // Strategy 2: Compress tables by removing less important rows
        var compressedContext = CompressTables(fullContext, maxTokens);
        
        if (EstimateTokenCount(compressedContext) <= maxTokens)
        {
            _logger.LogInformation("Using table compression strategy");
            return compressedContext;
        }
        
        // Strategy 3: Create intelligent summary
        _logger.LogInformation("Using intelligent summarization strategy");
        return _contextSummarizer.CreateQuerySpecificSummary(fullContext, userMessage, maxTokens);
    }

    private List<string> FindRelevantSections(string fullContext, string userMessage)
    {
        var sections = fullContext.Split(new[] { "\n\n---\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var relevantSections = new List<(string section, double score)>();
        
        var queryKeywords = ExtractKeywords(userMessage.ToLowerInvariant());
        
        foreach (var section in sections)
        {
            var sectionLower = section.ToLowerInvariant();
            var score = 0.0;
            
            // Score based on keyword matches
            foreach (var keyword in queryKeywords)
            {
                var matches = Regex.Matches(sectionLower, $@"\b{Regex.Escape(keyword)}\b").Count;
                score += matches * keyword.Length; // Longer keywords get higher weight
            }
            
            // Boost score for tables with headers that might be relevant
            if (section.Contains("| --- |")) // Markdown table
            {
                score += 10; // Base boost for being a table
            }
            
            relevantSections.Add((section, score));
        }
        
        // Return top scoring sections
        return relevantSections
            .OrderByDescending(x => x.score)
            .Take(Math.Max(1, sections.Length / 2)) // Take at least 1, at most half
            .Select(x => x.section)
            .ToList();
    }

    private List<string> ExtractKeywords(string text)
    {
        // Extract meaningful keywords from user query
        var commonWords = new HashSet<string> 
        { 
            "the", "is", "at", "which", "on", "and", "or", "but", "in", "with", "a", "an", 
            "what", "how", "where", "when", "why", "who", "can", "could", "would", "should",
            "me", "my", "i", "you", "your", "this", "that", "these", "those", "show", "tell"
        };
        
        var words = Regex.Matches(text, @"\b\w+\b")
            .Select(m => m.Value)
            .Where(w => w.Length > 2 && !commonWords.Contains(w))
            .Distinct()
            .ToList();
            
        return words;
    }

    private string CompressTables(string context, int maxTokens)
    {
        var lines = context.Split('\n').ToList();
        var compressedLines = new List<string>();
        var currentTokens = 0;
        
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lineTokens = EstimateTokenCount(line);
            
            // Always include headers and important structural elements
            if (line.StartsWith("#") || line.Contains("| --- |") || 
                (line.StartsWith("|") && i > 0 && lines[i-1].Contains("| --- |")))
            {
                compressedLines.Add(line);
                currentTokens += lineTokens;
            }
            // For data rows, include every nth row to maintain representative sample
            else if (line.StartsWith("|") && currentTokens + lineTokens <= maxTokens)
            {
                // Include every 2nd or 3rd row based on available space
                var skipFactor = currentTokens > maxTokens * 0.7 ? 3 : 2;
                if (compressedLines.Count(l => l.StartsWith("|") && !l.Contains("---")) % skipFactor == 0)
                {
                    compressedLines.Add(line);
                    currentTokens += lineTokens;
                }
            }
            else if (currentTokens + lineTokens <= maxTokens)
            {
                compressedLines.Add(line);
                currentTokens += lineTokens;
            }
        }
        
        return string.Join('\n', compressedLines);
    }

    private string TruncateWithSummary(string context, int maxTokens)
    {
        var summaryText = "\n\n[NOTE: Excel data has been truncated due to size. Some tables and data may not be shown. Ask for specific sections if needed.]\n\n";
        var summaryTokens = EstimateTokenCount(summaryText);
        var availableForContent = maxTokens - summaryTokens;
        
        var lines = context.Split('\n').ToList();
        var includedLines = new List<string>();
        var currentTokens = 0;
        
        // Prioritize headers and table structures
        foreach (var line in lines)
        {
            var lineTokens = EstimateTokenCount(line);
            if (currentTokens + lineTokens <= availableForContent)
            {
                includedLines.Add(line);
                currentTokens += lineTokens;
            }
            else
            {
                break;
            }
        }
        
        return string.Join('\n', includedLines) + summaryText;
    }
}

public class ContextOptimizationResult
{
    public string OptimizedExcelContext { get; set; } = string.Empty;
    public List<string> OptimizedConversationHistory { get; set; } = new();
    public int EstimatedTotalTokens { get; set; }
    public int TokenLimit { get; set; }
    public bool IsWithinLimit { get; set; }
    public bool CompressionApplied { get; set; }
} 