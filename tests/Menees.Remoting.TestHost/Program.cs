namespace Menees.Remoting.TestHost;

#region Using Directives

using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
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
		CtrlC = 5,
	}

	#endregion

	#region Main Entry Point

	public static int Main(string[] args)
	{
		ExitCode exitCode = ExitCode.Default;

		const int RequiredArgCount = 5;

		// Sometimes a lighter weight option is to use SysInternals PipeList utility from PowerShell
		// to see what pipes are open: .\pipelist.exe |select-string Menees
		// Or use PowerShell dir command: dir -path \\.\pipe\ -filter *Menees* |select Name
		// Or use the C# interactive window:
		// Directory.EnumerateFiles(@"\\.\pipe\", "*Menees*", SearchOption.AllDirectories)
		/* Debugger.Launch(); */

		if (args.Length != RequiredArgCount)
		{
			exitCode = FatalError(ExitCode.MissingArgs, $"Usage: {nameof(TestHost)} AssemblyPath TypeName ServerPathPrefix Max Min");
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

				// .NET Framework supports Load(AssemblyName), but .NET Core requires LoadFrom().
				Assembly assembly = Assembly.LoadFrom(assemblyPath);
				Type? serviceType = assembly.GetType(typeName);

				if (serviceType == null)
				{
					exitCode = FatalError(ExitCode.MissingType, $"Unable to load type {typeName} from assembly {assemblyPath}.");
				}
				else
				{
					Type? interfaceType = serviceType.GetInterfaces().FirstOrDefault();
					if (interfaceType == null)
					{
						exitCode = FatalError(ExitCode.MissingInterface, $"No interface found on type {serviceType}.");
					}
					else
					{
						using LogManager logManager = new();
						Type serverType = typeof(RmiServer<>).MakeGenericType(interfaceType);

						ServerSettings rmiServerSettings = new(serverPathPrefix + interfaceType.Name)
						{
							MaxListeners = maxListeners,
							MinListeners = minListeners,
							CreateLogger = logManager.LoggerFactory.CreateLogger,
						};
						object serviceInstance = Activator.CreateInstance(serviceType)!;
						using IServer rmiServer = (IServer)Activator.CreateInstance(serverType, serviceInstance, rmiServerSettings)!;

						ServerSettings messageServerSettings = new(serverPathPrefix + "Echo")
						{
							MaxListeners = maxListeners,
							MinListeners = minListeners,
							CreateLogger = logManager.LoggerFactory.CreateLogger,
						};
						using MessageServer<string, string> echoMessageServer = new(input => Task.FromResult(input), messageServerSettings);

						string hostServerPath = serverPathPrefix + nameof(IServerHost);
						using ServerHost host = new();
						using RmiServer<IServerHost> hostServer = new(host, hostServerPath, 1, loggerFactory: logManager.LoggerFactory);

						host.Add(rmiServer);
						host.Add(echoMessageServer);
						host.Add(hostServer);

						using IDisposable? manualExitHandler = HandleManualExit(host);
						host.WaitForExit();
						exitCode = (ExitCode)(host.ExitCode ?? 0);
					}
				}
			}
			catch (Exception ex)
			{
				exitCode = FatalError(ExitCode.UnhandledException, $"Unhandled exception: {ex}");
			}
		}

		return (int)exitCode;
	}

	#endregion

	#region Private Methods

	private static ExitCode FatalError(ExitCode exitCode, string message)
	{
		Error.WriteLine(message);
		Error.WriteLine($"Exit code: {exitCode}");
		return exitCode;
	}

	private static IDisposable? HandleManualExit(IServerHost host)
	{
		IDisposable? result;

#if NETCOREAPP
		result = PosixSignalRegistration.Create(
			PosixSignal.SIGINT,
			context => host.Exit((int)ExitCode.CtrlC));
#else
		result = null;
#endif

		return result;
	}

	#endregion
}