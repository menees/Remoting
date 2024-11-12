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

	internal const PipeOptions CurrentUserOnlyOption = PipeOptions.CurrentUserOnly;

	#endregion

	#region Constructors

	private PipeClientSecurity()
	{
		this.Options = CurrentUserOnlyOption;
	}

	#endregion

	#region Public Properties

	/// <summary>
	/// Gets an instance where the pipe can only connect to a server created by the same user.
	/// </summary>
	/// <remarks>
	/// On Windows, it verifies both the user account and elevation level.
	/// </remarks>
	public static PipeClientSecurity CurrentUserOnly { get; } = new PipeClientSecurity();

	#endregion

	#region Internal Properties

	internal PipeOptions Options { get; }

	#endregion
}
