using Microsoft.AspNetCore.Components;

namespace MentorLake.BlazorTableEditor;

public partial class TableContextMenu
{
	[Parameter] public SheetContext Sheet { get; set; }
	[Parameter] public EventCallback OnStructureChanged { get; set; }
	[Parameter] public EventCallback<int> OnRenameColumn { get; set; }
	[Parameter] public EventCallback OnCut { get; set; }
	[Parameter] public EventCallback OnCopy { get; set; }
	[Parameter] public EventCallback OnPaste { get; set; }

	private double _x;
	private double _y;
	private int _row;
	private int _col;
	private bool _allowRename;

	public bool IsOpen { get; private set; }

	private bool CanDeleteRow => Sheet.Model.RowCount > 1;
	private bool CanDeleteColumn => Sheet.Model.ColumnCount > 1;

	public void Open(double x, double y, int row, int col, bool allowRename = false)
	{
		_x = x;
		_y = y;
		_row = row;
		_col = col;
		_allowRename = allowRename;
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
		_allowRename = false;
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

		Sheet.DeleteColumn(_col);
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

		Sheet.DeleteRow(_row);
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
}
