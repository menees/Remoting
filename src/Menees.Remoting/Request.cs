namespace Menees.Remoting;

internal sealed class Request : Message
{
	public string? MethodSignature { get; set; }

	public List<TypedValue>? Arguments { get; set; }
}
