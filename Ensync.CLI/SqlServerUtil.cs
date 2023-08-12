using Microsoft.Data.SqlClient;

namespace Ensync.CLI;

internal static class SqlServerUtil
{
	internal static bool DatabaseExists(SqlConnection cn, string databaseName)
	{
		using var cmd = new SqlCommand("SELECT 1 FROM [sys].[databases] WHERE [Name]=@name", cn);		
		cmd.Parameters.AddWithValue("name", databaseName);
		var result = cmd.ExecuteScalar();
		return result?.Equals(1) ?? false;		
	}

	internal static void Execute(SqlConnection cn, string statement)
	{
		using var cmd = new SqlCommand(statement, cn);		
		cmd.ExecuteNonQuery();		
	}
}
