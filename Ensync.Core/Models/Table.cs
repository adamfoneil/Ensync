using Ensync.Core.Abstract;

namespace Ensync.Core.Models;

public class Table : DbObject
{
    public override ObjectType Type => ObjectType.Table;

    public HashSet<Column> Columns { get; init; } = new();
    public HashSet<Index> Indexes { get; init; } = new();
}
