using Ensync.Core;
using Ensync.Core.DbObjects;
using Index = Ensync.Core.DbObjects.Index;

namespace Testing.Core;

internal static class EmployeeSchema
{
	public static Table EmployeeTypeTable
	{
		get
		{
			return new()
			{
				Name = "dbo.EmployeeType",
				Columns = new Column[]
				{
					new() { Name = "Id", DataType = "int identity(1,1)", IsNullable = false },
					new() { Name = "Name", DataType = "nvarchar(50)", IsNullable = false }
				}
			};
		}
	}

	public static Table EmployeeTable
	{
		get
		{
			return new()
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
				}
			};
		}
	}

	public static Schema Instance
	{
		get
		{
			var employeeTypeTable = EmployeeTypeTable;
			var employeeTable = EmployeeTable;


			return new Schema()
			{
				Tables = new Table[]
				{
					employeeTable,
					employeeTypeTable
				},
				ForeignKeys = new ForeignKey[]
				{
					new()
					{
						Parent = employeeTable,
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
		}
	}
}
