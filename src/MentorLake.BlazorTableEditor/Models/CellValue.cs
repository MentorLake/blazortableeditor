namespace MentorLake.BlazorTableEditor.Models;

public class CellValue
{
	public string Value { get; set; }
	public string BackgroundColor { get; set; } = "#ffffff";
	public string TextColor { get; set; } = "#000000";

	public CellValue(string value = null)
	{
		Value = value;
	}

	public CellValue Clone() => new(Value) { BackgroundColor = BackgroundColor, TextColor = TextColor };

	public override string ToString() => Value ?? string.Empty;
}
