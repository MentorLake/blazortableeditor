namespace MentorLake.BlazorTableEditor.Models;

public record struct CellPosition(int Row, int Col)
{
	public static readonly CellPosition Invalid = new(-1, -1);

	public bool IsValid => Row >= 0 && Col >= 0;

	public override string ToString() => $"({Row}, {Col})";
}
