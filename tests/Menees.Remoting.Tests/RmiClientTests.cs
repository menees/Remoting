namespace Menees.Remoting;

[TestClass]
public class RmiClientTests
{
	#region Public Methods

	[TestMethod]
	public void RmiClientTest()
	{
		using (RmiClient<IServerHost> client = new(this.GetType().FullName!))
		{
			client.ConnectTimeout.ShouldBe(RmiClient<IServerHost>.DefaultConnectTimeout);
		}

		using (RmiClient<IServerHost> client = new(this.GetType().FullName!, connectTimeout: TimeSpan.FromSeconds(1)))
		{
			client.ConnectTimeout.ShouldBe(TimeSpan.FromSeconds(1));
		}
	}

	[TestMethod]
	public void CreateProxyTest()
	{
		string serverPath = nameof(this.CreateProxyTest);

		using RmiClient<ITester> client = new(serverPath, connectTimeout: TimeSpan.FromSeconds(2));
		ITester testerProxy = client.CreateProxy();
		testerProxy.ShouldNotBeNull();

		Tester tester = new();
		using RmiServer<ITester> server = new(serverPath, tester);
		server.ReportUnhandledException = RmiServerTests.WriteUnhandledServerException;
		server.Start();

		const int TestId = 123;
		testerProxy.TestId = TestId;
		int actualTestId = testerProxy.TestId;
		actualTestId.ShouldBe(TestId);

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

	[TestMethod]
	public void ThrowException()
	{
		string serverPath = nameof(this.ThrowException);

		using RmiClient<ITester> client = new(serverPath);
		ITester testerProxy = client.CreateProxy();
		testerProxy.ShouldNotBeNull();

		Tester tester = new();
		using RmiServer<ITester> server = new(serverPath, tester);
		server.ReportUnhandledException = RmiServerTests.WriteUnhandledServerException;
		server.Start();

		testerProxy.ThrowExceptionIfOdd(20).ShouldBe(10);
		ArgumentOutOfRangeException ex = Should.Throw<ArgumentOutOfRangeException>(() => testerProxy.ThrowExceptionIfOdd(19));
		ex.Message.ShouldStartWith("Only even numbers are supported.");

		testerProxy.Combine("Proxy", " still ", "works.").ShouldBe("Proxy still works.");
	}

	#endregion
}
