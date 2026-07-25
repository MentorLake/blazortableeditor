using System.Collections.Generic;

namespace MentorLake.BlazorTableEditor.Models;

public interface ITableValidator
{
	IReadOnlyDictionary<CellPosition, string> Validate(TableDataModel model);
}
