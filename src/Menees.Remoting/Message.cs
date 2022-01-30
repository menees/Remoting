namespace Menees.Remoting;

// TODO: Make all stream usage in Message use async. [Bill, 1/29/2022]
// Toub says use ConfigureAwait(false) everywhere and "return Lib.FooAsync().Result" is ok.
// https://devblogs.microsoft.com/pfxteam/should-i-expose-synchronous-wrappers-for-asynchronous-methods/
internal abstract class Message
{
	#region Public Methods

	public static T ReadFrom<T>(Stream stream, ISerializer serializer)
		where T : Message
	{
		if (!TryRead(stream, sizeof(int), out byte[] buffer))
		{
			throw new ArgumentException(
				$"Unable to read {sizeof(int)} byte message length from stream. Only {buffer.Length} bytes were available.");
		}

		// The message length is always written in little endian order.
		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse(buffer);
		}

		int length = BitConverter.ToInt32(buffer, 0);
		if (!TryRead(stream, length, out buffer))
		{
			throw new ArgumentException(
				$"Unable to read {length} byte message from stream. Only {buffer.Length} bytes were available.");
		}

		T result = (T?)serializer.Deserialize(buffer, typeof(T))
			?? throw new ArgumentNullException($"{typeof(T).Name} message cannot be null.");

		return result;
	}

	public void WriteTo(Stream stream, ISerializer serializer)
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

		stream.Write(messageLength, 0, messageLength.Length);
		stream.Write(message, 0, message.Length);
		stream.Flush();
	}

	#endregion

	#region Private Methods

	private static bool TryRead(Stream stream, int requiredCount, out byte[] data)
	{
		data = new byte[requiredCount];

		int totalCount = 0;
		while (totalCount < requiredCount)
		{
			// Read blocks until data arrives. Typically, it returns the exact amount we request (once
			// a writer pushes that into the stream). Theoretically, it could return less and then make
			// more data available on the next call. So we'll loop until the stream runs out (i.e., is closed
			// by the writer) or until we get what we want. https://stackoverflow.com/a/46797865/1882616
			int readCount = stream.Read(data, totalCount, requiredCount - totalCount);
			if (readCount <= 0)
			{
				break;
			}

			totalCount += readCount;
		}

		bool result = totalCount == requiredCount;
		if (!result)
		{
			// We don't want to give back an array that's too long, and we want the caller to know exactly
			// what (and how much) we received. This should be very rare and only in error conditions.
			Array.Resize(ref data, totalCount);
		}

		return result;
	}

	#endregion
}
