using Ensync.Core.Abstract;

namespace Ensync.Core.Models;

public class Schema : DbObject
{
    public override ObjectType Type => ObjectType.Schema;

    public HashSet<Table> Tables { get; init; } = new();
}
