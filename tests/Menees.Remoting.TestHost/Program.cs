namespace Menees.Remoting.TestHost;

#region Using Directives

using System.Diagnostics;
using System.Reflection;
using static System.Console;

#endregion

public static class Program
{
	#region Private Enums

	private enum ExitCode
	{
		Default = 0,
		UnhandledException = 1,
		MissingType = 2,
		MissingInterface = 3,
		MissingArgs = 4,
	}

	#endregion

	#region Main Entry Point

	public static int Main(string[] args)
	{
		ExitCode exitCode = ExitCode.Default;

		const int RequiredArgCount = 6;

		// Debugger.Launch();
		if (args.Length != RequiredArgCount)
		{
			exitCode = FataError(ExitCode.MissingArgs, $"Usage: {nameof(ServerHost)} AssemblyPath TypeName ServerPathPrefix Max Min");
		}
		else
		{
			string assemblyPath = args[0];
			string typeName = args[1];
			string serverPathPrefix = args[2];

			try
			{
				int maxListeners = int.Parse(args[3]);
				int minListeners = int.Parse(args[4]);
				bool launchDebugger = bool.Parse(args[5]);
				if (launchDebugger)
				{
					Debugger.Launch();
				}

				// .NET Framework supports Load(AssemblyName), but .NET Core requires LoadFrom().
				Assembly assembly = Assembly.LoadFrom(assemblyPath);
				Type? serviceType = assembly.GetType(typeName);

				if (serviceType == null)
				{
					exitCode = FataError(ExitCode.MissingType, $"Unable to load type {typeName} from assembly {assemblyPath}.");
				}
				else
				{
					Type? interfaceType = serviceType.GetInterfaces().FirstOrDefault();
					if (interfaceType == null)
					{
						exitCode = FataError(ExitCode.MissingInterface, $"No interface found on type {serviceType}.");
					}
					else
					{
						using LogManager logManager = new();
						Type serverType = typeof(RmiServer<>).MakeGenericType(interfaceType);

						ServerSettings rmiServerSettings = new(serverPathPrefix + interfaceType.Name)
						{
							MaxListeners = maxListeners,
							MinListeners = minListeners,
							LoggerFactory = logManager.Loggers,
						};
						object serviceInstance = Activator.CreateInstance(serviceType)!;
						using IServer rmiServer = (IServer)Activator.CreateInstance(serverType, serviceInstance, rmiServerSettings)!;

						ServerSettings messageServerSettings = new(serverPathPrefix + "Echo")
						{
							MaxListeners = maxListeners,
							MinListeners = minListeners,
							LoggerFactory = logManager.Loggers,
						};
						using MessageServer<string, string> echoMessageServer = new(input => Task.FromResult(input), messageServerSettings);

						string hostServerPath = serverPathPrefix + nameof(IServerHost);
						using Menees.Remoting.ServerHost host = new();
						using RmiServer<IServerHost> hostServer = new(host, hostServerPath, 1, loggerFactory: logManager.Loggers);

						host.Add(rmiServer);
						host.Add(echoMessageServer);
						host.Add(hostServer);

						host.WaitForExit();
						exitCode = (ExitCode)(host.ExitCode ?? 0);
					}
				}
			}
			catch (Exception ex)
			{
				exitCode = FataError(ExitCode.UnhandledException, $"Unhandled exception: {ex}");
			}
		}

		return (int)exitCode;
	}

	#endregion

	#region Private Methods

	private static ExitCode FataError(ExitCode exitCode, string message)
	{
		Error.WriteLine(message);
		Error.WriteLine($"Exit code: {exitCode}");
		return exitCode;
	}

	#endregion
}