using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Text;

namespace ExcelChatbot.Services;

public class ExcelToMarkdownConverter
{
    public string ConvertToMarkdown(string filePath)
    {
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
                return "Empty worksheet";

            return ConvertSheetDataToMarkdown(sheetData, workbookPart, Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error processing Excel file '{filePath}': {ex.Message}", ex);
        }
    }

    private string ConvertSheetDataToMarkdown(SheetData sheetData, WorkbookPart workbookPart, string fileName)
    {
        var markdown = new StringBuilder();
        markdown.AppendLine($"# Financial Data from {fileName}");
        markdown.AppendLine();

        var rows = sheetData.Elements<Row>().ToList();
        if (!rows.Any())
            return markdown.ToString();

        // Get all cells and determine the data structure
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

        // Find table regions by detecting headers and data patterns
        var tableRegions = DetectTableRegions(rows, allCells, workbookPart);
        
        if (tableRegions.Any())
        {
            foreach (var (tableIndex, region) in tableRegions.Select((r, i) => (i + 1, r)))
            {
                markdown.AppendLine($"## Table {tableIndex}");
                markdown.AppendLine();
                
                var tableMarkdown = ConvertRegionToTable(region, workbookPart);
                markdown.AppendLine(tableMarkdown);
                markdown.AppendLine();
            }
        }
        else
        {
            // Fallback: treat the entire sheet as one table
            markdown.AppendLine("## Data Table");
            markdown.AppendLine();
            
            var tableMarkdown = ConvertRowsToTable(rows, workbookPart);
            markdown.AppendLine(tableMarkdown);
        }

        return markdown.ToString();
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

            // Check if this looks like a header row (has multiple non-empty cells)
            var nonEmptyCells = rowCells.Where(c => !IsEmptyCell(c, workbookPart)).ToList();
            if (nonEmptyCells.Count >= 2)
            {
                var region = ExtractTableRegion(rows, row.RowIndex?.Value ?? 0, allCells, workbookPart);
                if (region.Rows.Count > 1) // At least header + 1 data row
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
        var currentRowIndex = startRowIndex + 1;
        var consecutiveEmptyRows = 0;

        foreach (var row in allRows.Where(r => (r.RowIndex?.Value ?? 0) > startRowIndex).OrderBy(r => r.RowIndex?.Value ?? 0))
        {
            var rowIndex = row.RowIndex?.Value ?? 0;
            
            // Check if row has data in the same column range
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
                // Stop if we encounter 2 consecutive empty rows
                if (consecutiveEmptyRows >= 2)
                    break;
            }
        }

        return region;
    }

    private string ConvertRegionToTable(TableRegion region, WorkbookPart workbookPart)
    {
        if (!region.Rows.Any())
            return "";

        return ConvertRowsToTable(region.Rows, workbookPart);
    }

    private string ConvertRowsToTable(List<Row> rows, WorkbookPart workbookPart)
    {
        if (!rows.Any())
            return "";

        var markdown = new StringBuilder();
        
        // Determine the column range
        var allCells = rows.SelectMany(r => r.Elements<Cell>()).ToList();
        if (!allCells.Any())
            return "";

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
                }
            }

            // Fill empty cells
            for (int i = 0; i < rowCells.Length; i++)
            {
                rowCells[i] ??= "";
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

        return markdown.ToString();
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