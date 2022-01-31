namespace Menees.Remoting;

internal sealed class Request : Message
{
	public string? MethodSignature { get; set; }

	public List<UserSerializedValue>? Arguments { get; set; }
}
