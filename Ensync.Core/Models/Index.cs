using Ensync.Core.Abstract;

namespace Ensync.Core.Models;

public enum SortDirection
{
    Ascending,
    Descending
}

public enum IndexType
{
    PrimaryKey = 1,
    UniqueIndex = 2,
    UniqueConstraint = 3,
    NonUnique = 4
}

public class Index : DbObject
{
    public override DbObjectType Type => DbObjectType.Index;
    public IndexType IndexType { get; init; }
    public required IEnumerable<Column> Columns { get; init; }

    public class Column
    {
        public required string Name { get; init; }
        public SortDirection Direction { get; init; }
    }
}
