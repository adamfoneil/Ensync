﻿using Dapper;
using Ensync.Core.Abstract;
using Ensync.Core.Models;
using Ensync.SqlServer.Internal;
using Microsoft.Data.SqlClient;

using Index = Ensync.Core.Models.Index;

namespace Ensync.SqlServer;

public class SqlServerSchemaInspector : SchemaInspector
{
	private readonly string _connectionString;

	public SqlServerSchemaInspector(string connectionString)
	{
		_connectionString = connectionString;
	}

	protected override async Task<IEnumerable<DbObject>> GetDbObjectsAsync()
	{
		using var cn = new SqlConnection(_connectionString);

		var tables = await cn.QueryAsync<Table>(
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

		var columns = await cn.QueryAsync<Column>(
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

		var checks = await cn.QueryAsync<CheckConstraint>(
			@"SELECT
				[ck].[parent_object_id] AS [ObjectId],
				[ck].[name] AS [Name],
				[ck].[definition] AS [Expression]
			FROM
				[sys].[check_constraints] [ck]
			WHERE
				[ck].[type]='C'");

		var indexes = await cn.QueryAsync<Index>(
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
				END AS [Type],
				[x].[index_id] AS [InternalId]
			FROM
				[sys].[indexes] [x]
				INNER JOIN [sys].[tables] [t] ON [x].[object_id]=[t].[object_id]
			WHERE
				[t].[type_desc]='USER_TABLE' AND
				[x].[type]<>0");

		var indexCols = await cn.QueryAsync<IndexColumnResult>(
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

		var columnLookup = columns.ToLookup(row => row.ObjectId);
		var checkLookup = checks.ToLookup(row => row.ObjectId);
		var indexLookup = indexes.ToLookup(row => row.ObjectId);
		var indexColLookup = indexCols.ToLookup(row => new IndexKey() { object_id = row.object_id, index_id = row.index_id });

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

		return tables;
	}
}
