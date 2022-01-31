namespace Menees.Remoting;

#region Using Directives

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections;

#endregion

[TestClass]
public sealed class AssemblyEvents
{
	#region Private Data Members

	private static ILoggerFactory? loggerFactory;
	private static ImmediateConsoleLoggerProvider? consoleLoggerProvider;

	#endregion

	#region Public Initialize/Cleanup Methods

	[AssemblyInitialize]
	public static void Initialize(TestContext testContext)
	{
		// We have to reference testContext to make the compiler happy,
		// and we have to take it as a parameter for AssemblyInitialize to work.
		testContext.GetHashCode();

		loggerFactory = LoggerFactory.Create(builder =>
		{
			builder
			.ClearProviders()
			.SetMinimumLevel(LogLevel.Trace)
			.AddDebug(); // Make the messages show up in the debugger's Output window.

			// Note: We can't use any of the AddConsole methods from Microsoft.Extensions.Logging.Console because
			// they all buffer the formatted lines in a worker queue, and the lines won't show up in the correct unit test
			// due to the way MSTest attaches and detaches from stdout and stderr for each test.
			// One workaround is to (re)create the logger factory and dispose of it in every test.
			// Another workaround is to use a better logging system like Serilog.Sinks.Console.
			// Or we can just use a simple local provider like ImmediateConsoleLoggerProvider.
			// https://codeburst.io/unit-testing-with-net-core-ilogger-t-e8c16c503a80
			// https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Console/src/ConsoleLogger.cs#L61
			consoleLoggerProvider = new ImmediateConsoleLoggerProvider();
			builder.AddProvider(consoleLoggerProvider);
		});

		Console.WriteLine("In Initialize");
		loggerFactory.CreateLogger<AssemblyEvents>().LogInformation("Also in Initialize");
	}

	[AssemblyCleanup]
	public static void Cleanup()
	{
		loggerFactory?.Dispose();
		consoleLoggerProvider?.Dispose();
	}

	#endregion

	#region Public Helper Methods

	public static ILogger<T> CreateLogger<T>()
		=> (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<T>();

	public static ILogger<RmiServer<T>> CreateServerLogger<T>()
		where T : class
		=> (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<RmiServer<T>>();

	#endregion

	#region Private Types

	private sealed class ImmediateConsoleLoggerProvider : ILoggerProvider
	{
		#region Public Methods

		public ILogger CreateLogger(string categoryName) => new ImmediateConsoleLogger(categoryName);

		public void Dispose()
		{
		}

		#endregion

		private sealed class ImmediateConsoleLogger : ILogger
		{
			#region Private Data Members

			private readonly string categoryName;
			private readonly Stack<object?> scope = new();

			#endregion

			#region Constructors

			public ImmediateConsoleLogger(string categoryName)
			{
				this.categoryName = categoryName;
			}

			#endregion

			#region Public Methods

			public IDisposable BeginScope<TState>(TState state)
				=> new Scope(this.scope, state);

			public bool IsEnabled(LogLevel logLevel) => true;

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
			{
				StringBuilder sb = new();
				sb.AppendFormat("{0:HH:mm:ss.fff} [{1}] ", DateTime.Now, logLevel);
				if (eventId.Id != 0)
				{
					sb.Append('#').Append(eventId.Id).Append(' ');
				}

				sb.Append(this.categoryName).Append(' ');

				string message = formatter(state, exception);
				sb.Append(message);

				foreach (object? value in this.scope)
				{
					sb.Append(' ');
					Append(sb, value);
				}

				string line = sb.ToString();
				Console.WriteLine(line);
			}

			#endregion

			#region Private Methods

			private static void Append(StringBuilder sb, object? value)
			{
				sb.Append('{');
				if (value is string text)
				{
					sb.Append(text);
				}
				else if (value is IDictionary dictionary)
				{
					foreach (DictionaryEntry entry in dictionary)
					{
						Append(sb, entry.Key);
						sb.Append('=');
						Append(sb, entry.Value);
					}
				}
				else if (value is IEnumerable enumerable)
				{
					foreach (object? item in enumerable)
					{
						Append(sb, item);
					}
				}
				else
				{
					sb.Append(value);
				}

				sb.Append('}');
			}

			#endregion

			#region Private Types

			private sealed class Scope : IDisposable
			{
				#region Private Data Members

				private readonly Stack<object?> scope;

				#endregion

				#region Constructors

				public Scope(Stack<object?> scope, object? state)
				{
					this.scope = scope;
					this.scope.Push(state);
				}

				#endregion

				#region Public Methods

				public void Dispose()
				{
					this.scope.Pop();
				}

				#endregion
			}

			#endregion
		}
	}

	#endregion
}
