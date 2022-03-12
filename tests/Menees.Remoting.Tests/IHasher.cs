namespace Menees.Remoting;

internal interface IHasher
{
	byte[] Hash(byte[] data);

	string Hash(string text);
}
