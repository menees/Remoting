namespace Menees.Remoting.Pipes;

#region Using Directives

using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Menees.Remoting.Security;

#endregion

/// <summary>
/// Represents security settings for a named pipe server.
/// </summary>
public sealed class PipeServerSecurity : ServerSecurity
{
	#region Constructors

	/// <summary>
	/// Creates a new instance using the specified pipe security.
	/// </summary>
	/// <param name="security">The pipe's access control and audit security.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="security"/> is null.</exception>
#if NETCOREAPP
	[SupportedOSPlatform("windows")]
#endif
	public PipeServerSecurity(PipeSecurity security)
	{
		this.Security = security ?? throw new ArgumentNullException(nameof(security));
	}

	// TODO: Remove bool parameter. [Bill, 2/20/2022]
	private PipeServerSecurity(bool isCurrentUserOnly)
	{
		this.IsCurrentUserOnly = isCurrentUserOnly;
		this.Options = isCurrentUserOnly ? PipeClientSecurity.CurrentUserOnlyOption : PipeOptions.None;
	}

	#endregion

	#region Public Properties

	/// <summary>
	/// Gets an instance where the pipe can only be connected to a client created by the same user.
	/// </summary>
	public static PipeServerSecurity CurrentUserOnly { get; } = new PipeServerSecurity(isCurrentUserOnly: true);

	#endregion

	#region Internal Properties

	internal bool IsCurrentUserOnly { get; }

	internal PipeOptions Options { get; }

	internal PipeSecurity? Security { get; }

	#endregion

	#region Internal Methods

	internal NamedPipeServerStream? CreatePipe(
		string pipeName, PipeDirection direction, int maxListeners, PipeTransmissionMode mode, PipeOptions options)
	{
		NamedPipeServerStream? result = null;

#if NETFRAMEWORK
		PipeSecurity? security = this.Security;
		if (this.IsCurrentUserOnly && security == null)
		{
			// From .NET 6.0's private NamedPipeServerStream.Create method for Windows.
			// https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.Pipes/src/System/IO/Pipes/NamedPipeServerStream.Windows.cs#L98
			using WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent();
			SecurityIdentifier identifier = currentIdentity.Owner!;

			// Grant full control to the owner so multiple servers can be opened.
			// Full control is the default per MSDN docs for CreateNamedPipe.
			PipeAccessRule rule = new(identifier, PipeAccessRights.FullControl, AccessControlType.Allow);
			security = new PipeSecurity();
			security.AddAccessRule(rule);
			security.SetOwner(identifier);
		}

		result = new(pipeName, direction, maxListeners, mode, options, 0, 0, security);
#else
		if (this.IsCurrentUserOnly)
		{
			result = new(pipeName, direction, maxListeners, mode, options | this.Options);
		}
		else if (OperatingSystem.IsWindows())
		{
			result = NamedPipeServerStreamAcl.Create(pipeName, direction, maxListeners, mode, options, 0, 0, this.Security);
		}
		else
		{
			throw new InvalidOperationException("Custom PipeSecurity is not supported on this OS platform.");
		}
#endif

		return result;
	}

	#endregion
}
