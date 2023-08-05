﻿using Ensync.Core;
using Ensync.Core.Models;
using SqlServer.LocalDb;
using Index = Ensync.Core.Models.Index;

namespace Testing.Core;

[TestClass]
public class Scripting
{
    [ClassInitialize]
    public static void Startup(TestContext testContext)
    {
        using var cn = LocalDb.GetConnection(Tables.DbName);
    }

    [TestMethod]
    public async Task CreateTables()
    {
        Table employeeTypeTable = new()
        {
            Name = "dbo.EmployeeType",
            Columns = new Column[]
            {
                new() { Name = "Id", DataType = "int identity(1,1)", IsNullable = false },
                new() { Name = "Name", DataType = "nvarchar(50)", IsNullable = false }
            }
        };

        Table employeeTable = new()
        {
            Name = "dbo.Employee",
            Columns = new Column[]
            {
                new() { Name = "Id", DataType = "int identity(1,1)", IsNullable = false },
                new() { Name = "FirstName", DataType = "nvarchar(50)", IsNullable = false  },
                new() { Name = "LastName", DataType = "nvarchar(50)", IsNullable = false },
                new() { Name = "EmployeeTypeId", DataType = "int", IsNullable = false }
            },
            Indexes = new Index[]
            {
                new()
                {
                    Name = "PK_Employee",
                    IndexType = IndexType.PrimaryKey,
                    Columns = new Index.Column[]
                    {
                        new() { Name = "Id" }
                    }
                }
            },
            ForeignKeys = new ForeignKey[]
            {
                new()
                {
                    ReferencedTable = employeeTypeTable,
                    Name = "FK_Employee_EmployeeType",
                    CascadeDelete = true,
                    Columns = new ForeignKey.Column[]
                    {
                        new() { ReferencedName = "Id", ReferencingName = "EmployeeTypeId" }
                    }
                }
            }
        };

        var source = new Schema()
        {
            Tables = new Table[]
            {
                employeeTable,
                employeeTypeTable
            }
        };

        var target = new Schema();

        var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(Tables.DbName));
        var script = (await source.CompareAsync(target, scriptBuilder)).ToArray(); 
    }
}