namespace Menees.Remoting;

internal sealed class Response : Message
{
	public TypedValue? Result { get; set; }

	/// <summary>
	/// Gets whether the <see cref="ReturnValue"/> is an exception that was thrown by
	/// the remote server method.
	/// </summary>
	/// <remarks>
	/// If this is true, then the client should rethrow the exception rather than returning
	/// the value as a normal result.
	/// </remarks>
	public bool IsServiceException { get; set; }
}
