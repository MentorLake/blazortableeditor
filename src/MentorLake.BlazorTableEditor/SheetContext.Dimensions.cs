namespace MentorLake.BlazorTableEditor;

public partial class SheetContext
{
	private int _layoutBodyWidth;
	private int[] _resolvedWidths;
	private int _resolvedTotal;
	private bool _columnLayoutDirty = true;

	public void SetLayoutBodyWidth(int width)
	{
		width = Math.Max(0, width);
		if (_layoutBodyWidth == width)
		{
			return;
		}

		_layoutBodyWidth = width;
		_columnLayoutDirty = true;
	}

	public int GetRowHeight(int row)
	{
		if (IsRowHidden(row))
		{
			return 0;
		}

		return RowHeights.TryGetValue(row, out var h) ? h : DefaultRowHeight;
	}

	public int GetBaseColumnWidth(int col) =>
		ColumnWidths.TryGetValue(col, out var w) ? w : DefaultColumnWidth;

	public int GetColumnWidth(int col)
	{
		EnsureResolvedColumnWidths();
		if (col < 0 || col >= _resolvedWidths.Length)
		{
			return DefaultColumnWidth;
		}

		return _resolvedWidths[col];
	}

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
		EnsureResolvedColumnWidths();
		if (_resolvedWidths is { Length: > 0 })
		{
			for (var c = 0; c < _resolvedWidths.Length; c++)
			{
				ColumnWidths[c] = _resolvedWidths[c];
			}
		}

		ColumnWidths[col] = Math.Max(40, width);
		_columnLayoutDirty = true;
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
		var left = 0;
		for (var c = 0; c < col && c < Model.ColumnCount; c++)
		{
			left += GetColumnWidth(c);
		}

		return left;
	}

	public int GetRowTop(int row)
	{
		var top = 0;
		for (var r = 0; r < row && r < Model.RowCount; r++)
		{
			top += GetRowHeight(r);
		}

		return top;
	}

	public int GetTotalWidth()
	{
		EnsureResolvedColumnWidths();
		return _resolvedTotal;
	}

	public int GetTotalHeight()
	{
		var total = 0;
		for (var r = 0; r < Model.RowCount; r++)
		{
			total += GetRowHeight(r);
		}

		return total;
	}

	internal void InvalidateColumnLayout()
	{
		_columnLayoutDirty = true;
	}

	private void EnsureResolvedColumnWidths()
	{
		var count = Model.ColumnCount;
		if (!_columnLayoutDirty && _resolvedWidths is not null && _resolvedWidths.Length == count)
		{
			return;
		}

		_resolvedWidths = new int[count];
		var baseTotal = 0;
		for (var c = 0; c < count; c++)
		{
			var w = GetBaseColumnWidth(c);
			_resolvedWidths[c] = w;
			baseTotal += w;
		}

		if (count > 0 && _layoutBodyWidth > baseTotal && baseTotal > 0)
		{
			var extra = _layoutBodyWidth - baseTotal;
			var assigned = 0;
			for (var c = 0; c < count; c++)
			{
				var share = c == count - 1
					? extra - assigned
					: (int)((long)extra * _resolvedWidths[c] / baseTotal);
				_resolvedWidths[c] += share;
				assigned += share;
			}

			_resolvedTotal = _layoutBodyWidth;
		}
		else
		{
			_resolvedTotal = baseTotal;
		}

		_columnLayoutDirty = false;
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

	private static void ShiftMapRange(Dictionary<int, int> map, int startIndex, int endIndex)
	{
		var count = endIndex - startIndex + 1;
		var next = new Dictionary<int, int>();
		foreach (var kvp in map)
		{
			if (kvp.Key < startIndex)
			{
				next[kvp.Key] = kvp.Value;
			}
			else if (kvp.Key > endIndex)
			{
				next[kvp.Key - count] = kvp.Value;
			}
		}

		map.Clear();
		foreach (var kvp in next)
		{
			map[kvp.Key] = kvp.Value;
		}
	}
}
