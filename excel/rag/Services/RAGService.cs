using Azure;
using Azure.AI.OpenAI;
using ExcelRAGChatbot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace ExcelRAGChatbot.Services;

public class RAGService
{
    private readonly OpenAIClient _openAIClient;
    private readonly AzureSearchService _searchService;
    private readonly AzureOpenAISettings _openAISettings;
    private readonly ILogger<RAGService> _logger;

    public RAGService(
        IOptions<AzureOpenAISettings> openAISettings,
        AzureSearchService searchService,
        ILogger<RAGService> logger)
    {
        _openAISettings = openAISettings.Value;
        _searchService = searchService;
        _logger = logger;
        
        var endpoint = new Uri(_openAISettings.Endpoint);
        var credential = new AzureKeyCredential(_openAISettings.ApiKey);
        _openAIClient = new OpenAIClient(endpoint, credential);
    }

    public async Task<string> GetAnswerAsync(string indexName, string question, List<string> conversationHistory)
    {
        try
        {
            // Search for relevant documents
            var relevantChunks = await _searchService.SearchAsync(indexName, question, maxResults: 5);
            
            if (!relevantChunks.Any())
            {
                return "I couldn't find any relevant information in your Excel files to answer this question. Please make sure you've uploaded Excel files and try rephrasing your question.";
            }

            // Build context from search results
            var context = BuildContextFromChunks(relevantChunks);
            
            // Generate response using LLM
            var response = await GenerateResponseAsync(question, context, conversationHistory);
            
            _logger.LogInformation($"Generated response for question: {question}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating answer for question: {question}");
            return "I encountered an error while processing your question. Please try again.";
        }
    }

    public async Task<string> GetAnswerWithFiltersAsync(string indexName, string question, SearchFilters filters, List<string> conversationHistory)
    {
        try
        {
            // Search with specific filters
            var relevantChunks = await _searchService.SearchWithFiltersAsync(indexName, question, filters, maxResults: 5);
            
            if (!relevantChunks.Any())
            {
                return "I couldn't find any relevant information matching your criteria. Try broadening your search or checking different tables.";
            }

            // Build context from filtered results
            var context = BuildContextFromChunks(relevantChunks);
            
            // Generate response
            var response = await GenerateResponseAsync(question, context, conversationHistory);
            
            _logger.LogInformation($"Generated filtered response for question: {question}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating filtered answer for question: {question}");
            return "I encountered an error while processing your filtered question. Please try again.";
        }
    }

    private string BuildContextFromChunks(List<DocumentChunk> chunks)
    {
        var context = new StringBuilder();
        context.AppendLine("# Relevant Financial Data");
        context.AppendLine();

        // Group chunks by file for better organization
        var groupedChunks = chunks.GroupBy(c => c.FileName).ToList();

        foreach (var fileGroup in groupedChunks)
        {
            context.AppendLine($"## From file: {fileGroup.Key}");
            context.AppendLine();

            foreach (var chunk in fileGroup.OrderBy(c => c.ChunkIndex))
            {
                context.AppendLine($"### {chunk.TableName}");
                context.AppendLine($"**Columns:** {string.Join(", ", chunk.ColumnHeaders)}");
                context.AppendLine($"**Rows:** {chunk.RowCount}");
                
                if (chunk.HasCurrencyData)
                    context.AppendLine("**Contains:** Currency/Financial data");
                if (chunk.HasDateData)
                    context.AppendLine("**Contains:** Date information");
                if (chunk.HasNumericData)
                    context.AppendLine("**Contains:** Numeric data");
                
                context.AppendLine();
                context.AppendLine(chunk.Content);
                context.AppendLine();
            }
        }

        return context.ToString();
    }

    private async Task<string> GenerateResponseAsync(string question, string context, List<string> conversationHistory)
    {
        var messages = new List<ChatRequestMessage>();

        // System message with context
        var systemPrompt = BuildSystemPrompt(context);
        messages.Add(new ChatRequestSystemMessage(systemPrompt));

        // Add recent conversation history (keep last 6 messages to avoid token limits)
        var recentHistory = conversationHistory.TakeLast(6).ToList();
        for (int i = 0; i < recentHistory.Count; i++)
        {
            if (i % 2 == 0)
                messages.Add(new ChatRequestUserMessage(recentHistory[i]));
            else
                messages.Add(new ChatRequestAssistantMessage(recentHistory[i]));
        }

        // Current question
        messages.Add(new ChatRequestUserMessage(question));

        var chatOptions = new ChatCompletionsOptions(_openAISettings.DeploymentName, messages)
        {
            Temperature = 0.3f,
            MaxTokens = 1500,
            NucleusSamplingFactor = 1.0f,
            FrequencyPenalty = 0.0f,
            PresencePenalty = 0.0f
        };

        var response = await _openAIClient.GetChatCompletionsAsync(chatOptions);
        return response.Value.Choices[0].Message.Content ?? "I'm sorry, I couldn't generate a response.";
    }

    private string BuildSystemPrompt(string context)
    {
        return $@"You are a financial data analysis assistant. You help users understand and analyze their Excel financial data using a Retrieval-Augmented Generation (RAG) approach.

The following financial data has been retrieved as most relevant to the user's question:

{context}

Instructions:
1. Base your responses ONLY on the provided context above
2. If the context doesn't contain enough information to answer the question, say so clearly
3. When referencing data, cite the specific table and file name
4. Provide clear, accurate financial analysis and insights
5. Show calculations step-by-step when needed
6. Be professional and helpful in your responses
7. If you notice patterns or trends in the data, point them out
8. Suggest follow-up questions when appropriate

Important: 
- Only use information from the context provided above
- Do not make assumptions about data not shown
- If multiple tables are referenced, clearly distinguish between them
- Use specific numbers and cite their sources (table name, file name)

Remember: This is retrieved data from the user's Excel files, so it represents their actual financial information.";
    }
} 