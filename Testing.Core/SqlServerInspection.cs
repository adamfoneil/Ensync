using Ensync.SqlServer;
using Microsoft.SqlServer.Dac;
using SqlServer.LocalDb;
using System.Reflection;

namespace Testing.Core;

[TestClass]
public class SqlServerInspection
{
    [TestMethod]
    [DataRow("Ginseng8.dacpac")]    
    [DataRow("UserVoice.dacpac")]
    public async Task InspectSchema(string resourceName)
    {
        var dbName = resourceName.Replace('.', '-');
        var connectionString = LocalDb.GetConnectionString(dbName);

        using var stream = GetResource($"Resources.{resourceName}");
        using var package = DacPackage.Load(stream);
        var services = new DacServices(connectionString);
        services.Deploy(package, dbName, true);

        var inspector = new SqlServerSchemaInspector(connectionString);
        var schema = await inspector.GetSchemaAsync();

        var script = await schema.CreateScriptAsync(new SqlServerScriptBuilder(connectionString), "\r\nGO\r\n");

        LocalDb.TryDropDatabaseIfExists(dbName, out _);
    }

    private static Stream GetResource(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream($"Testing.Core.{name}") ?? throw new Exception($"Resource {name} not found");
}
