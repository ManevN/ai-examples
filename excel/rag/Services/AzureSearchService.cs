using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using ExcelRAGChatbot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExcelRAGChatbot.Services;

public class AzureSearchService
{
    private readonly SearchIndexClient _indexClient;
    private readonly AzureSearchSettings _settings;
    private readonly ILogger<AzureSearchService> _logger;

    public AzureSearchService(IOptions<AzureSearchSettings> settings, ILogger<AzureSearchService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        
        var credential = new AzureKeyCredential(_settings.AdminKey);
        _indexClient = new SearchIndexClient(new Uri(_settings.ServiceUrl), credential);
    }

    public async Task<string> CreateSessionIndexAsync(string sessionId)
    {
        var indexName = $"{_settings.IndexPrefix}-{sessionId}";
        
        try
        {
            var searchIndex = new SearchIndex(indexName)
            {
                Fields =
                {
                    new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
                    new SearchableField("content") { IsFilterable = false, IsSortable = false },
                    new SimpleField("fileName", SearchFieldDataType.String) { IsFilterable = true },
                    new SimpleField("tableName", SearchFieldDataType.String) { IsFilterable = true },
                    new SimpleField("chunkIndex", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
                    new SimpleField("dataType", SearchFieldDataType.String) { IsFilterable = true },
                    new SimpleField("hasNumericData", SearchFieldDataType.Boolean) { IsFilterable = true },
                    new SimpleField("hasCurrencyData", SearchFieldDataType.Boolean) { IsFilterable = true },
                    new SimpleField("hasDateData", SearchFieldDataType.Boolean) { IsFilterable = true },
                    new SimpleField("columnHeaders", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsFilterable = true },
                    new SimpleField("rowCount", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
                    new SimpleField("sessionId", SearchFieldDataType.String) { IsFilterable = true },
                    new SimpleField("uploadTimestamp", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true }
                }
            };

            await _indexClient.CreateIndexAsync(searchIndex);
            _logger.LogInformation($"Created search index: {indexName}");
            
            return indexName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating search index for session {sessionId}");
            throw;
        }
    }

    public async Task IndexDocumentsAsync(string indexName, List<DocumentChunk> chunks)
    {
        try
        {
            var credential = new AzureKeyCredential(_settings.AdminKey);
            var searchClient = new SearchClient(new Uri(_settings.ServiceUrl), indexName, credential);

            var batch = IndexDocumentsBatch.Upload(chunks);
            await searchClient.IndexDocumentsAsync(batch);
            
            _logger.LogInformation($"Indexed {chunks.Count} documents in {indexName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error indexing documents in {indexName}");
            throw;
        }
    }

    public async Task<List<DocumentChunk>> SearchAsync(string indexName, string query, int maxResults = 5)
    {
        try
        {
            var credential = new AzureKeyCredential(_settings.AdminKey);
            var searchClient = new SearchClient(new Uri(_settings.ServiceUrl), indexName, credential);

            var searchOptions = new SearchOptions
            {
                Size = maxResults,
                IncludeTotalCount = true,
                SearchMode = SearchMode.Any,
                QueryType = SearchQueryType.Simple
            };

            // Add filters for financial data if relevant keywords found
            var filters = BuildFilters(query);
            if (!string.IsNullOrEmpty(filters))
            {
                searchOptions.Filter = filters;
            }

            var searchResults = await searchClient.SearchAsync<DocumentChunk>(query, searchOptions);
            var results = new List<DocumentChunk>();

            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                results.Add(result.Document);
            }

            _logger.LogInformation($"Found {results.Count} results for query: {query}");
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error searching in {indexName} for query: {query}");
            throw;
        }
    }

    public async Task<List<DocumentChunk>> SearchWithFiltersAsync(string indexName, string query, SearchFilters filters, int maxResults = 5)
    {
        try
        {
            var credential = new AzureKeyCredential(_settings.AdminKey);
            var searchClient = new SearchClient(new Uri(_settings.ServiceUrl), indexName, credential);

            var searchOptions = new SearchOptions
            {
                Size = maxResults,
                IncludeTotalCount = true,
                SearchMode = SearchMode.Any,
                QueryType = SearchQueryType.Simple
            };

            var filterExpression = BuildFilterExpression(filters);
            if (!string.IsNullOrEmpty(filterExpression))
            {
                searchOptions.Filter = filterExpression;
            }

            var searchResults = await searchClient.SearchAsync<DocumentChunk>(query, searchOptions);
            var results = new List<DocumentChunk>();

            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                results.Add(result.Document);
            }

            _logger.LogInformation($"Found {results.Count} filtered results for query: {query}");
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error searching with filters in {indexName}");
            throw;
        }
    }

    public async Task DeleteIndexAsync(string sessionId)
    {
        var indexName = $"{_settings.IndexPrefix}-{sessionId}";
        
        try
        {
            await _indexClient.DeleteIndexAsync(indexName);
            _logger.LogInformation($"Deleted search index: {indexName}");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning($"Index {indexName} not found for deletion");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting search index: {indexName}");
            throw;
        }
    }

    private string BuildFilters(string query)
    {
        var filters = new List<string>();
        var queryLower = query.ToLowerInvariant();

        // Filter by data type based on query content
        if (queryLower.Contains("revenue") || queryLower.Contains("sales") || queryLower.Contains("income") ||
            queryLower.Contains("cost") || queryLower.Contains("expense") || queryLower.Contains("budget") ||
            queryLower.Contains("profit") || queryLower.Contains("total") || queryLower.Contains("amount"))
        {
            filters.Add("hasCurrencyData eq true or hasNumericData eq true");
        }

        if (queryLower.Contains("date") || queryLower.Contains("month") || queryLower.Contains("year") ||
            queryLower.Contains("quarter") || queryLower.Contains("period"))
        {
            filters.Add("hasDateData eq true");
        }

        return filters.Any() ? $"({string.Join(" or ", filters)})" : "";
    }

    private string BuildFilterExpression(SearchFilters filters)
    {
        var expressions = new List<string>();

        if (!string.IsNullOrEmpty(filters.FileName))
        {
            expressions.Add($"fileName eq '{filters.FileName}'");
        }

        if (!string.IsNullOrEmpty(filters.TableName))
        {
            expressions.Add($"tableName eq '{filters.TableName}'");
        }

        if (filters.HasNumericData.HasValue)
        {
            expressions.Add($"hasNumericData eq {filters.HasNumericData.Value.ToString().ToLower()}");
        }

        if (filters.HasCurrencyData.HasValue)
        {
            expressions.Add($"hasCurrencyData eq {filters.HasCurrencyData.Value.ToString().ToLower()}");
        }

        if (filters.HasDateData.HasValue)
        {
            expressions.Add($"hasDateData eq {filters.HasDateData.Value.ToString().ToLower()}");
        }

        return expressions.Any() ? string.Join(" and ", expressions) : "";
    }
}

public class SearchFilters
{
    public string? FileName { get; set; }
    public string? TableName { get; set; }
    public bool? HasNumericData { get; set; }
    public bool? HasCurrencyData { get; set; }
    public bool? HasDateData { get; set; }
} 