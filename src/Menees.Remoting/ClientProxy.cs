namespace Menees.Remoting
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using System.Threading.Tasks;

	#endregion

	internal sealed class ClientProxy<TServiceInterface> : DispatchProxy
		where TServiceInterface : class
	{
		#region Private Data Members

		private RmiClient<TServiceInterface>? client;

		#endregion

		#region Constructors

		internal ClientProxy()
		{
			// Note: DispatchProxy requires a default constructor.
		}

		#endregion

		#region Internal Methods

		internal void Initialize(RmiClient<TServiceInterface> client)
		{
			this.client = client;
		}

		#endregion

		#region Protected Methods

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			if (this.client == null)
			{
				throw new InvalidOperationException("Client proxy was not initialized.");
			}

			// TODO: Serialize args and transmit to server using this.client. Deserialize result. [Bill, 1/23/2022]
			this.client.GetHashCode();

			throw new NotImplementedException();
		}

		#endregion
	}
}
