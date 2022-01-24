![windows build & test](https://github.com/menees/Remoting/workflows/windows%20build%20&%20test/badge.svg)

# Remoting
This repo provides a simple [IPC](https://en.wikipedia.org/wiki/Inter-process_communication)/[RMI](https://en.wikipedia.org/wiki/Remote_method_invocation)
library for .NET Standard 2.0. It's designed to help ease the migration from legacy [.NET Remoting](https://en.wikipedia.org/wiki/.NET_Remoting)
and [WCF](https://en.wikipedia.org/wiki/Windows_Communication_Foundation) APIs from .NET Framework when porting code to .NET (Core).

Instead of trying to [do all the things](https://knowyourmeme.com/memes/all-the-things), Menees.Remoting tries to be simple and provide
a single way to invoke .NET interface methods in a remote object. It doesn't try to be a general purpose IPC library.
It lets a server process expose a .NET interface for a given instance object, and it lets one or more client processes
invoke those .NET interface methods on that server's instance object.

Menees.Remoting doesn't use or care about MarshalByRefObject, OperationContract, ServiceContract, or other legacy technologies.
It uses named pipes and a serializer of your choice to do remote method invocation on a server object.

TODO: Add more explanation and examples.