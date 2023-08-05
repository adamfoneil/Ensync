namespace Ensync.Core.Models;

public class Schema
{
    public HashSet<Table> Tables { get; set; } = new();

    public IEnumerable<ScriptAction> Compare(Schema schema)
    {
        ArgumentNullException.ThrowIfNull(nameof(schema));

        throw new NotImplementedException();
    }
}
