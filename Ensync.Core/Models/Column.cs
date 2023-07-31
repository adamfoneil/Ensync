using Ensync.Core.Abstract;

namespace Ensync.Core.Models;

public class Column : DbObject
{
    public override ObjectType Type => ObjectType.Column;
    public string DataType { get; init; } = default!;
    public bool Nullable { get; init; }
}
