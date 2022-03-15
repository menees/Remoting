namespace Menees.Remoting.TestClient;

public interface ICalculator
{
	decimal Add(decimal value1, decimal value2);

	DateTime Add(DateTime dateTime, TimeSpan timeSpan);
}
