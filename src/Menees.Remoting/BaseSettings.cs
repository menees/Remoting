namespace Menees.Remoting;

#region Using Directives

using Microsoft.Extensions.Logging;

#endregion

/// <summary>
/// Settings used to initialize an <see cref="RmiBase{TServiceInterface}"/> instance.
/// </summary>
public abstract class BaseSettings
{
	#region Constructors

	/// <summary>
	/// Creates a new instance.
	/// </summary>
	/// <param name="serverPath">The path used to expose the service.</param>
	protected BaseSettings(string serverPath)
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

	#endregion
}
