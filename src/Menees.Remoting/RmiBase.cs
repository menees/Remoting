namespace Menees.Remoting
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using System.Text;

	#endregion

	/// <summary>
	/// Shared functionality for <see cref="RmiClient{T}"/> and <see cref="RmiServer{T}"/>.
	/// </summary>
	/// <typeparam name="TServiceInterface">The interface to remotely invoke/expose members for.</typeparam>
	public abstract class RmiBase<TServiceInterface> : IDisposable
		where TServiceInterface : class
	{
		#region Protected Data Members

		private static readonly ISerializer DefaultSerializer = new JSerializer();
		private bool disposed;

		#endregion

		#region Constructors

		/// <summary>
		/// Validates that <typeparamref name="TServiceInterface"/> is an interface.
		/// </summary>
		protected RmiBase(ISerializer? serializer)
		{
			Type interfaceType = typeof(TServiceInterface);
			if (!interfaceType.IsInterface)
			{
				throw new ArgumentException($"{nameof(TServiceInterface)} {interfaceType.FullName} must be an interface type.");
			}

			this.Serializer = serializer ?? DefaultSerializer;
		}

		#endregion

		#region Protected Properties

		private protected ISerializer Serializer { get; private set; }

		#endregion

		#region Public Methods

		/// <summary>
		/// Disposes of managed resources.
		/// </summary>
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			this.Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		#endregion

		#region Protected Methods

		/// <summary>
		/// Disposes of managed resources.
		/// </summary>
		/// <param name="disposing">True if <see cref="Dispose()"/> was called. False if this was called from a derived type's finalizer.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (!this.disposed)
			{
				// Allow any custom serializer to be GCed.
				this.Serializer = null!;
				this.disposed = true;
			}
		}

		#endregion

		#region Private Protected Methods

		private protected static string GetMethodSignature(MethodInfo methodInfo)
		{
			// For our purposes MethodInfo.ToString() returns a unique enough signature.
			// For example, typeof(string).GetMethods().Last(m => m.Name == "IndexOf").ToString()
			// returns "Int32 IndexOf(Char, Int32, Int32)".
			// For other options see: https://stackoverflow.com/a/1312321/1882616
			string result = methodInfo.ToString() ?? throw new InvalidOperationException("Null method signature is not supported.");
			return result;
		}

		#endregion
	}
}
