using Ensync.Core.Abstract;

namespace Ensync.Core.Models;

public class Table : DbObject
{
    public override DbObjectType Type => DbObjectType.Table;

    public IEnumerable<Column> Columns { get; init; } = Enumerable.Empty<Column>();
    public IEnumerable<Index> Indexes { get; init; } = Enumerable.Empty<Index>();
    public IEnumerable<ForeignKey> ForeignKeys { get; init; } = Enumerable.Empty<ForeignKey>();
    public IEnumerable<CheckConstraint> CheckConstraints { get; init; } = Enumerable.Empty<CheckConstraint>();    

    public long RowCount { get; init; }
    public bool HasData => RowCount > 0;

    public override IEnumerable<(DbObject? Parent, DbObject Child)> GetDependencies(Schema schema) =>
        schema.Tables
           .SelectMany(
                t => t.ForeignKeys.Where(fk => fk.ReferencedTable.Equals(this)),
                (t, fk) => ((DbObject?)t, (DbObject)fk));
}
