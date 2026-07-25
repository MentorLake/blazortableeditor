using MentorLake.BlazorTableEditor.Models;

namespace MentorLake.BlazorTableEditor;

public class SheetContext
{
	public const int DefaultRowHeight = 28;
	public const int DefaultColumnWidth = 100;
	public const int RowHeaderWidth = 48;
	public const int ColumnHeaderHeight = 28;

	public TableDataModel Model { get; private set; }

	public CellPosition ActiveCell { get; private set; } = new(0, 0);
	public CellPosition SelectionAnchor { get; private set; } = new(0, 0);
	public CellRegion? CurrentSelection { get; private set; }

	public Dictionary<int, int> RowHeights { get; } = new();
	public Dictionary<int, int> ColumnWidths { get; } = new();

	public event Action? StateChanged;
	public event Action? DataChanged;

	private bool _isDragFilling;
	private CellRegion _dragFillSource;
	private CellRegion? _dragFillPreview;

	private readonly Stack<Snapshot> _undoStack = new();
	private readonly Stack<Snapshot> _redoStack = new();

	private ITableValidator? _validator;
	private readonly Dictionary<CellPosition, string> _validationErrors = new();

	public CellRegion? DragFillPreview => _dragFillPreview;
	public bool IsDragFilling => _isDragFilling;

	public bool CanUndo => _undoStack.Count > 0;
	public bool CanRedo => _redoStack.Count > 0;

	public IReadOnlyDictionary<CellPosition, string> ValidationErrors => _validationErrors;

	public ITableValidator? Validator => _validator;

	public void SetValidator(ITableValidator? validator)
	{
		_validator = validator;
		Revalidate();
	}

	public bool HasError(int row, int col) =>
		_validationErrors.ContainsKey(new CellPosition(row, col));

	public string? GetError(int row, int col)
	{
		_validationErrors.TryGetValue(new CellPosition(row, col), out var msg);
		return msg;
	}

	private void Revalidate()
	{
		_validationErrors.Clear();
		if (_validator is null)
		{
			NotifyStateChanged();
			return;
		}

		var errors = _validator.Validate(Model);
		if (errors is not null)
		{
			foreach (var kvp in errors)
			{
				if (kvp.Key.IsValid && !string.IsNullOrEmpty(kvp.Value))
				{
					_validationErrors[kvp.Key] = kvp.Value;
				}
			}
		}

		NotifyStateChanged();
	}

	private void RevalidateInternal()
	{
		if (_validator is not null)
		{
			_validationErrors.Clear();
			var errors = _validator.Validate(Model);
			if (errors is not null)
			{
				foreach (var kvp in errors)
				{
					if (kvp.Key.IsValid && !string.IsNullOrEmpty(kvp.Value))
					{
						_validationErrors[kvp.Key] = kvp.Value;
					}
				}
			}
		}
	}

	public void Undo()
	{
		if (_undoStack.Count == 0) return;
		var current = CaptureSnapshot();
		var previous = _undoStack.Pop();
		_redoStack.Push(current);
		RestoreSnapshot(previous);
	}

	public void Redo()
	{
		if (_redoStack.Count == 0) return;
		var current = CaptureSnapshot();
		var next = _redoStack.Pop();
		_undoStack.Push(current);
		RestoreSnapshot(next);
	}

	private void PushUndoSnapshot()
	{
		_undoStack.Push(CaptureSnapshot());
		_redoStack.Clear();
	}

	private Snapshot CaptureSnapshot()
	{
		var cellsCopy = new Dictionary<string, CellValue>();
		foreach (var kvp in Model.Cells)
		{
			if (kvp.Value is not null)
			{
				cellsCopy[kvp.Key] = kvp.Value.Clone();
			}
		}

		return new Snapshot
		{
			Cells = cellsCopy,
			RowHeaders = new List<string>(Model.RowHeaders),
			ColumnHeaders = new List<string>(Model.ColumnHeaders),
			RowHeights = new Dictionary<int, int>(RowHeights),
			ColumnWidths = new Dictionary<int, int>(ColumnWidths),
			ActiveCell = ActiveCell,
			SelectionAnchor = SelectionAnchor,
			CurrentSelection = CurrentSelection
		};
	}

