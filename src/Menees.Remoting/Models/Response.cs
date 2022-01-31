namespace Menees.Remoting.Models;

internal sealed class Response : Message
{
	public UserSerializedValue? Result { get; set; }

	/// <summary>
	/// Gets any exception that was thrown by the remote server method.
	/// </summary>
	/// <remarks>
	/// If this is non-null, then the client should throw a new exception rather than treating
	/// <see cref="Result"/> as a normal value that's safe to deserialize.
	/// </remarks>
	public ServiceError? Error { get; set; }
}
