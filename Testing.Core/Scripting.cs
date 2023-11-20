using Ensync.Core.Extensions;
using Ensync.SqlServer;
using SqlServer.LocalDb;

namespace Testing.Core;

[TestClass]
public class Scripting
{
    [ClassInitialize]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Required by Test Framework")]
    public static void Startup(TestContext testContext)
    {
        using var cn = LocalDb.GetConnection(Diffs.DbName);
    }

    [TestMethod]
    public async Task CreateTables()
    {
        var source = EmployeeSchema.Instance;

        var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(Diffs.DbName));
        var actions = (await source.CreateAsync(scriptBuilder)).ToArray();
        var script = actions.ToSqlScript("\r\nGO\r\n", scriptBuilder);
        Assert.IsTrue(script.Equals(
@"CREATE TABLE [dbo].[Employee] (
   [Id] int identity(1,1) NOT NULL,
   [FirstName] nvarchar(50) NOT NULL,
   [LastName] nvarchar(50) NOT NULL,
   [EmployeeTypeId] int NOT NULL
)
GO
ALTER TABLE [dbo].[Employee] ADD CONSTRAINT [PK_Employee] PRIMARY KEY ([Id] ASC)
GO
CREATE TABLE [dbo].[EmployeeType] (
   [Id] int identity(1,1) NOT NULL,
   [Name] nvarchar(50) NOT NULL
)
GO
ALTER TABLE [dbo].[Employee] ADD CONSTRAINT [FK_Employee_EmployeeType] FOREIGN KEY ([EmployeeTypeId]) REFERENCES [dbo].[EmployeeType] ([Id]) ON DELETE CASCADE"));
    }
}
