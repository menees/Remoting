namespace Menees.Remoting;

#region Using Directives

using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Menees.Remoting.Json;

#endregion

/// <summary>
/// Shared functionality for <see cref="RmiClient{T}"/> and <see cref="RmiServer{T}"/>.
/// </summary>
/// <typeparam name="TServiceInterface">The interface to remotely invoke/expose members for.</typeparam>
public abstract class RmiBase<TServiceInterface> : IDisposable
	where TServiceInterface : class
{
	#region Protected Data Members

	private bool disposed;
	private Func<string, Type?> tryGetType = RequireGetType;
	private ISerializer? systemSerializer;
	private ISerializer? userSerializer;
	private ILoggerFactory? loggerFactory;

	#endregion

	#region Constructors

	/// <summary>
	/// Validates that <typeparamref name="TServiceInterface"/> is an interface.
	/// </summary>
	/// <param name="settings">Parameters used to initialize this instance.</param>
	protected RmiBase(BaseSettings settings)
	{
		Type interfaceType = typeof(TServiceInterface);
		if (!interfaceType.IsInterface)
		{
			throw new ArgumentException($"{nameof(TServiceInterface)} {interfaceType.FullName} must be an interface type.");
		}

		this.userSerializer = settings?.Serializer;
		this.loggerFactory = settings?.LoggerFactory;
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
				this.tryGetType = value;

				// On the next serialization, we need to create a new serializer instance using the new tryGetType lambda.
				this.systemSerializer = null;
			}
		}
	}

	#endregion

	#region Protected Properties

	private protected ISerializer SystemSerializer
		=> this.systemSerializer ??= new JSerializer(new(this.tryGetType));

	private protected ISerializer UserSerializer
		=> this.userSerializer ?? this.SystemSerializer;

	private protected ILoggerFactory Loggers
		=> this.loggerFactory ?? NullLoggerFactory.Instance;

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
			this.loggerFactory = null;
			this.disposed = true;
		}
	}

	#endregion

	#region Private Protected Methods

	private protected static string GetMethodSignature(MethodInfo methodInfo)
	{
		// For our purposes MethodInfo.ToString() returns a unique enough signature.
		// For example, typeof(string).GetMethods().Last(m => m.Name == "IndexOf").ToString()
		// returns "Int32 IndexOf(Char, Int32, Int32)".
		// For other options see: https://stackoverflow.com/a/1312321/1882616
		string result = methodInfo.ToString() ?? throw new InvalidOperationException("Null method signature is not supported.");
		return result;
	}

	#endregion

	#region Private Methods

	private static Type? RequireGetType(string qualifiedTypeName)
		=> Type.GetType(qualifiedTypeName, throwOnError: true);

	#endregion
}
