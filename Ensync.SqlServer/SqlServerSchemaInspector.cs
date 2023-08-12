using Dapper;
using Ensync.Core.Abstract;
using Ensync.Core.DbObjects;
using Ensync.SqlServer.Internal;
using Microsoft.Data.SqlClient;

using Index = Ensync.Core.DbObjects.Index;

namespace Ensync.SqlServer;

public class SqlServerSchemaInspector : SchemaInspector
{
	private readonly string _connectionString;

	public SqlServerSchemaInspector(string connectionString)
	{
		_connectionString = connectionString;
	}

	protected override async Task<(IEnumerable<Table> Tables, IEnumerable<ForeignKey> ForeignKeys)> GetDbObjectsAsync()
	{
		using var cn = new SqlConnection(_connectionString);

		IEnumerable<Table> tables = await GetTablesAsync(cn);
		IEnumerable<Column> columns = await GetColumnAsync(cn);
		IEnumerable<CheckConstraint> checks = await GetCheckConstraintsAsync(cn);
		IEnumerable<Index> indexes = await GetIndexesAsync(cn);
		IEnumerable<IndexColumnResult> indexCols = await GetIndexColumnsAsync(cn);
		IEnumerable<ForeignKey> foreignKeys = await GetForeignKeysAsync(cn, tables);

		var columnLookup = columns.ToLookup(row => row.ObjectId);
		var checkLookup = checks.ToLookup(row => row.ObjectId);
		var indexLookup = indexes.ToLookup(row => row.ObjectId);
		var indexColLookup = indexCols.ToLookup(row => new IndexKey() { object_id = row.object_id, index_id = row.index_id });
		var fkLookup = foreignKeys.ToLookup(row => row.ObjectId);

		foreach (var x in indexes)
		{
			var indexKey = new IndexKey() { object_id = x.ObjectId, index_id = x.InternalId };
			x.Columns = indexColLookup[indexKey].Select(row => new Index.Column()
			{
				Name = row.name,
				Order = row.key_ordinal,
				Direction = (row.is_descending_key) ? SortDirection.Descending : SortDirection.Ascending
			});
		}

		foreach (var t in tables)
		{
			t.Columns = columnLookup[t.ObjectId].ToArray();
			foreach (var col in t.Columns) col.Parent = t;

			t.Indexes = indexLookup[t.ObjectId].ToArray();
			foreach (var x in t.Indexes) x.Parent = t;

			t.CheckConstraints = checkLookup[t.ObjectId].ToArray();
			foreach (var c in t.CheckConstraints) c.Parent = t;
		}

		return (tables, foreignKeys);
	}

