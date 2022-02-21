namespace Menees.Remoting.Security;

/// <summary>
/// Represents security settings for a <see cref="Node"/>-derived type.
/// </summary>
/// <devnote>
/// This is an empty base class and not a marker (empty) interface because with a base class
/// we can ensure the constructor is only callable by types in the current assembly. If this was
/// a marker interface, then anything could implement it. However, the security types in this
/// assembly only support specific derived types that are also in this assembly.
/// </devnote>
public abstract class NodeSecurity
{
	private protected NodeSecurity()
	{
	}
}
