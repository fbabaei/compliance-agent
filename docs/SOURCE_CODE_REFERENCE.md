# Compliance Agent – Source Code Reference

Explains every file in `src/Compliance.Agent.Api/`, what it does, and which architecture component it implements.

---

## `Program.cs`

**Role:** Composition root — the only place where concrete types are selected and wired.

Key responsibilities:
1. Reads configuration sections (`AzureOpenAI`, `Guardrails`, `BlobStorage`, `ConnectionStrings`).
2. Conditionally registers SQL or in-memory repositories based on whether `SqlConnectionString` is set.
3. Conditionally registers Blob or file-based ingestion based on whether `BlobStorage:ContainerUri` is set.
4. Conditionally registers Azure OpenAI agents or stub agents based on whether `AzureOpenAI:Endpoint` is configured.
5. Registers MAF workflow classes (`ComplianceChatWorkflow`, `ComplianceExtractionWorkflow`) as singletons.
6. Registers Application Insights when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set.
7. Runs `DbInitializer` at startup when SQL is configured.
8. Maps all HTTP endpoints using ASP.NET Core Minimal APIs.

No business logic lives here — only wiring.

---

## Models/

Data contracts shared across all layers. All are C# records (immutable).

| File | Purpose |
|---|---|
| `ChatRequest.cs` | `POST /api/chat` request body: `SessionId?`, `Message`, `UseRag` |
| `ChatResponse.cs` | Chat agent response: `SessionId`, `Message`, `IsOffTopic`, `MissingFields`, `SuggestedActions` |
| `ChatMessage.cs` | A single conversation turn: `Role` ("user"/"assistant"), `Content`, `TimestampUtc` |
| `SessionModel.cs` | A complete conversation session: `Id`, `CreatedAtUtc`, list of `ChatMessage` |
| `CaseDraft.cs` | The structured compliance case: `Fields` dict, `ClassificationCodes`, `Jurisdictions`, `MissingFields`, `Status`, `UpdatedAtUtc` |
| `CaseDraftUpdateRequest.cs` | PATCH-style payload for `PUT /api/case/{id}` |
| `UploadResponse.cs` | `POST /api/upload` response: session/doc/draft IDs, missing fields, summary, blob URI |
| `ExtractionResult.cs` | Extraction agent output: `Draft`, `MissingFields`, `ReasoningSummary` |
| `DocumentIngestionResult.cs` | Result from ingestion service: `DocumentId`, `BlobLikeUri`, `DocumentText` |
| `DependencyReadinessResult.cs` | Health check payload: `IsReady`, `Checks` dict |
| `ApiError.cs` | Standard error body: `Code`, `Message` |

---

## Options/

Strongly-typed configuration objects bound via `IOptions<T>`.

| File | Config section | Key fields |
|---|---|---|
| `AzureOpenAiOptions.cs` | `AzureOpenAI` | `Endpoint`, `ChatDeployment` ("gpt-5.2"), `EmbeddingDeployment` |
| `BlobStorageOptions.cs` | `BlobStorage` | `ContainerUri` |
| `GuardrailOptions.cs` | `Guardrails` | `BlockedPhrases` — list of strings the off-topic detector rejects |

---

## Repositories/

Persistence layer. Each storage concern has one interface and two implementations.

### Interfaces

| Interface | Methods |
|---|---|
| `ISessionRepository` | `CreateSession()`, `GetSession(Guid)`, `AppendMessage(Guid, ChatMessage)`, `DeleteSession(Guid)` |
| `ICaseDraftRepository` | `GetCaseDraft(Guid)`, `UpsertCaseDraft(CaseDraft)` |
| `IFAQRepository` | `VectorSearch(string query, int topK)` |

### In-memory implementations (dev/test)

| File | How it works |
|---|---|
| `InMemorySessionRepository.cs` | `ConcurrentDictionary<Guid, SessionModel>` — thread-safe, process-lifetime |
| `InMemoryCaseDraftRepository.cs` | `ConcurrentDictionary<Guid, CaseDraft>` |
| `InMemoryFaqRepository.cs` | Seeded with 3 compliance strings; scores by token overlap |

### SQL implementations (production)

| File | How it works |
|---|---|
| `SqlSessionRepository.cs` | Reads/writes `dbo.sessions`; serializes `messages` list to `messages_json` JSON column |
| `SqlCaseDraftRepository.cs` | MERGE-upsert into `dbo.case_drafts`; collections stored as JSON columns |
| `SqlFaqRepository.cs` | Queries `dbo.faq_knowledge_base`; client-side token scoring (upgrade path: SQL vector search) |

---

## Services/

Cross-cutting business logic used by the agents.

