namespace Menees.Remoting;

#region Using Directives

using System.Reflection;
using System.Runtime.InteropServices;
using Menees.Remoting.Security;
using Microsoft.Extensions.Logging;

#endregion

/// <summary>
/// Settings used to initialize a client or server node.
/// </summary>
public abstract class NodeSettings
{
	#region Private Data Members

	// I'm keeping this private for now (even though BaseTests duplicates it) because I may want to
	// support other CLR scopes later (e.g., Framework, Core, Mono, Wasm, SQL CLR, Native).
	private static readonly bool IsDotNetFramework = RuntimeInformation.FrameworkDescription.Contains("Framework");

	private Func<string, Type?> tryGetType = RequireGetType;

	#endregion

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
	/// Gets or sets an optional function for creating categorized loggers for status information.
	/// </summary>
	/// <remarks>
	/// An <see cref="ILoggerFactory.CreateLogger(string)"/> method can be assigned to this.
	/// We don't require a full <see cref="ILoggerFactory"/> interface since we never need to
	/// call its <see cref="ILoggerFactory.AddProvider(ILoggerProvider)"/> method. Technically,
	/// an <see cref="ILoggerProvider.CreateLogger(string)"/> method could also be used, but
	/// that would be atypical.
	/// </remarks>
	public Func<string, ILogger>? CreateLogger { get; set; }

	/// <summary>
	/// Gets security settings.
	/// </summary>
	public NodeSecurity? Security => this.GetSecurity();

	/// <summary>
	/// Allows customization of how an assembly-qualified type name (serialized from
	/// <see cref="Type.AssemblyQualifiedName"/>) should be deserialized into a .NET
	/// <see cref="Type"/>.
	/// </summary>
	/// <remarks>
	/// This is useful for type translation and security. It's for translation if you're supporting
	/// calls between different runtimes (e.g., Framework and "Core") or versions
	/// (e.g., .NET 6.0 and 7.0). When mixing runtimes, many types will be in different
	/// assemblies (e.g., int, string, Uri, IPAddress, Stack&lt;T>), so this handler needs
	/// to deal with that for all your supported types. Even mixing versions of the same
	/// runtime is complicated because strongly-named assemblies embed their version
	/// in their AssemblyQualifiedName.
	/// <para/>
	/// A secure system needs to support a known list of legal/safe/valid types that it
	/// can load dynamically. It shouldn't just trust and load an arbitrary assembly and
	/// then load an arbitrary type out of it. Doing that can execute malicious code
	/// in the current process (e.g., via the Type's static constructor or the assembly's
	/// module initializer). So, a security best practice is to validate every assembly-
	/// qualified type name before you load the type.
	/// <para/>
	/// However, this is a case where security is at odds with convenience. The default for
	/// this property just calls <see cref="RequireGetType"/> to try to load the type,
	/// and it throws an exception if the type can't be loaded.
	/// <para/>
	/// https://github.com/dotnet/runtime/issues/31567#issuecomment-558335944
	/// https://stackoverflow.com/a/66963611/1882616
	/// https://github.com/dotnet/runtime/issues/43482#issue-722814247 (related Exception comment)
	/// </remarks>
	public Func<string, Type?> TryGetType
	{
		get => this.tryGetType;
		set => this.tryGetType = value ?? RequireGetType;
	}

	#endregion

	#region Internal Methods

	/// <summary>
	/// Loads a type given an assembly-qualified type name.
	/// </summary>
	/// <param name="typeName">An assembly-qualified type name.</param>
	/// <returns>The .NET Type associated with <paramref name="typeName"/>.</returns>
	public static Type RequireGetType(string typeName)
	{
		Type? result = Type.GetType(typeName, throwOnError: false, ignoreCase: true);
		if (result == null)
		{
			// Fallback to the Type.GetType overload where we can pass a custom assembly resolver.
			// This is important since a single type name can contain multiple assembly references
			// (e.g., Dictionary<string, Uri> includes asm refs for Dictionary<>, string, and Uri).
			Assembly[]? assemblies = null;
			result = Type.GetType(
				typeName,
				assemblyName =>
				{
					Assembly? assembly = null;
					string simpleName = assemblyName.Name ?? string.Empty;

					// Try to translate the simple built-in scalar types correctly across different runtimes.
					if ((IsDotNetFramework && simpleName.Equals("System.Private.CoreLib", StringComparison.OrdinalIgnoreCase))
						|| (!IsDotNetFramework && simpleName.Equals("MsCorLib", StringComparison.OrdinalIgnoreCase)))
					{
						assembly = typeof(string).Assembly;
					}
					else
					{
						// See if any assembly is already loaded with the same simple name.
						// This ignores versions and strong naming, so it's convenient but insecure.
						// We'll allow a lower version to match in case a .NET 7.0 client needs to
						// call into a .NET 6.0 server.
						// https://github.com/dotnet/fsharp/issues/3408#issuecomment-319519926
						assemblies ??= AppDomain.CurrentDomain.GetAssemblies();
						assembly = assemblies.FirstOrDefault(asm => asm.GetName().Name?.Equals(simpleName, StringComparison.OrdinalIgnoreCase) ?? false);
					}

					return assembly;
				},
				typeResolver: null,
				throwOnError: false,
				ignoreCase: true);
		}

		if (result == null)
		{
			throw new TypeLoadException($"Unable to load type \"{typeName}\".");
		}

		return result;
	}

	#endregion

	#region Private Protected Methods

	// This is needed because C#9 covariant returns only work for read-only properties.
	// We need NodeSettings.Security to be read-only, but we need Client|ServerSettings.Security
	// to be read-write. So the derived type's Security property has to be "new" not an override.
	private protected abstract NodeSecurity? GetSecurity();

	#endregion
}
