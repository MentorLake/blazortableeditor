using MentorLake.BlazorTableEditor.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace MentorLake.BlazorTableEditor;

public partial class MentorLakeTableEditor
{

	private void InsertRowAtActive() => Context.InsertRow(Context.ActiveCell.Row);
	private void DeleteRowAtActive() => Context.DeleteRow(Context.ActiveCell.Row);
	private void InsertColAtActive() => Context.InsertColumn(Context.ActiveCell.Col);
	private void DeleteColAtActive() => Context.DeleteColumn(Context.ActiveCell.Col);
}
