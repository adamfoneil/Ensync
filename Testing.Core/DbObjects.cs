using Ensync.Core;
using System.Reflection;
using System.Text.Json;

namespace Testing.Core;

[TestClass]
public class DbObjects
{
	[TestMethod]
	[Obsolete("This ended up not working")]
	public void IgnoreScriptActions()
	{
		var scriptActions = ParseJsonResource<List<ScriptActionKey>>("Resources.Json.ScriptActions.json");
		var ignoreActions = ParseJsonResource<List<ScriptActionKey>>("Resources.Json.IgnoreList.json");
	}

	private static Stream GetResource(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream($"Testing.Core.{name}") ?? throw new Exception($"Resource {name} not found");

	private static string GetResourceString(string name)
	{
		using var stream = GetResource(name);
		return new StreamReader(stream).ReadToEnd();
	}

	private static T? ParseJsonResource<T>(string resource)
	{
		var json = GetResourceString(resource);
		return JsonSerializer.Deserialize<T>(json);
	}
}