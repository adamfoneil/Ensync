using Ensync.Core.Abstract;

namespace Ensync.Core.DbObjects;

public class Placeholder : DbObject
{
	private readonly DbObjectType _type;

	public Placeholder(DbObjectType type, string name)
	{
		_type = type;
		Name = name;
	}

	public override DbObjectType Type => _type;
}
