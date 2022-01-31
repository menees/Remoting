using System.Reflection;
using static System.Console;

ExitCode exitCode = ExitCode.Default;

const int RequiredArgCount = 5;

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

		AssemblyName assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
		Assembly assembly = Assembly.Load(assemblyName);
		Type? serviceType = assembly.GetType(typeName);

		if (serviceType == null)
		{
			exitCode = FataError(ExitCode.MissingType, $"Unable to load type {typeName} from assembly {assemblyName}.");
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
				Type serverType = typeof(RmiServer<>).MakeGenericType(interfaceType);

				string serverPath = serverPathPrefix + "`TargetType";
				object serviceInstance = Activator.CreateInstance(serviceType)!;
				using IRmiServer server = (IRmiServer)Activator.CreateInstance(serverType, serverPathPrefix, serviceInstance, maxListeners, minListeners)!;

				// TODO: Configure logging and pass a logger to server. [Bill, 1/31/2022]
				ServerHostManager manager = new();
				using RmiServer<IServerHost> managerServer = new(serverPathPrefix + "`Manager", manager, 1);

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