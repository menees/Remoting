namespace Menees.Remoting;

[TestClass]
public class RmiClientTests : BaseTests
{
	#region Public Methods

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

	[TestMethod]
	public void Constructor()
	{
		using (RmiClient<IServerHost> client = new(this.GetType().FullName!, loggerFactory: this.Loggers))
		{
			client.ConnectTimeout.ShouldBe(ClientSettings.DefaultConnectTimeout);
		}

		using (RmiClient<IServerHost> client = new(this.GetType().FullName!, connectTimeout: TimeSpan.FromSeconds(1), loggerFactory: this.Loggers))
		{
			client.ConnectTimeout.ShouldBe(TimeSpan.FromSeconds(1));
		}
	}

	[TestMethod]
	public void CreateProxy()
	{
		string serverPath = this.GenerateServerPath();

		using RmiClient<ITester> client = new(serverPath, connectTimeout: TimeSpan.FromSeconds(2), loggerFactory: this.Loggers);
		ITester testerProxy = client.CreateProxy();
		testerProxy.ShouldNotBeNull();

		Tester tester = new();
		using RmiServer<ITester> server = new(tester, serverPath, loggerFactory: this.Loggers);
		server.ReportUnhandledException = RmiServerTests.WriteUnhandledServerException;
		server.Start();

		const int TestId = 123;
		TestProxy(testerProxy, TestId, isSingleClient: true);
	}

	[TestMethod]
	public void ThrowException()
	{
		string serverPath = this.GenerateServerPath();

		using RmiClient<ITester> client = new(serverPath, loggerFactory: this.Loggers);
		ITester testerProxy = client.CreateProxy();
		testerProxy.ShouldNotBeNull();

		Tester tester = new();
		using RmiServer<ITester> server = new(tester, serverPath, loggerFactory: this.Loggers);
		server.ReportUnhandledException = RmiServerTests.WriteUnhandledServerException;
		server.Start();

		testerProxy.ThrowExceptionIfOdd(20).ShouldBe(10);
		ArgumentOutOfRangeException ex = Should.Throw<ArgumentOutOfRangeException>(() => testerProxy.ThrowExceptionIfOdd(19));
		ex.Message.ShouldStartWith("Only even numbers are supported.");

		testerProxy.Combine("Proxy", " still ", "works.").ShouldBe("Proxy still works.");
	}

	[TestMethod]
	public void CustomSerializer()
	{
		string serverPath = this.GenerateServerPath();

		ClientSettings clientSettings = new(serverPath)
		{
			ConnectTimeout = TimeSpan.FromSeconds(2),
			LoggerFactory = this.Loggers,
			Serializer = new TestSerializer(),
		};

		using RmiClient<ITester> client = new(clientSettings);
		ITester testerProxy = client.CreateProxy();
		testerProxy.ShouldNotBeNull();

		ServerSettings serverSettings = new(serverPath)
		{
			LoggerFactory = this.Loggers,
			Serializer = new TestSerializer(),
		};
		Tester tester = new();
		using RmiServer<ITester> server = new(tester, serverSettings);
		server.ReportUnhandledException = RmiServerTests.WriteUnhandledServerException;
		server.Start();

		const int TestId = 456;
		TestProxy(testerProxy, TestId, isSingleClient: true);
	}

	[TestMethod]
	public async Task CrossProcessServerAndClientAsync()
	{
		await this.TestCrossProcessServerAsync(
			this.GenerateServerPathPrefix(),
			async prefix => await TestCrossProcessClientAsync(10, prefix, Scenario.Calculator, 10).ConfigureAwait(false),
			8,
			rmiServiceType: typeof(Calculator)).ConfigureAwait(false);
	}

	#endregion
}
