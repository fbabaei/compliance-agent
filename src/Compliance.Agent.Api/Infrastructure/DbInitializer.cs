using Microsoft.Data.SqlClient;

namespace Compliance.Agent.Api.Infrastructure;

/// <summary>
/// Creates the required database tables on first startup if they do not already exist.
/// </summary>
public sealed class DbInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(string connectionString, ILogger<DbInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing database schema…");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var ddl = """
            IF OBJECT_ID('dbo.sessions', 'U') IS NULL
            CREATE TABLE dbo.sessions (
                id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                created_at_utc   DATETIMEOFFSET   NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                messages_json    NVARCHAR(MAX)    NOT NULL DEFAULT '[]'
            );

            IF OBJECT_ID('dbo.case_drafts', 'U') IS NULL
            CREATE TABLE dbo.case_drafts (
                id                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                session_id            UNIQUEIDENTIFIER NOT NULL,
                source_type           NVARCHAR(32)     NOT NULL,
                original_text         NVARCHAR(MAX)    NOT NULL DEFAULT '',
                document_blob_url     NVARCHAR(2048)   NULL,
                fields_json           NVARCHAR(MAX)    NOT NULL DEFAULT '{}',
                classification_codes  NVARCHAR(MAX)    NOT NULL DEFAULT '[]',
                jurisdictions         NVARCHAR(MAX)    NOT NULL DEFAULT '[]',
                missing_fields        NVARCHAR(MAX)    NOT NULL DEFAULT '[]',
                status                NVARCHAR(64)     NOT NULL DEFAULT 'Draft',
                updated_at_utc        DATETIMEOFFSET   NOT NULL DEFAULT SYSDATETIMEOFFSET()
            );

            IF OBJECT_ID('dbo.faq_knowledge_base', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.faq_knowledge_base (
                    id      INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    content NVARCHAR(MAX) NOT NULL
                );

                INSERT INTO dbo.faq_knowledge_base (content) VALUES
                    ('Report potential sanctions screening hits within required SLA for your jurisdiction.'),
                    ('Preserve auditable decision evidence for each classification and jurisdiction mapping.'),
                    ('Escalate high-risk cross-border transactions for enhanced due diligence.'),
                    ('All case drafts require a trigger date, jurisdiction, case title, and classification reasoning.'),
                    ('For EU jurisdiction, GDPR data handling requirements apply to all case evidence.');
            END;

            IF OBJECT_ID('dbo.audit_log', 'U') IS NULL
            CREATE TABLE dbo.audit_log (
                id            BIGINT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
                event_time    DATETIMEOFFSET   NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                event_type    NVARCHAR(64)     NOT NULL,
                entity_type   NVARCHAR(64)     NOT NULL,
                entity_id     NVARCHAR(128)    NOT NULL,
                details_json  NVARCHAR(MAX)    NULL
            );
            """;

        await using var cmd = new SqlCommand(ddl, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("Database schema ready.");
    }
}
