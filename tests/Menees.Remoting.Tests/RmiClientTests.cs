namespace Menees.Remoting;

[TestClass]
public class RmiClientTests : BaseTests
{
	#region Public Methods

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
		const string ServerPath = nameof(this.CreateProxy);

		using RmiClient<ITester> client = new(ServerPath, connectTimeout: TimeSpan.FromSeconds(2), loggerFactory: this.Loggers);
		ITester testerProxy = client.CreateProxy();
		testerProxy.ShouldNotBeNull();

		Tester tester = new();
		using RmiServer<ITester> server = new(tester, ServerPath, loggerFactory: this.Loggers);
		server.ReportUnhandledException = RmiServerTests.WriteUnhandledServerException;
		server.Start();

		const int TestId = 123;
		TestProxy(testerProxy, TestId, isSingleClient: true);
	}

	[TestMethod]
	public void ThrowException()
	{
		const string ServerPath = nameof(this.ThrowException);

		using RmiClient<ITester> client = new(ServerPath, loggerFactory: this.Loggers);
		ITester testerProxy = client.CreateProxy();
		testerProxy.ShouldNotBeNull();

		Tester tester = new();
		using RmiServer<ITester> server = new(tester, ServerPath, loggerFactory: this.Loggers);
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
		const string ServerPath = nameof(this.CustomSerializer);

		ClientSettings clientSettings = new(ServerPath)
		{
			ConnectTimeout = TimeSpan.FromSeconds(2),
			LoggerFactory = this.Loggers,
			Serializer = new TestSerializer(),
		};

		using RmiClient<ITester> client = new(clientSettings);
		ITester testerProxy = client.CreateProxy();
		testerProxy.ShouldNotBeNull();

		ServerSettings serverSettings = new(ServerPath)
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

	#endregion

	#region Internal Methods

	internal static void TestProxy(ITester testerProxy, int testId, bool isSingleClient)
	{
		testerProxy.TestId = testId;
		int actualTestId = testerProxy.TestId;

		// With multiple simultaneous clients, we can't guarantee that the value returned from the property
		// will be what we pushed in because another thread/client could have changed it.
		if (isSingleClient)
		{
			actualTestId.ShouldBe(testId);
		}

		testerProxy.Combine("A", "B").ShouldBe("AB");
		testerProxy.Combine("A", "B", "C").ShouldBe("ABC");

		Widget paper = testerProxy.CreateWidget("Paper", 0.01m, 85, 110);
		paper.Name.ShouldBe("Paper");
		paper.Cost.ShouldBe(0.01m);
		paper.Dimensions.ShouldBe(new[] { 85, 110 });

		paper = testerProxy.UpdateWidget(paper, "Fancy Paper", 0.02m, null);
		paper.Name.ShouldBe("Fancy Paper");
		paper.Cost.ShouldBe(0.02m);
		paper.Dimensions.ShouldBe(new[] { 85, 110 });
	}

	#endregion
}
