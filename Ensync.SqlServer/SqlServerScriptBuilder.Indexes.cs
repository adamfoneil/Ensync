using Ensync.Core.Abstract;
using Ensync.Core.Models;

using Index = Ensync.Core.Models.Index;

namespace Ensync.SqlServer;

public partial class SqlServerScriptBuilder
{
    private string IndexDefinition(DbObject @object)
    {
        var index = @object as Index ?? throw new Exception("Unexpected object type");
        var columnList = string.Join(", ", index.Columns.OrderBy(col => col.Order).Select(col => $"{FormatName(col.Name)} {((col.Direction == SortDirection.Ascending) ? "ASC" : "DESC")}"));
        return $"({columnList})";
    }

    private IEnumerable<(StatementPlacement, string)> CreateIndex(DbObject? parent, DbObject child)
    {
        var index = child as Index ?? throw new Exception("Unexpected object type");
        var statement = index.IndexType switch
        {
            IndexType.UniqueIndex => $"CREATE UNIQUE INDEX {FormatName(index)} ON {FormatName(parent!)} {IndexDefinition(index)}",
            IndexType.UniqueConstraint => $"ALTER TABLE {FormatName(parent!)} ADD CONSTRAINT {FormatName(index)} UNIQUE {IndexDefinition(index)}",
            IndexType.PrimaryKey => $"ALTER TABLE {FormatName(parent!)} ADD CONSTRAINT {FormatName(index)} PRIMARY KEY {IndexDefinition(index)}",
            IndexType.NonUnique => $"CREATE INDEX {FormatName(index)} ON {FormatName(parent!)} {IndexDefinition(index)}",
            _ => throw new NotSupportedException($"Unrecognized index type {index.IndexType}")
        };

        yield return (StatementPlacement.Immediate, statement);
    }

    private IEnumerable<(StatementPlacement, string)> AlterIndex(DbObject? parent, DbObject child)
    {
        throw new NotImplementedException();
    }

    private IEnumerable<(StatementPlacement, string)> DropIndex(DbObject? parent, DbObject child)
    {
        var index = child as Index ?? throw new Exception("Unexpected object type");

        var statement = index.IndexType switch
        {
            IndexType.UniqueIndex or IndexType.NonUnique => $"DROP INDEX {FormatName(index)} ON {FormatName(parent!)}",
            IndexType.UniqueConstraint or IndexType.PrimaryKey => $"ALTER TABLE {FormatName(parent!)} DROP CONSTRAINT {FormatName(child)}",
            _ => throw new NotSupportedException($"Unrecognized index type {index.IndexType}")
        };

        yield return (StatementPlacement.Immediate, statement);
    }
}
