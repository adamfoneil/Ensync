using Ensync.Core.Abstract;

namespace Ensync.Core.DbObjects;

public class CheckConstraint : DbObject
{
	public override DbObjectType Type => DbObjectType.CheckConstraint;

	public string Expression { get; init; } = default!;
}
