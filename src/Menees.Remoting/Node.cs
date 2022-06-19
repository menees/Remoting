namespace Menees.Remoting;

#region Using Directives

using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Menees.Remoting.Json;

#endregion

/// <summary>
/// Shared functionality for all client and server nodes.
/// </summary>
public abstract class Node : IDisposable
{
	#region Protected Data Members

	private readonly string serverPath;

	private bool disposed;
	private Func<string, Type?> tryGetType = RequireGetType;
	private ISerializer? systemSerializer;
	private ISerializer? userSerializer;
	private Func<string, ILogger>? createLogger;

	#endregion

	#region Constructors

	/// <summary>
	///
	/// </summary>
	/// <param name="settings">Parameters used to initialize this instance.</param>
	protected Node(NodeSettings settings)
	{
		this.serverPath = settings.ServerPath;
		this.userSerializer = settings?.Serializer;
		this.createLogger = settings?.CreateLogger;
	}

	#endregion

	#region Public Properties

	/// <summary>
	/// Allows customization of how an assembly-qualified type name (serialized from
	/// <see cref="Type.AssemblyQualifiedName"/>) should be deserialized into a .NET
	/// <see cref="Type"/>.
	/// </summary>
	/// <remarks>
	/// A secure system needs to support a known list of legal/safe/valid types that it
	/// can load dynamically. It shouldn't just trust and load an arbitrary assembly and
	/// then load an arbitrary type out of it. Doing that can execute malicious code
	/// in the current process (e.g., via the Type's static constructor or the assembly's
	/// module initializer). So a security best practice is to validate every assembly-
	/// qualified type name before you load the type.
	/// <para/>
	/// However, this is a case where security is at odds with convenience. The default for
	/// this property just calls <see cref="Type.GetType(string, bool)"/> to try to load the type,
	/// and it throws an exception if the type can't be loaded.
	/// <para/>
	/// https://github.com/dotnet/runtime/issues/31567#issuecomment-558335944
	/// https://stackoverflow.com/a/66963611/1882616
	/// https://github.com/dotnet/runtime/issues/43482#issue-722814247 (related Exception comment)
	/// </remarks>
	public Func<string, Type?> TryGetType
	{
		get => this.tryGetType;
		set
		{
			if (this.tryGetType != value)
			{
				this.tryGetType = value ?? RequireGetType;

				// On the next serialization, we need to create a new serializer instance using the new tryGetType lambda.
				this.systemSerializer = null;
			}
		}
	}

	#endregion

	#region Internal Properties

	internal ISerializer SystemSerializer
		=> this.systemSerializer ??= new JSerializer(new(this.tryGetType));

	internal ISerializer? UserSerializer => this.userSerializer;

	#endregion

	#region Public Methods

	/// <summary>
	/// Disposes of managed resources.
	/// </summary>
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		this.Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	#endregion

	#region Internal Methods

	internal ILogger CreateLogger(Type type)
	{
		ILogger result = NullLogger.Instance;

		if (this.createLogger != null)
		{
			ILogger logger = this.createLogger(type.FullName ?? string.Empty);

			// ILogger.BeginScope is weird. I'd prefer to pass a dictionary with a named entry like "ServerPath",
			// so I can provide more info on what type of scope information is available. However, Microsoft's
			// Console provider only does ToString() when it shows the scope values, so that would only show
			// a scope dictionary as its .NET type name, which is useless. Microsoft's other standard providers
			// don't even support scopes.
			//
			// At https://nblumhardt.com/2016/11/ilogger-beginscope/, Nicholas Blumhardt discuss how he
			// implemented ILogger.BeginScope in Serilog. It handles dictionaries as named value pairs, and
			// it attaches loose values like strings to a named "Scope" property with a list of values. I hope
			// other logging libraries handle dictionaries similarly. However, since I can't know what the ILogger
			// implementation does with scopes, I'll just go with a simple string scope. Every ILogger library
			// should support that.
			result = new ScopedLogger<string>(logger, this.serverPath);
		}

		return result;
	}

	#endregion

	#region Protected Methods

	/// <summary>
	/// Disposes of managed resources.
	/// </summary>
	/// <param name="disposing">True if <see cref="Dispose()"/> was called. False if this was called from a derived type's finalizer.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!this.disposed)
		{
			// Allow any custom serializer to be GCed.
			this.userSerializer = null;
			this.systemSerializer = null;
			this.createLogger = null;
			this.disposed = true;
		}
	}

	#endregion

	#region Private Methods

	private static Type? RequireGetType(string qualifiedTypeName)
		=> Type.GetType(qualifiedTypeName, throwOnError: true);

	#endregion

	#region Private Types

	private sealed class ScopedLogger<TScope> : ILogger
	{
		#region Private Data Members

		private readonly ILogger logger;
		private readonly TScope scope;

		#endregion

		#region Constructors

		public ScopedLogger(ILogger logger, TScope scope)
		{
			this.logger = logger;
			this.scope = scope;
		}

		#endregion

		#region Public Methods

		public IDisposable BeginScope<TState>(TState state) => this.logger.BeginScope(state);

		public bool IsEnabled(LogLevel logLevel) => this.logger.IsEnabled(logLevel);

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			// ILogger.BeginScope is a weird API. LoggerExternalScopeProvider uses AsyncLocal<T>, so it's not safe to
			// start the scope from a class constructor and dispose of it from the class's Dispose method. Doing that
			// causes the scope to "stay open" and pick up any pushed scopes from spawned Tasks/awaits, which leads
			// to bizarre nested/stacked scopes. Due to AsyncLocal, ILogger.BeginScope can only be safely used in a local
			// call context wrapped around one or more local ILogger.Log calls with no intervening Task.Runs or awaits.
			// To avoid having to do BeginScope around all our Log calls, it's easier to make this wrapper class that does it.
			// https://stackoverflow.com/questions/63851259/since-iloggert-is-a-singleton-how-different-threads-can-use-beginscope-with#comment128022925_63852241
			using IDisposable logScope = this.BeginScope(this.scope);
			this.logger.Log(logLevel, eventId, state, exception, formatter);
		}

		#endregion
	}

	#endregion
}
