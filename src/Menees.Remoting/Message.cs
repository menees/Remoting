﻿namespace Menees.Remoting
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	#endregion

	internal abstract class Message
	{
		#region Public Methods

		public static T ReadFrom<T>(Stream stream, ISerializer serializer)
			where T : Message
		{
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
}
