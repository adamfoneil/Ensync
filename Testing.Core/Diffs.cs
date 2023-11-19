using Ensync.Core;
using Ensync.Core.Abstract;
using Ensync.Core.DbObjects;
using Ensync.Core.Extensions;
using Ensync.SqlServer;
using SqlServer.LocalDb;
using Index = Ensync.Core.DbObjects.Index;

namespace Testing.Core;

[TestClass]
public class Diffs
{
	public const string DbName = "EnsyncDemo";

    [ClassInitialize]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Required by Test Framework")]
	public static void Startup(TestContext testContext)
	{
		using var cn = LocalDb.GetConnection(DbName);
	}

	[TestMethod]
	public void DropReferencedTable()
	{
		var parent = new Table()
		{
			Name = "dbo.Parent",
			Columns = new[]
			{
				new Column() { Name = "Id"}
			}.ToHashSet()
		};

		var child = new Table()
		{
			Name = "dbo.Child",
			Columns = new[]
			{
				new Column() { Name = "ParentId" }
			}.ToHashSet()
		};

		var schema = new Schema()
		{
			Tables = new[] { parent, child }.ToHashSet(),
			ForeignKeys = new[]
			{
				new ForeignKey()
				{
					Name = "FK_Child_Parent",
					Parent = child,
					ReferencedTable = parent,
					Columns = new ForeignKey.Column[]
					{
						new() { ReferencedName = "Id", ReferencingName = "ParentId" }
					}
				}
			}
		};

		var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
		scriptBuilder.SetMetadata(new()
		{
			TableNames = new[] { "dbo.Parent" }.ToHashSet(),
			ForeignKeyNames = new[] { "FK_Child_Parent" }.ToHashSet()
		});
		var statements = scriptBuilder.GetScript(ScriptActionType.Drop, schema, null, parent, []);
		Assert.IsTrue(statements.Select(item => item.Item1).SequenceEqual(new[]
		{
			"ALTER TABLE [dbo].[Child] DROP CONSTRAINT [FK_Child_Parent]",
			"DROP TABLE [dbo].[Parent]"
		}));
	}

	[TestMethod]
	public async Task AddColumn()
	{
		var columns = new Column[]
		{
			new() { Name = "Column1", DataType = "nvarchar(50)" },
			new() { Name = "Column2", DataType = "nvarchar(50)" }
		};

		var sourceTable = new Table() { Name = "dbo.Whatever", Columns = columns.Concat(new[] { new Column() { Name = "Column3", DataType = "bit" } }) };
		var targetTable = new Table() { Name = "dbo.Whatever", Columns = columns };

		var sourceSchema = new Schema() { Tables = new[] { sourceTable } };
		var targetSchema = new Schema() { Tables = new[] { targetTable } };

		var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
		var script = await sourceSchema.CompareAsync(targetSchema, scriptBuilder);
		var statements = script.ToSqlStatements(scriptBuilder);
		Assert.IsTrue(statements.SequenceEqual(new[] { "ALTER TABLE [dbo].[Whatever] ADD [Column3] bit NOT NULL" }));
	}

	[TestMethod]
	public async Task DropColumn()
	{
		var columns = new Column[]
		{
			new() { Name = "Column1", DataType = "nvarchar(50)" },
			new() { Name = "Column2", DataType = "nvarchar(50)" }
		};

		var sourceTable = new Table() { Name = "dbo.Whatever", Columns = columns };
		var targetTable = new Table() { Name = "dbo.Whatever", Columns = columns.Concat(new[] { new Column() { Name = "Column3", DataType = "bit" } }) };

		var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
		var script = await sourceTable.CompareAsync(targetTable, scriptBuilder);
		var statements = script.ToSqlStatements(scriptBuilder);
		Assert.IsTrue(statements.SequenceEqual(new[] { "ALTER TABLE [dbo].[Whatever] DROP COLUMN [Column3]" }));
	}

	[TestMethod]
	public async Task DropEmptyTable()
	{
		var targetTable = new Table()
		{
			Name = "dbo.Whatever"
		};

		var sourceSchema = new Schema();
		var targetSchema = new Schema() { Tables = new Table[] { targetTable } };

		var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
		scriptBuilder.SetMetadata(new()
		{
			TableNames = new[] { "dbo.Whatever" }.ToHashSet()
		});
		var script = await sourceSchema.CompareAsync(targetSchema, scriptBuilder);
		Assert.IsTrue(script.ToSqlStatements(scriptBuilder).SequenceEqual(new[]
		{
			"DROP TABLE [dbo].[Whatever]"
		}));
	}

