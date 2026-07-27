using Microsoft.AspNetCore.Components;

namespace MentorLake.BlazorTableEditor;

public partial class TableColumnFilterPopup
{
	[Parameter] public SheetContext Sheet { get; set; }
	[Parameter] public EventCallback OnFilterChanged { get; set; }

	public bool IsOpen { get; private set; }

	private int _col = -1;
	private double _x;
	private double _y;
	private string _columnTitle = string.Empty;
	private string _searchText = string.Empty;
	private List<string> _allValues = new();
	private HashSet<string> _checked = new(StringComparer.Ordinal);
	private bool _hasExistingFilter;
	private bool _selectAllChecked;

	private IEnumerable<string> VisibleValues
	{
		get
		{
			if (string.IsNullOrWhiteSpace(_searchText))
			{
				return _allValues;
			}

			return _allValues.Where(MatchesSearch);
		}
	}

	private bool CanApply => _checked.Count > 0;

	public void Open(int col, double clientX, double clientY)
	{
		if (Sheet is null || col < 0 || col >= Sheet.Model.ColumnCount)
		{
			return;
		}

		_col = col;
		_x = clientX;
		_y = clientY;
		_columnTitle = col < Sheet.Model.ColumnHeaders.Count
			? Sheet.Model.ColumnHeaders[col]
			: $"Column {col + 1}";
		_searchText = string.Empty;
		_allValues = Sheet.GetFilterValuesForColumn(col);
		_hasExistingFilter = Sheet.IsColumnFiltered(col);

		var existing = Sheet.GetColumnFilter(col);
		_checked = existing is not null
			? new HashSet<string>(existing, StringComparer.Ordinal)
			: new HashSet<string>(_allValues, StringComparer.Ordinal);

		RefreshSelectAllState();
		IsOpen = true;
		StateHasChanged();
	}

	public void Close()
	{
		if (!IsOpen)
		{
			return;
		}

		IsOpen = false;
		_col = -1;
		StateHasChanged();
	}

	private void OnSearchInput(ChangeEventArgs e)
	{
		_searchText = e.Value?.ToString() ?? string.Empty;
		RefreshSelectAllState();
	}

	private void OnSelectAllChanged(ChangeEventArgs e)
	{
		var isChecked = e.Value is bool b && b;
		foreach (var value in VisibleValues)
		{
			if (isChecked)
			{
				_checked.Add(value);
			}
			else
			{
				_checked.Remove(value);
			}
		}

		RefreshSelectAllState();
	}

	private void OnValueChanged(string key, ChangeEventArgs e)
	{
		var isChecked = e.Value is bool b && b;
		if (isChecked)
		{
			_checked.Add(key);
		}
		else
		{
			_checked.Remove(key);
		}

		RefreshSelectAllState();
	}

	private async Task ApplyAsync()
	{
		if (!CanApply || _col < 0)
		{
			return;
		}

		var allowed = new HashSet<string>(_checked, StringComparer.Ordinal);
		Sheet.ApplyColumnFilter(_col, allowed);
		IsOpen = false;
		_col = -1;
		StateHasChanged();
		await OnFilterChanged.InvokeAsync();
	}

	private async Task ClearFilterAsync()
	{
		if (_col < 0)
		{
			return;
		}

		Sheet.ClearColumnFilter(_col);
		IsOpen = false;
		_col = -1;
		StateHasChanged();
		await OnFilterChanged.InvokeAsync();
	}

	private void RefreshSelectAllState()
	{
		var visible = VisibleValues.ToList();
		_selectAllChecked = visible.Count > 0 && visible.All(v => _checked.Contains(v));
	}

	private bool MatchesSearch(string value)
	{
		var label = GetDisplayLabel(value);
		return label.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
	}

	private static string GetDisplayLabel(string key) =>
		key == SheetContext.FilterBlankKey ? SheetContext.FilterBlankDisplay : key;
}