	private static async Task<IEnumerable<IndexColumnResult>> GetIndexColumnsAsync(SqlConnection cn)
	{
		return await cn.QueryAsync<IndexColumnResult>(
			@"SELECT
				[xcol].[object_id],
				[xcol].[index_id],
				[col].[name],
				[xcol].[key_ordinal],
				[xcol].[is_descending_key]
			FROM
				[sys].[index_columns] [xcol]
				INNER JOIN [sys].[indexes] [x] ON [xcol].[object_id]=[x].[object_id] AND [xcol].[index_id]=[x].[index_id]
				INNER JOIN [sys].[columns] [col] ON [xcol].[object_id]=[col].[object_id] AND [xcol].[column_id]=[col].[column_id]
				INNER JOIN [sys].[tables] [t] ON [x].[object_id]=[t].[object_id]
			WHERE
				[t].[type_desc]='USER_TABLE'");
	}

	private static async Task<IEnumerable<Index>> GetIndexesAsync(SqlConnection cn)
	{
		return await cn.QueryAsync<Index>(
			@"SELECT
				[x].[object_id] AS [ObjectId],
				[x].[name] AS [Name],
				CONVERT(bit, CASE
					WHEN [x].[type_desc]='CLUSTERED' THEN 1
					ELSE 0
				END) AS [IsClustered],
				CASE
					WHEN [x].[is_primary_key]=1 THEN 1
					WHEN [x].[is_unique]=1 AND [x].[is_unique_constraint]=0 THEN 2
					WHEN [x].[is_unique_constraint]=1 THEN 3
					WHEN [x].[is_unique]=0 THEN 4
				END AS [IndexType],
				[x].[index_id] AS [InternalId]
			FROM
				[sys].[indexes] [x]
				INNER JOIN [sys].[tables] [t] ON [x].[object_id]=[t].[object_id]
			WHERE
				[t].[type_desc]='USER_TABLE' AND
				[x].[type]<>0");
	}

	private static async Task<IEnumerable<CheckConstraint>> GetCheckConstraintsAsync(SqlConnection cn)
	{
		return await cn.QueryAsync<CheckConstraint>(
			@"SELECT
				[ck].[parent_object_id] AS [ObjectId],
				[ck].[name] AS [Name],
				[ck].[definition] AS [Expression]
			FROM
				[sys].[check_constraints] [ck]
			WHERE
				[ck].[type]='C'");
	}

	private static async Task<IEnumerable<Column>> GetColumnAsync(SqlConnection cn)
	{
		return await cn.QueryAsync<Column>(
			@"WITH [identityColumns] AS (
				SELECT [object_id], [name] FROM [sys].[columns] WHERE [is_identity]=1
			), [source] AS (
				SELECT
					[col].[object_id] AS [ObjectId],
					[col].[name] AS [Name],
					TYPE_NAME([col].[system_type_id]) AS [DataType],
					[col].[is_nullable] AS [IsNullable],
					[def].[definition]  AS [DefaultValue],
					[col].[collation_name] AS [Collation],
					CASE
						WHEN TYPE_NAME([col].[system_type_id]) LIKE 'nvar%' AND [col].[max_length]>0 THEN ([col].[max_length]/2)
						WHEN TYPE_NAME([col].[system_type_id]) LIKE 'nvar%' AND [col].[max_length]=-1 THEN -1
						ELSE NULL
					END AS [MaxLength],
					[col].[precision] AS [Precision],
					[col].[scale] AS [Scale],
					[col].[column_id] AS [InternalId],
					[calc].[definition] AS [Expression],
					CASE
						WHEN [ic].[name] IS NOT NULL THEN 1
						ELSE 0
					END AS [IsIdentity],
					[col].[system_type_id]
				FROM
					[sys].[columns] [col]
					INNER JOIN [sys].[tables] [t] ON [col].[object_id]=[t].[object_id]
					LEFT JOIN [sys].[default_constraints] [def] ON [col].[default_object_id]=[def].[object_id]
					LEFT JOIN [sys].[computed_columns] [calc] ON [col].[object_id]=[calc].[object_id] AND [col].[column_id]=[calc].[column_id]
					LEFT JOIN [identityColumns] [ic] ON [ic].[object_id]=[col].[object_id] AND [ic].[name]=[col].[name]
				WHERE
					[t].[type_desc]='USER_TABLE'
			) SELECT
				[ObjectId],
				[Name],
				CASE
					WHEN [system_type_id]=106 THEN [DataType] + '(' + CONVERT(varchar, [Precision]) + ',' + CONVERT(varchar, [Scale]) + ')'						
					WHEN [MaxLength]=-1 THEN [DataType] + '(max)'
					WHEN [MaxLength] IS NULL THEN [DataType]
					ELSE [DataType] + '(' + CONVERT(varchar, [MaxLength]) + ')'
				END AS [DataType],
				[IsNullable],
				[DefaultValue],
				[Collation],
				[Precision],
				[InternalId],
				[Expression],
				CASE
					WHEN [Expression] IS NOT NULL THEN 1
					ELSE 0
				END AS [IsCalculated],
				CASE
					WHEN [IsIdentity]=1 THEN ' identity(1,1)'
					ELSE NULL
				END AS [TypeModifier]
			FROM
				[source]");
	}

	private static async Task<IEnumerable<Table>> GetTablesAsync(SqlConnection cn)
	{
		return await cn.QueryAsync<Table>(
			@"WITH [clusteredIndexes] AS (
					SELECT [name], [object_id] FROM [sys].[indexes] WHERE [type_desc]='CLUSTERED'
				), [identityColumns] AS (
					SELECT [object_id], [name] FROM [sys].[columns] WHERE [is_identity]=1
				) SELECT
					SCHEMA_NAME([t].[schema_id]) + '.' + [t].[name] AS [Name],					
					[t].[object_id] AS [ObjectId],
					[c].[name] AS [ClusteredIndex],
					[i].[name] AS [IdentityColumn]					
				FROM
					[sys].[tables] [t]
					LEFT JOIN [clusteredIndexes] [c] ON [t].[object_id]=[c].[object_id]
					LEFT JOIN [identityColumns] [i] ON [t].[object_id]=[i].[object_id]
				WHERE					
					[t].[name] NOT IN ('__MigrationHistory', '__EFMigrationsHistory')");
	}

	private static async Task<IEnumerable<ForeignKey>> GetForeignKeysAsync(SqlConnection cn, IEnumerable<Table> tables)
	{
		var tableDictionary = tables.ToDictionary(item => item.Name);

		var foreignKeys = await cn.QueryAsync<ForeignKeysResult>(
			@"SELECT
				[fk].[object_id] AS [ObjectId],
				[child_t].[object_id] AS [ReferencingObjectId],
				[fk].[name] AS [ConstraintName],
				SCHEMA_NAME([ref_t].[schema_id]) AS [ReferencedSchema],
				[ref_t].[name] AS [ReferencedTable],
				SCHEMA_NAME([child_t].[schema_id]) AS [ReferencingSchema],
				[child_t].[name] AS [ReferencingTable],				
				CONVERT(bit, [fk].[delete_referential_action]) AS [CascadeDelete],
				CONVERT(bit, [fk].[update_referential_action]) AS [CascadeUpdate]
			FROM
				[sys].[foreign_keys] [fk]
				INNER JOIN [sys].[tables] [ref_t] ON [fk].[referenced_object_id]=[ref_t].[object_id]
				INNER JOIN [sys].[tables] [child_t] ON [fk].[parent_object_id]=[child_t].[object_id]");

		var columns = await cn.QueryAsync<ForeignKeyColumnsResult>(
			@"SELECT
				[fkcol].[constraint_object_id] AS [ObjectId],
				[child_col].[name] AS [ReferencingName],
				[ref_col].[name] AS [ReferencedName]
			FROM
				[sys].[foreign_key_columns] [fkcol]
				INNER JOIN [sys].[tables] [child_t] ON [fkcol].[parent_object_id]=[child_t].[object_id]
				INNER JOIN [sys].[columns] [child_col] ON
					[child_t].[object_id]=[child_col].[object_id] AND
					[fkcol].[parent_column_id]=[child_col].[column_id]
				INNER JOIN [sys].[tables] [ref_t] ON [fkcol].[referenced_object_id]=[ref_t].[object_id]
				INNER JOIN [sys].[columns] [ref_col] ON
					[ref_t].[object_id]=[ref_col].[object_id] AND
					[fkcol].[referenced_column_id]=[ref_col].[column_id]");

		var colLookup = columns.ToLookup(row => row.ObjectId);

		return foreignKeys.Select(fk => new ForeignKey()
		{
			Name = fk.ConstraintName,
			ObjectId = fk.ReferencingObjectId,
			ReferencedTable = tableDictionary[$"{fk.ReferencedSchema}.{fk.ReferencedTable}"],
			Parent = tableDictionary[$"{fk.ReferencingSchema}.{fk.ReferencingTable}"],
			CascadeDelete = fk.CascadeDelete,
			CascadeUpdate = fk.CascadeUpdate,
			Columns = colLookup[fk.ObjectId].Select(fkcol => new ForeignKey.Column()
			{
				ReferencedName = fkcol.ReferencedName,
				ReferencingName = fkcol.ReferencingName
			})
		});
	}
}