	private void RestoreSnapshot(Snapshot snap)
	{
		Model.Cells = new Dictionary<string, CellValue>();
		foreach (var kvp in snap.Cells)
		{
			if (kvp.Value is not null)
			{
				Model.Cells[kvp.Key] = kvp.Value.Clone();
			}
		}

		Model.RowHeaders.Clear();
		Model.RowHeaders.AddRange(snap.RowHeaders);

		Model.ColumnHeaders.Clear();
		Model.ColumnHeaders.AddRange(snap.ColumnHeaders);

		RowHeights.Clear();
		foreach (var kvp in snap.RowHeights)
		{
			RowHeights[kvp.Key] = kvp.Value;
		}

		ColumnWidths.Clear();
		foreach (var kvp in snap.ColumnWidths)
		{
			ColumnWidths[kvp.Key] = kvp.Value;
		}

		ActiveCell = snap.ActiveCell;
		SelectionAnchor = snap.SelectionAnchor;
		CurrentSelection = snap.CurrentSelection;

		NotifyDataChanged();
		NotifyStateChanged();
	}

	private sealed class Snapshot
	{
		public Dictionary<string, CellValue> Cells { get; init; } = new();
		public List<string> RowHeaders { get; init; } = new();
		public List<string> ColumnHeaders { get; init; } = new();
		public Dictionary<int, int> RowHeights { get; init; } = new();
		public Dictionary<int, int> ColumnWidths { get; init; } = new();
		public CellPosition ActiveCell { get; init; }
		public CellPosition SelectionAnchor { get; init; }
		public CellRegion? CurrentSelection { get; init; }
	}

	public SheetContext(TableDataModel? model = null, bool addSampleIfEmpty = true)
	{
		Model = model ?? new TableDataModel();
		if (addSampleIfEmpty && Model.Cells.Count == 0)
		{
			Model.AddSampleData();
		}

		for (int r = 0; r < Model.RowCount; r++)
		{
			RowHeights[r] = DefaultRowHeight;
		}

		for (int c = 0; c < Model.ColumnCount; c++)
		{
			ColumnWidths[c] = DefaultColumnWidth;
		}

		CurrentSelection = new CellRegion(0, 0, 0, 0);
		ActiveCell = new CellPosition(0, 0);
		SelectionAnchor = ActiveCell;
	}

	public void NotifyStateChanged() => StateChanged?.Invoke();
	public void NotifyDataChanged()
	{
		RevalidateInternal();
		DataChanged?.Invoke();
	}

	public int GetRowHeight(int row) =>
		RowHeights.TryGetValue(row, out var h) ? h : DefaultRowHeight;

	public int GetColumnWidth(int col) =>
		ColumnWidths.TryGetValue(col, out var w) ? w : DefaultColumnWidth;

