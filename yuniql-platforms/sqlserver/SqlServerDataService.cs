using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Yuniql.Extensibility;
using Yuniql.Extensibility.SqlBatchParser;

namespace Yuniql.SqlServer
{
    ///<inheritdoc/>
    public class SqlServerDataService : IDataService
    {
        private string _connectionString;
        private readonly ITraceService _traceService;

        ///<inheritdoc/>
        public SqlServerDataService(ITraceService traceService)
        {
            this._traceService = traceService;
        }

        ///<inheritdoc/>
        public void Initialize(string connectionString)
        {
            this._connectionString = connectionString;
        }

        ///<inheritdoc/>
        public IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        ///<inheritdoc/>
        public IDbConnection CreateMasterConnection()
        {
            var masterConnectionStringBuilder = new SqlConnectionStringBuilder(_connectionString);
            masterConnectionStringBuilder.InitialCatalog = "master";

            return new SqlConnection(masterConnectionStringBuilder.ConnectionString);
        }

        ///<inheritdoc/>
        public ConnectionInfo GetConnectionInfo()
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(_connectionString);
            return new ConnectionInfo { DataSource = connectionStringBuilder.DataSource, Database = connectionStringBuilder.InitialCatalog };
        }

        ///<inheritdoc/>
        public bool IsTransactionalDdlSupported => true;

        ///<inheritdoc/>
        public bool IsSchemaSupported { get; } = true;

        ///<inheritdoc/>
        public bool IsBatchSqlSupported { get; } = true;

        ///<inheritdoc/>
        public bool IsUpsertSupported { get; } = true;

        ///<inheritdoc/>
        public string TableName { get; set; } = "__yuniqldbversion";

        ///<inheritdoc/>
        public string SchemaName { get; set; } = "dbo";

        ///<inheritdoc/>
        public List<string> BreakStatements(string sqlStatementRaw)
        {
            var sqlBatchParser = new SqlBatchParser(_traceService, new GoSqlBatchLineAnalyzer(), new CommentAnalyzer());
            return sqlBatchParser.Parse(sqlStatementRaw).Select(s => s.BatchText).ToList();
        }

        ///<inheritdoc/>
        public string GetSqlForCheckIfDatabaseExists()
            => @"SELECT ISNULL(database_id, 0) FROM [sys].[databases] WHERE name = '${YUNIQL_DB_NAME}'";

        ///<inheritdoc/>
        public string GetSqlForCreateDatabase()
            => @"CREATE DATABASE [${YUNIQL_DB_NAME}];";

        ///<inheritdoc/>
        public string GetSqlForCreateSchema()
            => @"CREATE SCHEMA [${YUNIQL_SCHEMA_NAME}];";

        ///<inheritdoc/>
        public string GetSqlForCheckIfDatabaseConfigured()
            => @"SELECT ISNULL(OBJECT_ID('[${YUNIQL_SCHEMA_NAME}].[${YUNIQL_TABLE_NAME}]'), 0)";

        ///<inheritdoc/>
        public string GetSqlForConfigureDatabase()
            => @"
IF OBJECT_ID('[${YUNIQL_SCHEMA_NAME}].[${YUNIQL_TABLE_NAME}]') IS NULL 
BEGIN
    CREATE TABLE [${YUNIQL_SCHEMA_NAME}].[${YUNIQL_TABLE_NAME}] (
	    [SequenceId] [SMALLINT] IDENTITY(1,1) NOT NULL,
	    [Version] [NVARCHAR](512) NOT NULL,
	    [AppliedOnUtc] [DATETIME] NOT NULL,
	    [AppliedByUser] [NVARCHAR](32) NOT NULL,
	    [AppliedByTool] [NVARCHAR](32) NOT NULL,
	    [AppliedByToolVersion] [NVARCHAR](16) NOT NULL,
	    [Status] [NVARCHAR](32) NOT NULL,
	    [DurationMs] [INT] NOT NULL,
	    [FailedScriptPath] [NVARCHAR](4000) NULL,
	    [FailedScriptError] [NVARCHAR](4000) NULL,
	    [AdditionalArtifacts] [NVARCHAR](4000) NULL,
        CONSTRAINT [PK___YuniqlDbVersion] PRIMARY KEY CLUSTERED ([SequenceId] ASC),
        CONSTRAINT [IX___YuniqlDbVersion] UNIQUE NONCLUSTERED  ([Version] ASC
    ));

    ALTER TABLE [${YUNIQL_SCHEMA_NAME}].[${YUNIQL_TABLE_NAME}] ADD  CONSTRAINT [DF___YuniqlDbVersion_AppliedOnUtc]  DEFAULT (GETUTCDATE()) FOR [AppliedOnUtc];
    ALTER TABLE [${YUNIQL_SCHEMA_NAME}].[${YUNIQL_TABLE_NAME}] ADD  CONSTRAINT [DF___YuniqlDbVersion_AppliedByUser]  DEFAULT (SUSER_SNAME()) FOR [AppliedByUser];
END                
            ";