| File | Role |
|---|---|
| `IOffTopicDetector.cs` + `OffTopicDetector.cs` | Checks the user message against `GuardrailOptions.BlockedPhrases` (case-insensitive). Returns `true` if the message should be rejected. |
| `IJurisdictionValidator.cs` + `JurisdictionValidator.cs` | Validates a jurisdiction code against the allowed list: US, UK, EU, UAE, CA, AU, SG, CH. |
| `ISchemaValidator.cs` + `SchemaValidator.cs` | Checks a `CaseDraft.Fields` dictionary against a required-field list; returns missing fields. |
| `IDocumentIngestionService.cs` | Contract: `IngestAsync(IFormFile, CancellationToken) → DocumentIngestionResult` |
| `DocumentIngestionService.cs` | Dev stub: reads the uploaded file as UTF-8 text, returns a fake document ID. |
| `BlobDocumentIngestionService.cs` | Production: uploads to Azure Blob Storage via `BlobContainerClient` + `DefaultAzureCredential`; returns the real blob URI. |
| `IDependencyReadinessService.cs` + `DependencyReadinessService.cs` | Checks whether Azure OpenAI endpoint is configured; powers `GET /health/ready`. |

---

## Agents/

The two AI agents from the architecture, plus their MAF wrappers.

### Interfaces

| Interface | Purpose |
|---|---|
| `IChatAgentService.cs` | `ProcessAsync(ChatRequest, CancellationToken) → ChatResponse` |
| `IExtractionAgentService.cs` | `ExtractFromTextAsync` and `ExtractFromDocumentAsync` → `ExtractionResult` |

### Stub implementations (dev — no Azure required)

| File | How it works |
|---|---|
| `ChatAgentService.cs` | Regex intent detection; calls `InMemoryFaqRepository` for RAG; delegates to `ExtractionAgentService` when case-creation is detected. |
| `ExtractionAgentService.cs` | Regex-based field extraction from plain text. Returns a partially filled `CaseDraft`. |

### Azure OpenAI implementations (production)

| File | How it works |
|---|---|
| `AzureOpenAiChatAgentService.cs` | Uses `AzureOpenAIClient` → `ChatClient` (GPT-5.2). Builds a multi-turn message list from the session history. Fetches FAQ entries from `IFAQRepository` and injects them into the system prompt for RAG grounding. Delegates to `IExtractionAgentService` when the user intent is case-creation. Uses `ModelChatMessage` type alias to disambiguate from `OpenAI.Chat.ChatMessage`. |
| `AzureOpenAiExtractionAgentService.cs` | Uses GPT-5.2 with a strict JSON-only system prompt. Parses the JSON response into a `CaseDraft`. Falls back to regex for classification codes if the model omits them. |

### Microsoft Agent Framework (MAF) layer (`Agents/Maf/`)

This layer wraps the agent services as **MAF workflow nodes** to satisfy the architecture's "Microsoft Agent Framework" requirement.

| File | MAF type | Role |
|---|---|---|
| `ChatExecutor.cs` | `Executor<ChatRequest, ChatResponse>` | MAF node wrapping `IChatAgentService`; entry point of the chat workflow |
| `ExtractionExecutor.cs` | `Executor<DocumentExtractionRequest, ExtractionResult>` | MAF node wrapping `IExtractionAgentService`; entry point of the extraction workflow |
| `DocumentExtractionRequest.cs` | Record | Input type for `ExtractionExecutor`: `SessionId`, `DocumentText`, `BlobUri` |
| `ComplianceChatWorkflow.cs` | `Workflow` (via `WorkflowBuilder`) | Holds the compiled chat pipeline; `RunAsync` creates an isolated `InProcessExecution.Run` per request |
| `ComplianceExtractionWorkflow.cs` | `Workflow` (via `WorkflowBuilder`) | Holds the compiled extraction pipeline; `ExtractAsync` creates an isolated run per request |

**Data flow:**

```
POST /api/chat
    → ComplianceChatWorkflow.RunAsync(ChatRequest)
        → InProcessExecution.RunAsync(workflow, chatRequest)
            → ChatExecutor.HandleAsync(chatRequest, context)
                → IChatAgentService.ProcessAsync(...)
                    → (if case-creation) IExtractionAgentService.ExtractFromTextAsync(...)
            ← ChatResponse
        ← WorkflowEvent: ExecutorCompletedEvent["ChatAgent"]
    ← ChatResponse

POST /api/upload
    → IDocumentIngestionService.IngestAsync(file)
    → ComplianceExtractionWorkflow.ExtractAsync(DocumentExtractionRequest)
        → InProcessExecution.RunAsync(workflow, documentExtractionRequest)
            → ExtractionExecutor.HandleAsync(request, context)
                → IExtractionAgentService.ExtractFromDocumentAsync(...)
            ← ExtractionResult
        ← WorkflowEvent: ExecutorCompletedEvent["ExtractionAgent"]
    ← UploadResponse
```

---

## Infrastructure/

| File | Role |
|---|---|
| `DbInitializer.cs` | Runs idempotent DDL at startup: creates `dbo.sessions`, `dbo.case_drafts`, `dbo.faq_knowledge_base`, `dbo.audit_log`. Seeds 5 FAQ entries. Only runs when `SqlConnectionString` is configured. |

---

## Configuration files

| File | Purpose |
|---|---|
| `appsettings.json` | Default values; placeholder strings for all Azure config. Safe to commit. |
| `appsettings.Development.json` | Overrides for local dev (e.g. `DetailedErrors: true`). |
| `global.json` (repo root) | Pins SDK to `10.0.203` with `rollForward: latestFeature`. |
| `ComplianceAgent.slnx` (repo root) | Solution file referencing the API project. |
