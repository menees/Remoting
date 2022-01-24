namespace Menees.Remoting
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.IO.Pipes;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	#endregion

	internal sealed class PipeClient : PipeBase
	{
		#region Private Data Members

		private NamedPipeClientStream? pipe;

		#endregion

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

		public void Connect(TimeSpan timeout)
		{
			this.EnsurePipe().Connect(ConvertTimeout(timeout));
		}

		public async Task ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
		{
			await this.EnsurePipe().ConnectAsync(ConvertTimeout(timeout), cancellationToken);
		}

		#endregion

		#region Protected Methods

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			this.pipe?.Dispose();
		}

		#endregion

		#region Private Methods

		private NamedPipeClientStream EnsurePipe()
		{
			this.pipe ??= new(this.ServerName, this.PipeName, Direction, Options) { ReadMode = Mode };
			return this.pipe;
		}

		#endregion
	}
}