        ///<inheritdoc/>
        public string GetSqlForGetCurrentVersion()
            => @"SELECT TOP 1 [Version] FROM [${YUNIQL_SCHEMA_NAME}].[${YUNIQL_TABLE_NAME}] WHERE Status = 'Successful' ORDER BY [SequenceId] DESC;";

        ///<inheritdoc/>
        public string GetSqlForGetAllVersions()
            => @"
SELECT [SequenceId], [Version], [AppliedOnUtc], [AppliedByUser], [AppliedByTool], [AppliedByToolVersion], [Status], [DurationMs], [FailedScriptPath], [FailedScriptError], [AdditionalArtifacts]
FROM [${YUNIQL_SCHEMA_NAME}].[${YUNIQL_TABLE_NAME}] ORDER BY Version ASC;
            ";

        ///<inheritdoc/>
        public string GetSqlForInsertVersion()
            => @"
INSERT ([Version], [AppliedOnUtc], [AppliedByUser], [AppliedByTool], [AppliedByToolVersion], [Status], [DurationMs], [FailedScriptPath], [FailedScriptError], [AdditionalArtifacts]) 
VALUES ('${YUNIQL_VERSION}', '${YUNIQL_APPLIED_BY_TOOL}', '${YUNIQL_APPLIED_BY_TOOL_VERSION}', '${YUNIQL_STATUS}', '${YUNIQL_DURATION_MS}', '${YUNIQL_FAILED_SCRIPT_PATH}', '${YUNIQL_FAILED_SCRIPT_ERROR}', '${YUNIQL_ADDITIONAL_ARTIFACTS}');
            ";

        ///<inheritdoc/>
        public string GetSqlForUpdateVersion()
            => throw new NotSupportedException("Not supported for the target platform");

        ///<inheritdoc/>
        public string GetSqlForUpsertVersion()
            => @"
MERGE [${YUNIQL_SCHEMA_NAME}].[${YUNIQL_TABLE_NAME}] AS T
USING (SELECT 
	'${YUNIQL_VERSION}' [Version], 
	GETUTCDATE() [AppliedOnUtc], 
	SUSER_SNAME() [AppliedByUser], 
	'${YUNIQL_APPLIED_BY_TOOL}' [AppliedByTool], 
	'${YUNIQL_APPLIED_BY_TOOL_VERSION}' [AppliedByToolVersion], 
	'${YUNIQL_STATUS}' [Status], 
	'${YUNIQL_DURATION_MS}' [DurationMs], 
	'${YUNIQL_FAILED_SCRIPT_PATH}' [FailedScriptPath], 
	'${YUNIQL_FAILED_SCRIPT_ERROR}' [FailedScriptError], 
	'${YUNIQL_ADDITIONAL_ARTIFACTS}' [AdditionalArtifacts]) AS S 
ON T.[Version] = S.[Version]
WHEN MATCHED THEN
  UPDATE SET 	
	T.[AppliedOnUtc] = S.[AppliedOnUtc],
	T.[AppliedByUser] = S.[AppliedByUser],
	T.[AppliedByTool]= S.[AppliedByTool], 
	T.[AppliedByToolVersion] = S.[AppliedByToolVersion],
	T.[Status] = S.[Status],
	T.[DurationMs] = S.[DurationMs],
	T.[FailedScriptPath] = S.[FailedScriptPath],
	T.[FailedScriptError] = S.[FailedScriptError],
	T.[AdditionalArtifacts] = S.[AdditionalArtifacts]
WHEN NOT MATCHED THEN
  INSERT ([Version], [AppliedOnUtc], [AppliedByUser], [AppliedByTool], [AppliedByToolVersion], [Status], [DurationMs], [FailedScriptPath], [FailedScriptError], [AdditionalArtifacts]) 
  VALUES (S.[Version], GETUTCDATE(), SUSER_SNAME(), S.[AppliedByTool], S.[AppliedByToolVersion], S.[Status], S.[DurationMs], S.[FailedScriptPath], S.[FailedScriptError], S.[AdditionalArtifacts]);
            ";

        ///<inheritdoc/>
        public bool UpdateDatabaseConfiguration(IDbConnection dbConnection, ITraceService traceService = null, string metaSchemaName = null, string metaTableName = null)
        {
            //no need to update tracking table as the structure has no been changed so far
            return false;
        }

        ///<inheritdoc/>
        public bool TryParseErrorFromException(Exception exception, out string result)
        {
            result = null;
            if (exception is SqlException sqlException)
            {
                result = $"(0x{sqlException.ErrorCode:X}) Error {sqlException.Number}: {sqlException.Message}";
                return true;
            }
            return false;
        }
    }
}