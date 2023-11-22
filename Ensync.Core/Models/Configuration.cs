namespace Ensync.Core.Models;

public class Configuration
{
	public required string AssemblyPath { get; set; }
	public Target[] DatabaseTargets { get; set; } = Array.Empty<Target>();

	public class Target
	{
		public required string Name { get; init; }
		public required string Type { get; init; }
		public required string ConnectionString { get; init; }
		public bool IsProduction { get; init; } // if true, then CLI prompts you to type the database name
	}
}
