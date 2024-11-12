namespace Menees.Remoting.Models;

internal abstract class Message
{
	#region Public Methods

	public static T ReadFrom<T>(Stream stream, ISerializer serializer)
		where T : Message
	{
		byte[] buffer = RequireRead(stream, sizeof(int), "message length");
		CheckEndianOrder(buffer);

		int length = BitConverter.ToInt32(buffer, 0);
		buffer = RequireRead(stream, length, "message body");

		T result = (T?)serializer.Deserialize(buffer, typeof(T))
			?? throw new ArgumentNullException($"{typeof(T).Name} message cannot be null.");

		return result;
	}

	public static async Task<T> ReadFromAsync<T>(Stream stream, ISerializer serializer, CancellationToken cancellationToken)
		where T : Message
	{
		byte[] buffer = await RequireReadAsync(stream, sizeof(int), "message length", cancellationToken).ConfigureAwait(false);
		CheckEndianOrder(buffer);

		int length = BitConverter.ToInt32(buffer, 0);
		buffer = await RequireReadAsync(stream, length, "message body", cancellationToken).ConfigureAwait(false);

		T result = (T?)serializer.Deserialize(buffer, typeof(T))
			?? throw new ArgumentNullException($"{typeof(T).Name} message cannot be null.");

		return result;
	}

	public void WriteTo(Stream stream, ISerializer serializer)
	{
		byte[] message = serializer.Serialize(this, this.GetType());
		byte[] messageLength = BitConverter.GetBytes(message.Length);
		CheckEndianOrder(messageLength);

		stream.Write(messageLength, 0, messageLength.Length);
		stream.Write(message, 0, message.Length);
		stream.Flush();
	}

	public async Task WriteToAsync(Stream stream, ISerializer serializer, CancellationToken cancellationToken)
	{
		byte[] message = serializer.Serialize(this, this.GetType());
		byte[] messageLength = BitConverter.GetBytes(message.Length);
		CheckEndianOrder(messageLength);

		// In .NET Framework the stream.ReadAsync implementation ignores cancellationToken.
		// This is a workaround. https://stackoverflow.com/a/12893018/1882616
		using CancellationTokenRegistration registration = cancellationToken.Register(stream.Close);

		// Check cancellation before and after each operation so we don't get an ObjectDisposedException
		// trying to use the stream after we closed it due to the cancellation token registration.
		cancellationToken.ThrowIfCancellationRequested();
		await stream.WriteAsync(messageLength, 0, messageLength.Length, cancellationToken).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
		await stream.WriteAsync(message, 0, message.Length, cancellationToken).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
		await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
	}

	#endregion

	#region Private Methods

	private static void CheckEndianOrder(byte[] buffer)
	{
		// Always write the message length bytes in little endian order since that's Intel's
		// preferred ordering, so it'll be the most common order we see. If the client or server
		// is using a different endianness, this will handle it. For example, if BitConverter.ToInt32
		// expects the incoming bytes to be in big endian order or BitConverter.GetBytes(int)
		// provides the outgoing bytes in big endian order, then this will reverse them.
		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse(buffer);
		}
	}

	private static byte[] RequireRead(Stream stream, int requiredCount, string forWhat)
	{
		byte[] result = new byte[requiredCount];

		int totalCount = 0;
		while (totalCount < requiredCount)
		{
			// Read blocks until data arrives. Typically, it returns the exact amount we request (once
			// a writer pushes that into the stream). Theoretically, it could return less and then make
			// more data available on the next call. So we'll loop until the stream runs out (i.e., is closed
			// by the writer) or until we get what we want. https://stackoverflow.com/a/46797865/1882616
			int readCount = stream.Read(result, totalCount, requiredCount - totalCount);
			if (readCount <= 0)
			{
				break;
			}

			totalCount += readCount;
		}

		if (totalCount != requiredCount)
		{
			throw new EndOfStreamException(
				$"Unable to read {requiredCount} byte {forWhat} from stream. Only {totalCount} bytes were available.");
		}

		return result;
	}

	private static async Task<byte[]> RequireReadAsync(Stream stream, int requiredCount, string forWhat, CancellationToken cancellationToken)
	{
		byte[] result = new byte[requiredCount];

		// In .NET Framework the stream.ReadAsync implementation ignores cancellationToken.
		// This is a workaround. https://stackoverflow.com/a/12893018/1882616
		// .NET 7 adds full support for stream and pipe cancellation.
		// https://devblogs.microsoft.com/dotnet/performance_improvements_in_net_7/#file-i-o
		using CancellationTokenRegistration registration = cancellationToken.Register(stream.Close);

		// We'll check cancellation before and after each operation so we don't get an ObjectDisposedException
		// trying to use the stream after we closed it due to the cancellation token registration.
		void ThrowIfCancellationRequested()
		{
			if (cancellationToken.IsCancellationRequested)
			{
				throw new OperationCanceledException($"Stream read of {forWhat} was canceled.");
			}
		}

		int totalCount = 0;
		while (totalCount < requiredCount)
		{
			// ReadAsync waits until data arrives. Typically, it returns the exact amount we request (once
			// a writer pushes that into the stream). Theoretically, it could return less and then make
			// more data available on the next call. So we'll loop until the stream runs out (i.e., is closed
			// by the writer) or until we get what we want. https://stackoverflow.com/a/46797865/1882616
			// https://github.com/dotnet/runtime/issues/16598 and https://github.com/dotnet/runtime/issues/58216
			ThrowIfCancellationRequested();
			int readCount = await stream.ReadAsync(result, totalCount, requiredCount - totalCount, cancellationToken).ConfigureAwait(false);
			ThrowIfCancellationRequested();
			if (readCount <= 0)
			{
				break;
			}

			totalCount += readCount;
		}

		if (totalCount != requiredCount)
		{
			ThrowIfCancellationRequested();
			throw new EndOfStreamException(
				$"Unable to read {requiredCount} byte {forWhat} from stream. Only {totalCount} bytes were available.");
		}

		return result;
	}

	#endregion
}
