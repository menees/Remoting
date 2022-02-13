namespace Menees.Remoting;

#region Using Directives

using Menees.Remoting.Security;
using Microsoft.Extensions.Logging;

#endregion

/// <summary>
/// Settings used to initialize a client or server node.
/// </summary>
public abstract class NodeSettings
{
	#region Constructors

	/// <summary>
	/// Creates a new instance.
	/// </summary>
	/// <param name="serverPath">The path used to expose the service.</param>
	protected NodeSettings(string serverPath)
	{
		this.ServerPath = serverPath;
	}

	#endregion

	#region Public Properties

	/// <summary>
	/// Gets the path used to expose the service.
	/// </summary>
	/// <remarks>
	/// A server will expose this path, and a client will connect to this path.
	/// </remarks>
	public string ServerPath { get; }

	/// <summary>
	/// Gets or sets an optional custom serializer.
	/// </summary>
	/// <remarks>
	/// Note: Associated client and server instances must use compatible serializers.
	/// </remarks>
	public ISerializer? Serializer { get; set; }

	/// <summary>
	/// Gets or sets an optional factory for creating type-specific server loggers for status information.
	/// </summary>
	public ILoggerFactory? LoggerFactory { get; set; }

	/// <summary>
	/// Gets security settings.
	/// </summary>
	public NodeSecurity? Security => this.GetSecurity();

	#endregion

	#region Private Protected Methods

	// This is needed because C#9 covariant returns only work for read-only properties.
	// We need NodeSettings.Security to be read-only, but we need Client|ServerSettings.Security
	// to be read-write. So the derived type's Security property has to be "new" not an override.
	private protected abstract NodeSecurity? GetSecurity();

	#endregion
}
