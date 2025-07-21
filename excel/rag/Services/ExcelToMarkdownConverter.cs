using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ExcelRAGChatbot.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace ExcelRAGChatbot.Services;

public class ExcelToMarkdownConverter
{
    private readonly ILogger<ExcelToMarkdownConverter> _logger;

    public ExcelToMarkdownConverter(ILogger<ExcelToMarkdownConverter> logger)
    {
        _logger = logger;
    }

    public List<DocumentChunk> ConvertToChunks(string filePath, string sessionId)
    {
        var chunks = new List<DocumentChunk>();
        var fileName = Path.GetFileName(filePath);

        try
        {
            using var document = SpreadsheetDocument.Open(filePath, false);
            var workbookPart = document.WorkbookPart;
            if (workbookPart == null)
                throw new InvalidOperationException("Invalid Excel file: no workbook found.");

            var worksheetPart = workbookPart.WorksheetParts.FirstOrDefault();
            if (worksheetPart == null)
                throw new InvalidOperationException("Invalid Excel file: no worksheet found.");

            var worksheet = worksheetPart.Worksheet;
            var sheetData = worksheet.GetFirstChild<SheetData>();
            if (sheetData == null)
            {
                _logger.LogWarning($"Empty worksheet in file: {fileName}");
                return chunks;
            }

            chunks = ProcessWorksheetToChunks(sheetData, workbookPart, fileName, sessionId);
            _logger.LogInformation($"Created {chunks.Count} chunks from {fileName}");

            return chunks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing Excel file '{filePath}'");
            throw new InvalidOperationException($"Error processing Excel file '{filePath}': {ex.Message}", ex);
        }
    }

    private List<DocumentChunk> ProcessWorksheetToChunks(SheetData sheetData, WorkbookPart workbookPart, string fileName, string sessionId)
    {
        var chunks = new List<DocumentChunk>();
        var rows = sheetData.Elements<Row>().ToList();
        
        if (!rows.Any())
            return chunks;

        // Get all cells for analysis
        var allCells = new Dictionary<string, Cell>();
        foreach (var row in rows)
        {
            foreach (var cell in row.Elements<Cell>())
            {
                if (cell.CellReference != null)
                {
                    allCells[cell.CellReference.Value!] = cell;
                }
            }
        }

        // Detect table regions
        var tableRegions = DetectTableRegions(rows, allCells, workbookPart);
        
        if (tableRegions.Any())
        {
            foreach (var (tableIndex, region) in tableRegions.Select((r, i) => (i + 1, r)))
            {
                var chunk = CreateChunkFromTable(region, workbookPart, fileName, $"Table {tableIndex}", sessionId, tableIndex - 1);
                if (chunk != null)
                {
                    chunks.Add(chunk);
                }
            }
        }
        else
        {
            // Fallback: treat entire sheet as one chunk
            var chunk = CreateChunkFromRows(rows, workbookPart, fileName, "Data", sessionId, 0);
            if (chunk != null)
            {
                chunks.Add(chunk);
            }
        }

        return chunks;
    }

    private List<TableRegion> DetectTableRegions(List<Row> rows, Dictionary<string, Cell> allCells, WorkbookPart workbookPart)
    {
        var regions = new List<TableRegion>();
        var processedRows = new HashSet<uint>();

        foreach (var row in rows.OrderBy(r => r.RowIndex?.Value ?? 0))
        {
            if (processedRows.Contains(row.RowIndex?.Value ?? 0))
                continue;

            var rowCells = row.Elements<Cell>().ToList();
            if (!rowCells.Any())
                continue;

            // Check if this looks like a header row
            var nonEmptyCells = rowCells.Where(c => !IsEmptyCell(c, workbookPart)).ToList();
            if (nonEmptyCells.Count >= 2)
            {
                var region = ExtractTableRegion(rows, row.RowIndex?.Value ?? 0, allCells, workbookPart);
                if (region.Rows.Count > 1)
                {
                    regions.Add(region);
                    foreach (var regionRow in region.Rows)
                    {
                        processedRows.Add(regionRow.RowIndex?.Value ?? 0);
                    }
                }
            }
        }

        return regions;
    }

    private TableRegion ExtractTableRegion(List<Row> allRows, uint startRowIndex, Dictionary<string, Cell> allCells, WorkbookPart workbookPart)
    {
        var region = new TableRegion { StartRowIndex = startRowIndex };
        var headerRow = allRows.FirstOrDefault(r => (r.RowIndex?.Value ?? 0) == startRowIndex);
        
        if (headerRow == null)
            return region;

        region.Rows.Add(headerRow);
        
        // Determine column range from header row
        var headerCells = headerRow.Elements<Cell>().Where(c => !IsEmptyCell(c, workbookPart)).ToList();
        if (!headerCells.Any())
            return region;

        var minColumn = headerCells.Min(c => GetColumnIndex(c.CellReference?.Value ?? "A1"));
        var maxColumn = headerCells.Max(c => GetColumnIndex(c.CellReference?.Value ?? "A1"));

        // Find subsequent rows that belong to this table
        var consecutiveEmptyRows = 0;

        foreach (var row in allRows.Where(r => (r.RowIndex?.Value ?? 0) > startRowIndex).OrderBy(r => r.RowIndex?.Value ?? 0))
        {
            var rowCells = row.Elements<Cell>()
                .Where(c => {
                    var colIndex = GetColumnIndex(c.CellReference?.Value ?? "A1");
                    return colIndex >= minColumn && colIndex <= maxColumn && !IsEmptyCell(c, workbookPart);
                })
                .ToList();

            if (rowCells.Any())
            {
                region.Rows.Add(row);
                consecutiveEmptyRows = 0;
            }
            else
            {
                consecutiveEmptyRows++;
                if (consecutiveEmptyRows >= 2)
                    break;
            }
        }

        return region;
    }