	[TestMethod]
	public async Task DropColumnWithIndex()
	{
		var sourceColumns = new Column[]
		{
			new() { Name = "Column2", DataType = "nvarchar(50)" }
		};

		var targetColumns = sourceColumns.Concat(new Column[]
		{
			new() { Name = "Column1", DataType = "nvarchar(50)" }
		});

		var index = new Index()
		{
			Name = "IX_Whatever_Column1",
			IndexType = IndexType.NonUnique,
			Columns = new Index.Column[]
			{
				new() { Name = "Column1" }
			}
		};

		var sourceTable = new Table() { Name = "dbo.Whatever", Columns = sourceColumns };
		var targetTable = new Table() { Name = "dbo.Whatever", Columns = targetColumns, Indexes = new Index[] { index } };

		var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
		scriptBuilder.SetMetadata(new()
		{
			IndexNames = new[] { "IX_Whatever_Column1" }.ToHashSet()
		});
		var script = await sourceTable.CompareAsync(targetTable, scriptBuilder);
		var statements = script.ToSqlStatements(scriptBuilder);
		Assert.IsTrue(statements.SequenceEqual(new[]
		{
			"DROP INDEX [IX_Whatever_Column1] ON [dbo].[Whatever]",
			"ALTER TABLE [dbo].[Whatever] DROP COLUMN [Column1]"
		}));
	}

	[TestMethod]
	public async Task AlterColumn()
	{
		var sourceTable = new Table()
		{
			Name = "dbo.Whatever",
			Columns = new Column[]
			{
				new() { Name = "Column1", DataType = "nvarchar(50)", IsNullable = false },
				new() { Name = "Column2", DataType = "datetime", IsNullable = true },
				new() { Name = "Column3", DataType = "bit", IsNullable = false }
			}
		};

		var targetTable = new Table()
		{
			Name = "dbo.Whatever",
			Columns = new Column[]
			{
				new() { Name = "Column1", DataType = "nvarchar(40)", IsNullable = false },
				new() { Name = "Column2", DataType = "datetime", IsNullable = false },
				new() { Name = "Column3", DataType = "bit", IsNullable = false }
			}
		};

		var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
		var script = await sourceTable.CompareAsync(targetTable, scriptBuilder);
		Assert.IsTrue(script.ToSqlStatements(scriptBuilder).SequenceEqual(new[]
		{
			"ALTER TABLE [dbo].[Whatever] ALTER COLUMN [Column1] nvarchar(50) NOT NULL",
			"ALTER TABLE [dbo].[Whatever] ALTER COLUMN [Column2] datetime NULL"
		}));
	}

	[TestMethod]
	public async Task AlterColumnWithIndex()
	{
		var sourceTable = new Table()
		{
			Name = "dbo.Whatever",
			Columns = new Column[]
			{
				new() { Name = "Column1", DataType = "nvarchar(50)", IsNullable = false },
			},
			Indexes = new Index[]
			{
				new()
				{
					Name = "IX_Whatever_Column1",
					IndexType = IndexType.NonUnique,
					Columns = new Index.Column[]
					{
						new() { Name = "Column1" }
					}
				}
			}
		};

		var targetTable = new Table()
		{
			Name = "dbo.Whatever",
			Columns = new Column[]
			{
				new() { Name = "Column1", DataType = "nvarchar(40)", IsNullable = false },
			},
			Indexes = new Index[]
			{
				new()
				{
					Name = "IX_Whatever_Column1",
					IndexType = IndexType.NonUnique,
					Columns = new Index.Column[]
					{
						new() { Name = "Column1" }
					}
				}
			}
		};

		var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
		scriptBuilder.SetMetadata(new DatabaseMetadata()
		{
			IndexNames = ["IX_Whatever_Column1"]
		});
		var script = await sourceTable.CompareAsync(targetTable, scriptBuilder);
		Assert.IsTrue(script.ToSqlStatements(scriptBuilder).SequenceEqual(new[]
		{
			"DROP INDEX [IX_Whatever_Column1] ON [dbo].[Whatever]",
			"ALTER TABLE [dbo].[Whatever] ALTER COLUMN [Column1] nvarchar(50) NOT NULL",
			"CREATE INDEX [IX_Whatever_Column1] ON [dbo].[Whatever] ([Column1] ASC)",
		}));
	}

