namespace Menees.Remoting;

/// <summary>
/// Defines the basic, non-type-specific API for a <see cref="MessageServer{TIn, TOut}"/>
/// and an <see cref="RmiServer{TServiceInterface}"/>.
/// </summary>
public interface IServer : IDisposable
{
	#region Public Properties

	/// <summary>
	/// Used to report any unhandled or unobserved exceptions from server listener threads.
	/// </summary>
	Action<Exception>? ReportUnhandledException { get; set; }

	/// <summary>
	/// See <see cref="Node.TryGetType"/>.
	/// </summary>
	Func<string, Type?> TryGetType { get; set; }

	#endregion

	#region Public Methods

	/// <summary>
	/// Starts listening for incoming requests.
	/// </summary>
	void Start();

	#endregion
}
