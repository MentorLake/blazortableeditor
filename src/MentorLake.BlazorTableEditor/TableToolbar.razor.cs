using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace MentorLake.BlazorTableEditor;

public partial class TableToolbar
{
	[Parameter] public SheetContext Sheet { get; set; }
	[Parameter] public string CsvStatus { get; set; }
	[Parameter] public EventCallback OnUndo { get; set; }
	[Parameter] public EventCallback OnRedo { get; set; }
	[Parameter] public EventCallback OnCut { get; set; }
	[Parameter] public EventCallback OnCopy { get; set; }
	[Parameter] public EventCallback OnPaste { get; set; }
	[Parameter] public EventCallback OnExportCsv { get; set; }
	[Parameter] public EventCallback OnImportCsv { get; set; }
	[Parameter] public EventCallback<InputFileChangeEventArgs> OnCsvFileSelected { get; set; }
	[Parameter] public EventCallback OnStructureChanged { get; set; }

	private async Task UndoAsync() => await OnUndo.InvokeAsync();
	private async Task RedoAsync() => await OnRedo.InvokeAsync();
	private async Task CutAsync() => await OnCut.InvokeAsync();
	private async Task CopyAsync() => await OnCopy.InvokeAsync();
	private async Task PasteAsync() => await OnPaste.InvokeAsync();
	private async Task ExportCsvAsync() => await OnExportCsv.InvokeAsync();
	private async Task ImportCsvAsync() => await OnImportCsv.InvokeAsync();
	private async Task OnCsvFileSelectedAsync(InputFileChangeEventArgs e) => await OnCsvFileSelected.InvokeAsync(e);

	private async Task InsertRowAtActive()
	{
		Sheet.InsertRow(Sheet.ActiveCell.Row);
		await OnStructureChanged.InvokeAsync();
	}

	private async Task DeleteRowAtActive()
	{
		Sheet.DeleteRow(Sheet.ActiveCell.Row);
		await OnStructureChanged.InvokeAsync();
	}

	private async Task InsertColAtActive()
	{
		Sheet.InsertColumn(Sheet.ActiveCell.Col);
		await OnStructureChanged.InvokeAsync();
	}

	private async Task DeleteColAtActive()
	{
		Sheet.DeleteColumn(Sheet.ActiveCell.Col);
		await OnStructureChanged.InvokeAsync();
	}

	private string GetActiveColName()
	{
		var col = Sheet.ActiveCell.Col;
		if (col >= 0 && col < Sheet.Model.ColumnHeaders.Count)
		{
			return Sheet.Model.ColumnHeaders[col];
		}

		return "?";
	}
}
