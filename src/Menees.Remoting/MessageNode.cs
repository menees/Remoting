namespace Menees.Remoting;

/// <summary>
/// Shared functionality for <see cref="MessageClient{TIn, TOut}"/> and <see cref="MessageServer{TIn, TOut}"/>.
/// </summary>
/// <typeparam name="TIn">The request message type.</typeparam>
/// <typeparam name="TOut">The response message type.</typeparam>
public abstract class MessageNode<TIn, TOut> : Node
{
	#region Constructors

	/// <summary>
	/// Creates a new instance.
	/// </summary>
	protected MessageNode(NodeSettings settings)
		: base(settings)
	{
	}

	#endregion
}