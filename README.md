![windows build & test](https://github.com/menees/Remoting/workflows/windows%20build%20&%20test/badge.svg)

# Remoting
This repo provides a simple [RMI](https://en.wikipedia.org/wiki/Remote_method_invocation) and [IPC](https://en.wikipedia.org/wiki/Inter-process_communication)
library for .NET Framework 4.8 and .NET 6.0+ applications. It's designed to help ease the migration from legacy [WCF](https://en.wikipedia.org/wiki/Windows_Communication_Foundation)
and [.NET Remoting](https://en.wikipedia.org/wiki/.NET_Remoting) when porting code from .NET Framework to modern .NET. However, this library doesn't try to do
[all the things](https://knowyourmeme.com/memes/all-the-things) like WCF and .NET Remoting.

Menees.Remoting:
* Prefers simplicity over performance.
* Is designed for non-chatty communication.
* Can't pass or return a .NET reference directly.
* Requires values to be serialized going to and from the server.
* Uses named pipes and a serializer of your choice (default is [System.Text.Json](https://docs.microsoft.com/en-us/dotnet/api/system.text.json)).
* Doesn't use or care about MarshalByRefObject, OperationContract, ServiceContract, etc.

## RMI
Menees.Remoting provides [RmiClient](src/Menees.Remoting/RmiClient.cs) to invoke .NET interface methods in a remote [RmiServer](src/Menees.Remoting/RmiServer.cs).
The server process exposes a .NET interface for a given instance object. Then one or more client processes invoke .NET interface methods on the server's instance object.
.NET's [DispatchProxy.Create](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.dispatchproxy.create) is used to generate the interface proxy, so clients can invoke 
the interface methods as normal C# calls.

``` C#
[TestMethod]
public void HasherExample()
{
    const string ServerPath = "Menees.Remoting.RmiClientTests.HasherExample";

    using RmiServer<IHasher> server = new(new Hasher(), ServerPath);
    server.Start();

    using RmiClient<IHasher> client = new(ServerPath);
    IHasher proxy = client.CreateProxy();

    proxy.Hash(new byte[] { 1, 2, 3, 4 }).Length.ShouldBe(20);
    proxy.Hash("Testing").ShouldBe("0820b32b206b7352858e8903a838ed14319acdfd");
}

internal interface IHasher
{
    byte[] Hash(byte[] data);
    string Hash(string text);
}

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
```

For more usage examples see:
* [RmiClientTests](tests/Menees.Remoting.Tests/RmiClientTests.cs)
* [RmiServerTests](tests/Menees.Remoting.Tests/RmiServerTests.cs)

Due to the synchronous API of the underlying
[DispatchProxy.Invoke](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.dispatchproxy.invoke) method, all the client invocations are sent
synchronously to the server. The `RmiClient` can't do asynchronous calls to the `RmiServer` due to risks with
[sync over async](https://devblogs.microsoft.com/pfxteam/should-i-expose-synchronous-wrappers-for-asynchronous-methods/).
For more on DispatchProxy's lack of InvokeAsync support see [#19349](https://github.com/dotnet/runtime/issues/19349)
and the comments for [Migrating RealProxy Usage to DispatchProxy](https://devblogs.microsoft.com/dotnet/migrating-realproxy-usage-to-dispatchproxy/).

## IPC
Menees.Remoting provides [MessageClient](src/Menees.Remoting/MessageClient.cs) to send a `TIn`-typed request message to
a [MessageServer](src/Menees.Remoting/MessageServer.cs) and receive a `TOut`-typed response message. Message IPC only
supports asynchronous calls, and the server requires a lambda that takes a `TIn` and returns a `Task<TOut>`.

``` C#
[TestMethod]
public async Task Base64ExampleAsync()
{
    const string ServerPath = "Menees.Remoting.MessageNodeTests.Base64ExampleAsync";

    using MessageServer<byte[], string> server = new(data => Task.FromResult(Convert.ToBase64String(data)), ServerPath);
    server.Start();

    using MessageClient<byte[], string> client = new(ServerPath);
    string response = await client.SendAsync(new byte[] { 1, 2, 3, 4 }).ConfigureAwait(false);
    response.ShouldBe("AQIDBA==");
}
```

For more usage examples see:
* [MessageNodeTests](tests/Menees.Remoting.Tests/MessageNodeTests.cs)

## No .NET Standard 2.0 Support
This library doesn't target .NET Standard 2.0 because that standard doesn't support:
* [DispatchProxy.Create](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.dispatchproxy.create)
 due to lack of [Reflection.Emit](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit) support in .NET Standard 2.0.
* Named pipe security (see [#26869](https://github.com/dotnet/runtime/issues/26869) and [StackOverflow](https://stackoverflow.com/a/54896975/1882616)).
