namespace Menees.Remoting;

/// <summary>
/// Defines the basic, non-type-specific API for an <see cref="RmiServer{TServiceInterface}"/>.
/// </summary>
public interface IRmiServer : IDisposable
{
	/// <summary>
	/// Used to report any unhandled or unobserved exceptions from server listener threads.
	/// </summary>
	Action<Exception>? ReportUnhandledException { get; set; }

	/// <summary>
	/// Starts listening for incoming requests.
	/// </summary>
	void Start();
}
