using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace ExcelChatbot.Services;

public class ContextSummarizer
{
    private readonly ILogger<ContextSummarizer> _logger;

    public ContextSummarizer(ILogger<ContextSummarizer> logger)
    {
        _logger = logger;
    }

    public string CreateExcelSummary(string fullMarkdownContent, int maxTokens = 2000)
    {
        _logger.LogInformation("Creating Excel data summary for large context");
        
        var summary = new StringBuilder();
        summary.AppendLine("# Excel Data Summary");
        summary.AppendLine();
        summary.AppendLine("*This is a summary of your Excel data. Some details may be omitted due to size constraints.*");
        summary.AppendLine();

        // Split content by files (separated by ---)
        var files = fullMarkdownContent.Split(new[] { "\n\n---\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var file in files)
        {
            var fileSummary = SummarizeFile(file);
            summary.AppendLine(fileSummary);
            summary.AppendLine();
            
            // Check if we're approaching the token limit
            if (EstimateTokens(summary.ToString()) > maxTokens * 0.8)
            {
                summary.AppendLine("*[Additional files omitted due to size constraints]*");
                break;
            }
        }

        return summary.ToString();
    }

    private string SummarizeFile(string fileContent)
    {
        var lines = fileContent.Split('\n');
        var summary = new StringBuilder();
        
        // Extract file name from first header
        var fileName = lines.FirstOrDefault(l => l.StartsWith("# "))?.Substring(2) ?? "Unknown File";
        summary.AppendLine($"## {fileName}");
        
        // Find all tables and summarize them
        var tables = ExtractTables(fileContent);
        
        if (tables.Any())
        {
            summary.AppendLine($"**Contains {tables.Count} table(s):**");
            
            foreach (var (tableIndex, table) in tables.Select((t, i) => (i + 1, t)))
            {
                var tableSummary = SummarizeTable(table, tableIndex);
                summary.AppendLine(tableSummary);
            }
        }
        else
        {
            summary.AppendLine("*No structured tables detected*");
        }

        return summary.ToString();
    }

    private List<string> ExtractTables(string content)
    {
        var tables = new List<string>();
        var lines = content.Split('\n');
        var currentTable = new List<string>();
        var inTable = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("|") && line.Contains("|"))
            {
                if (!inTable)
                {
                    inTable = true;
                    currentTable.Clear();
                }
                currentTable.Add(line);
            }
            else if (inTable && currentTable.Any())
            {
                // End of table
                tables.Add(string.Join('\n', currentTable));
                currentTable.Clear();
                inTable = false;
            }
        }

        // Don't forget the last table if file ends with a table
        if (inTable && currentTable.Any())
        {
            tables.Add(string.Join('\n', currentTable));
        }

        return tables;
    }

    private string SummarizeTable(string tableContent, int tableIndex)
    {
        var lines = tableContent.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        
        if (lines.Length < 2)
            return $"- **Table {tableIndex}**: Empty or invalid table";

        var summary = new StringBuilder();
        
        // Extract headers
        var headerLine = lines.FirstOrDefault(l => l.StartsWith("|") && !l.Contains("---"));
        if (headerLine != null)
        {
            var headers = ParseTableRow(headerLine).Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
            var dataRowCount = lines.Count(l => l.StartsWith("|") && !l.Contains("---")) - 1; // Exclude header
            
            summary.Append($"- **Table {tableIndex}**: {dataRowCount} rows, {headers.Count} columns");
            
            if (headers.Any())
            {
                summary.Append($" ({string.Join(", ", headers.Take(5))}{(headers.Count > 5 ? "..." : "")})");
            }
            
            // Analyze numeric patterns
            var numericAnalysis = AnalyzeNumericPatterns(lines);
            if (!string.IsNullOrEmpty(numericAnalysis))
            {
                summary.AppendLine();
                summary.Append($"  - {numericAnalysis}");
            }
        }
        else
        {
            summary.Append($"- **Table {tableIndex}**: {lines.Length} rows (structure unclear)");
        }

        return summary.ToString();
    }

    private List<string> ParseTableRow(string row)
    {
        // Simple parser for markdown table rows
        return row.Split('|')
            .Skip(1) // Skip first empty element
            .Take(row.Split('|').Length - 2) // Skip last empty element
            .Select(cell => cell.Trim())
            .ToList();
    }

