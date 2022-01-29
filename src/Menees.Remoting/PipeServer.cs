namespace Menees.Remoting;

#region Using Directives

using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

#endregion

internal sealed class PipeServer : PipeBase
{
	#region Private Data Members

	private static readonly bool IsNetFramework = RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework");

	private readonly HashSet<PipeServerListener> listeners = new();
	private readonly int minListeners;
	private readonly int maxListeners;
	private readonly ILogger logger;

	#endregion

	#region Constructors

	internal PipeServer(string pipeName, int minListeners, int maxListeners, Action<Stream> processRequest, ILogger logger)
		: base(pipeName)
	{
		if (minListeners <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(minListeners), $"{nameof(minListeners)} must be positive.");
		}

		if (maxListeners != NamedPipeServerStream.MaxAllowedServerInstances)
		{
			if (maxListeners <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(maxListeners),
					$"{nameof(maxListeners)} must be positive or {nameof(NamedPipeServerStream.MaxAllowedServerInstances)}.");
			}
			else if (maxListeners < minListeners)
			{
				throw new ArgumentOutOfRangeException(nameof(maxListeners), $"{nameof(maxListeners)} must be >= {nameof(minListeners)}.");
			}
		}

		this.minListeners = minListeners;
		this.maxListeners = maxListeners;
		this.ProcessRequest = processRequest;
		this.logger = logger;

		// Note: We don't create any listeners here in the constructor because we want to finish construction first.
		// If we created even one listener here, then it could start processing on a worker thread and immediately
		// call back in to EnsureMinListeners, which would mean a listener was using a partially constructed PipeServer. Yuck.
		// It's safer, better, and easier to reason about if we require a separate post-constructor call to EnsureMinListeners.
	}

	#endregion

	#region Public Properties

	public Action<Stream> ProcessRequest { get; }

	public Action<Exception>? ReportUnhandledException { get; set; }

	#endregion

	#region Private Properties

	// .NET Framework requires the Asynchronous option in order to call BeginWaitForConnection in the listener,
	// and everything works correctly in the server even though the client is synchronous.
	//
	// .NET 6.0.1 and up deadlocks due to blocked async threads with the threadpool is all in use. It shows up
	// in unit tests when using Parallel.ForEach. The blocks happen because our Message class is using synchronous
	// I/O with the stream. When we use the None option, it all works without deadlocking the async threads.
	private static PipeOptions Options => IsNetFramework ? PipeOptions.Asynchronous : PipeOptions.None;

	#endregion

	#region Internal Methods

	internal void LogTrace(string? message, params object?[] args)
	{
		// TODO: Use a static template expression. [Bill, 1/29/2022]
#pragma warning disable CA2254 // Template should be a static expression.
		this.logger.LogTrace(message, args);
#pragma warning restore CA2254 // Template should be a static expression
	}

	internal void EnsureMinListeners()
	{
		this.LogTrace("About to request server listener lock.");
		lock (this.listeners)
		{
			this.LogTrace("Initial Listeners: {Count}", this.listeners.Count);

			// Make a list copy of the hashset since we may need to remove members from the hashset.
			foreach (PipeServerListener listener in this.listeners.ToList())
			{
				// Listeners normally self-dispose, so we can just let go of them here.
				if (listener.State == ListenerState.Disposed)
				{
					this.listeners.Remove(listener);
				}
			}

			// TODO: Fix inconsistent capitalization. [Bill, 1/29/2022]
			this.LogTrace("Non-Disposed Listeners: {Count}", this.listeners.Count);

			int availableListeners = (this.maxListeners == NamedPipeServerStream.MaxAllowedServerInstances
				? int.MaxValue : this.maxListeners) - this.listeners.Count;

			this.LogTrace("Available Listeners: {Count}", availableListeners);
			if (availableListeners > 0)
			{
				int waitingCount = this.listeners.Count(listener => listener.State == ListenerState.WaitingForConnection);
				this.LogTrace("Waiting Listeners: {Count}", waitingCount);
				if (waitingCount < this.minListeners)
				{
					int createCount = Math.Min(this.minListeners - waitingCount, availableListeners);
					this.LogTrace("Create Listeners: {Count}", createCount);
					for (int i = 0; i < createCount; i++)
					{
						// Pass the actual maxListeners value to the new pipe since it's externally visible using SysInternals' PipeList.
						NamedPipeServerStream pipe = new(this.PipeName, Direction, this.maxListeners, Mode, Options);
						PipeServerListener listener = new(this, pipe);
						this.listeners.Add(listener);
					}
				}
			}
		}
	}

	#endregion

	#region Protected Methods

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		lock (this.listeners)
		{
			foreach (PipeServerListener listener in this.listeners)
			{
				listener.Dispose();
			}

			this.listeners.Clear();
		}
	}

	#endregion
}
