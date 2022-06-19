namespace Menees.Remoting.Pipes;

#region Using Directives

using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Extensions.Logging;

#endregion

internal sealed class PipeClient : PipeNode
{
	#region Private Data Members

#pragma warning disable SA1310 // Field names should not contain underscore. Named like WinError.h constant.
	private const int ERROR_SEM_TIMEOUT = unchecked((int)0x80070079);
#pragma warning restore SA1310 // Field names should not contain underscore

	private readonly PipeClientSecurity? security;

	#endregion

	#region Constructors

	internal PipeClient(string pipeName, string serverName, Node owner, PipeClientSecurity? security)
		: base(pipeName, owner)
	{
		this.ServerName = serverName;
		this.security = security;
	}

	#endregion

	#region Public Properties

	public string ServerName { get; }

	#endregion

	#region Public Methods

	internal void SendRequest(TimeSpan connectTimeout, Action<Stream> sendRequest)
	{
		using NamedPipeClientStream pipe = this.CreatePipe(PipeOptions.None);

		connectTimeout = EnsureNonNegative(connectTimeout);
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
				this.LogFileNotFound(ex);
			}
			catch (IOException ex) when (ex.HResult == ERROR_SEM_TIMEOUT)
			{
				throw NewSemaphoreTimeoutException(ex);
			}

			remainingWaitTime = connectTimeout == TimeSpan.MaxValue ? TimeSpan.MaxValue : connectTimeout - stopwatch.Elapsed;
		}
		while (!connected && remainingWaitTime > TimeSpan.Zero);

		this.EnsureConnected(connected, pipe);
		sendRequest(pipe);
	}

	internal async Task SendRequestAsync(
		TimeSpan connectTimeout,
		Func<Stream, CancellationToken, Task> sendRequestAsync,
		CancellationToken cancellationToken)
	{
		using NamedPipeClientStream pipe = this.CreatePipe(PipeOptions.Asynchronous);

		connectTimeout = EnsureNonNegative(connectTimeout);
		bool connected = false;
		TimeSpan remainingWaitTime = connectTimeout;
		Stopwatch stopwatch = Stopwatch.StartNew();
		do
		{
			try
			{
				int remainingTimeout = ConvertTimeout(remainingWaitTime);
				int connectAttemptTimeout = remainingTimeout == Timeout.Infinite ? Timeout.Infinite : Math.Max(1, remainingTimeout);
				await pipe.ConnectAsync(connectAttemptTimeout, cancellationToken).ConfigureAwait(false);
				connected = true;
				break;
			}
			catch (FileNotFoundException ex)
			{
				this.LogFileNotFound(ex);
			}
			catch (IOException ex) when (ex.HResult == ERROR_SEM_TIMEOUT)
			{
				throw NewSemaphoreTimeoutException(ex);
			}

			remainingWaitTime = connectTimeout == TimeSpan.MaxValue ? TimeSpan.MaxValue : connectTimeout - stopwatch.Elapsed;
		}
		while (!connected && remainingWaitTime > TimeSpan.Zero);

		this.EnsureConnected(connected, pipe);
		await sendRequestAsync(pipe, cancellationToken).ConfigureAwait(false);
	}

	#endregion

	#region Private Methods

	private static TimeSpan EnsureNonNegative(TimeSpan connectTimeout)
	{
		// System.Threading.Timeout.InfiniteTimeSpan is -1 milliseconds (-00:00:00.0010000).
		// To simplify our "while" loop and remainingWaitTime logic we'll start from a non-negative value.
		if (connectTimeout < TimeSpan.Zero)
		{
			connectTimeout = TimeSpan.MaxValue;
		}

		return connectTimeout;
	}

	private static TimeoutException NewSemaphoreTimeoutException(IOException ex)
	{
		// If a very short timeout is used (e.g., 1ms), then the pipe's semaphore wait will fail
		// with an IOException("The semaphore timeout period has expired.").
		// https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-waitnamedpipea#remarks
		return new TimeoutException("Could not connect to the server due to a semaphore timeout.", ex);
	}

	private void EnsureConnected(bool connected, NamedPipeClientStream pipe)
	{
		if (!connected)
		{
			// The code in NamedPipeClientStream.Connect(int) does "throw new TimeoutException();" with no message.
			throw new TimeoutException("Could not connect to the server within the specified timeout period.");
		}

		this.security?.CheckConnection(pipe);
		pipe.ReadMode = Mode;
	}

	private NamedPipeClientStream CreatePipe(PipeOptions options)
	{
		// .NET 6 supports PipeOptions.CurrentUserOnly, but we have to simulate that in .NET Framework.
		options |= this.security?.Options ?? PipeOptions.None;

		// We only use a pipe for a single request. Remotely invoked interfaces shouldn't be chatty anyway.
		// Single-use connections are easier to reason about and manage the state for. They also give us
		// a lot of freedom to swap in other transports later (e.g., Http, ZeroMQ, TcpClient/TcpListener) if desired.
		// HTTP 1.0 used non-persistent connections, and it was fine for non-chatty interfaces.
		NamedPipeClientStream result = new(this.ServerName, this.PipeName, Direction, options);

		return result;
	}

	private void LogFileNotFound(FileNotFoundException ex)
	{
		// We can get FileNotFoundException with "Unable to find the specified file" due to a documented race condition
		// with the Win32 WaitNamedPipe API: "A return value of TRUE indicates that there is at least one instance of the pipe available.
		// A subsequent CreateFile call to the pipe can fail, because the instance was closed by the server or opened by another client."
		// So our client Connect call saw that a server pipe was available, but another waiting client thread grabbed it before we could.
		// We'll just retry as long as we're within our overall connect timeout interval.
		// https://stackoverflow.com/questions/23432640/namedpipeclientstream-connect-throws-system-io-filenotfoundexception-unable-t
		// https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-waitnamedpipea?redirectedfrom=MSDN#remarks
		this.Logger.LogDebug(ex, "Retry connect since another client connected first.");
	}

	#endregion
}
