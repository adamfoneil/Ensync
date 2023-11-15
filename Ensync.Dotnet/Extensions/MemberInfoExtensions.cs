using System.Reflection;

namespace Ensync.Dotnet7.Extensions;

internal static class MemberInfoExtensions
{
	public static bool HasAttribute<T>(this MemberInfo memberInfo, out T attribute) where T : Attribute
	{
		ArgumentNullException.ThrowIfNull(memberInfo, nameof(memberInfo));

		attribute = memberInfo.GetCustomAttribute<T>()!;

		if (attribute is not null) return true;

		return false;
	}

	public static bool HasAttributeWhere<T>(this MemberInfo memberInfo, Func<T, bool> predicate) where T : Attribute
	{
		var attr = memberInfo.GetCustomAttribute<T>();
		if (attr is not null)
		{
			return predicate(attr);
		}

		return false;
	}
}
