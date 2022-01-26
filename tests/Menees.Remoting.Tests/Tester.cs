namespace Menees.Remoting;

internal class Tester : ITester
{
	public int TestId { get; set; }

	public string Combine(string part1, string part2, string? part3 = null)
	{
		string result = string.Concat(part1, part2, part3);
		return result;
	}

	public Widget CreateWidget(string name, decimal cost, params int[] dimensions)
	{
		Widget result = new()
		{
			Name = name,
			Cost = cost,
			Dimensions = dimensions,
		};

		return result;
	}

	public Widget UpdateWidget(Widget widget, string? newName, decimal? newCost, int[]? newDimensions)
	{
		if (newName != null)
		{
			widget.Name = newName;
		}

		if (newCost != null)
		{
			widget.Cost = newCost.Value;
		}

		if (newDimensions != null)
		{
			widget.Dimensions = newDimensions;
		}

		return widget;
	}
}
