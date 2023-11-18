using Ensync.Core.Extensions;
using System.Reflection;

namespace Testing.Core;

[TestClass]
public class JsonParse
{
	[TestMethod]
	public void FindConnectionString()
	{
		var appSettingsJson = new StreamReader(GetResource("Resources.appsettings.json")).ReadToEnd();
		var connectionInfo = JsonHelper.FindFirstConnectionString(appSettingsJson);
		Assert.IsTrue(connectionInfo.ConnectionString!.Equals("Server=(localdb)\\mssqllocaldb;Database=LiteInvoice;Integrated Security=true"));
	}

	private static Stream GetResource(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream($"Testing.Core.{name}") ?? throw new Exception($"Resource {name} not found");
}
