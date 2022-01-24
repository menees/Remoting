namespace Menees.Remoting
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Pipes;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

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
			// We only use a pipe for a single request. Remotely invoked interfaces shouldn't be chatty anyway,
			// Single-use connections are easier to reason about and manage the state for. They also give us
			// a lot of freedom to swap in other transports later (e.g., Http, ZeroMQ, TcpClient/TcpListener) if desired.
			// HTTP 1.0 used non-persistent connections, and it was fine for non-chatty interfaces.
			using NamedPipeClientStream pipe = new(this.ServerName, this.PipeName, Direction, Options) { ReadMode = Mode };
			pipe.Connect(ConvertTimeout(connectTimeout));
			sendRequest(pipe);
		}

		#endregion
	}
}
