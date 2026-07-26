using MentorLake.BlazorTableEditor.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor(IJSRuntime _jsRuntime) : IAsyncDisposable
{
	[Parameter] public TableDataModel? Model { get; set; }
	[Parameter] public EventCallback<TableDataModel> ModelChanged { get; set; }
	[Parameter] public ITableValidator? Validator { get; set; }
	[Parameter] public bool ShowToolbar { get; set; }
	[Parameter] public int ViewportOverscan { get; set; } = 4;
	private SheetContext Context { get; set; } = null!;
	private ElementReference _rootRef;
	private ElementReference _viewportRef;
	private ElementReference _editorRef;
	private ElementReference _headerEditorRef;
	private bool _isEditing;
	private HeaderEditKind _headerEditKind = HeaderEditKind.None;
	private int _headerEditIndex = -1;
	private string _editValue = string.Empty;
	private CellPosition _editPos = CellPosition.Invalid;
	private bool _suppressBlurCommit;

	private enum HeaderEditKind
	{
		None,
		Column,
		Row
	}

	private bool IsEditingHeader => _headerEditKind != HeaderEditKind.None;
	private bool IsEditingColumnHeader(int col) =>
		_isEditing && _headerEditKind == HeaderEditKind.Column && _headerEditIndex == col;
	private bool IsEditingRowHeader(int row) =>
		_isEditing && _headerEditKind == HeaderEditKind.Row && _headerEditIndex == row;
	private bool _isSelecting;
	private bool _isResizingCol;
	private bool _isResizingRow;
	private int? _pressedColHeader;
	private int? _pressedRowHeader;
	private double _scrollLeft;
	private double _scrollTop;
	private double _viewportWidth = 800;
	private double _viewportHeight = 500;
	private int _firstRow;
	private int _lastRow;
	private int _firstCol;
	private int _lastCol;
	private DotNetObjectReference<MentorLakeTableEditor>? _dotNetRef;
	private bool _jsReady;
	private ClipboardGrid _internalClipboard = ClipboardGrid.Empty;
	private CellRegion? _clipboardSource;
	private ClipboardVisualMode _clipboardMode = ClipboardVisualMode.None;
	private bool _contextMenuOpen;
	private double _contextMenuX;
	private double _contextMenuY;
	private int _contextRow;
	private int _contextCol;
	private HeaderEditKind _contextHeaderKind = HeaderEditKind.None;
	private InputFile? _csvInput;
	private string? _csvStatus;
	private bool _disposed;
	private IJSObjectReference? _module;
	private IJSObjectReference? _instance;

	private enum ClipboardVisualMode
	{
		None,
		Copy,
		Cut
	}

	protected override void OnInitialized()
	{
		Context = new SheetContext(Model, addSampleIfEmpty: true);
		Context.SetValidator(Validator);
		WireContext(Context);
		RecomputeVisibleRange();
	}

	protected override void OnParametersSet()
	{
		bool modelChanged = Model is not null && !ReferenceEquals(Context.Model, Model);
		bool validatorChanged = !ReferenceEquals(Context.Validator, Validator);

		if (modelChanged)
		{
			UnwireContext(Context);
			Context = new SheetContext(Model, addSampleIfEmpty: false);
			Context.SetValidator(Validator);
			WireContext(Context);
			RecomputeVisibleRange();
		}
		else if (validatorChanged)
		{
			Context.SetValidator(Validator);
		}
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!_jsReady)
		{
			await EnsureJsInitializedAsync();
		}

		if (_clearResizeClassAfterRender)
		{
			_clearResizeClassAfterRender = false;
			_isResizingCol = false;
			_isResizingRow = false;
			if (_instance is not null)
			{
				try
				{
					await _instance.InvokeVoidAsync("clearResizeClasses");
				}
				catch
				{
				}
			}

			await InvokeAsync(StateHasChanged);
		}

		if (_isEditing)
		{
			try
			{
				if (IsEditingHeader)
				{
					await _headerEditorRef.FocusAsync();
				}
				else
				{
					await _editorRef.FocusAsync();
				}
			}
			catch
			{
			}
		}
	}

	private async Task EnsureJsInitializedAsync()
	{
		try
		{
			_module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", $"./_content/MentorLake.BlazorTableEditor/{nameof(MentorLakeTableEditor)}.razor.js");
			_instance = await _module.InvokeAsync<IJSObjectReference>("createInstance");
			_dotNetRef ??= DotNetObjectReference.Create(this);
			var ok = await _instance.InvokeAsync<bool>("init", _viewportRef, _dotNetRef);
			_jsReady = ok;
			if (_jsReady)
			{
				await RefreshViewportMetricsAsync();
				StateHasChanged();
			}
		}
		catch (Exception e)
		{
			Console.WriteLine(e);

			_jsReady = false;
			if (_viewportWidth <= 0 || _viewportHeight <= 0)
			{
				_viewportWidth = 900;
				_viewportHeight = 520;
				RecomputeVisibleRange();
			}
		}
	}

	private async Task OnViewportScroll(EventArgs _)
	{
		if (_contextMenuOpen)
		{
			CloseContextMenu();
		}

		if (!_jsReady)
		{
			await EnsureJsInitializedAsync();
		}

		await RefreshViewportMetricsAsync();
		StateHasChanged();
	}

	private void WireContext(SheetContext ctx)
	{
		ctx.StateChanged += OnContextChanged;
		ctx.DataChanged += OnDataChanged;
	}

	private void UnwireContext(SheetContext ctx)
	{
		ctx.StateChanged -= OnContextChanged;
		ctx.DataChanged -= OnDataChanged;
	}

	private void OnContextChanged() => _ = InvokeAsync(StateHasChanged);

	private void OnDataChanged() => _ = InvokeAsync(async () =>
	{
		if (ModelChanged.HasDelegate)
		{
			await ModelChanged.InvokeAsync(Context.Model);
		}

		StateHasChanged();
	});

	private static string BuildCellClass(bool isActive, bool isSelected, bool inFill, bool inClipboard, ClipboardVisualMode mode, bool hasError = false)
	{
		var css = "bte-cell";
		if (isActive) css += " is-active";
		if (isSelected) css += " is-selected";
		if (inFill) css += " is-fill";
		if (inClipboard && mode == ClipboardVisualMode.Copy) css += " is-copied";
		if (inClipboard && mode == ClipboardVisualMode.Cut) css += " is-cut";
		if (hasError) css += " is-error";
		return css;
	}

	private bool IsInClipboardSource(int row, int col) =>
		_clipboardSource?.Contains(new CellPosition(row, col)) == true;

	private string GetClipboardOverlayClass() =>
		_clipboardMode == ClipboardVisualMode.Cut
			? "bte-clipboard-source is-cut"
			: "bte-clipboard-source is-copy";

	private string GetClipboardBadgeText() =>
		_clipboardMode == ClipboardVisualMode.Cut ? "CUT" : "COPIED";

	private void SetClipboardVisual(CellRegion region, ClipboardVisualMode mode)
	{
		_clipboardSource = region.Normalize();
		_clipboardMode = mode;
		StateHasChanged();
	}

	private void ClearClipboardVisual()
	{
		if (_clipboardMode == ClipboardVisualMode.None && _clipboardSource is null)
		{
			return;
		}

		_clipboardSource = null;
		_clipboardMode = ClipboardVisualMode.None;
		StateHasChanged();
	}

	private static string BuildCellStyle(int left, int top, int width, int height, CellValue? cell)
	{
		var style = $"left:{left}px;top:{top}px;width:{width}px;height:{height}px;";
		if (!string.IsNullOrEmpty(cell?.BackgroundColor) && cell!.BackgroundColor != "#ffffff")
		{
			style += $"background:{cell.BackgroundColor};";
		}

		if (!string.IsNullOrEmpty(cell?.TextColor) && cell!.TextColor != "#000000")
		{
			style += $"color:{cell.TextColor};";
		}

		return style;
	}

	private string GetRegionBox(CellRegion region)
	{
		var s = region.Normalize();
		var left = Context.GetColumnLeft(s.StartCol);
		var top = Context.GetRowTop(s.StartRow);
		var right = Context.GetColumnLeft(s.EndCol) + Context.GetColumnWidth(s.EndCol);
		var bottom = Context.GetRowTop(s.EndRow) + Context.GetRowHeight(s.EndRow);
		return $"left:{left}px;top:{top}px;width:{right - left}px;height:{bottom - top}px;";
	}

	private string GetActiveColName()
	{
		var col = Context.ActiveCell.Col;
		if (col >= 0 && col < Context.Model.ColumnHeaders.Count)
		{
			return Context.Model.ColumnHeaders[col];
		}

		return "?";
	}

	[JSInvokable]
	public Task OnViewportMetrics(double width, double height, double scrollLeft, double scrollTop)
	{
		_viewportWidth = Math.Max(1, width);
		_viewportHeight = Math.Max(1, height);
		_scrollLeft = Math.Max(0, scrollLeft);
		_scrollTop = Math.Max(0, scrollTop);
		RecomputeVisibleRange();
		return InvokeAsync(StateHasChanged);
	}

	private async Task RefreshViewportMetricsAsync()
	{
		if (!_jsReady)
		{
			return;
		}

		try
		{
			var metrics = await _instance.InvokeAsync<double[]>("getMetrics", _viewportRef);
			if (metrics is { Length: >= 4 })
			{
				_viewportWidth = Math.Max(1, metrics[0]);
				_viewportHeight = Math.Max(1, metrics[1]);
				_scrollLeft = Math.Max(0, metrics[2]);
				_scrollTop = Math.Max(0, metrics[3]);
				RecomputeVisibleRange();
			}
		}
		catch
		{
		}
	}

	private void RecomputeVisibleRange()
	{
		int rowCount = Context.Model.RowCount;
		int colCount = Context.Model.ColumnCount;
		if (rowCount == 0 || colCount == 0)
		{
			_firstRow = _lastRow = _firstCol = _lastCol = 0;
			return;
		}

		double bodyScrollTop = _scrollTop;
		double bodyViewHeight = Math.Max(1, _viewportHeight - SheetContext.ColumnHeaderHeight);
		double bodyScrollLeft = _scrollLeft;
		double bodyViewWidth = Math.Max(1, _viewportWidth - SheetContext.RowHeaderWidth);

		int y = 0;
		_firstRow = 0;
		for (int r = 0; r < rowCount; r++)
		{
			int h = Context.GetRowHeight(r);
			if (y + h > bodyScrollTop)
			{
				_firstRow = r;
				break;
			}

			y += h;
			_firstRow = r;
		}

		_firstRow = Math.Max(0, _firstRow - ViewportOverscan);

		double visibleBottom = bodyScrollTop + bodyViewHeight;
		y = Context.GetRowTop(_firstRow);
		_lastRow = _firstRow;
		for (int r = _firstRow; r < rowCount; r++)
		{
			y += Context.GetRowHeight(r);
			_lastRow = r;
			if (y >= visibleBottom)
			{
				break;
			}
		}

		_lastRow = Math.Min(rowCount - 1, _lastRow + ViewportOverscan);

		int x = 0;
		_firstCol = 0;
		for (int c = 0; c < colCount; c++)
		{
			int w = Context.GetColumnWidth(c);
			if (x + w > bodyScrollLeft)
			{
				_firstCol = c;
				break;
			}

			x += w;
			_firstCol = c;
		}

		_firstCol = Math.Max(0, _firstCol - ViewportOverscan);

		double visibleRight = bodyScrollLeft + bodyViewWidth;
		x = Context.GetColumnLeft(_firstCol);
		_lastCol = _firstCol;
		for (int c = _firstCol; c < colCount; c++)
		{
			x += Context.GetColumnWidth(c);
			_lastCol = c;
			if (x >= visibleRight)
			{
				break;
			}
		}

		_lastCol = Math.Min(colCount - 1, _lastCol + ViewportOverscan);
	}

	private void OnCellMouseDown(int row, int col, MouseEventArgs e)
	{
		if (_jsReady || e.Button != 0)
		{
			return;
		}

		if (_isEditing)
		{
			CommitEdit();
		}

		_isSelecting = true;
		Context.SetActiveCell(row, col, e.ShiftKey);
		_ = FocusRootAsync();
	}

	private void OnCellMouseEnter(int row, int col)
	{
		if (_jsReady)
		{
			return;
		}

		if (Context.IsDragFilling)
		{
			Context.UpdateDragFillPreview(row, col);
			return;
		}

		if (_isSelecting)
		{
			Context.UpdateSelectionTo(row, col);
		}
	}

	private void OnRootMouseLeave(MouseEventArgs e)
	{
		if (_isResizingCol || _isResizingRow || _jsReady)
		{
			return;
		}

		OnGlobalMouseUp(e);
	}

	private void OnGlobalMouseUp(MouseEventArgs e)
	{
		var hadPressedHeader = _pressedColHeader is not null || _pressedRowHeader is not null;
		if (hadPressedHeader)
		{
			_pressedColHeader = null;
			_pressedRowHeader = null;
		}

		if (_isResizingCol || _isResizingRow)
		{
			if (hadPressedHeader)
			{
				StateHasChanged();
			}

			return;
		}

		if (_jsReady)
		{
			if (hadPressedHeader)
			{
				StateHasChanged();
			}

			return;
		}

		if (Context.IsDragFilling)
		{
			Context.EndDragFill();
		}

		_isSelecting = false;

		if (hadPressedHeader)
		{
			StateHasChanged();
		}
	}

	[JSInvokable]
	public Task OnSelectionDragBegin(int row, int col, bool shiftKey)
	{
		if (_isEditing)
		{
			CommitEdit();
		}

		_isSelecting = true;
		Context.SetActiveCell(row, col, shiftKey, notify: false);
		_ = FocusRootAsync();
		return Task.CompletedTask;
	}

	[JSInvokable]
	public Task OnSelectionDragEnd(int row, int col)
	{
		_isSelecting = false;
		Context.UpdateSelectionTo(row, col, notify: true);
		_ = FocusRootAsync();
		return Task.CompletedTask;
	}

	[JSInvokable]
	public Task OnFillDragBegin()
	{
		Context.StartDragFill(notify: false);
		return Task.CompletedTask;
	}

	[JSInvokable]
	public Task OnFillDragEnd(int row, int col)
	{
		if (!Context.IsDragFilling)
		{
			Context.StartDragFill(notify: false);
		}

		Context.UpdateDragFillPreview(row, col, notify: false);
		Context.EndDragFill();
		return Task.CompletedTask;
	}

	private void OnCanvasMouseMove(MouseEventArgs e)
	{
	}

	private bool _clearResizeClassAfterRender;

	[JSInvokable]
	public Task OnColumnResizeBegin(int columnIndex)
	{
		_isResizingCol = true;
		_isSelecting = false;
		Context.BeginColumnResizeGesture();
		return Task.CompletedTask;
	}

	[JSInvokable]
	public Task OnColumnResizeEnd(int columnIndex, int width)
	{
		Context.SetColumnWidth(columnIndex, width, notify: false);
		_isResizingCol = true;
		_clearResizeClassAfterRender = true;
		RecomputeVisibleRange();
		return InvokeAsync(StateHasChanged);
	}

	[JSInvokable]
	public Task OnRowResizeBegin(int rowIndex)
	{
		_isResizingRow = true;
		_isSelecting = false;
		Context.BeginRowResizeGesture();
		return Task.CompletedTask;
	}

	[JSInvokable]
	public Task OnRowResizeEnd(int rowIndex, int height)
	{
		Context.SetRowHeight(rowIndex, height, notify: false);
		_isResizingRow = true;
		_clearResizeClassAfterRender = true;
		RecomputeVisibleRange();
		return InvokeAsync(StateHasChanged);
	}

	private void OnColHeaderMouseDown(int col, MouseEventArgs e)
	{
		if (e.Button != 0)
		{
			return;
		}

		// Keep focus in the header editor when clicking the header being edited.
		if (IsEditingColumnHeader(col))
		{
			return;
		}

		if (_isEditing)
		{
			CommitEdit();
		}

		_pressedColHeader = col;
		_pressedRowHeader = null;
		Context.SelectColumn(col, e.ShiftKey);
		_isSelecting = false;
		_ = FocusRootAsync();
	}

	private void OnRowHeaderMouseDown(int row, MouseEventArgs e)
	{
		if (e.Button != 0)
		{
			return;
		}

		// Keep focus in the header editor when clicking the header being edited.
		if (IsEditingRowHeader(row))
		{
			return;
		}

		if (_isEditing)
		{
			CommitEdit();
		}

		_pressedRowHeader = row;
		_pressedColHeader = null;
		Context.SelectRow(row, e.ShiftKey);
		_isSelecting = false;
		_ = FocusRootAsync();
	}

	private void OnHeaderMouseUp(MouseEventArgs e)
	{
		if (_pressedColHeader is null && _pressedRowHeader is null)
		{
			return;
		}

		_pressedColHeader = null;
		_pressedRowHeader = null;
		StateHasChanged();
	}

	private void OnFillHandleMouseDown(MouseEventArgs e)
	{
		if (_jsReady || e.Button != 0)
		{
			return;
		}

		Context.StartDragFill();
	}

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
		if (kind == HeaderEditKind.None || index < 0)
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

		if (kind == HeaderEditKind.Column)
		{
			if (index >= Context.Model.ColumnCount)
			{
				return;
			}

			if (selectHeader)
			{
				Context.SelectColumn(index, extendSelection: false);
			}
			_editValue = Context.Model.ColumnHeaders[index];
		}
		else
		{
			if (index >= Context.Model.RowCount)
			{
				return;
			}

			if (selectHeader)
			{
				Context.SelectRow(index, extendSelection: false);
			}
			_editValue = Context.Model.RowHeaders[index];
		}

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
			else if (_headerEditKind == HeaderEditKind.Row)
			{
				Context.SetRowHeader(_headerEditIndex, _editValue);
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
			var kind = _headerEditKind;
			var index = _headerEditIndex;
			var value = _editValue;
			_suppressBlurCommit = true;
			if (_isEditing && IsEditingHeader)
			{
				if (kind == HeaderEditKind.Column)
				{
					Context.SetColumnHeader(index, value);
				}
				else if (kind == HeaderEditKind.Row)
				{
					Context.SetRowHeader(index, value);
				}
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
			var kind = _headerEditKind;
			var index = _headerEditIndex;
			var value = _editValue;
			_suppressBlurCommit = true;
			if (_isEditing && IsEditingHeader)
			{
				if (kind == HeaderEditKind.Column)
				{
					Context.SetColumnHeader(index, value);
				}
				else if (kind == HeaderEditKind.Row)
				{
					Context.SetRowHeader(index, value);
				}
			}

			ClearEditState();

			var next = index + (e.ShiftKey ? -1 : 1);
			if (kind == HeaderEditKind.Column && next >= 0 && next < Context.Model.ColumnCount)
			{
				BeginHeaderEdit(HeaderEditKind.Column, next);
			}
			else if (kind == HeaderEditKind.Row && next >= 0 && next < Context.Model.RowCount)
			{
				BeginHeaderEdit(HeaderEditKind.Row, next);
			}
			else
			{
				_ = FocusRootAsync();
				StateHasChanged();
			}
		}
	}

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

		switch (e.Key)
		{
			case "ArrowRight":
				Context.SetActiveCell(Context.ActiveCell.Row, Context.ActiveCell.Col + 1, e.ShiftKey);
				break;
			case "ArrowLeft":
				Context.SetActiveCell(Context.ActiveCell.Row, Context.ActiveCell.Col - 1, e.ShiftKey);
				break;
			case "ArrowDown":
				Context.SetActiveCell(Context.ActiveCell.Row + 1, Context.ActiveCell.Col, e.ShiftKey);
				break;
			case "ArrowUp":
				Context.SetActiveCell(Context.ActiveCell.Row - 1, Context.ActiveCell.Col, e.ShiftKey);
				break;
			case "Enter":
			case "F2":
				BeginEdit(Context.ActiveCell.Row, Context.ActiveCell.Col);
				break;
			case "Escape":
				if (_contextMenuOpen)
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
				Context.ClearSelectionValues();
				break;
			case "Tab":
				Context.SetActiveCell(Context.ActiveCell.Row, Context.ActiveCell.Col + (e.ShiftKey ? -1 : 1));
				break;
			default:
				if (e.Key.Length == 1 && !e.CtrlKey && !e.AltKey && !e.MetaKey)
				{
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
		_contextRow = row;
		_contextCol = col;
		_contextHeaderKind = headerKind;

		if (selectCell && !Context.IsSelected(row, col))
		{
			Context.SetActiveCell(row, col);
		}

		_contextMenuX = e.ClientX;
		_contextMenuY = e.ClientY;
		_contextMenuOpen = true;
		_isSelecting = false;
		StateHasChanged();
	}

	private void CloseContextMenu()
	{
		if (!_contextMenuOpen)
		{
			return;
		}

		_contextMenuOpen = false;
		_contextHeaderKind = HeaderEditKind.None;
		StateHasChanged();
	}

	private bool CanRenameHeader => _contextHeaderKind != HeaderEditKind.None;

	private void ContextRenameHeader()
	{
		if (!CanRenameHeader)
		{
			CloseContextMenu();
			return;
		}

		var kind = _contextHeaderKind;
		var index = kind == HeaderEditKind.Column ? _contextCol : _contextRow;
		_contextMenuOpen = false;
		_contextHeaderKind = HeaderEditKind.None;
		BeginHeaderEdit(kind, index, selectHeader: false);
	}

	private bool CanDeleteRow => Context.Model.RowCount > 1;
	private bool CanDeleteColumn => Context.Model.ColumnCount > 1;

	private void ContextInsertColLeft()
	{
		Context.InsertColumn(_contextCol);
		CloseContextMenu();
		RecomputeVisibleRange();
	}

	private void ContextInsertColRight()
	{
		Context.InsertColumn(_contextCol + 1);
		CloseContextMenu();
		RecomputeVisibleRange();
	}

	private void ContextDeleteColumn()
	{
		if (!CanDeleteColumn)
		{
			CloseContextMenu();
			return;
		}

		Context.DeleteColumn(_contextCol);
		CloseContextMenu();
		RecomputeVisibleRange();
	}

	private void ContextInsertRowAbove()
	{
		Context.InsertRow(_contextRow);
		CloseContextMenu();
		RecomputeVisibleRange();
	}

	private void ContextInsertRowBelow()
	{
		Context.InsertRow(_contextRow + 1);
		CloseContextMenu();
		RecomputeVisibleRange();
	}

	private void ContextDeleteRow()
	{
		if (!CanDeleteRow)
		{
			CloseContextMenu();
			return;
		}

		Context.DeleteRow(_contextRow);
		CloseContextMenu();
		RecomputeVisibleRange();
	}

	private void ContextUndo()
	{
		Context.Undo();
		CloseContextMenu();
		RecomputeVisibleRange();
	}

	private void ContextRedo()
	{
		Context.Redo();
		CloseContextMenu();
		RecomputeVisibleRange();
	}

	private void Undo()
	{
		Context.Undo();
		RecomputeVisibleRange();
	}

	private void Redo()
	{
		Context.Redo();
		RecomputeVisibleRange();
	}

	private async Task UndoAsync()
	{
		Undo();
		await FocusRootAsync();
	}

	private async Task RedoAsync()
	{
		Redo();
		await FocusRootAsync();
	}

	private async Task ContextCutAsync()
	{
		CloseContextMenu();
		await CutAsync();
	}

	private async Task ContextCopyAsync()
	{
		CloseContextMenu();
		await CopyAsync();
	}

	private async Task ContextPasteAsync()
	{
		CloseContextMenu();
		await PasteAsync();
	}

	private async Task CopyAsync()
	{
		var region = Context.GetEffectiveSelection();
		_internalClipboard = Context.CopySelection();
		SetClipboardVisual(region, ClipboardVisualMode.Copy);
		await WriteClipboardTextAsync(_internalClipboard.ToTsv());
		await FocusRootAsync();
	}

	private async Task CutAsync()
	{
		var region = Context.GetEffectiveSelection();
		_internalClipboard = Context.CutSelection();
		SetClipboardVisual(region, ClipboardVisualMode.Cut);
		await WriteClipboardTextAsync(_internalClipboard.ToTsv());
		await FocusRootAsync();
	}

	private async Task PasteAsync()
	{
		var text = await ReadClipboardTextAsync();
		ClipboardGrid grid;

		if (!string.IsNullOrEmpty(text))
		{
			grid = ClipboardGrid.FromTsv(text);
			if (!grid.IsEmpty)
			{
				_internalClipboard = grid;
			}
		}

		if (_internalClipboard.IsEmpty)
		{
			return;
		}

		Context.PasteClipboard(_internalClipboard);
		ClearClipboardVisual();
		RecomputeVisibleRange();
		await FocusRootAsync();
		StateHasChanged();
	}

	private async Task WriteClipboardTextAsync(string text)
	{
		try
		{
			await _instance.InvokeVoidAsync("writeText", text);
		}
		catch
		{
		}
	}

	private async Task<string?> ReadClipboardTextAsync()
	{
		try
		{
			return await _instance.InvokeAsync<string?>("readText");
		}
		catch
		{
			return null;
		}
	}

	private async Task ExportCsvAsync()
	{
		try
		{
			var csv = Context.Model.ToCsv(includeHeaders: false);
			var fileName = $"table-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
			await _instance.InvokeVoidAsync("downloadText", fileName, csv, "text/csv;charset=utf-8");
			_csvStatus = $"Exported {fileName}";
		}
		catch (Exception ex)
		{
			_csvStatus = $"Export failed: {ex.Message}";
		}

		StateHasChanged();
	}

	private async Task TriggerCsvImport()
	{
		try
		{
			await _instance.InvokeVoidAsync("clickElement", "bte-csv-input");
		}
		catch
		{
			_csvStatus = "Could not open file picker";
			StateHasChanged();
		}
	}

	private async Task OnCsvFileSelected(InputFileChangeEventArgs e)
	{
		var file = e.File;
		if (file is null)
		{
			return;
		}

		try
		{
			await using var stream = file.OpenReadStream(maxAllowedSize: 20 * 1024 * 1024);
			using var reader = new StreamReader(stream);
			var csv = await reader.ReadToEndAsync();
			var model = TableDataModel.FromCsv(csv, firstRowIsHeader: true);

			UnwireContext(Context);
			Context = new SheetContext(model, addSampleIfEmpty: false);
			Context.SetValidator(Validator);
			WireContext(Context);
			_clipboardSource = null;
			_clipboardMode = ClipboardVisualMode.None;
			_scrollLeft = 0;
			_scrollTop = 0;
			RecomputeVisibleRange();

			if (_jsReady)
			{
				try
				{
					await _instance.InvokeVoidAsync("setScroll", _viewportRef, 0, 0);
				}
				catch
				{
				}
			}

			_csvStatus = $"Imported {file.Name} ({Context.Model.RowCount}×{Context.Model.ColumnCount})";
		}
		catch (Exception ex)
		{
			_csvStatus = $"Import failed: {ex.Message}";
		}

		StateHasChanged();
	}

	private async Task FocusRootAsync()
	{
		try
		{
			await _rootRef.FocusAsync();
		}
		catch
		{
		}
	}

	private void InsertRowAtActive() => Context.InsertRow(Context.ActiveCell.Row);
	private void DeleteRowAtActive() => Context.DeleteRow(Context.ActiveCell.Row);
	private void InsertColAtActive() => Context.InsertColumn(Context.ActiveCell.Col);
	private void DeleteColAtActive() => Context.DeleteColumn(Context.ActiveCell.Col);

	public async ValueTask DisposeAsync()
	{
		if (_disposed) return;
		_disposed = true;

		UnwireContext(Context);
		_dotNetRef?.Dispose();

		if (_instance is not null)
		{
			try
			{
				await _instance.InvokeVoidAsync("dispose");
			}
			catch (JSDisconnectedException)
			{

			}

			await _instance.DisposeAsync();
			_instance = null;
		}

		if (_module is not null)
		{
			await _module.DisposeAsync();
			_module = null;
		}
	}
}
