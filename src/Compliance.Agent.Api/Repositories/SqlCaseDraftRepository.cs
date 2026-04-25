using System.Text.Json;
using Compliance.Agent.Api.Models;
using Microsoft.Data.SqlClient;

namespace Compliance.Agent.Api.Repositories;

public sealed class SqlCaseDraftRepository : ICaseDraftRepository
{
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public SqlCaseDraftRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public CaseDraft? GetCaseDraft(Guid id)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var cmd = new SqlCommand(
            """
            SELECT id, session_id, source_type, original_text, document_blob_url,
                   fields_json, classification_codes, jurisdictions, missing_fields,
                   status, updated_at_utc
            FROM dbo.case_drafts WHERE id = @id
            """, connection);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapRow(reader) : null;
    }

    public void UpsertCaseDraft(CaseDraft draft)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var cmd = new SqlCommand(
            """
            MERGE dbo.case_drafts AS target
            USING (SELECT @id AS id) AS source ON target.id = source.id
            WHEN MATCHED THEN
                UPDATE SET
                    fields_json          = @fields,
                    classification_codes = @codes,
                    jurisdictions        = @jurisdictions,
                    missing_fields       = @missing,
                    status               = @status,
                    updated_at_utc       = @updated
            WHEN NOT MATCHED THEN
                INSERT (id, session_id, source_type, original_text, document_blob_url,
                        fields_json, classification_codes, jurisdictions, missing_fields,
                        status, updated_at_utc)
                VALUES (@id, @sessionId, @sourceType, @originalText, @blobUrl,
                        @fields, @codes, @jurisdictions, @missing, @status, @updated);
            """, connection);

        cmd.Parameters.AddWithValue("@id", draft.Id);
        cmd.Parameters.AddWithValue("@sessionId", draft.SessionId);
        cmd.Parameters.AddWithValue("@sourceType", draft.SourceType);
        cmd.Parameters.AddWithValue("@originalText", draft.OriginalText);
        cmd.Parameters.AddWithValue("@blobUrl", (object?)draft.DocumentBlobUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fields", JsonSerializer.Serialize(draft.Fields, JsonOpts));
        cmd.Parameters.AddWithValue("@codes", JsonSerializer.Serialize(draft.ClassificationCodes, JsonOpts));
        cmd.Parameters.AddWithValue("@jurisdictions", JsonSerializer.Serialize(draft.Jurisdictions, JsonOpts));
        cmd.Parameters.AddWithValue("@missing", JsonSerializer.Serialize(draft.MissingFields, JsonOpts));
        cmd.Parameters.AddWithValue("@status", draft.Status);
        cmd.Parameters.AddWithValue("@updated", draft.UpdatedAtUtc);
        cmd.ExecuteNonQuery();
    }

    private static CaseDraft MapRow(SqlDataReader r)
    {
        var fields = JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(5), JsonOpts)
                     ?? new Dictionary<string, string>();
        var codes = JsonSerializer.Deserialize<List<string>>(r.GetString(6), JsonOpts) ?? [];
        var jurisdictions = JsonSerializer.Deserialize<List<string>>(r.GetString(7), JsonOpts) ?? [];
        var missing = JsonSerializer.Deserialize<List<string>>(r.GetString(8), JsonOpts) ?? [];

        return new CaseDraft(
            Id: r.GetGuid(0),
            SessionId: r.GetGuid(1),
            SourceType: r.GetString(2),
            OriginalText: r.GetString(3),
            DocumentBlobUrl: r.IsDBNull(4) ? null : r.GetString(4),
            Fields: fields,
            ClassificationCodes: codes,
            Jurisdictions: jurisdictions,
            MissingFields: missing,
            Status: r.GetString(9),
            UpdatedAtUtc: r.GetDateTimeOffset(10));
    }
}
