namespace Menees.Remoting;

public sealed class Widget
{
	public string Name { get; set; } = string.Empty;

	public decimal Cost { get; set; }

	public int[] Dimensions { get; set; } = Array.Empty<int>();
}