    private string AnalyzeNumericPatterns(string[] tableLines)
    {
        var dataLines = tableLines.Where(l => l.StartsWith("|") && !l.Contains("---")).Skip(1).ToArray();
        
        if (!dataLines.Any())
            return "";

        var numericColumns = 0;
        var totalColumns = 0;
        var currencyDetected = false;
        var percentageDetected = false;
        var dateDetected = false;

        // Analyze first few data rows to identify patterns
        foreach (var line in dataLines.Take(Math.Min(5, dataLines.Length)))
        {
            var cells = ParseTableRow(line);
            totalColumns = Math.Max(totalColumns, cells.Count);
            
            foreach (var cell in cells)
            {
                var cleanCell = cell.Trim();
                
                if (Regex.IsMatch(cleanCell, @"^\$?[\d,]+\.?\d*$"))
                    numericColumns++;
                    
                if (cleanCell.Contains("$") || cleanCell.Contains("€") || cleanCell.Contains("£"))
                    currencyDetected = true;
                    
                if (cleanCell.Contains("%"))
                    percentageDetected = true;
                    
                if (Regex.IsMatch(cleanCell, @"\d{1,2}[/-]\d{1,2}[/-]\d{2,4}"))
                    dateDetected = true;
            }
        }

        var patterns = new List<string>();
        if (currencyDetected) patterns.Add("currency values");
        if (percentageDetected) patterns.Add("percentages");
        if (dateDetected) patterns.Add("dates");
        if (numericColumns > totalColumns * 0.3) patterns.Add("numeric data");

        return patterns.Any() ? $"Contains: {string.Join(", ", patterns)}" : "";
    }

    public string CreateQuerySpecificSummary(string fullContext, string userQuery, int maxTokens = 3000)
    {
        _logger.LogInformation($"Creating query-specific summary for: {userQuery}");
        
        var queryKeywords = ExtractQueryKeywords(userQuery);
        var relevantSections = FindRelevantSections(fullContext, queryKeywords);
        
        var summary = new StringBuilder();
        summary.AppendLine("# Relevant Excel Data");
        summary.AppendLine();
        summary.AppendLine($"*Data filtered based on your query: \"{userQuery}\"*");
        summary.AppendLine();
        
        foreach (var section in relevantSections.Take(3)) // Limit to top 3 most relevant sections
        {
            if (EstimateTokens(summary.ToString()) > maxTokens * 0.8)
                break;
                
            summary.AppendLine(section);
            summary.AppendLine();
        }
        
        if (relevantSections.Count > 3)
        {
            summary.AppendLine($"*[{relevantSections.Count - 3} additional sections available - ask more specific questions to access them]*");
        }
        
        return summary.ToString();
    }

    private List<string> ExtractQueryKeywords(string query)
    {
        var financialTerms = new HashSet<string>
        {
            "revenue", "income", "profit", "loss", "expense", "cost", "sales", "budget",
            "total", "sum", "average", "growth", "margin", "roi", "return", "investment",
            "assets", "liability", "equity", "cash", "flow", "balance", "sheet",
            "quarter", "monthly", "annual", "yearly", "period"
        };
        
        var words = Regex.Matches(query.ToLowerInvariant(), @"\b\w+\b")
            .Select(m => m.Value)
            .Where(w => w.Length > 2)
            .ToList();
            
        // Prioritize financial terms
        var keywords = words.Where(w => financialTerms.Contains(w)).ToList();
        keywords.AddRange(words.Where(w => !financialTerms.Contains(w) && w.Length > 3));
        
        return keywords.Distinct().Take(10).ToList();
    }

    private List<string> FindRelevantSections(string content, List<string> keywords)
    {
        var sections = content.Split(new[] { "\n\n---\n\n", "\n## " }, StringSplitOptions.RemoveEmptyEntries);
        var scoredSections = new List<(string section, double score)>();
        
        foreach (var section in sections)
        {
            var score = 0.0;
            var sectionLower = section.ToLowerInvariant();
            
            foreach (var keyword in keywords)
            {
                var matches = Regex.Matches(sectionLower, $@"\b{Regex.Escape(keyword)}\b").Count;
                score += matches * (keyword.Length > 5 ? 2 : 1); // Longer keywords get higher weight
            }
            
            // Bonus for tables
            if (section.Contains("| --- |"))
                score += 5;
                
            scoredSections.Add((section, score));
        }
        
        return scoredSections
            .OrderByDescending(x => x.score)
            .Where(x => x.score > 0)
            .Select(x => x.section)
            .ToList();
    }

    private int EstimateTokens(string text)
    {
        return (int)Math.Ceiling(text.Length / 4.0);
    }
} 