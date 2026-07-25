using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorTableEditor.Models;

public class TableDataModel
{
    public List<string> ColumnHeaders { get; set; } = new();
    public List<string> RowHeaders { get; set; } = new();
    public Dictionary<string, CellValue> Cells { get; set; } = new();

    [JsonIgnore]
    public int RowCount => RowHeaders.Count;

    [JsonIgnore]
    public int ColumnCount => ColumnHeaders.Count;

    public TableDataModel(int rows = 100, int cols = 26)
    {
        for (int c = 0; c < cols; c++)
        {
            ColumnHeaders.Add(GetColumnLetter(c));
        }

        for (int r = 0; r < rows; r++)
        {
            RowHeaders.Add((r + 1).ToString());
        }
    }

    private static string GetColumnLetter(int col)
    {
        string letter = "";
        int index = col;
        while (index >= 0)
        {
            letter = (char)('A' + (index % 26)) + letter;
            index = (index / 26) - 1;
        }
        return letter;
    }

    public CellValue? GetCell(int row, int col)
    {
        var key = GetKey(row, col);
        return Cells.TryGetValue(key, out var value) ? value : null;
    }

    public void SetCell(int row, int col, CellValue? value)
    {
        var key = GetKey(row, col);
        if (value == null || (value.Value == null && string.IsNullOrEmpty(value.Format)))
        {
            Cells.Remove(key);
        }
        else
        {
            Cells[key] = value;
        }
    }

    public void ClearCell(int row, int col)
    {
        Cells.Remove(GetKey(row, col));
    }

    private static string GetKey(int row, int col) => $"{row},{col}";

    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(this, options);
    }

    public static TableDataModel FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var model = JsonSerializer.Deserialize<TableDataModel>(json, options) ?? new TableDataModel();
        if (model.ColumnHeaders.Count == 0) model = new TableDataModel(100, 26);
        return model;
    }

    public void AddSampleData()
    {
        SetCell(0, 0, new CellValue("Hello"));
        SetCell(0, 1, new CellValue(42));
        SetCell(1, 0, new CellValue(3.14159) { Format = "N2" });
        SetCell(2, 2, new CellValue("Blazor Table Editor") { BackgroundColor = "#e6f3ff" });
    }
}
