namespace MentorLake.BlazorTableEditor.Models;

public class CellValue
{
	public object? Value { get; set; }
	public string? Format { get; set; }
	public string? BackgroundColor { get; set; } = "#ffffff";
	public string? TextColor { get; set; } = "#000000";

	public CellValue(object? value = null)
	{
		Value = value;
	}

	public CellValue Clone() => new(Value) { Format = Format, BackgroundColor = BackgroundColor, TextColor = TextColor };

	public override string ToString()
	{
		if (Value == null) return string.Empty;
		if (string.IsNullOrEmpty(Format)) return Value.ToString() ?? string.Empty;

		return Value switch
		{
			double d => d.ToString(Format),
			decimal m => m.ToString(Format),
			DateTime dt => dt.ToString(Format),
			_ => Value.ToString() ?? string.Empty
		};
	}
}
