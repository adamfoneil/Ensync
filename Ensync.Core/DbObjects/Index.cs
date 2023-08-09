using Ensync.Core.Abstract;

namespace Ensync.Core.DbObjects;

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
    public required IEnumerable<Column> Columns { get; set; }
    public int InternalId { get; init; }
    public bool IsClustered { get; init; }

    public class Column
    {
        public required string Name { get; init; }
        public SortDirection Direction { get; init; }
        public int Order { get; init; }
    }

    public override IEnumerable<(DbObject? Parent, DbObject Child)> GetDependencies(Schema schema) =>
        (IndexType == IndexType.PrimaryKey || IndexType == IndexType.UniqueConstraint) ?
            schema.ForeignKeys.Where(fkInfo => fkInfo.ForeignKey.ReferencedTable.Equals(Parent))
                .Select(fkInfo => ((DbObject?)fkInfo.Parent, (DbObject)fkInfo.ForeignKey)) :
            Enumerable.Empty<(DbObject?, DbObject)>();
}