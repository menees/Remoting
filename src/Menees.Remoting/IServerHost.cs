namespace Menees.Remoting;

/// <summary>
/// Defines the basic API for an <see cref="IServer"/> host that can be told when to exit.
/// </summary>
public interface IServerHost
{
	/// <summary>
	/// Gets whether the host is ready to receive <see cref="IServer"/> requests.
	/// </summary>
	/// <remarks>
	/// This is primarily for testing readiness at host startup (e.g., when a new host worker process is launched).
	/// In out-of-process scenarios, if the host has exited, then calling this property will throw an exception
	/// because the client won't be able to connect to the server.
	/// </remarks>
	bool IsReady { get; }

	/// <summary>
	/// Tells the host to begin the shutdown process and to signal any associated <see cref="IServer"/> instances to stop.
	/// </summary>
	/// <param name="exitCode">An optional exit code to pass to the host.</param>
	void Exit(int? exitCode = null);
}
