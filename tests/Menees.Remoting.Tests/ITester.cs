namespace Menees.Remoting;

internal interface ITester
{
	int TestId { get; set; }

	string Combine(string part1, string part2, string? part3 = null);

	Widget CreateWidget(string name, decimal cost, params int[] dimensions);

	Widget UpdateWidget(Widget widget, string? newName, decimal? newCost, int[]? newDimensions);
}
