using System.Globalization;

namespace MentorLake.BlazorTableEditor;

public partial class SheetContext
{
	public const string FilterBlankKey = "";
	public const string FilterBlankDisplay = "(Blanks)";

	private readonly Dictionary<int, HashSet<string>> _columnFilters = new();
	private readonly HashSet<int> _hiddenRows = new();

	public bool HasAnyFilter => _columnFilters.Count > 0;

	public bool IsColumnFiltered(int col) => _columnFilters.ContainsKey(col);

	public bool IsRowHidden(int row) => _hiddenRows.Contains(row);

	public IReadOnlyCollection<string> GetColumnFilter(int col) =>
		_columnFilters.TryGetValue(col, out var set) ? set : null;

	public List<string> GetFilterValuesForColumn(int col)
	{
		var values = new HashSet<string>(StringComparer.Ordinal);
		for (var r = 0; r < Model.RowCount; r++)
		{
			if (!RowPassesOtherFilters(r, col))
			{
				continue;
			}

			values.Add(GetFilterKey(r, col));
		}

		return values
			.OrderBy(v => v == FilterBlankKey ? 1 : 0)
			.ThenBy(v => TryParseSortNumber(v, out var n) ? 0 : 1)
			.ThenBy(v => TryParseSortNumber(v, out var n) ? n : 0)
			.ThenBy(v => v, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public void ApplyColumnFilter(int col, HashSet<string> allowedValues)
	{
		if (col < 0 || col >= Model.ColumnCount)
		{
			return;
		}

		if (allowedValues is null)
		{
			_columnFilters.Remove(col);
		}
		else
		{
			var available = GetFilterValuesForColumn(col);
			if (allowedValues.Count >= available.Count && available.All(allowedValues.Contains))
			{
				_columnFilters.Remove(col);
			}
			else
			{
				_columnFilters[col] = new HashSet<string>(allowedValues, StringComparer.Ordinal);
			}
		}

		RecomputeHiddenRows();
		EnsureActiveCellVisible();
		NotifyStateChanged();
	}

	public void ClearColumnFilter(int col)
	{
		if (!_columnFilters.Remove(col))
		{
			return;
		}

		RecomputeHiddenRows();
		EnsureActiveCellVisible();
		NotifyStateChanged();
	}

	public void ClearAllFilters()
	{
		if (_columnFilters.Count == 0)
		{
			return;
		}

		_columnFilters.Clear();
		_hiddenRows.Clear();
		NotifyStateChanged();
	}

	public int FindNextVisibleRow(int fromRow, int direction)
	{
		if (Model.RowCount == 0)
		{
			return fromRow;
		}

		var step = direction >= 0 ? 1 : -1;
		var row = fromRow + step;
		while (row >= 0 && row < Model.RowCount)
		{
			if (!IsRowHidden(row))
			{
				return row;
			}

			row += step;
		}

		return fromRow;
	}

	public int GetFirstVisibleRow()
	{
		for (var r = 0; r < Model.RowCount; r++)
		{
			if (!IsRowHidden(r))
			{
				return r;
			}
		}

		return 0;
	}

	internal void RecomputeHiddenRows()
	{
		_hiddenRows.Clear();
		if (_columnFilters.Count == 0)
		{
			return;
		}

		for (var r = 0; r < Model.RowCount; r++)
		{
			if (!RowPassesAllFilters(r))
			{
				_hiddenRows.Add(r);
			}
		}
	}

	internal void ShiftFiltersAfterColumnInsert(int index)
	{
		if (_columnFilters.Count == 0)
		{
			return;
		}

		var next = new Dictionary<int, HashSet<string>>();
		foreach (var kvp in _columnFilters.OrderByDescending(k => k.Key))
		{
			var key = kvp.Key >= index ? kvp.Key + 1 : kvp.Key;
			next[key] = kvp.Value;
		}

		_columnFilters.Clear();
		foreach (var kvp in next)
		{
			_columnFilters[kvp.Key] = kvp.Value;
		}
	}

	internal void ShiftFiltersAfterColumnDelete(int index)
	{
		if (_columnFilters.Count == 0)
		{
			return;
		}

		var next = new Dictionary<int, HashSet<string>>();
		foreach (var kvp in _columnFilters)
		{
			if (kvp.Key == index)
			{
				continue;
			}

			var key = kvp.Key > index ? kvp.Key - 1 : kvp.Key;
			next[key] = kvp.Value;
		}

		_columnFilters.Clear();
		foreach (var kvp in next)
		{
			_columnFilters[kvp.Key] = kvp.Value;
		}

		RecomputeHiddenRows();
	}

	private bool RowPassesAllFilters(int row)
	{
		foreach (var kvp in _columnFilters)
		{
			if (!kvp.Value.Contains(GetFilterKey(row, kvp.Key)))
			{
				return false;
			}
		}

		return true;
	}

	private bool RowPassesOtherFilters(int row, int excludeCol)
	{
		foreach (var kvp in _columnFilters)
		{
			if (kvp.Key == excludeCol)
			{
				continue;
			}

			if (!kvp.Value.Contains(GetFilterKey(row, kvp.Key)))
			{
				return false;
			}
		}

		return true;
	}

	private string GetFilterKey(int row, int col)
	{
		var display = GetCellDisplay(row, col);
		return display ?? FilterBlankKey;
	}

	private void EnsureActiveCellVisible()
	{
		if (!IsRowHidden(ActiveCell.Row))
		{
			return;
		}

		var row = GetFirstVisibleRow();
		SetActiveCell(row, ActiveCell.Col, extendSelection: false, notify: false);
	}

	private static bool TryParseSortNumber(string value, out double number)
	{
		return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out number)
		       || double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out number);
	}
}
