using System.Globalization;
using MentorLake.BlazorTableEditor.Models;

namespace MentorLake.BlazorTableEditor.Demo.Validators;

public sealed class DemoTableValidator : ITableValidator
{
	public IReadOnlyDictionary<CellPosition, string> Validate(TableDataModel model)
	{
		var errors = new Dictionary<CellPosition, string>();

		if (model is null || model.RowCount == 0 || model.ColumnCount == 0)
		{
			return errors;
		}

		for (int r = 0; r < model.RowCount; r++)
		{
			for (int c = 0; c < model.ColumnCount; c++)
			{
				var value = model.GetCell(r, c)?.Value;

				if (string.IsNullOrEmpty(value))
				{
					continue;
				}

				if (c == 0)
				{
					if (string.IsNullOrWhiteSpace(value))
					{
						errors[new CellPosition(r, c)] = "Name is required.";
					}
					else if (value.Length > 50)
					{
						errors[new CellPosition(r, c)] = "Name must be 50 characters or fewer.";
					}
				}

				if (c == 1 && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
				{
					if (i < 0)
					{
						errors[new CellPosition(r, c)] = "Value must be non-negative.";
					}
					else if (i > 10000)
					{
						errors[new CellPosition(r, c)] = "Value must be 10000 or less.";
					}
				}

				if (c == 2 && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d < 0)
				{
					errors[new CellPosition(r, c)] = "Amount must be non-negative.";
				}

				if (value.Contains("error", StringComparison.OrdinalIgnoreCase))
				{
					errors[new CellPosition(r, c)] = "Text cannot contain the word 'error'.";
				}
			}
		}

		return errors;
	}
}
