using MentorLake.BlazorTableEditor.Models;

namespace MentorLake.BlazorTableEditor;

public partial class SheetContext
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

	public event Action StateChanged;
	public event Action DataChanged;

	public SheetContext(TableDataModel model = null, bool addSampleIfEmpty = true)
	{
		Model = model ?? new TableDataModel();
		if (addSampleIfEmpty && Model.Cells.Count == 0)
		{
			Model.AddSampleData();
		}

		for (var r = 0; r < Model.RowCount; r++)
		{
			RowHeights[r] = DefaultRowHeight;
		}

		for (var c = 0; c < Model.ColumnCount; c++)
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
		RecomputeHiddenRows();
		RevalidateInternal();
		DataChanged?.Invoke();
	}
}
