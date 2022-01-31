namespace Menees.Remoting;

#region Using Directives

using System.IO.Pipes;
using Microsoft.Extensions.Logging;

#endregion

internal sealed class PipeServer : PipeBase
{
	#region Private Data Members

	private readonly HashSet<PipeServerListener> listeners = new();
	private readonly int minListeners;
	private readonly int maxListeners;
	private readonly ILogger logger;

	#endregion

	#region Constructors

	internal PipeServer(string pipeName, int minListeners, int maxListeners, Func<Stream, Task> processRequestAsync, ILoggerFactory loggers)
		: base(pipeName, loggers)
	{
		if (minListeners <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(minListeners), $"{nameof(minListeners)} must be positive.");
		}

		// Stephen Toub made a comment "that Windows supports this across processes",
		// and it seems to imply that Unix doesn't. Maybe it's a per process limit in Unix.
		// https://github.com/dotnet/corefx/pull/24798#issuecomment-338809086
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
		this.ProcessRequestAsync = processRequestAsync;
		this.logger = loggers.CreateLogger(this.GetType());

		// Note: We don't create any listeners here in the constructor because we want to finish construction first.
		// If we created even one listener here, then it could start processing on a worker thread and immediately
		// call back in to EnsureMinListeners, which would mean a listener was using a partially constructed PipeServer. Yuck.
		// It's safer, better, and easier to reason about if we require a separate post-constructor call to EnsureMinListeners.
	}

	#endregion

	#region Public Properties

	public Func<Stream, Task> ProcessRequestAsync { get; }

	public Action<Exception>? ReportUnhandledException { get; set; }

	#endregion

	#region Internal Methods

	internal void LogTrace(string? message, params object?[] args)
		=> this.Log(LogLevel.Trace, null, message, args);

	internal void Log(LogLevel logLevel, Exception? ex, string? message, params object?[] args)
	{
		// TODO: Use a static template expression. [Bill, 1/29/2022]
		// https://docs.microsoft.com/en-us/dotnet/core/extensions/high-performance-logging
#pragma warning disable CA2254 // Template should be a static expression.
		using (this.logger.BeginScope(new Dictionary<string, object> { { nameof(this.PipeName), this.PipeName } }))
		{
			this.logger.Log(logLevel, ex, message, args);
		}
#pragma warning restore CA2254 // Template should be a static expression
	}

	internal void EnsureMinListeners()
	{
		this.LogTrace("About to request server listener lock.");
		lock (this.listeners)
		{
			this.LogTrace("Initial listeners: {Count}", this.listeners.Count);

			// Make a list copy of the hashset since we may need to remove members from the hashset.
			foreach (PipeServerListener listener in this.listeners.ToList())
			{
				// Listeners normally self-dispose, so we can just let go of them here.
				if (listener.State == ListenerState.Disposed)
				{
					this.listeners.Remove(listener);
				}
			}

			this.LogTrace("Non-disposed listeners: {Count}", this.listeners.Count);

			int availableListeners = (this.maxListeners == NamedPipeServerStream.MaxAllowedServerInstances
				? int.MaxValue : this.maxListeners) - this.listeners.Count;

			this.LogTrace("Available listeners: {Count}", availableListeners);
			if (availableListeners > 0)
			{
				int waitingCount = this.listeners.Count(listener => listener.State <= ListenerState.WaitingForConnection);
				this.LogTrace("Waiting listeners: {Count}", waitingCount);
				if (waitingCount < this.minListeners)
				{
					int createCount = Math.Min(this.minListeners - waitingCount, availableListeners);
					this.LogTrace("Create listeners: {Count}", createCount);
					for (int i = 0; i < createCount; i++)
					{
						NamedPipeServerStream? pipe = null;
						try
						{
							// Pass the actual maxListeners value to the new pipe since it's externally visible using SysInternals' PipeList.
							pipe = new(this.PipeName, Direction, this.maxListeners, Mode, PipeOptions.Asynchronous);
						}
						catch (IOException ex)
						{
							// We can get "All pipe instances are busy." if we reached the requested max limit or hit an OS limit.
							this.Log(LogLevel.Debug, ex, "Unable to create new named pipe server.");
						}

						if (pipe == null)
						{
							// "Merry Christmas. Shitter's full." https://www.youtube.com/watch?v=BeskbiJjCXI#t=21s
							break;
						}

						PipeServerListener listener = new(this, pipe);
						this.listeners.Add(listener);

						// Note: We're intentionally not waiting on this to return. This is a true fire-and-forget case.
						// When an I/O thread signals the listener that a client has connected, that listener will
						// call back into us to start a new listener when available.
#pragma warning disable VSTHRD110 // Observe result of async calls. Intentionally fire-and-forget.
						listener.StartAsync().ConfigureAwait(false);
#pragma warning restore VSTHRD110 // Observe result of async calls
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
