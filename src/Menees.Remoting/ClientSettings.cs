namespace Menees.Remoting;

#region Using Directives

using Menees.Remoting.Security;

#endregion

/// <summary>
/// Settings used to initialize an <see cref="RmiClient{TServiceInterface}"/> instance.
/// </summary>
public sealed class ClientSettings : NodeSettings
{
	#region Constructors

	/// <inheritdoc/>
	public ClientSettings(string serverPath)
		: base(serverPath)
	{
	}

	#endregion

	#region Public Properties

	/// <summary>
	/// Gets the default interval to wait for a connection to a remote <see cref="RmiServer{TServiceInterface}"/>.
	/// </summary>
	public static TimeSpan DefaultConnectTimeout { get; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets the name of the remote server machine.
	/// </summary>
	/// <remarks>
	/// This defaults to "." for the local system.
	/// </remarks>
	public string ServerHost { get; set; } = ".";

	/// <summary>
	/// Gets or sets the interval to wait for a connection to a remote <see cref="RmiServer{TServiceInterface}"/>.
	/// </summary>
	/// <remarks>
	/// This defaults to <see cref="DefaultConnectTimeout"/>.
	/// </remarks>
	public TimeSpan ConnectTimeout { get; set; } = DefaultConnectTimeout;

	/// <summary>
	/// Gets or sets client security settings.
	/// </summary>
	public new ClientSecurity? Security { get; set; }

	#endregion

	#region Private Protected Methods

	private protected override NodeSecurity? GetSecurity()
		=> this.Security;

	#endregion
}
