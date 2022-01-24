namespace Menees.Remoting
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using System.Text;

	#endregion

	/// <summary>
	/// Used to invoke a <typeparamref name="TServiceInterface"/> interface member on a <see cref="RmiServer{T}"/>.
	/// </summary>
	/// <typeparam name="TServiceInterface">The interface to remotely invoke members on.</typeparam>
	public sealed class RmiClient<TServiceInterface> : RmiBase<TServiceInterface>
		where TServiceInterface : class
	{
		#region Private Data Members

		private readonly PipeClient pipe;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new client instance to invoke methods on a <see cref="RmiServer{TServiceInterface}"/> instance.
		/// </summary>
		/// <param name="serverPath">The path used to expose the service.</param>
		/// <param name="serverHost">The name of the remote server machine. Use "." for the local system.</param>
		/// <param name="serializer">An optional custom serializer.
		/// Note: The associated <see cref="RmiServer{TServiceInterface}"/> instance must use a compatible serializer.</param>
		public RmiClient(
			string serverPath,
			string serverHost = ".",
			ISerializer? serializer = null)
			: base(serializer)
		{
			this.pipe = new(serverPath, serverHost);
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Creates a <typeparamref name="TServiceInterface"/> proxy that remotely
		/// invokes members on an <see cref="RmiServer{TServiceInterface}"/> using the
		/// path passed to this <see cref="RmiClient{TServiceInterface}"/>'s constructor.
		/// </summary>
		/// <returns>A new proxy instance associated with this client.</returns>
		public TServiceInterface CreateProxy()
		{
			TServiceInterface result = DispatchProxy.Create<TServiceInterface, ClientProxy<TServiceInterface>>();
			if (result is not ClientProxy<TServiceInterface> proxy)
			{
				throw new InvalidOperationException("Unsupported proxy type.");
			}

			proxy.Initialize(this);
			return result;
		}

		#endregion
	}
}
