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
    public override ObjectType Type => ObjectType.Index;
    public IndexType IndexType { get; init; }
    public SortDirection SortDirection { get; init; }
    public HashSet<string> Columns { get; set; } = new HashSet<string>();
}
