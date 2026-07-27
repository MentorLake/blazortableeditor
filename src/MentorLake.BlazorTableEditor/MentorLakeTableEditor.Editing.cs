using MentorLake.BlazorTableEditor.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{

	private bool _isEditing;
	private HeaderEditKind _headerEditKind = HeaderEditKind.None;
	private int _headerEditIndex = -1;
	private string _editValue = string.Empty;
	private CellPosition _editPos = CellPosition.Invalid;
	private bool _suppressBlurCommit;

	private enum HeaderEditKind
	{
		None,
		Column
	}

	private bool IsEditingHeader => _headerEditKind != HeaderEditKind.None;
	private bool IsEditingColumnHeader(int col) =>
		_isEditing && _headerEditKind == HeaderEditKind.Column && _headerEditIndex == col;

	private void BeginEdit(int row, int col)
	{
		if (IsEditingHeader)
		{
			CommitEdit();
		}

		Context.SetActiveCell(row, col);
		var cell = Context.GetValue(row, col);
		_editValue = cell?.Value?.ToString() ?? string.Empty;
		_editPos = new CellPosition(row, col);
		_headerEditKind = HeaderEditKind.None;
		_headerEditIndex = -1;
		_isEditing = true;
		StateHasChanged();
	}

	private void BeginHeaderEdit(HeaderEditKind kind, int index, bool selectHeader = true)
	{
		if (kind != HeaderEditKind.Column || index < 0 || index >= Context.Model.ColumnCount)
		{
			return;
		}

		if (_isEditing)
		{
			// Already editing this same header — ignore repeated dblclick.
			if (_headerEditKind == kind && _headerEditIndex == index)
			{
				return;
			}

			CommitEdit();
		}

		if (selectHeader)
		{
			Context.SelectColumn(index, extendSelection: false);
		}

		_editValue = Context.Model.ColumnHeaders[index];
		_headerEditKind = kind;
		_headerEditIndex = index;
		_editPos = CellPosition.Invalid;
		_isEditing = true;
		_pressedColHeader = null;
		_pressedRowHeader = null;
		StateHasChanged();
	}

	private void CommitEdit()
	{
		if (_suppressBlurCommit)
		{
			_suppressBlurCommit = false;
			return;
		}

		if (!_isEditing)
		{
			return;
		}

		if (IsEditingHeader)
		{
			if (_headerEditKind == HeaderEditKind.Column)
			{
				Context.SetColumnHeader(_headerEditIndex, _editValue);
			}

			ClearEditState();
			_ = FocusRootAsync();
			StateHasChanged();
			return;
		}

		if (!_editPos.IsValid)
		{
			return;
		}

		Context.SetValue(_editPos.Row, _editPos.Col, _editValue);
		ClearEditState();
		_ = FocusRootAsync();
		StateHasChanged();
	}

	private void CancelEdit()
	{
		_suppressBlurCommit = true;
		ClearEditState();
		_ = FocusRootAsync();
		StateHasChanged();
	}

	private void ClearEditState()
	{
		_isEditing = false;
		_editPos = CellPosition.Invalid;
		_headerEditKind = HeaderEditKind.None;
		_headerEditIndex = -1;
		_editValue = string.Empty;
	}

	private void OnEditorKeyDown(KeyboardEventArgs e)
	{
		if (e.Key == "Enter")
		{
			var row = _editPos.Row;
			var col = _editPos.Col;
			_suppressBlurCommit = true;
			if (_isEditing && _editPos.IsValid)
			{
				Context.SetValue(row, col, _editValue);
			}

			ClearEditState();
			Context.SetActiveCell(row + 1, col);
			_ = FocusRootAsync();
		}
		else if (e.Key == "Escape")
		{
			CancelEdit();
		}
		else if (e.Key == "Tab")
		{
			var row = _editPos.Row;
			var col = _editPos.Col;
			_suppressBlurCommit = true;
			if (_isEditing && _editPos.IsValid)
			{
				Context.SetValue(row, col, _editValue);
			}

			ClearEditState();
			Context.SetActiveCell(row, col + (e.ShiftKey ? -1 : 1));
			_ = FocusRootAsync();
		}
	}

	private void OnHeaderEditorKeyDown(KeyboardEventArgs e)
	{
		if (e.Key == "Enter")
		{
			var index = _headerEditIndex;
			var value = _editValue;
			_suppressBlurCommit = true;
			if (_isEditing && IsEditingHeader && _headerEditKind == HeaderEditKind.Column)
			{
				Context.SetColumnHeader(index, value);
			}

			ClearEditState();
			_ = FocusRootAsync();
			StateHasChanged();
		}
		else if (e.Key == "Escape")
		{
			CancelEdit();
		}
		else if (e.Key == "Tab")
		{
			var index = _headerEditIndex;
			var value = _editValue;
			_suppressBlurCommit = true;
			if (_isEditing && IsEditingHeader && _headerEditKind == HeaderEditKind.Column)
			{
				Context.SetColumnHeader(index, value);
			}

			ClearEditState();

			var next = index + (e.ShiftKey ? -1 : 1);
			if (next >= 0 && next < Context.Model.ColumnCount)
			{
				BeginHeaderEdit(HeaderEditKind.Column, next);
			}
			else
			{
				_ = FocusRootAsync();
				StateHasChanged();
			}
		}
	}
}
