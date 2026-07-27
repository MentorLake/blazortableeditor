namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{

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
}
