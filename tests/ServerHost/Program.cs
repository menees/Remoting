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

				string targetServerPath = serverPathPrefix + interfaceType.Name;
				ServerSettings targetServerSettings = new(targetServerPath)
				{
					MaxListeners = maxListeners,
					MinListeners = minListeners,
					LoggerFactory = logManager.Loggers,
				};
				object serviceInstance = Activator.CreateInstance(serviceType)!;
				using IRmiServer server = (IRmiServer)Activator.CreateInstance(serverType, serviceInstance, targetServerSettings)!;

				string hostServerPath = serverPathPrefix + nameof(IServerHost);
				ServerHostManager manager = new();
				using RmiServer<IServerHost> managerServer = new(manager, hostServerPath, 1, loggerFactory: logManager.Loggers);

				server.Start();
				managerServer.Start();
				manager.WaitForShutdown();
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