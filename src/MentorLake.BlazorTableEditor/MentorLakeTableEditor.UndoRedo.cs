namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{

	private void Undo()
	{
		if (ReadOnly)
		{
			return;
		}

		Context.Undo();
		RecomputeVisibleRange();
	}

	private void Redo()
	{
		if (ReadOnly)
		{
			return;
		}

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
}
