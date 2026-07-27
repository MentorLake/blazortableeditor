using MentorLake.BlazorTableEditor.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{

	private InputFile? _csvInput;
	private string? _csvStatus;

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
}
