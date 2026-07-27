using Microsoft.AspNetCore.Components.Web;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{
	private TableContextMenu _contextMenu;

	private void OnRootContextMenu(MouseEventArgs e)
	{
		OpenContextMenu(e, Context.ActiveCell.Row, Context.ActiveCell.Col);
	}

	private void OpenContextMenu(MouseEventArgs e, int row, int col, bool selectCell = true, HeaderEditKind headerKind = HeaderEditKind.None)
	{
		if (_isEditing)
		{
			CommitEdit();
		}

		row = Math.Clamp(row, 0, Math.Max(0, Context.Model.RowCount - 1));
		col = Math.Clamp(col, 0, Math.Max(0, Context.Model.ColumnCount - 1));

		if (selectCell && !Context.IsSelected(row, col))
		{
			Context.SetActiveCell(row, col);
		}

		_isSelecting = false;
		_contextMenu.Open(e.ClientX, e.ClientY, row, col, allowRename: headerKind == HeaderEditKind.Column);
	}

	private void CloseContextMenu()
	{
		if (_contextMenu is not null)
		{
			_contextMenu.Close();
		}
	}

	private bool IsContextMenuOpen => _contextMenu is not null && _contextMenu.IsOpen;

	private void OnContextMenuStructureChanged() => OnSheetStructureChanged();

	private void OnToolbarStructureChanged() => OnSheetStructureChanged();

	private void OnSheetStructureChanged()
	{
		RecomputeVisibleRange();
	}

	private void OnContextMenuRenameColumn(int index)
	{
		BeginHeaderEdit(HeaderEditKind.Column, index, selectHeader: false);
	}
}
