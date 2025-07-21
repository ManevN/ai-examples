using Azure;
using Azure.AI.OpenAI;
using ExcelChatbot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExcelChatbot.Services;

public class AzureOpenAIService
{
    private readonly OpenAIClient _client;
    private readonly AzureOpenAISettings _settings;
    private readonly TokenManager _tokenManager;
    private readonly ILogger<AzureOpenAIService> _logger;

    public AzureOpenAIService(IOptions<AzureOpenAISettings> settings, TokenManager tokenManager, ILogger<AzureOpenAIService> logger)
    {
        _settings = settings.Value;
        _tokenManager = tokenManager;
        _logger = logger;
        
        var endpoint = new Uri(_settings.Endpoint);
        var credential = new AzureKeyCredential(_settings.ApiKey);
        
        _client = new OpenAIClient(endpoint, credential);
    }

    public async Task<string> GetChatResponseAsync(string userMessage, string excelContext, List<string> conversationHistory)
    {
        try
        {
            // Optimize context to stay within token limits
            var optimization = _tokenManager.OptimizeContext(
                userMessage, 
                excelContext, 
                conversationHistory, 
                _settings.DeploymentName);

            if (optimization.CompressionApplied)
            {
                _logger.LogInformation($"Context optimized: {optimization.EstimatedTotalTokens}/{optimization.TokenLimit} tokens (compression applied)");
            }

            var messages = BuildMessages(userMessage, optimization.OptimizedExcelContext, optimization.OptimizedConversationHistory);
            
            // Adjust max tokens based on available budget
            var maxResponseTokens = Math.Min(2000, optimization.TokenLimit - optimization.EstimatedTotalTokens - 100);
            
            var chatCompletionsOptions = new ChatCompletionsOptions(_settings.DeploymentName, messages)
            {
                Temperature = 0.3f,
                MaxTokens = maxResponseTokens,
                NucleusSamplingFactor = 1.0f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f
            };

            _logger.LogInformation($"Sending request to Azure OpenAI... (Est. tokens: {optimization.EstimatedTotalTokens}, Max response: {maxResponseTokens})");
            
            var response = await _client.GetChatCompletionsAsync(chatCompletionsOptions);
            var responseMessage = response.Value.Choices[0].Message.Content;
            
            // Add compression notice if context was optimized
            if (optimization.CompressionApplied)
            {
                responseMessage += "\n\n*Note: Due to the large amount of data, some Excel content may have been compressed or filtered to focus on the most relevant information for your query.*";
            }
            
            _logger.LogInformation("Received response from Azure OpenAI");
            
            return responseMessage ?? "I'm sorry, I couldn't generate a response.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Azure OpenAI API");
            return "I encountered an error while processing your request. Please try again.";
        }
    }

    private List<ChatRequestMessage> BuildMessages(string userMessage, string excelContext, List<string> conversationHistory)
    {
        var messages = new List<ChatRequestMessage>();

        // System message with context
        var systemPrompt = BuildSystemPrompt(excelContext);
        messages.Add(new ChatRequestSystemMessage(systemPrompt));

        // Add conversation history (keep last 10 messages to avoid token limits)
        var recentHistory = conversationHistory.TakeLast(10).ToList();
        for (int i = 0; i < recentHistory.Count; i++)
        {
            if (i % 2 == 0)
            {
                // User message
                messages.Add(new ChatRequestUserMessage(recentHistory[i]));
            }
            else
            {
                // Assistant message
                messages.Add(new ChatRequestAssistantMessage(recentHistory[i]));
            }
        }

        // Current user message
        messages.Add(new ChatRequestUserMessage(userMessage));

        return messages;
    }

    private string BuildSystemPrompt(string excelContext)
    {
        var contextInfo = "";
        if (excelContext.Contains("[NOTE: Excel data has been truncated"))
        {
            contextInfo = "\n\nIMPORTANT: The Excel data shown below has been optimized due to size constraints. Some information may not be visible. If you need specific data that's not shown, inform the user that they can ask for specific sections or tables.";
        }
        else if (excelContext.Length > 10000) // Arbitrary threshold indicating compression may have been applied
        {
            contextInfo = "\n\nNOTE: The Excel data may have been optimized to focus on content most relevant to the user's query.";
        }

        return $@"You are a financial data analysis assistant. You have access to financial data from Excel files that has been converted to markdown format. Your role is to help users understand and analyze this data by answering their questions.{contextInfo}

The financial data available to you:

{excelContext}

Instructions:
1. Always base your responses on the provided data
2. If asked about data that's not available in the context, clearly state that you don't have that information and suggest the user ask for specific sections
3. When referencing specific numbers or data points, cite the table and row where applicable
4. Provide clear, accurate financial analysis and insights
5. If calculations are needed, show your work step by step
6. Be helpful and professional in your responses
7. If you notice any patterns, trends, or anomalies in the data, point them out
8. When appropriate, suggest follow-up questions that might provide additional insights
9. If the data appears to be truncated or compressed, acknowledge this and offer to help with specific sections

Remember: You can only work with the data provided in the context above. Do not make assumptions about data that is not explicitly shown.";
    }
} 