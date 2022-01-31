namespace Menees.Remoting;

#region Using Directives

using System.Diagnostics;
using System.IO.Pipes;

#endregion

internal sealed class PipeClient : PipeBase
{
	#region Constructors

	internal PipeClient(string pipeName, string serverName)
		: base(pipeName)
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

		Stopwatch stopwatch = Stopwatch.StartNew();
		while (stopwatch.Elapsed < connectTimeout)
		{
			try
			{
				TimeSpan remainingWaitTime = connectTimeout - stopwatch.Elapsed;
				pipe.Connect(Math.Max(1, ConvertTimeout(remainingWaitTime)));
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

				// TODO: Log this exception? [Bill, 1/31/2022]
				ex.GetHashCode();
			}
		}

		pipe.ReadMode = Mode;
		sendRequest(pipe);
	}

	#endregion
}
