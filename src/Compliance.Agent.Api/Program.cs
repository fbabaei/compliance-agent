using Compliance.Agent.Api.Agents;
using Compliance.Agent.Api.Agents.Maf;
using Compliance.Agent.Api.Infrastructure;
using Compliance.Agent.Api.Models;
using Compliance.Agent.Api.Options;
using Compliance.Agent.Api.Repositories;
using Compliance.Agent.Api.Services;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

// ── Configuration ──────────────────────────────────────────────────────────
builder.Services.Configure<AzureOpenAiOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.Configure<GuardrailOptions>(builder.Configuration.GetSection("Guardrails"));
builder.Services.Configure<BlobStorageOptions>(builder.Configuration.GetSection("BlobStorage"));

// ── Observability ──────────────────────────────────────────────────────────
var appInsightsCs = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(appInsightsCs))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

// ── Core services (always registered) ─────────────────────────────────────
builder.Services.AddSingleton<IOffTopicDetector, OffTopicDetector>();
builder.Services.AddSingleton<IJurisdictionValidator, JurisdictionValidator>();
builder.Services.AddSingleton<ISchemaValidator, SchemaValidator>();
builder.Services.AddSingleton<IDependencyReadinessService, DependencyReadinessService>();

// ── Storage tier: SQL if connection string present, else in-memory ─────────
var sqlCs = builder.Configuration.GetConnectionString("SqlConnectionString");
var hasSql = !string.IsNullOrWhiteSpace(sqlCs);

if (hasSql)
{
    builder.Services.AddSingleton<ISessionRepository>(_ => new SqlSessionRepository(sqlCs!));
    builder.Services.AddSingleton<ICaseDraftRepository>(_ => new SqlCaseDraftRepository(sqlCs!));
    builder.Services.AddSingleton<IFAQRepository>(_ => new SqlFaqRepository(sqlCs!));
}
else
{
    builder.Services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
    builder.Services.AddSingleton<ICaseDraftRepository, InMemoryCaseDraftRepository>();
    builder.Services.AddSingleton<IFAQRepository, InMemoryFaqRepository>();
}

// ── Document ingestion: Blob if container URI present, else in-memory ──────
var blobContainerUri = builder.Configuration["BlobStorage:ContainerUri"];
var hasBlob = !string.IsNullOrWhiteSpace(blobContainerUri)
              && Uri.TryCreate(blobContainerUri, UriKind.Absolute, out _);

if (hasBlob)
{
    builder.Services.AddSingleton<IDocumentIngestionService, BlobDocumentIngestionService>();
}
else
{
    builder.Services.AddSingleton<IDocumentIngestionService, DocumentIngestionService>();
}

// ── Agent tier: Azure OpenAI if endpoint is configured, else stub ──────────
var openAiEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
var hasOpenAi = !string.IsNullOrWhiteSpace(openAiEndpoint)
                && openAiEndpoint != "https://your-openai-resource.openai.azure.com/";

if (hasOpenAi)
{
    builder.Services.AddSingleton<IExtractionAgentService, AzureOpenAiExtractionAgentService>();
    builder.Services.AddSingleton<IChatAgentService, AzureOpenAiChatAgentService>();
}
else
{
    builder.Services.AddSingleton<IExtractionAgentService, ExtractionAgentService>();
    builder.Services.AddSingleton<IChatAgentService, ChatAgentService>();
}

// ── Microsoft Agent Framework (MAF) workflow orchestration ─────────────────
// Executors are the MAF processing nodes; workflows hold the compiled graph.
// Both are singletons — the Workflow is built once, InProcessExecution.RunAsync
// creates an isolated run per request at call time.
builder.Services.AddSingleton<ExtractionExecutor>();
builder.Services.AddSingleton<ChatExecutor>();
builder.Services.AddSingleton<ComplianceExtractionWorkflow>();
builder.Services.AddSingleton<ComplianceChatWorkflow>();

var app = builder.Build();

// ── DB initialization (runs once at startup when SQL is configured) ─────────
if (hasSql)
{
    using var scope = app.Services.CreateScope();
    var dbInit = new DbInitializer(sqlCs!, scope.ServiceProvider.GetRequiredService<ILogger<DbInitializer>>());
    await dbInit.InitializeAsync();
}

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
	app.Urls.Add("http://0.0.0.0:8080");
}

