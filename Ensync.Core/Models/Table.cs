using Ensync.Core.Abstract;

namespace Ensync.Core.Models;

public class Table : DbObject
{
    public override DbObjectType Type => DbObjectType.Table;

    public HashSet<Column> Columns { get; init; } = new();
    public HashSet<Index> Indexes { get; init; } = new();

    public long RowCount { get; init; }
}
