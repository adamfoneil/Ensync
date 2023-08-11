namespace Ensync.Core;


public class Configuration
{
    public required string AssemblyPath { get; set; }

    public Target[] DatabaseTargets { get; set; } = Array.Empty<Target>();

    public class Target
    {
        public required string Name { get; init; }
        public required string Type { get; init; }
        public required string Expression { get; init; }
    }
}
