namespace Menees.Remoting.Pipes;

#region Using Directives

using System.IO.Pipes;
using System.Runtime;
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
	#region Private Data Members

	private readonly Scope scope;

	#endregion

	#region Constructors

	/// <summary>
	/// Creates a new instance using the specified pipe security.
	/// </summary>
	/// <param name="security">The pipe's access control and audit security.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="security"/> is null.</exception>
	[SupportedOSPlatform("windows")]
	public PipeServerSecurity(PipeSecurity security)
	{
		this.scope = Scope.CustomSecurity;
		this.Security = security ?? throw new ArgumentNullException(nameof(security));

		if (!OperatingSystem.IsWindows())
		{
			throw new InvalidOperationException("Custom PipeSecurity is not supported on this OS platform.");
		}
	}

	private PipeServerSecurity(Scope scope)
	{
		this.scope = scope;
	}

	#endregion

	#region Private Enums

	private enum Scope
	{
		/// <summary>
		/// Use this.Security with custom ACLs.
		/// </summary>
		CustomSecurity,

		/// <summary>
		/// Only the current user (at the same elevation level) can connect.
		/// </summary>
		CurrentUserOnly,

		/// <summary>
		/// Any user can connect.
		/// </summary>
		Everyone,
	}

	#endregion

	#region Public Properties

	/// <summary>
	/// Gets an instance where the pipe can only be connected to clients created by the same user
	/// (and at the same elevation level).
	/// </summary>
	public static PipeServerSecurity CurrentUserOnly { get; } = new(Scope.CurrentUserOnly);

	/// <summary>
	/// Gets an instance where the pipe can be connected to clients created by any user.
	/// </summary>
	public static PipeServerSecurity Everyone { get; } = new(Scope.Everyone);

	#endregion

	#region Private Properties

	private PipeSecurity? Security { get; }

	#endregion

	#region Internal Methods

	internal NamedPipeServerStream? CreatePipe(
		string pipeName, PipeDirection direction, int maxListeners, PipeTransmissionMode mode, PipeOptions options)
	{
		options |= this.scope == Scope.CurrentUserOnly ? PipeClientSecurity.CurrentUserOnlyOption : PipeOptions.None;

		NamedPipeServerStream? result;
		if (OperatingSystem.IsWindows())
		{
			// NamedPipeServerStreamAcl.Create requires pipeSecurity == null with PipeOptions.CurrentUserOnly.
			PipeSecurity? security = options.HasFlag(PipeOptions.CurrentUserOnly)
				? null
				: (this.Security ?? this.CreateWindowsPipeSecurity());
			result = NamedPipeServerStreamAcl.Create(pipeName, direction, maxListeners, mode, options, 0, 0, security);
		}
		else if (this.scope == Scope.CurrentUserOnly || this.scope == Scope.Everyone)
		{
			result = new(pipeName, direction, maxListeners, mode, options);
			if (this.scope == Scope.Everyone)
			{
				// https://stackoverflow.com/a/57067615/1882616 - /tmp/CoreFxPipe_ prefix if not rooted
				// https://stackoverflow.com/a/77908370/1882616 - Allow writing to a named pipe by another user
				string rootedPipeName = Path.IsPathRooted(pipeName) ? pipeName : $"/tmp/CoreFxPipe_{pipeName}";
				UnixFileMode unixFileMode = File.GetUnixFileMode(rootedPipeName);
				unixFileMode |= UnixFileMode.OtherRead | UnixFileMode.OtherWrite;
				File.SetUnixFileMode(rootedPipeName, unixFileMode);
			}
		}
		else
		{
			throw new InvalidOperationException("Custom PipeSecurity is not supported on this OS platform.");
		}

		return result;
	}

	#endregion

	#region Private Methods

	[SupportedOSPlatform("windows")]
	private PipeSecurity CreateWindowsPipeSecurity()
	{
		PipeSecurity result = new();

		// From .NET 6.0's private NamedPipeServerStream.Create method for Windows.
		// https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.Pipes/src/System/IO/Pipes/NamedPipeServerStream.Windows.cs#L98
		using (WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent())
		{
			SecurityIdentifier? owner = currentIdentity.Owner;
			if (owner != null)
			{
				// Grant full control to the owner so multiple servers can be opened.
				// Full control is the default per MSDN docs for CreateNamedPipe.
				PipeAccessRule rule = new(owner, PipeAccessRights.FullControl, AccessControlType.Allow);
				result.AddAccessRule(rule);
				result.SetOwner(owner);
			}
		}

		if (this.scope == Scope.Everyone)
		{
			// Let everyone else read from and write to the pipe, but don't let them change its ownership,
			// security, or create/delete instances. This is necessary for cross-account calls (e.g., if a
			// web worker process is running as a different account from a back-end service). This is also
			// necessary if the same user account needs to call across a UAC boundary (e.g., a normal
			// non-admin process calls into a service launched by Windows Service Control Manager, which
			// runs services with full admin privileges if available). Without this, NamedPipeClientStream's
			// Connect() would fail with "UnauthorizedAccessException: Access to the path is denied."
			// https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.Pipes/src/System/IO/Pipes/PipeAccessRights.cs
			SecurityIdentifier everyone = new(WellKnownSidType.WorldSid, null);
			result.AddAccessRule(new(everyone, PipeAccessRights.ReadWrite, AccessControlType.Allow));
		}

		return result;
	}

	#endregion
}
