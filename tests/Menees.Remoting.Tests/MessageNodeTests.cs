namespace Menees.Remoting;

#region Using Directives

using System.IO.Pipes;
using System.Runtime.InteropServices;
using Menees.Remoting.Pipes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

#endregion

[TestClass]
public class MessageNodeTests : BaseTests
{
	#region Public Methods

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

	[TestMethod]
	public async Task CodeNameToStringInProcessAsync()
	{
		string serverPath = this.GenerateServerPath();

		using MessageServer<CodeName, string> server = new(
			async codeName => await Task.FromResult($"{codeName.Code}: {codeName.Name}").ConfigureAwait(false),
			serverPath,
			loggerFactory: this.LoggerFactory);
		server.Start();

		using MessageClient<CodeName, string> client = new(serverPath, loggerFactory: this.LoggerFactory);
		string response = await client.SendAsync(new CodeName { Code = 1, Name = "Billy" }).ConfigureAwait(false);
		response.ShouldBe("1: Billy");
		response = await client.SendAsync(new CodeName { Code = 2, Name = "Bob" }).ConfigureAwait(false);
		response.ShouldBe("2: Bob");
	}

	[TestMethod]
	public async Task StringToCodeNameInProcessAsync()
	{
		string serverPath = this.GenerateServerPath();

		using MessageServer<string, CodeName> server = new(
			async text => await Task.FromResult(new CodeName { Code = text.Length, Name = text }).ConfigureAwait(false),
			serverPath,
			maxListeners: 10,
			loggerFactory: this.LoggerFactory);
		server.Start();

		using MessageClient<string, CodeName> client = new(serverPath, loggerFactory: this.LoggerFactory);
		IEnumerable<Task> tasks = Enumerable.Range(1, 20).Select(async item =>
		{
			string combined = "Item " + item;
			CodeName response = await client.SendAsync(combined).ConfigureAwait(false);
			response.Code.ShouldBe(combined.Length);
			response.Name.ShouldBe(combined);
		});
		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task CrossProcessServerAsync()
	{
		await this.TestCrossProcessServerAsync(
			this.GenerateServerPathPrefix(),
			async prefix =>
			{
				using MessageClient<string, string> echoClient = new(prefix + "Echo", loggerFactory: this.LoggerFactory);
				for (int i = 1; i <= 5; i++)
				{
					string input = $"Test {i}";
					(await echoClient.SendAsync(input).ConfigureAwait(false)).ShouldBe(input);
				}
			},
			2).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task CrossProcessServerAndClientAsync()
	{
		await this.TestCrossProcessServerAsync(
			this.GenerateServerPathPrefix(),
			async prefix => await TestCrossProcessClientAsync(4, prefix, Scenario.Message, 5).ConfigureAwait(false),
			2).ConfigureAwait(false);
	}

	[TestMethod]
	public async Task CancellationAsync()
	{
		string serverPath = this.GenerateServerPath();

		using CancellationTokenSource serverCancellationSource = new();
		ServerSettings serverSettings = new(serverPath)
		{
			CancellationToken = serverCancellationSource.Token,
			CreateLogger = this.LoggerFactory.CreateLogger,
		};
		using MessageServer<string, int> server = new(
			async (value, cancel) =>
			{
				while (!cancel.IsCancellationRequested)
				{
					await Task.Delay(100, cancel).ConfigureAwait(false);
				}

				cancel.ThrowIfCancellationRequested();
				return value?.Length ?? 0;
			},
			serverSettings);
		server.Start();

		using MessageClient<string, int> client = new(serverPath, loggerFactory: this.LoggerFactory);
		using CancellationTokenSource clientCancellationSource = new(TimeSpan.FromSeconds(0.5));
		try
		{
			await client.SendAsync("Test", clientCancellationSource.Token).ConfigureAwait(false);
			Assert.Fail("Should not reach here.");
		}
		catch (TaskCanceledException ex)
		{
			// Sometimes .NET Framework throws this.
			// Should.ThrowAsync doesn't work with TaskCanceledException.
			// https://github.com/shouldly/shouldly/issues/831
			ex.ShouldBeOfType<TaskCanceledException>();
		}
		catch (OperationCanceledException ex)
		{
			// Core throws this, and Framework sometimes does.
			ex.ShouldBeOfType<OperationCanceledException>();
		}

#if NET8_0_OR_GREATER
		await serverCancellationSource.CancelAsync().ConfigureAwait(false);
#else
		serverCancellationSource.Cancel();
#endif
	}

	[TestMethod]
	public async Task ThrowExceptionAsync()
	{
		string serverPath = this.GenerateServerPath();

		const string ExceptionMessage = "Only even numbers are supported.";
		using MessageServer<int, string> server = new(
			async value =>
			{
				if (value % 2 == 1)
				{
					throw new ArgumentException(ExceptionMessage);
				}

				return await Task.FromResult(value.ToString()).ConfigureAwait(false);
			},
			serverPath,
			loggerFactory: this.LoggerFactory);
		server.Start();

		using MessageClient<int, string> client = new(serverPath, loggerFactory: this.LoggerFactory);
		for (int i = 0; i < 2; i++)
		{
			try
			{
				string response = await client.SendAsync(i).ConfigureAwait(false);
				i.ShouldBe(0);
				response.ShouldBe(i.ToString());
			}
			catch (ArgumentException ex)
			{
				i.ShouldBe(1);
				ex.Message.ShouldBe(ExceptionMessage);
			}
		}
	}

	[TestMethod]
	public async Task SecurityVariationsAsync()
	{
		await TestAsync(1, PipeClientSecurity.CurrentUserOnly, null).ConfigureAwait(false);
		await TestAsync(2, null, PipeServerSecurity.CurrentUserOnly).ConfigureAwait(false);
		await TestAsync(3, PipeClientSecurity.CurrentUserOnly, PipeServerSecurity.CurrentUserOnly).ConfigureAwait(false);

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			try
			{
				// This empty PipeSecurity instance doesn't grant any user access to the pipe (even the current user).
				PipeSecurity pipeSecurity = new();
				await TestAsync(4, PipeClientSecurity.CurrentUserOnly, new PipeServerSecurity(pipeSecurity)).ConfigureAwait(false);
				Assert.Fail("Client should not have access to connect to server.");
			}
			catch (UnauthorizedAccessException ex)
			{
				Assert.IsTrue(ex != null);
			}
		}

		async Task TestAsync(int code, PipeClientSecurity? clientSecurity, PipeServerSecurity? serverSecurity)
		{
			string serverPath = this.GenerateServerPath() + $".{code}";

			ServerSettings serverSettings = new(serverPath)
			{
				CreateLogger = this.LoggerFactory.CreateLogger,
				Security = serverSecurity,
			};
			using MessageServer<CodeName, string> server = new(
				async codeName => await Task.FromResult($"{codeName.Code}: {codeName.Name}").ConfigureAwait(false),
				serverSettings);
			server.Start();

			ClientSettings clientSettings = new(serverPath)
			{
				CreateLogger = this.LoggerFactory.CreateLogger,
				Security = clientSecurity,
			};
			using MessageClient<CodeName, string> client = new(clientSettings);

			string response = await client.SendAsync(new CodeName { Code = code, Name = "First" }).ConfigureAwait(false);
			response.ShouldBe($"{code}: First");
			response = await client.SendAsync(new CodeName { Code = 2 * code, Name = "Second" }).ConfigureAwait(false);
			response.ShouldBe($"{2 * code}: Second");
		}
	}

	#endregion

	#region Private Types

	private sealed class CodeName
	{
		#region Public Properties

		public int Code { get; set; }

		public string Name { get; set; } = string.Empty;

		#endregion
	}

	#endregion
}
