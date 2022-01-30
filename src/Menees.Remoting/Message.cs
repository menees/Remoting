namespace Menees.Remoting;

internal abstract class Message
{
	#region Public Methods

	public static async Task<T> ReadFromAsync<T>(Stream stream, ISerializer serializer)
		where T : Message
	{
		byte[] buffer = await RequireReadAsync(stream, sizeof(int), "message length").ConfigureAwait(false);

		// The message length is always written in little endian order.
		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse(buffer);
		}

		int length = BitConverter.ToInt32(buffer, 0);
		buffer = await RequireReadAsync(stream, length, "message body").ConfigureAwait(false);

		T result = (T?)serializer.Deserialize(buffer, typeof(T))
			?? throw new ArgumentNullException($"{typeof(T).Name} message cannot be null.");

		return result;
	}

	public async Task WriteToAsync(Stream stream, ISerializer serializer)
	{
		byte[] message = serializer.Serialize(this, this.GetType());

		// Always write the message length bytes in little endian order since that's Intel's
		// preferred ordering, so it'll be the most common order we see. If the client or server
		// client is using a different endianness, this will handle it.
		byte[] messageLength = BitConverter.GetBytes(message.Length);
		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse(messageLength);
		}

		await stream.WriteAsync(messageLength, 0, messageLength.Length).ConfigureAwait(false);
		await stream.WriteAsync(message, 0, message.Length).ConfigureAwait(false);
		await stream.FlushAsync().ConfigureAwait(false);
	}

	#endregion

	#region Private Methods

	private static async Task<byte[]> RequireReadAsync(Stream stream, int requiredCount, string forWhat)
	{
		byte[] result = new byte[requiredCount];

		int totalCount = 0;
		while (totalCount < requiredCount)
		{
			// ReadAsync waits until data arrives. Typically, it returns the exact amount we request (once
			// a writer pushes that into the stream). Theoretically, it could return less and then make
			// more data available on the next call. So we'll loop until the stream runs out (i.e., is closed
			// by the writer) or until we get what we want. https://stackoverflow.com/a/46797865/1882616
			int readCount = await stream.ReadAsync(result, totalCount, requiredCount - totalCount).ConfigureAwait(false);
			if (readCount <= 0)
			{
				break;
			}

			totalCount += readCount;
		}

		if (totalCount != requiredCount)
		{
			throw new ArgumentException(
				$"Unable to read {requiredCount} byte {forWhat} from stream. Only {totalCount} bytes were available.");
		}

		return result;
	}

	#endregion
}
