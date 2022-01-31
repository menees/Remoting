namespace Menees.Remoting;

#region Using Directives

using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Extensions.Logging;

#endregion

internal sealed class PipeClient : PipeBase
{
	#region Constructors

	internal PipeClient(string pipeName, string serverName, ILoggerFactory loggers)
		: base(pipeName, loggers)
	{
		this.ServerName = serverName;
	}

	#endregion

	#region Public Properties

	public string ServerName { get; }

	#endregion

	#region Public Methods

	internal void SendRequest(TimeSpan connectTimeout, Action<Stream> sendRequest)
	{
		// We only use a pipe for a single request. Remotely invoked interfaces shouldn't be chatty anyway.
		// Single-use connections are easier to reason about and manage the state for. They also give us
		// a lot of freedom to swap in other transports later (e.g., Http, ZeroMQ, TcpClient/TcpListener) if desired.
		// HTTP 1.0 used non-persistent connections, and it was fine for non-chatty interfaces.
		using NamedPipeClientStream pipe = new(this.ServerName, this.PipeName, Direction, PipeOptions.None);

		// System.Threading.Timeout.InfiniteTimeSpan is -1 milliseconds (-00:00:00.0010000).
		// To simplify our "while" loop and remainingWaitTime logic we'll start from a non-negative value.
		if (connectTimeout < TimeSpan.Zero)
		{
			connectTimeout = TimeSpan.MaxValue;
		}

		const int ERROR_SEM_TIMEOUT = unchecked((int)0x80070079);
		bool connected = false;
		TimeSpan remainingWaitTime = connectTimeout;
		Stopwatch stopwatch = Stopwatch.StartNew();
		do
		{
			try
			{
				int remainingTimeout = ConvertTimeout(remainingWaitTime);
				int connectAttemptTimeout = remainingTimeout == Timeout.Infinite ? Timeout.Infinite : Math.Max(1, remainingTimeout);
				pipe.Connect(connectAttemptTimeout);
				connected = true;
				break;
			}
			catch (FileNotFoundException ex)
			{
				// We can get FileNotFoundException with "Unable to find the specified file" due to a documented race condition
				// with the Win32 WaitNamedPipe API: "A return value of TRUE indicates that there is at least one instance of the pipe available.
				// A subsequent CreateFile call to the pipe can fail, because the instance was closed by the server or opened by another client."
				// So our client Connect call saw that a server pipe was available, but another waiting client thread grabbed it before we could.
				// We'll just retry as long as we're within our overall connect timeout interval.
				// https://stackoverflow.com/questions/23432640/namedpipeclientstream-connect-throws-system-io-filenotfoundexception-unable-t
				// https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-waitnamedpipea?redirectedfrom=MSDN#remarks
				this.Loggers.CreateLogger(this.GetType()).LogDebug(ex, "Need to retry connect since another client connected first.");
			}
			catch (IOException ex) when (ex.HResult == ERROR_SEM_TIMEOUT)
			{
				// If a very short timeout is used (e.g., 1ms), then the pipe's semaphore wait will fail
				// with an IOException("The semaphore timeout period has expired.").
				// https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-waitnamedpipea#remarks
				throw new TimeoutException("Could not connect to the server due to a semaphore timeout.", ex);
			}

			remainingWaitTime = connectTimeout == TimeSpan.MaxValue ? TimeSpan.MaxValue : connectTimeout - stopwatch.Elapsed;
		}
		while (!connected && remainingWaitTime > TimeSpan.Zero);

		if (!connected)
		{
			// The code in NamedPipeClientStream.Connect(int) does "throw new TimeoutException();" with no message.
			throw new TimeoutException("Could not connect to the server within the specified timeout period.");
		}

		pipe.ReadMode = Mode;
		sendRequest(pipe);
	}

	#endregion
}
