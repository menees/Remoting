namespace Menees.Remoting.Pipes;

#region Using Directives

using System.IO.Pipes;
using Microsoft.Extensions.Logging;

#endregion

internal abstract class PipeNode : IDisposable
{
	#region Private Data Members

	private IDisposable? logScope;
	private bool disposed;

	#endregion

	#region Constructors

	protected PipeNode(string pipeName, ILoggerFactory loggers)
	{
		this.PipeName = pipeName;
		this.Loggers = loggers;
		this.Logger = loggers.CreateLogger(this.GetType());
		this.logScope = this.Logger.BeginScope(this.CreateScope());
	}

	#endregion

	#region Public Properties

	public string PipeName { get; }

	#endregion

	#region Protected Properties

	protected static PipeDirection Direction => PipeDirection.InOut;

	/// <summary>
	/// Message mode is only supported on Windows, so we'll use Byte mode and frame each "message" manually.
	/// </summary>
	protected static PipeTransmissionMode Mode => PipeTransmissionMode.Byte;

	#endregion

	#region Private Protected Properties

	private protected ILogger Logger { get; }

	private protected ILoggerFactory Loggers { get; }

	#endregion

	#region Public Methods

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		this.Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	#endregion

	#region Internal Methods

	internal Dictionary<string, object> CreateScope()
		=> new() { { nameof(this.PipeName), this.PipeName } };

	#endregion

	#region Protected Methods

	protected static int ConvertTimeout(TimeSpan timeout)
	{
		double totalMilliseconds = timeout.TotalMilliseconds;

		int result;
		if (totalMilliseconds >= 0 && totalMilliseconds <= int.MaxValue)
		{
			result = (int)totalMilliseconds;
		}
		else
		{
			result = Timeout.Infinite;
		}

		return result;
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!this.disposed)
		{
			if (disposing)
			{
				this.logScope?.Dispose();
				this.logScope = null;
			}

			this.disposed = true;
		}
	}

	#endregion
}
