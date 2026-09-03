using Microsoft.AspNetCore.Components.Web;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{

	private void OnKeyDown(KeyboardEventArgs e)
	{
		if (_isEditing)
		{
			return;
		}

		if (e.CtrlKey || e.MetaKey)
		{
			switch (e.Key.ToLowerInvariant())
			{
				case "c":
					_ = CopyAsync();
					return;
				case "x":
					_ = CutAsync();
					return;
				case "v":
					_ = PasteAsync();
					return;
				case "z":
					if (e.ShiftKey)
						Redo();
					else
						Undo();
					return;
				case "y":
					Redo();
					return;
			}
		}

		if (_vvDropdown is not null && _vvDropdown.IsOpen)
		{
			switch (e.Key)
			{
				case "ArrowDown":
					_vvDropdown.MoveHighlight(1);
					return;
				case "ArrowUp":
					_vvDropdown.MoveHighlight(-1);
					return;
				case "Enter":
				case " ":
					_ = _vvDropdown.CommitHighlightAsync();
					return;
				case "Escape":
					CloseValidValueDropdown();
					return;
				case "ArrowLeft":
				case "ArrowRight":
				case "Tab":
					CloseValidValueDropdown();
					break;
				default:
					return;
			}
		}

		switch (e.Key)
		{
			case "ArrowRight":
				CloseValidValueDropdown();
				Context.SetActiveCell(Context.ActiveCell.Row, Context.ActiveCell.Col + 1, e.ShiftKey);
				SyncValidValueDropdownContent();
				break;
			case "ArrowLeft":
				CloseValidValueDropdown();
				Context.SetActiveCell(Context.ActiveCell.Row, Context.ActiveCell.Col - 1, e.ShiftKey);
				SyncValidValueDropdownContent();
				break;
			case "ArrowDown":
				CloseValidValueDropdown();
				Context.SetActiveCell(
					Context.FindNextVisibleRow(Context.ActiveCell.Row, 1),
					Context.ActiveCell.Col,
					e.ShiftKey);
				SyncValidValueDropdownContent();
				break;
			case "ArrowUp":
				CloseValidValueDropdown();
				Context.SetActiveCell(
					Context.FindNextVisibleRow(Context.ActiveCell.Row, -1),
					Context.ActiveCell.Col,
					e.ShiftKey);
				SyncValidValueDropdownContent();
				break;
			case "Enter":
			case "F2":
				BeginEdit(Context.ActiveCell.Row, Context.ActiveCell.Col);
				break;
			case " ":
				if (Context.HasValidValuesForColumn(Context.ActiveCell.Col))
				{
					OpenValidValueDropdown();
				}

				break;
			case "Escape":
				if (IsFilterPopupOpen)
				{
					CloseFilterPopup();
				}
				else if (IsContextMenuOpen)
				{
					CloseContextMenu();
				}
				else if (_clipboardMode != ClipboardVisualMode.None)
				{
					ClearClipboardVisual();
				}
				else
				{
					Context.ClearSelection();
				}

				break;
			case "Delete":
			case "Backspace":
				CloseValidValueDropdown();
				Context.ClearSelectionValues();
				break;
			case "Tab":
				CloseValidValueDropdown();
				Context.SetActiveCell(Context.ActiveCell.Row, Context.ActiveCell.Col + (e.ShiftKey ? -1 : 1));
				SyncValidValueDropdownContent();
				break;
			default:
				if (e.Key.Length == 1 && !e.CtrlKey && !e.AltKey && !e.MetaKey)
				{
					if (Context.HasValidValuesForColumn(Context.ActiveCell.Col))
					{
						break;
					}

					_editValue = e.Key;
					_editPos = Context.ActiveCell;
					_headerEditKind = HeaderEditKind.None;
					_headerEditIndex = -1;
					_isEditing = true;
					StateHasChanged();
				}

				break;
		}
	}
}
