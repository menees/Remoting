namespace Menees.Remoting;

internal sealed class Request : Message
{
	public string? MethodSignature { get; set; }

	public List<(object? Value, Type DataType)>? Arguments { get; set; }
}
