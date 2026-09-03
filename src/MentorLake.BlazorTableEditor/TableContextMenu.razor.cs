using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MentorLake.BlazorTableEditor;

public partial class TableContextMenu : IAsyncDisposable
{
	[Inject] private IJSRuntime Js { get; set; }

	[Parameter] public SheetContext Sheet { get; set; }
	[Parameter] public EventCallback OnStructureChanged { get; set; }
	[Parameter] public EventCallback<int> OnRenameColumn { get; set; }
	[Parameter] public EventCallback OnCut { get; set; }
	[Parameter] public EventCallback OnCopy { get; set; }
	[Parameter] public EventCallback OnPaste { get; set; }

	private ElementReference _popoverRef;
	private IJSObjectReference _module;
	private double _x;
	private double _y;
	private int _row;
	private int _col;
	private bool _allowRename;
	private bool _domOpen;
	private bool _shouldShow;
	private bool _shouldHide;
	private bool _disposed;

	public bool IsOpen { get; private set; }

	private string AnchorStyle =>
		$"left:{_x.ToString(CultureInfo.InvariantCulture)}px;top:{_y.ToString(CultureInfo.InvariantCulture)}px;";

	private void GetDeleteRowRange(out int startRow, out int endRow)
	{
		var sel = Sheet.GetEffectiveSelection();
		if (sel.StartRow <= _row && _row <= sel.EndRow)
		{
			startRow = sel.StartRow;
			endRow = sel.EndRow;
		}
		else
		{
			startRow = _row;
			endRow = _row;
		}
	}

	private void GetDeleteColumnRange(out int startCol, out int endCol)
	{
		var sel = Sheet.GetEffectiveSelection();
		if (sel.StartCol <= _col && _col <= sel.EndCol)
		{
			startCol = sel.StartCol;
			endCol = sel.EndCol;
		}
		else
		{
			startCol = _col;
			endCol = _col;
		}
	}

	private int DeleteRowCount
	{
		get
		{
			GetDeleteRowRange(out var start, out var end);
			return end - start + 1;
		}
	}

	private int DeleteColumnCount
	{
		get
		{
			GetDeleteColumnRange(out var start, out var end);
			return end - start + 1;
		}
	}

	private bool CanDeleteRow
	{
		get
		{
			if (Sheet.Model.RowCount <= 1)
			{
				return false;
			}

			GetDeleteRowRange(out var start, out var end);
			return end - start + 1 < Sheet.Model.RowCount;
		}
	}

	private bool CanDeleteColumn
	{
		get
		{
			if (Sheet.Model.ColumnCount <= 1)
			{
				return false;
			}

			GetDeleteColumnRange(out var start, out var end);
			return end - start + 1 < Sheet.Model.ColumnCount;
		}
	}

	private string DeleteRowLabel => DeleteRowCount > 1 ? $"Delete {DeleteRowCount} rows" : "Delete row";
	private string DeleteColumnLabel => DeleteColumnCount > 1 ? $"Delete {DeleteColumnCount} columns" : "Delete column";

	public void Open(double x, double y, int row, int col, bool allowRename = false)
	{
		_x = x;
		_y = y;
		_row = row;
		_col = col;
		_allowRename = allowRename;
		IsOpen = true;
		_shouldHide = false;
		_shouldShow = true;
		StateHasChanged();
	}

	public void Close()
	{
		if (!IsOpen && !_domOpen)
		{
			return;
		}

		IsOpen = false;
		_allowRename = false;
		_shouldShow = false;
		_shouldHide = true;
		StateHasChanged();
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (_disposed)
		{
			return;
		}

		if (!_shouldShow && !_shouldHide)
		{
			return;
		}

		var show = _shouldShow;
		var hide = _shouldHide;
		_shouldShow = false;
		_shouldHide = false;

		await EnsureModuleAsync();
		if (_disposed || _module is null)
		{
			return;
		}

		if (hide || (show && _domOpen))
		{
			await HideDomAsync();
			_domOpen = false;
		}

		if (!show || !IsOpen)
		{
			return;
		}

		var opened = await _module.InvokeAsync<bool>("showPopover", _popoverRef);
		if (_disposed)
		{
			return;
		}

		if (!IsOpen)
		{
			if (opened)
			{
				await HideDomAsync();
			}

			_domOpen = false;
			return;
		}

		_domOpen = opened;
		if (!opened)
		{
			IsOpen = false;
			_allowRename = false;
		}
	}

	private async Task HideDomAsync()
	{
		try
		{
			await _module.InvokeVoidAsync("hidePopover", _popoverRef);
		}
		catch
		{
		}
	}

	private async Task EnsureModuleAsync()
	{
		if (_module is not null)
		{
			return;
		}

		try
		{
			_module = await Js.InvokeAsync<IJSObjectReference>(
				"import",
				$"./_content/MentorLake.BlazorTableEditor/{nameof(MentorLakeTableEditor)}.razor.js");
		}
		catch
		{
			_module = null;
		}
	}

	private async Task OnToggle(EventArgs e)
	{
		var open = false;
		if (_module is not null)
		{
			try
			{
				open = await _module.InvokeAsync<bool>("isPopoverOpen", _popoverRef);
			}
			catch
			{
			}
		}

		_domOpen = open;
		if (open == IsOpen)
		{
			return;
		}

		IsOpen = open;
		if (!open)
		{
			_allowRename = false;
		}

		StateHasChanged();
	}

	private async Task RenameHeaderAsync()
	{
		if (!_allowRename)
		{
			Close();
			return;
		}

		var index = _col;
		Close();
		await OnRenameColumn.InvokeAsync(index);
	}

	private async Task Undo()
	{
		Sheet.Undo();
		Close();
		await OnStructureChanged.InvokeAsync();
	}

	private async Task Redo()
	{
		Sheet.Redo();
		Close();
		await OnStructureChanged.InvokeAsync();
	}

	private async Task InsertColLeft()
	{
		Sheet.InsertColumn(_col);
		Close();
		await OnStructureChanged.InvokeAsync();
	}

	private async Task InsertColRight()
	{
		Sheet.InsertColumn(_col + 1);
		Close();
		await OnStructureChanged.InvokeAsync();
	}

	private async Task DeleteColumn()
	{
		if (!CanDeleteColumn)
		{
			Close();
			return;
		}

		GetDeleteColumnRange(out var start, out var end);
		Sheet.DeleteColumns(start, end);
		Close();
		await OnStructureChanged.InvokeAsync();
	}

	private async Task InsertRowAbove()
	{
		Sheet.InsertRow(_row);
		Close();
		await OnStructureChanged.InvokeAsync();
	}

	private async Task InsertRowBelow()
	{
		Sheet.InsertRow(_row + 1);
		Close();
		await OnStructureChanged.InvokeAsync();
	}

	private async Task DeleteRow()
	{
		if (!CanDeleteRow)
		{
			Close();
			return;
		}

		GetDeleteRowRange(out var start, out var end);
		Sheet.DeleteRows(start, end);
		Close();
		await OnStructureChanged.InvokeAsync();
	}

	private async Task CutAsync()
	{
		Close();
		await OnCut.InvokeAsync();
	}

	private async Task CopyAsync()
	{
		Close();
		await OnCopy.InvokeAsync();
	}

	private async Task PasteAsync()
	{
		Close();
		await OnPaste.InvokeAsync();
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		if (_module is not null)
		{
			try
			{
				if (_domOpen)
				{
					await HideDomAsync();
					_domOpen = false;
				}

				await _module.DisposeAsync();
			}
			catch (JSDisconnectedException)
			{
			}

			_module = null;
		}
	}
}
