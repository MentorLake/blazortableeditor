namespace MentorLake.BlazorTableEditor;

public partial class SheetContext
{
	public int GetRowHeight(int row) =>
		RowHeights.TryGetValue(row, out var h) ? h : DefaultRowHeight;

	public int GetColumnWidth(int col) =>
		ColumnWidths.TryGetValue(col, out var w) ? w : DefaultColumnWidth;

	public void SetRowHeight(int row, int height, bool notify = true)
	{
		RowHeights[row] = Math.Max(18, height);
		if (notify)
		{
			NotifyStateChanged();
		}
	}

	public void SetColumnWidth(int col, int width, bool notify = true)
	{
		ColumnWidths[col] = Math.Max(40, width);
		if (notify)
		{
			NotifyStateChanged();
		}
	}

	public void BeginRowResizeGesture()
	{
		PushUndoSnapshot();
	}

	public void BeginColumnResizeGesture()
	{
		PushUndoSnapshot();
	}

	public int GetColumnLeft(int col)
	{
		int left = 0;
		for (int c = 0; c < col && c < Model.ColumnCount; c++)
		{
			left += GetColumnWidth(c);
		}

		return left;
	}

	public int GetRowTop(int row)
	{
		int top = 0;
		for (int r = 0; r < row && r < Model.RowCount; r++)
		{
			top += GetRowHeight(r);
		}

		return top;
	}

	public int GetTotalWidth()
	{
		int total = 0;
		for (int c = 0; c < Model.ColumnCount; c++)
		{
			total += GetColumnWidth(c);
		}

		return total;
	}

	public int GetTotalHeight()
	{
		int total = 0;
		for (int r = 0; r < Model.RowCount; r++)
		{
			total += GetRowHeight(r);
		}

		return total;
	}

	private static void ShiftMap(Dictionary<int, int> map, int index, bool insert, int defaultValue)
	{
		var ordered = insert
			? map.OrderByDescending(k => k.Key).ToList()
			: map.OrderBy(k => k.Key).ToList();

		var next = new Dictionary<int, int>();
		foreach (var kvp in ordered)
		{
			if (insert)
			{
				if (kvp.Key >= index)
				{
					next[kvp.Key + 1] = kvp.Value;
				}
				else
				{
					next[kvp.Key] = kvp.Value;
				}
			}
			else
			{
				if (kvp.Key > index)
				{
					next[kvp.Key - 1] = kvp.Value;
				}
				else if (kvp.Key < index)
				{
					next[kvp.Key] = kvp.Value;
				}
			}
		}

		if (insert)
		{
			next[index] = defaultValue;
		}

		map.Clear();
		foreach (var kvp in next)
		{
			map[kvp.Key] = kvp.Value;
		}
	}
}
