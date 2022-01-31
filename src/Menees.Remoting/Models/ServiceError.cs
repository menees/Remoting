namespace Menees.Remoting.Models;

#region Using Directives

using System.Reflection;

#endregion

/// <summary>
/// Represents an exception that occured while processing a service request to
/// <see cref="RmiServer{TServiceInterface}"/>.
/// </summary>
/// <remarks>
/// We can't safely/securely serialize .NET exceptions from a server back to a client, so
/// this represents the minimal safe exception details we want to report to the client.
/// The server exception's stack trace and other properties (e.g., <see cref="Exception.Data"/>)
/// might leak security critical information. Other exception sub-properties like the
/// <see cref="Exception.TargetSite"/>'s MethodHandle.Value (IntPtr) can't be serialized
/// because a server handle is meaningless to the client.
/// <para/>
/// For more info see:
/// https://github.com/dotnet/runtime/issues/43482#issue-722814247
/// https://github.com/dotnet/runtime/issues/43026#issuecomment-705904399
/// https://github.com/dotnet/runtime/issues/43482#issuecomment-691760422
/// </remarks>
/// <seealso cref="RmiServer{TServiceInterface}.ReportUnhandledException"/>.
internal sealed class ServiceError
{
	#region Constructors

	public ServiceError()
	{
		// This is required for JSON deserialization.
	}

	internal ServiceError(Exception ex)
	{
		this.ExceptionType = ex.GetType();
		this.Message = ex.Message;
		if (ex.InnerException != null)
		{
			this.InnerError = new(ex.InnerException);
		}
	}

	#endregion

	#region Public Properties

	public Type ExceptionType { get; set; } = typeof(InvalidOperationException);

	public string Message { get; set; } = string.Empty;

	public ServiceError? InnerError { get; set; }

	#endregion

	#region Public Methods

	public void ThrowException() => throw this.CreateException();

	#endregion

	#region Private Methods

	private Exception CreateException()
	{
		Exception? innerException = null;
		if (this.InnerError != null)
		{
			innerException = this.InnerError.CreateException();
		}

		// Try to throw a new exception of the requested type. The new exception's StackTrace will be from the client,
		// but its message, data type, and inner exception will match the server's exception info.
		Exception result;
		ConstructorInfo? constructor = this.ExceptionType.GetConstructor(new[] { typeof(string), typeof(Exception) });
		if (constructor != null)
		{
			result = (Exception)constructor.Invoke(new object?[] { this.Message, innerException });
		}
		else
		{
			result = new InvalidOperationException(this.Message, innerException);
		}

		return result;
	}

	#endregion
}