	public void SetRowHeight(int row, int height)
	{
		RowHeights[row] = Math.Max(18, height);
		NotifyStateChanged();
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

	public CellValue? GetValue(int row, int col) => Model.GetCell(row, col);

	public string GetCellDisplay(int row, int col) => GetValue(row, col)?.ToString() ?? string.Empty;

	public void SetValue(int row, int col, object? value, string? format = null)
	{
		PushUndoSnapshot();
		if (value is null || (value is string s && string.IsNullOrEmpty(s)))
		{
			Model.ClearCell(row, col);
		}
		else
		{
			var cell = GetValue(row, col) ?? new CellValue();
			cell.Value = value;
			if (format is not null)
			{
				cell.Format = format;
			}

			Model.SetCell(row, col, cell);
		}

		NotifyDataChanged();
		NotifyStateChanged();
	}

	public void SetActiveCell(int row, int col, bool extendSelection = false)
	{
		if (Model.RowCount == 0 || Model.ColumnCount == 0)
		{
			return;
		}

		row = Math.Clamp(row, 0, Model.RowCount - 1);
		col = Math.Clamp(col, 0, Model.ColumnCount - 1);

		ActiveCell = new CellPosition(row, col);

		if (extendSelection)
		{
			CurrentSelection = new CellRegion(
				SelectionAnchor.Row,
				SelectionAnchor.Col,
				row,
				col).Normalize();
		}
		else
		{
			SelectionAnchor = ActiveCell;
			CurrentSelection = new CellRegion(row, col, row, col);
		}

		NotifyStateChanged();
	}

	public void UpdateSelectionTo(int row, int col)
	{
		if (Model.RowCount == 0 || Model.ColumnCount == 0)
		{
			return;
		}

		row = Math.Clamp(row, 0, Model.RowCount - 1);
		col = Math.Clamp(col, 0, Model.ColumnCount - 1);

		CurrentSelection = new CellRegion(
			SelectionAnchor.Row,
			SelectionAnchor.Col,
			row,
			col).Normalize();

		NotifyStateChanged();
	}

	public void UpdateSelection(CellRegion region)
	{
		CurrentSelection = region.Normalize();
		NotifyStateChanged();
	}

	public void ClearSelection()
	{
		CurrentSelection = new CellRegion(ActiveCell.Row, ActiveCell.Col, ActiveCell.Row, ActiveCell.Col);
		NotifyStateChanged();
	}

	public void SelectColumn(int col, bool extendSelection = false)
	{
		if (Model.RowCount == 0 || Model.ColumnCount == 0)
		{
			return;
		}

		col = Math.Clamp(col, 0, Model.ColumnCount - 1);
		int lastRow = Model.RowCount - 1;

		if (extendSelection)
		{
			int startCol = Math.Min(SelectionAnchor.Col, col);
			int endCol = Math.Max(SelectionAnchor.Col, col);
			ActiveCell = new CellPosition(0, col);
			CurrentSelection = new CellRegion(0, startCol, lastRow, endCol);
		}
		else
		{
			ActiveCell = new CellPosition(0, col);
			SelectionAnchor = ActiveCell;
			CurrentSelection = new CellRegion(0, col, lastRow, col);
		}

		NotifyStateChanged();
	}

	public void SelectRow(int row, bool extendSelection = false)
	{
		if (Model.RowCount == 0 || Model.ColumnCount == 0)
		{
			return;
		}

		row = Math.Clamp(row, 0, Model.RowCount - 1);
		int lastCol = Model.ColumnCount - 1;

		if (extendSelection)
		{
			int startRow = Math.Min(SelectionAnchor.Row, row);
			int endRow = Math.Max(SelectionAnchor.Row, row);
			ActiveCell = new CellPosition(row, 0);
			CurrentSelection = new CellRegion(startRow, 0, endRow, lastCol);
		}
		else
		{
			ActiveCell = new CellPosition(row, 0);
			SelectionAnchor = ActiveCell;
			CurrentSelection = new CellRegion(row, 0, row, lastCol);
		}

		NotifyStateChanged();
	}

	public bool IsSelected(int row, int col) =>
		CurrentSelection?.Contains(new CellPosition(row, col)) == true;

	public bool IsActive(int row, int col) =>
		ActiveCell.Row == row && ActiveCell.Col == col;

	public bool IsColumnHeaderSelected(int col)
	{
		if (CurrentSelection is not { } sel || Model.RowCount == 0)
		{
			return false;
		}

		var n = sel.Normalize();
		return n.StartCol <= col && col <= n.EndCol
		                         && n.StartRow == 0 && n.EndRow == Model.RowCount - 1;
	}

	public bool IsRowHeaderSelected(int row)
	{
		if (CurrentSelection is not { } sel || Model.ColumnCount == 0)
		{
			return false;
		}

		var n = sel.Normalize();
		return n.StartRow <= row && row <= n.EndRow
		                         && n.StartCol == 0 && n.EndCol == Model.ColumnCount - 1;
	}

	public void InsertRow(int index)
	{
		if (index < 0 || index > Model.RowCount)
		{
			return;
		}

		PushUndoSnapshot();
		Model.RowHeaders.Insert(index, (Model.RowCount + 1).ToString());

		var newCells = new Dictionary<string, CellValue>();
		foreach (var kvp in Model.Cells)
		{
			ParseKey(kvp.Key, out int r, out int c);
			if (r >= index)
			{
				newCells[$"{r + 1},{c}"] = kvp.Value;
			}
			else
			{
				newCells[kvp.Key] = kvp.Value;
			}
		}

		Model.Cells = newCells;

		ShiftMap(RowHeights, index, insert: true, DefaultRowHeight);

		for (int r = 0; r < Model.RowCount; r++)
		{
			Model.RowHeaders[r] = (r + 1).ToString();
		}

		AdjustSelectionAfterRowInsert(index);
		NotifyDataChanged();
		NotifyStateChanged();
	}

	public void DeleteRow(int index)
	{
		if (index < 0 || index >= Model.RowCount || Model.RowCount <= 1)
		{
			return;
		}

		PushUndoSnapshot();
		Model.RowHeaders.RemoveAt(index);

		var newCells = new Dictionary<string, CellValue>();
		foreach (var kvp in Model.Cells)
		{
			ParseKey(kvp.Key, out int r, out int c);
			if (r < index)
			{
				newCells[kvp.Key] = kvp.Value;
			}
			else if (r > index)
			{
				newCells[$"{r - 1},{c}"] = kvp.Value;
			}
		}

		Model.Cells = newCells;

		ShiftMap(RowHeights, index, insert: false, DefaultRowHeight);

		for (int r = 0; r < Model.RowCount; r++)
		{
			Model.RowHeaders[r] = (r + 1).ToString();
		}

		AdjustSelectionAfterRowDelete(index);
		NotifyDataChanged();
		NotifyStateChanged();
	}

	public void InsertColumn(int index)
	{
		if (index < 0 || index > Model.ColumnCount)
		{
			return;
		}

		PushUndoSnapshot();
		Model.ColumnHeaders.Insert(index, GetColumnLetter(Model.ColumnCount));

		var newCells = new Dictionary<string, CellValue>();
		foreach (var kvp in Model.Cells)
		{
			ParseKey(kvp.Key, out int r, out int c);
			if (c >= index)
			{
				newCells[$"{r},{c + 1}"] = kvp.Value;
			}
			else
			{
				newCells[kvp.Key] = kvp.Value;
			}
		}

		Model.Cells = newCells;

		ShiftMap(ColumnWidths, index, insert: true, DefaultColumnWidth);

		for (int c = 0; c < Model.ColumnCount; c++)
		{
			Model.ColumnHeaders[c] = GetColumnLetter(c);
		}

		AdjustSelectionAfterColumnInsert(index);
		NotifyDataChanged();
		NotifyStateChanged();
	}

	public void DeleteColumn(int index)
	{
		if (index < 0 || index >= Model.ColumnCount || Model.ColumnCount <= 1)
		{
			return;
		}

		Model.ColumnHeaders.RemoveAt(index);

		var newCells = new Dictionary<string, CellValue>();
		foreach (var kvp in Model.Cells)
		{
			ParseKey(kvp.Key, out int r, out int c);
			if (c < index)
			{
				newCells[kvp.Key] = kvp.Value;
			}
			else if (c > index)
			{
				newCells[$"{r},{c - 1}"] = kvp.Value;
			}
		}

		Model.Cells = newCells;

		ShiftMap(ColumnWidths, index, insert: false, DefaultColumnWidth);

		for (int c = 0; c < Model.ColumnCount; c++)
		{
			Model.ColumnHeaders[c] = GetColumnLetter(c);
		}

		AdjustSelectionAfterColumnDelete(index);
		NotifyDataChanged();
		NotifyStateChanged();
	}

	public CellRegion GetEffectiveSelection()
	{
		if (CurrentSelection is { } sel)
		{
			return sel.Normalize();
		}

		return new CellRegion(ActiveCell.Row, ActiveCell.Col, ActiveCell.Row, ActiveCell.Col);
	}

	public ClipboardGrid CopySelection()
	{
		var region = GetEffectiveSelection();
		int rows = region.Height;
		int cols = region.Width;
		var cells = new CellValue?[rows, cols];

		for (int r = 0; r < rows; r++)
		{
			for (int c = 0; c < cols; c++)
			{
				var source = GetValue(region.StartRow + r, region.StartCol + c);
				cells[r, c] = source?.Clone();
			}
		}

		return new ClipboardGrid { Rows = rows, Cols = cols, Cells = cells };
	}

	public ClipboardGrid CutSelection()
	{
		var grid = CopySelection();
		ClearSelectionValues();
		return grid;
	}

	public void ClearSelectionValues()
	{
		PushUndoSnapshot();
		var region = GetEffectiveSelection();
		for (int r = region.StartRow; r <= region.EndRow; r++)
		{
			for (int c = region.StartCol; c <= region.EndCol; c++)
			{
				Model.ClearCell(r, c);
			}
		}

		NotifyDataChanged();
		NotifyStateChanged();
	}

	public CellRegion PasteClipboard(ClipboardGrid clipboard, bool tileToSelection = true)
	{
		if (clipboard.IsEmpty || Model.RowCount == 0 || Model.ColumnCount == 0)
		{
			return GetEffectiveSelection();
		}

		PushUndoSnapshot();

		var selection = GetEffectiveSelection();
		int startRow = selection.StartRow;
		int startCol = selection.StartCol;

		int pasteRows = clipboard.Rows;
		int pasteCols = clipboard.Cols;

		if (tileToSelection && (selection.Height > 1 || selection.Width > 1))
		{
			if (selection.Height >= clipboard.Rows && selection.Height % clipboard.Rows == 0)
			{
				pasteRows = selection.Height;
			}

			if (selection.Width >= clipboard.Cols && selection.Width % clipboard.Cols == 0)
			{
				pasteCols = selection.Width;
			}
		}

		pasteRows = Math.Min(pasteRows, Model.RowCount - startRow);
		pasteCols = Math.Min(pasteCols, Model.ColumnCount - startCol);

		for (int r = 0; r < pasteRows; r++)
		{
			for (int c = 0; c < pasteCols; c++)
			{
				int tr = startRow + r;
				int tc = startCol + c;
				var source = clipboard.Cells[r % clipboard.Rows, c % clipboard.Cols];
				if (source is null || source.Value is null)
				{
					Model.ClearCell(tr, tc);
				}
				else
				{
					Model.SetCell(tr, tc, source.Clone());
				}
			}
		}

		var pasted = new CellRegion(startRow, startCol, startRow + pasteRows - 1, startCol + pasteCols - 1);
		CurrentSelection = pasted;
		SelectionAnchor = new CellPosition(pasted.StartRow, pasted.StartCol);
		ActiveCell = new CellPosition(pasted.StartRow, pasted.StartCol);

		NotifyDataChanged();
		NotifyStateChanged();
		return pasted;
	}

	public void StartDragFill()
	{
		_dragFillSource = (CurrentSelection ?? new CellRegion(ActiveCell.Row, ActiveCell.Col, ActiveCell.Row, ActiveCell.Col)).Normalize();
		_isDragFilling = true;
		_dragFillPreview = _dragFillSource;
		NotifyStateChanged();
	}

	public void UpdateDragFillPreview(int row, int col)
	{
		if (!_isDragFilling)
		{
			return;
		}

		row = Math.Clamp(row, 0, Model.RowCount - 1);
		col = Math.Clamp(col, 0, Model.ColumnCount - 1);

		var source = _dragFillSource;
		int startRow = source.StartRow;
		int endRow = source.EndRow;
		int startCol = source.StartCol;
		int endCol = source.EndCol;

		if (row > source.EndRow)
		{
			endRow = row;
		}
		else if (row < source.StartRow)
		{
			startRow = row;
		}

		if (col > source.EndCol)
		{
			endCol = col;
		}
		else if (col < source.StartCol)
		{
			startCol = col;
		}

		_dragFillPreview = new CellRegion(startRow, startCol, endRow, endCol).Normalize();
		NotifyStateChanged();
	}

	public void EndDragFill()
	{
		if (!_isDragFilling)
		{
			return;
		}

		if (_dragFillPreview is { } preview && !preview.Equals(_dragFillSource))
		{
			PushUndoSnapshot();
			ApplyDragFill(_dragFillSource, preview);
		}

		_isDragFilling = false;
		_dragFillPreview = null;
		NotifyStateChanged();
	}

	public void CancelDragFill()
	{
		_isDragFilling = false;
		_dragFillPreview = null;
		NotifyStateChanged();
	}

	private void ApplyDragFill(CellRegion source, CellRegion target)
	{
		source = source.Normalize();
		target = target.Normalize();

		if (source.Width <= 0 || source.Height <= 0)
		{
			return;
		}

		for (int tr = target.StartRow; tr <= target.EndRow; tr++)
		{
			for (int tc = target.StartCol; tc <= target.EndCol; tc++)
			{
				if (source.Contains(new CellPosition(tr, tc)))
				{
					continue;
				}

				int srcRowOffset = Math.Abs(tr - source.StartRow) % source.Height;
				int srcColOffset = Math.Abs(tc - source.StartCol) % source.Width;
				int sr = source.StartRow + srcRowOffset;
				int sc = source.StartCol + srcColOffset;

				var sourceValue = GetValue(sr, sc);
				if (sourceValue is not null)
				{
					Model.SetCell(tr, tc, new CellValue(sourceValue.Value) { Format = sourceValue.Format, BackgroundColor = sourceValue.BackgroundColor, TextColor = sourceValue.TextColor });
				}
				else
				{
					Model.ClearCell(tr, tc);
				}
			}
		}

		CurrentSelection = target;
		ActiveCell = new CellPosition(target.EndRow, target.EndCol);
		SelectionAnchor = new CellPosition(target.StartRow, target.StartCol);

		NotifyDataChanged();
		NotifyStateChanged();
	}

	private static void ParseKey(string key, out int row, out int col)
	{
		var parts = key.Split(',');
		row = int.Parse(parts[0]);
		col = int.Parse(parts[1]);
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

	private static string GetColumnLetter(int colIndex)
	{
		string letter = string.Empty;
		int index = colIndex;
		while (index >= 0)
		{
			letter = (char)('A' + (index % 26)) + letter;
			index = (index / 26) - 1;
		}

		return letter;
	}

	private void AdjustSelectionAfterRowInsert(int index)
	{
		ActiveCell = ShiftRowDown(ActiveCell, index);
		SelectionAnchor = ShiftRowDown(SelectionAnchor, index);
		if (CurrentSelection is { } sel)
		{
			var n = sel.Normalize();
			int start = n.StartRow >= index ? n.StartRow + 1 : n.StartRow;
			int end = n.EndRow >= index ? n.EndRow + 1 : n.EndRow;
			CurrentSelection = new CellRegion(start, n.StartCol, end, n.EndCol);
		}
	}

	private void AdjustSelectionAfterRowDelete(int index)
	{
		ActiveCell = ShiftRowUpAfterDelete(ActiveCell, index);
		SelectionAnchor = ShiftRowUpAfterDelete(SelectionAnchor, index);
		if (CurrentSelection is { } sel)
		{
			var n = sel.Normalize();
			if (n.StartRow == index && n.EndRow == index)
			{
				int row = Math.Min(index, Model.RowCount - 1);
				CurrentSelection = new CellRegion(row, n.StartCol, row, n.EndCol);
				ActiveCell = new CellPosition(row, Math.Clamp(ActiveCell.Col, n.StartCol, n.EndCol));
				SelectionAnchor = ActiveCell;
			}
			else
			{
				int start = n.StartRow > index ? n.StartRow - 1 : n.StartRow;
				int end = n.EndRow >= index ? n.EndRow - 1 : n.EndRow;
				if (end < start)
				{
					end = start;
				}

				start = Math.Clamp(start, 0, Model.RowCount - 1);
				end = Math.Clamp(end, 0, Model.RowCount - 1);
				CurrentSelection = new CellRegion(start, n.StartCol, end, n.EndCol);
			}
		}

		ClampSelectionToGrid();
	}

	private void AdjustSelectionAfterColumnInsert(int index)
	{
		ActiveCell = ShiftColRight(ActiveCell, index);
		SelectionAnchor = ShiftColRight(SelectionAnchor, index);
		if (CurrentSelection is { } sel)
		{
			var n = sel.Normalize();
			int start = n.StartCol >= index ? n.StartCol + 1 : n.StartCol;
			int end = n.EndCol >= index ? n.EndCol + 1 : n.EndCol;
			CurrentSelection = new CellRegion(n.StartRow, start, n.EndRow, end);
		}
	}

	private void AdjustSelectionAfterColumnDelete(int index)
	{
		ActiveCell = ShiftColLeftAfterDelete(ActiveCell, index);
		SelectionAnchor = ShiftColLeftAfterDelete(SelectionAnchor, index);
		if (CurrentSelection is { } sel)
		{
			var n = sel.Normalize();
			if (n.StartCol == index && n.EndCol == index)
			{
				int col = Math.Min(index, Model.ColumnCount - 1);
				CurrentSelection = new CellRegion(n.StartRow, col, n.EndRow, col);
				ActiveCell = new CellPosition(Math.Clamp(ActiveCell.Row, n.StartRow, n.EndRow), col);
				SelectionAnchor = ActiveCell;
			}
			else
			{
				int start = n.StartCol > index ? n.StartCol - 1 : n.StartCol;
				int end = n.EndCol >= index ? n.EndCol - 1 : n.EndCol;
				if (end < start)
				{
					end = start;
				}

				start = Math.Clamp(start, 0, Model.ColumnCount - 1);
				end = Math.Clamp(end, 0, Model.ColumnCount - 1);
				CurrentSelection = new CellRegion(n.StartRow, start, n.EndRow, end);
			}
		}

		ClampSelectionToGrid();
	}

	private static CellPosition ShiftRowDown(CellPosition pos, int index) =>
		pos.Row >= index ? new CellPosition(pos.Row + 1, pos.Col) : pos;

	private CellPosition ShiftRowUpAfterDelete(CellPosition pos, int index)
	{
		if (pos.Row > index)
		{
			return new CellPosition(pos.Row - 1, pos.Col);
		}

		if (pos.Row == index)
		{
			return new CellPosition(Math.Min(index, Model.RowCount - 1), pos.Col);
		}

		return pos;
	}

	private static CellPosition ShiftColRight(CellPosition pos, int index) =>
		pos.Col >= index ? new CellPosition(pos.Row, pos.Col + 1) : pos;

	private CellPosition ShiftColLeftAfterDelete(CellPosition pos, int index)
	{
		if (pos.Col > index)
		{
			return new CellPosition(pos.Row, pos.Col - 1);
		}

		if (pos.Col == index)
		{
			return new CellPosition(pos.Row, Math.Min(index, Model.ColumnCount - 1));
		}

		return pos;
	}

	private void ClampSelectionToGrid()
	{
		if (Model.RowCount == 0 || Model.ColumnCount == 0)
		{
			return;
		}

		int row = Math.Clamp(ActiveCell.Row, 0, Model.RowCount - 1);
		int col = Math.Clamp(ActiveCell.Col, 0, Model.ColumnCount - 1);
		ActiveCell = new CellPosition(row, col);

		int ar = Math.Clamp(SelectionAnchor.Row, 0, Model.RowCount - 1);
		int ac = Math.Clamp(SelectionAnchor.Col, 0, Model.ColumnCount - 1);
		SelectionAnchor = new CellPosition(ar, ac);

		if (CurrentSelection is { } sel)
		{
			var n = sel.Normalize();
			CurrentSelection = new CellRegion(
				Math.Clamp(n.StartRow, 0, Model.RowCount - 1),
				Math.Clamp(n.StartCol, 0, Model.ColumnCount - 1),
				Math.Clamp(n.EndRow, 0, Model.RowCount - 1),
				Math.Clamp(n.EndCol, 0, Model.ColumnCount - 1));
		}
	}
}
