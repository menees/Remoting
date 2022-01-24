namespace Menees.Remoting
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.IO.Pipes;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	#endregion

	/// <summary>
	/// Exposes the <typeparamref name="TServiceInterface"/> interface from a given service object instance
	/// as a remotely invokable server.
	/// </summary>
	/// <typeparam name="TServiceInterface">The interface to make available for remote invocation.</typeparam>
	public sealed class RmiServer<TServiceInterface> : RmiBase<TServiceInterface>
		where TServiceInterface : class
	{
		#region Private Data Members

		private readonly PipeServer pipe;
		private readonly TServiceInterface serviceInstance;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new server instance with the specified name
		/// </summary>
		/// <param name="serverPath">The path used to expose the service.</param>
		/// <param name="serviceInstance">An instance of <typeparamref name="TServiceInterface"/> on which to execute remote invocations.
		/// </param>
		/// <param name="maxListeners">The maximum number of server listener tasks to start.</param>
		/// <param name="minListeners">The minimim number of server listener tasks to start.</param>
		/// <param name="serializer">An optional custom serializer.
		/// Note: All connecting <see cref="RmiClient{TServiceInterface}"/> instances must use a compatible serializer.
		/// </param>
		public RmiServer(
			string serverPath,
			TServiceInterface serviceInstance,
			int maxListeners = NamedPipeServerStream.MaxAllowedServerInstances,
			int minListeners = 1,
			ISerializer? serializer = null)
			: base(serializer)
		{
			this.serviceInstance = serviceInstance;

			// Note: The pipe is created with no listeners until we explicitly start them.
			this.pipe = new(serverPath, minListeners, maxListeners);
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Starts listening for incoming requests.
		/// </summary>
		public void Start() => this.pipe.EnsureMinListeners();

		#endregion
	}
}
