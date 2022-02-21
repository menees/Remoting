namespace Menees.Remoting.Pipes;

#region Using Directives

using System.IO.Pipes;
using System.Security.Principal;
using Menees.Remoting.Security;

#endregion

/// <summary>
/// Represents security settings for a named pipe client connection.
/// </summary>
public sealed class PipeClientSecurity : ClientSecurity
{
	#region Internal Constants

	internal const PipeOptions CurrentUserOnlyOption =
#if NETFRAMEWORK
		PipeOptions.None;
#else
		PipeOptions.CurrentUserOnly;
#endif

	#endregion

	#region Constructors

	// TODO: Remove bool parameter. [Bill, 2/20/2022]
	private PipeClientSecurity(bool isCurrentUserOnly)
	{
		this.IsCurrentUserOnly = isCurrentUserOnly;
		this.Options = isCurrentUserOnly ? CurrentUserOnlyOption : PipeOptions.None;
	}

	#endregion

	#region Public Properties

	/// <summary>
	/// Gets an instance where the pipe can only connect to a server created by the same user.
	/// </summary>
	/// <remarks>
	/// On Windows, it verifies both the user account and elevation level.
	/// </remarks>
	public static PipeClientSecurity CurrentUserOnly { get; } = new PipeClientSecurity(isCurrentUserOnly: true);

	#endregion

	#region Internal Properties

	internal bool IsCurrentUserOnly { get; }

	internal PipeOptions Options { get; }

	#endregion

	#region Internal Methods

	internal void CheckConnection(NamedPipeClientStream pipe)
	{
		if (this.IsCurrentUserOnly)
		{
#if NETFRAMEWORK
			// This code is from .NET 6.0's ValidateRemotePipeUser for Windows.
			// https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.Pipes/src/System/IO/Pipes/NamedPipeClientStream.Windows.cs
			PipeSecurity accessControl = pipe.GetAccessControl();
			IdentityReference? remoteOwnerSid = accessControl.GetOwner(typeof(SecurityIdentifier));
			using WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent();
			SecurityIdentifier? currentUserSid = currentIdentity.Owner;
			if (remoteOwnerSid != currentUserSid)
			{
				pipe.Close();
				throw new UnauthorizedAccessException("Could not connect to the pipe because it was not owned by the current user.");
			}
#else
			// .NET 6.0 already handles PipeOptions.CurrentUserOnly validation.
			GC.KeepAlive(pipe);
#endif
		}
	}

	#endregion
}
