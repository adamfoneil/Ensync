namespace Ensync.Dotnet.Extensions;

public static class StringHelper
{
	public static bool TryParseColumnReference(string expression, out (string TableName, string ColumnName) result)
	{
		try
		{
			result = ParseColumnReference(expression);
			return true;
		}
		catch
		{
			result = (default!, default!);
			return false;
		}
	}

	public static (string TableName, string ColumnName) ParseColumnReference(string expression)
	{
		var parts = expression.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length > 3) throw new Exception("Can't have more than three parts to a column reference");
		return (string.Join(".", parts[..^1]), parts[^1]);
	}
}