	[TestMethod]
	public async Task AddIndex()
	{
		var sourceTable = new Table()
		{
			Name = "dbo.Whatever",
			Indexes = new Index[]
			{
				new() { Name = "IX_Hello", IndexType = IndexType.NonUnique, Columns = new Index.Column[]
				{
					new() { Name = "Column1" },
					new() { Name = "Column2" }
				}}
			}
		};

		var targetTable = new Table()
		{
			Name = "dbo.Whatever"
		};

		var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
		var script = await sourceTable.CompareAsync(targetTable, scriptBuilder);
		Assert.IsTrue(script.ToSqlStatements(scriptBuilder).SequenceEqual(new[]
		{
			"CREATE INDEX [IX_Hello] ON [dbo].[Whatever] ([Column1] ASC, [Column2] ASC)"
		}));
	}

	[TestMethod]
	public async Task DropIndex()
	{
		var sourceTable = new Table()
		{
			Name = "dbo.Whatever"
		};

		var targetTable = new Table()
		{
			Name = "dbo.Whatever",
			Indexes = new Index[]
			{
				new() { Name = "IX_Hello", IndexType = IndexType.NonUnique, Columns = new Index.Column[]
				{
					new() { Name = "Column1" },
					new() { Name = "Column2" }
				}}
			}
		};

		var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
		scriptBuilder.SetMetadata(new()
		{
			IndexNames = ["IX_Hello"]
		});
		var script = await sourceTable.CompareAsync(targetTable, scriptBuilder);
		Assert.IsTrue(script.ToSqlStatements(scriptBuilder).SequenceEqual(new[]
		{
			"DROP INDEX [IX_Hello] ON [dbo].[Whatever]"
		}));
	}

	[TestMethod]
	public async Task DropForeignKey()
	{
		var source = EmployeeSchema.Instance;
		source.ForeignKeys = Enumerable.Empty<ForeignKey>();
		var target = EmployeeSchema.Instance;

		var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
		scriptBuilder.SetMetadata(new()
		{
			ForeignKeyNames = new[] { "FK_Employee_EmployeeType" }.ToHashSet()
		});
		var script = await source.CompareAsync(target, scriptBuilder);
		Assert.IsTrue(script.ToSqlStatements(scriptBuilder).SequenceEqual(new[]
		{
			"ALTER TABLE [dbo].[Employee] DROP CONSTRAINT [FK_Employee_EmployeeType]"
		}));

	}

	[TestMethod]
	public async Task AddPKColumn()
	{
		var sourceTable = new Table()
		{
			Name = "dbo.Whatever",
			Indexes = new Index[]
			{
				new() { Name = "PK_Whatever", IndexType = IndexType.PrimaryKey, Columns = new Index.Column[]
				{
					new() { Name = "Column1" },
					new() { Name = "Column2" }
				}}
			}
		};

		var targetTable = new Table()
		{
			Name = "dbo.Whatever",
			Indexes = new Index[]
			{
				new() { Name = "PK_Whatever", IndexType = IndexType.PrimaryKey, Columns = new Index.Column[]
				{
					new() { Name = "Column1" }
				}}
			}
		};

		var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
		scriptBuilder.SetMetadata(new DatabaseMetadata()
		{
			TableNames = new[] { "dbo.Whatever" }.ToHashSet(),
			IndexNames = new[] { "PK_Hello" }.ToHashSet()
		});

		var script = await sourceTable.CompareAsync(targetTable, scriptBuilder);
		Assert.IsTrue(script.ToSqlStatements(scriptBuilder).SequenceEqual(new[]
		{
			"ALTER TABLE [dbo].[Whatever] DROP CONSTRAINT [PK_Whatever]",
			"ALTER TABLE [dbo].[Whatever] ADD CONSTRAINT [PK_Whatever] PRIMARY KEY ([Column1] ASC, [Column2] ASC)"
		}));
	}
}
