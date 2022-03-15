namespace Menees.Remoting.TestClient;

public sealed class Calculator : ICalculator
{
	public decimal Add(decimal value1, decimal value2) => value1 + value2;

	public DateTime Add(DateTime dateTime, TimeSpan timeSpan) => dateTime + timeSpan;
}
