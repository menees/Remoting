using System.Diagnostics;
using System.Reflection;
using static System.Console;

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
				ServerHostManager manager = new();
				using RmiServer<IServerHost> managerServer = new(manager, hostServerPath, 1, loggerFactory: logManager.Loggers);

				rmiServer.Start();
				echoMessageServer.Start();
				managerServer.Start();
				manager.WaitForShutdown();

				// Give the IServerHost.Shutdown() client a little time to receive our response and disconnect.
				// Otherwise, this process could end too soon, and the client would get an ArgumentException
				// like "Unable to read 4 byte message length from stream. Only 0 bytes were available.".
				Thread.Sleep(TimeSpan.FromSeconds(1));
			}
		}
	}
	catch (Exception ex)
	{
		exitCode = FataError(ExitCode.UnhandledException, $"Unhandled exception: {ex}");
	}
}

return (int)exitCode;

static ExitCode FataError(ExitCode exitCode, string message)
{
	Error.WriteLine(message);
	Error.WriteLine($"Exit code: {exitCode}");
	return exitCode;
}