    private DocumentChunk? CreateChunkFromTable(TableRegion region, WorkbookPart workbookPart, string fileName, string tableName, string sessionId, int chunkIndex)
    {
        if (!region.Rows.Any())
            return null;

        return CreateChunkFromRows(region.Rows, workbookPart, fileName, tableName, sessionId, chunkIndex);
    }

    private DocumentChunk? CreateChunkFromRows(List<Row> rows, WorkbookPart workbookPart, string fileName, string tableName, string sessionId, int chunkIndex)
    {
        if (!rows.Any())
            return null;

        var markdown = new StringBuilder();
        var columnHeaders = new List<string>();
        var hasNumericData = false;
        var hasCurrencyData = false;
        var hasDateData = false;

        // Add table header
        markdown.AppendLine($"## {tableName} - {fileName}");
        markdown.AppendLine();

        // Determine column range
        var allCells = rows.SelectMany(r => r.Elements<Cell>()).ToList();
        if (!allCells.Any())
            return null;

        var minColumn = allCells.Min(c => GetColumnIndex(c.CellReference?.Value ?? "A1"));
        var maxColumn = allCells.Max(c => GetColumnIndex(c.CellReference?.Value ?? "A1"));
        var columnCount = maxColumn - minColumn + 1;

        bool headerProcessed = false;

        foreach (var row in rows.OrderBy(r => r.RowIndex?.Value ?? 0))
        {
            var rowCells = new string[columnCount];
            
            // Fill the row array
            foreach (var cell in row.Elements<Cell>())
            {
                var colIndex = GetColumnIndex(cell.CellReference?.Value ?? "A1");
                if (colIndex >= minColumn && colIndex <= maxColumn)
                {
                    var cellValue = GetCellValue(cell, workbookPart);
                    rowCells[colIndex - minColumn] = cellValue.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
                    
                    // Analyze data types
                    AnalyzeCellContent(cellValue, ref hasNumericData, ref hasCurrencyData, ref hasDateData);
                }
            }

            // Fill empty cells
            for (int i = 0; i < rowCells.Length; i++)
            {
                rowCells[i] ??= "";
            }

            // Capture headers
            if (!headerProcessed)
            {
                columnHeaders.AddRange(rowCells.Where(c => !string.IsNullOrWhiteSpace(c)));
            }

            // Add the row to markdown
            markdown.AppendLine($"| {string.Join(" | ", rowCells)} |");

            // Add header separator after first row
            if (!headerProcessed)
            {
                var separator = string.Join(" | ", Enumerable.Repeat("---", columnCount));
                markdown.AppendLine($"| {separator} |");
                headerProcessed = true;
            }
        }

        var content = markdown.ToString();
        var rowCount = rows.Count - 1; // Exclude header

        return new DocumentChunk
        {
            Id = $"{sessionId}-{chunkIndex}",
            Content = content,
            FileName = fileName,
            TableName = tableName,
            ChunkIndex = chunkIndex,
            ColumnHeaders = columnHeaders.ToArray(),
            RowCount = rowCount,
            HasNumericData = hasNumericData,
            HasCurrencyData = hasCurrencyData,
            HasDateData = hasDateData,
            SessionId = sessionId
        };
    }

    private void AnalyzeCellContent(string cellValue, ref bool hasNumericData, ref bool hasCurrencyData, ref bool hasDateData)
    {
        if (string.IsNullOrWhiteSpace(cellValue))
            return;

        // Check for currency
        if (cellValue.Contains("$") || cellValue.Contains("€") || cellValue.Contains("£") || cellValue.Contains("%"))
        {
            hasCurrencyData = true;
        }

        // Check for numeric data
        if (Regex.IsMatch(cellValue, @"^\$?[\d,]+\.?\d*%?$"))
        {
            hasNumericData = true;
        }

        // Check for dates
        if (Regex.IsMatch(cellValue, @"\d{1,2}[/-]\d{1,2}[/-]\d{2,4}"))
        {
            hasDateData = true;
        }
    }

    private bool IsEmptyCell(Cell cell, WorkbookPart workbookPart)
    {
        var value = GetCellValue(cell, workbookPart);
        return string.IsNullOrWhiteSpace(value);
    }

    private string GetCellValue(Cell cell, WorkbookPart workbookPart)
    {
        if (cell.CellValue == null)
            return "";

        var value = cell.CellValue.Text;

        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            var sharedStringTablePart = workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
            if (sharedStringTablePart != null)
            {
                var sharedStringTable = sharedStringTablePart.SharedStringTable;
                if (int.TryParse(value, out int index) && index < sharedStringTable.ChildElements.Count)
                {
                    return sharedStringTable.ChildElements[index].InnerText;
                }
            }
        }

        return value;
    }

    private int GetColumnIndex(string cellReference)
    {
        if (string.IsNullOrEmpty(cellReference))
            return 1;

        var columnPart = new string(cellReference.TakeWhile(char.IsLetter).ToArray());
        
        int columnIndex = 0;
        for (int i = 0; i < columnPart.Length; i++)
        {
            columnIndex = columnIndex * 26 + (columnPart[i] - 'A' + 1);
        }
        
        return columnIndex;
    }

    private class TableRegion
    {
        public uint StartRowIndex { get; set; }
        public List<Row> Rows { get; set; } = new();
    }
} 