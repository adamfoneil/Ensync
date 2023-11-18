using Ensync.Core.Models;

namespace Testing.Core;

public class Config
{
	[TestMethod]
	public void DefaultConfig()
	{
		var config = new Configuration()
		{
			AssemblyPath = "./bin/Debug/Entities.dll",
			DatabaseTargets =
            [
                new Configuration.Target()
				{
					Name = "Local",
					Type = "SqlServer",
					ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=Hello;Integrated Security=true"
				},
				new Configuration.Target()
				{
					Name = "QA",
					Type = "SqlServer",
					ConnectionString = "@ConnectionStrings:QA"
				}
			]
		};

	}
}
