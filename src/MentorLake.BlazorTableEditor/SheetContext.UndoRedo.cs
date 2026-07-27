using MentorLake.BlazorTableEditor.Models;

namespace MentorLake.BlazorTableEditor;

public partial class SheetContext
{
	private readonly Stack<Snapshot> _undoStack = new();
	private readonly Stack<Snapshot> _redoStack = new();

	public bool CanUndo => _undoStack.Count > 0;
	public bool CanRedo => _redoStack.Count > 0;

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
}
