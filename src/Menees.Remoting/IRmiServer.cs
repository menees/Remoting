namespace Menees.Remoting;

/// <summary>
/// Defines the basic, non-type-specific API for an <see cref="RmiServer{TServiceInterface}"/>.
/// </summary>
public interface IRmiServer : IDisposable
{
	#region Public Properties

	/// <summary>
	/// Used to report any unhandled or unobserved exceptions from server listener threads.
	/// </summary>
	Action<Exception>? ReportUnhandledException { get; set; }

	/// <summary>
	/// See <see cref="RmiBase{TServiceInterface}.TryGetType"/>.
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
