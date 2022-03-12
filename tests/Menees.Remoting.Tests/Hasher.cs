namespace Menees.Remoting;

using System.Security.Cryptography;

internal class Hasher : IHasher
{
	public byte[] Hash(byte[] data)
	{
		using HashAlgorithm hasher = SHA1.Create();
		return hasher.ComputeHash(data);
	}

	public string Hash(string text)
	{
		byte[] hash = this.Hash(Encoding.UTF8.GetBytes(text));
		return string.Concat(hash.Select(b => $"{b:x2}"));
	}
}
