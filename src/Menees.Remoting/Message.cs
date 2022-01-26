namespace Menees.Remoting;

internal abstract class Message
{
	#region Public Methods

	public static T ReadFrom<T>(Stream stream, ISerializer serializer)
		where T : Message
	{
		// The message length is always written in little endian order.
		byte[] messageLength = ReadExactly(stream, sizeof(int));
		if (!BitConverter.IsLittleEndian)
		{
			Array.Reverse(messageLength);
		}

		int length = BitConverter.ToInt32(messageLength, 0);
		byte[] message = ReadExactly(stream, length);

		T result = (T?)serializer.Deserialize(message, typeof(T))
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

	private static byte[] ReadExactly(Stream stream, int requiredCount)
	{
		byte[] result = new byte[requiredCount];
		int totalCount = 0;
		while (totalCount < requiredCount)
		{
			int readCount = stream.Read(result, totalCount, requiredCount - totalCount);
			if (readCount <= 0)
			{
				throw new ArgumentException(
					$"Unable to read {requiredCount} bytes from stream. Only {totalCount} bytes were available.");
			}

			totalCount += readCount;
		}

		return result;
	}

	#endregion
}