app.MapGet("/", () => Results.Ok(new
{
	service = "Compliance Agent API",
	version = "v1",
	message = "Service ready"
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/health/ready", async (IDependencyReadinessService readiness, CancellationToken ct) =>
{
	var result = await readiness.CheckAsync(ct);
	return result.IsReady
		? Results.Ok(result)
		: Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

app.MapPost("/api/session", (ISessionRepository sessions) =>
{
	var created = sessions.CreateSession();
	return Results.Ok(created);
});

app.MapGet("/api/session/{id:guid}", (Guid id, ISessionRepository sessions) =>
{
	var session = sessions.GetSession(id);
	return session is null ? Results.NotFound() : Results.Ok(session);
});

app.MapDelete("/api/session/{id:guid}", (Guid id, ISessionRepository sessions) =>
{
	return sessions.DeleteSession(id) ? Results.NoContent() : Results.NotFound();
});

app.MapPost("/api/chat", async (
	ChatRequest request,
	ComplianceChatWorkflow chatWorkflow,
	CancellationToken ct) =>
{
	if (string.IsNullOrWhiteSpace(request.Message))
	{
		return Results.BadRequest(new ApiError("chat-message-required", "Message is required."));
	}

	// Routed through the MAF ComplianceChatWorkflow (WorkflowBuilder + ChatExecutor)
	var response = await chatWorkflow.RunAsync(request, ct);
	return Results.Ok(response);
});

app.MapPost("/api/upload", async (
	HttpRequest httpRequest,
	IDocumentIngestionService ingestion,
	CancellationToken ct) =>
{
	if (!httpRequest.HasFormContentType)
	{
		return Results.BadRequest(new ApiError("invalid-content-type", "Expected multipart/form-data."));
	}

	var form = await httpRequest.ReadFormAsync(ct);
	var file = form.Files.FirstOrDefault();
	if (file is null || file.Length == 0)
	{
		return Results.BadRequest(new ApiError("file-required", "Upload must include a file."));
	}

	var sessionId = Guid.TryParse(form["sessionId"], out var parsedSession) ? parsedSession : Guid.NewGuid();
	var upload = await ingestion.IngestAsync(file, ct);
	// Routed through the MAF ComplianceExtractionWorkflow (WorkflowBuilder + ExtractionExecutor)
	var extractionWorkflow = app.Services.GetRequiredService<ComplianceExtractionWorkflow>();
	var extractionRequest = new DocumentExtractionRequest(sessionId, upload.DocumentText, upload.BlobLikeUri);
	var result = await extractionWorkflow.ExtractAsync(extractionRequest, ct);

	var response = new UploadResponse(
		sessionId,
		upload.DocumentId,
		result.Draft.Id,
		result.MissingFields,
		result.ReasoningSummary,
		upload.BlobLikeUri);

	return Results.Ok(response);
});

app.MapGet("/api/case/{id:guid}", (Guid id, ICaseDraftRepository drafts) =>
{
	var draft = drafts.GetCaseDraft(id);
	return draft is null ? Results.NotFound() : Results.Ok(draft);
});

app.MapPut("/api/case/{id:guid}", (Guid id, CaseDraftUpdateRequest request, ICaseDraftRepository drafts) =>
{
	var existing = drafts.GetCaseDraft(id);
	if (existing is null)
	{
		return Results.NotFound();
	}

	var updated = existing with
	{
		Fields = request.Fields ?? existing.Fields,
		Jurisdictions = request.Jurisdictions?.ToList() ?? existing.Jurisdictions,
		MissingFields = request.MissingFields?.ToList() ?? existing.MissingFields,
		Status = request.Status ?? existing.Status,
		UpdatedAtUtc = DateTimeOffset.UtcNow
	};

	drafts.UpsertCaseDraft(updated);
	return Results.Ok(updated);
});

app.MapPost("/api/case/{id:guid}/confirm", (Guid id, ICaseDraftRepository drafts) =>
{
	var existing = drafts.GetCaseDraft(id);
	if (existing is null)
	{
		return Results.NotFound();
	}

	var finalized = existing with { Status = "Confirmed", UpdatedAtUtc = DateTimeOffset.UtcNow };
	drafts.UpsertCaseDraft(finalized);
	return Results.Ok(finalized);
});

app.Run();
