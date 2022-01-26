using System.Reflection;
using static System.Console;

ExitCode exitCode = ExitCode.Default;

const int RequiredArgCount = 3;

if (args.Length != RequiredArgCount)
{
	exitCode = FataError(ExitCode.MissingArgs, $"Usage: {nameof(ServerHost)} AssemblyPath TypeName ServerPathPrefix");
}
else
{
	string assemblyPath = args[0];
	string typeName = args[1];
	string serverPathPrefix = args[2];

	try
	{
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

				// TODO: Support max and min listeners. [Bill, 1/26/2022]
				string serverPath = serverPathPrefix + "`TargetType";
				object serviceInstance = Activator.CreateInstance(serviceType)!;
				IRmiServer server = (IRmiServer)Activator.CreateInstance(serverType, serverPathPrefix, serviceInstance)!;

				// TODO: Support known IServerHost interface with Shutdown method that disposes server. [Bill, 1/26/2022]
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