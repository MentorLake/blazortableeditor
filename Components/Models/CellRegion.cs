namespace BlazorTableEditor.Models;

public record struct CellRegion(int StartRow, int StartCol, int EndRow, int EndCol)
{
    public int Width => EndCol - StartCol + 1;
    public int Height => EndRow - StartRow + 1;
    public int Rows => Height;
    public int Cols => Width;

    public bool IsValid => StartRow <= EndRow && StartCol <= EndCol && StartRow >= 0 && StartCol >= 0;

    public bool Contains(CellPosition pos)
    {
        return pos.Row >= StartRow && pos.Row <= EndRow &&
               pos.Col >= StartCol && pos.Col <= EndCol;
    }

    public bool Intersects(CellRegion other)
    {
        return !(EndRow < other.StartRow || other.EndRow < StartRow ||
                 EndCol < other.StartCol || other.EndCol < StartCol);
    }

    public CellRegion Normalize()
    {
        int minRow = Math.Min(StartRow, EndRow);
        int maxRow = Math.Max(StartRow, EndRow);
        int minCol = Math.Min(StartCol, EndCol);
        int maxCol = Math.Max(StartCol, EndCol);
        return new CellRegion(minRow, minCol, maxRow, maxCol);
    }

    public override string ToString() => $"[{StartRow},{StartCol}]-[{EndRow},{EndCol}]";
}
