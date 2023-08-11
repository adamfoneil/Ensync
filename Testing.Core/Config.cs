﻿using Ensync.Core;

namespace Testing.Core;

public class Config
{
    [TestMethod]
    public void DefaultConfig()
    {
        var config = new Configuration()
        {
            AssemblyPath = "./bin/Debug/Entities.dll",
            DatabaseTargets = new[]
            {
                new Configuration.Target()
                {
                    Name = "Local",
                    Type = "SqlServer",
                    Expression = "Server=(localdb)\\mssqllocaldb;Database=Hello;Integrated Security=true"
                },
                new Configuration.Target()
                {
                    Name = "QA",
                    Type = "SqlServer",
                    Expression = "@ConnectionStrings:QA"
                }
            }
        };
        
    }
}
