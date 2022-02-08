namespace Menees.Remoting;

#region Using Directives

using System.IO.Pipes;

#endregion

/// <summary>
/// Settings used to initialize an <see cref="RmiServer{TServiceInterface}"/> instance.
/// </summary>
public sealed class ServerSettings : NodeSettings
{
	#region Public Constants

	/// <summary>
	/// Represents the maximum number of server instances that the system resources allow.
	/// </summary>
	public const int MaxAllowedListeners = NamedPipeServerStream.MaxAllowedServerInstances;

	#endregion

	#region Constructors

	/// <inheritdoc/>
	public ServerSettings(string serverPath)
		: base(serverPath)
	{
	}

	#endregion

	#region Public Properties

	/// <summary>
	/// Gets or sets the maximum number of server listener tasks to start.
	/// </summary>
	/// <remarks>
	/// This defaults to <see cref="MaxAllowedListeners"/>.
	/// </remarks>
	public int MaxListeners { get; set; } = MaxAllowedListeners;

	/// <summary>
	/// Gets or sets the minimim number of server listener tasks to start.
	/// </summary>
	/// <remarks>
	/// This defaults to 1.
	/// </remarks>
	public int MinListeners { get; set; } = 1;

	/// <summary>
	/// Gets or sets a token that signals a cancellation request.
	/// </summary>
	public CancellationToken CancellationToken { get; set; }

	#endregion
}
