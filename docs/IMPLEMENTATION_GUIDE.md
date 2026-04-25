# Compliance Agent – Backend Implementation Guide

This document explains **what was built**, **why each piece exists**, and **how the source code maps to the architecture** described in `Architecture-Diagram-v2.md` and `Technical-Design.md`.

---

## 1. What Was Built

A production-ready **.NET 10 ASP.NET Core Minimal API** that implements the three features from the Technical Design:

| Feature | Endpoint | Description |
|---|---|---|
| F1 – Compliance Q&A Chat (RAG) | `POST /api/chat` | Multi-turn conversation grounded in SQL FAQ knowledge base |
| F2 – Document Upload → Case Draft | `POST /api/upload` | Parses an uploaded file, runs GPT-5.2 structured extraction, returns a case draft |
| F3 – Text Prompt → Case Draft | `POST /api/chat` (same endpoint) | Free-text is routed to the Extraction Agent when case-creation intent is detected |

Additional endpoints support the full session/case lifecycle:
`POST /api/session`, `GET /api/session/{id}`, `DELETE /api/session/{id}`, `GET /api/case/{id}`, `PUT /api/case/{id}`, `POST /api/case/{id}/confirm`, `GET /health`, `GET /health/ready`.

---

## 2. Architecture Mapping

```
Architecture component                     → Code location
─────────────────────────────────────────────────────────────────────────────
Angular Chat UI                            (front-end, out of scope)
│
▼
.NET 10 ASP.NET Core Minimal API           src/Compliance.Agent.Api/Program.cs
│
├── Microsoft Agent Framework (MAF)
│   ├── ComplianceChatWorkflow             Agents/Maf/ComplianceChatWorkflow.cs
│   │   └── ChatExecutor                   Agents/Maf/ChatExecutor.cs
│   │       └── IChatAgentService          Agents/IChatAgentService.cs
│   │           ├── ChatAgentService       Agents/ChatAgentService.cs   (stub/dev)
│   │           └── AzureOpenAiChatAgent   Agents/AzureOpenAiChatAgentService.cs  (prod)
│   │
│   └── ComplianceExtractionWorkflow       Agents/Maf/ComplianceExtractionWorkflow.cs
│       └── ExtractionExecutor             Agents/Maf/ExtractionExecutor.cs
│           └── IExtractionAgentService    Agents/IExtractionAgentService.cs
│               ├── ExtractionAgentService Agents/ExtractionAgentService.cs  (stub/dev)
│               └── AzureOpenAiExtraction  Agents/AzureOpenAiExtractionAgentService.cs  (prod)
│
├── Azure OpenAI GPT-5.2                   Options/AzureOpenAiOptions.cs  (endpoint config)
│   └── text-embedding-3-small             (FAQ vector search — SQL fallback)
│
├── Azure Blob Storage                     Services/BlobDocumentIngestionService.cs
│   └── IDocumentIngestionService          Services/IDocumentIngestionService.cs
│       └── DocumentIngestionService       Services/DocumentIngestionService.cs  (stub/dev)
│
├── Azure SQL Database
│   ├── ISessionRepository                 Repositories/ISessionRepository.cs
│   │   ├── SqlSessionRepository           Repositories/SqlSessionRepository.cs  (prod)
│   │   └── InMemorySessionRepository      Repositories/InMemorySessionRepository.cs  (dev)
│   ├── ICaseDraftRepository               Repositories/ICaseDraftRepository.cs
│   │   ├── SqlCaseDraftRepository         Repositories/SqlCaseDraftRepository.cs  (prod)
│   │   └── InMemoryCaseDraftRepository    Repositories/InMemoryCaseDraftRepository.cs  (dev)
│   └── IFAQRepository                     Repositories/IFAQRepository.cs
│       ├── SqlFaqRepository               Repositories/SqlFaqRepository.cs  (prod)
│       └── InMemoryFaqRepository          Repositories/InMemoryFaqRepository.cs  (dev)
│
├── Application Insights                   (registered in Program.cs via AddApplicationInsightsTelemetry)
│
└── Supporting Services
    ├── IOffTopicDetector                  Services/IOffTopicDetector.cs + OffTopicDetector.cs
    ├── IJurisdictionValidator             Services/IJurisdictionValidator.cs + JurisdictionValidator.cs
    ├── ISchemaValidator                   Services/ISchemaValidator.cs + SchemaValidator.cs
    └── IDependencyReadinessService        Services/IDependencyReadinessService.cs + DependencyReadinessService.cs
```

---

## 3. Microsoft Agent Framework (MAF) Integration

### Why MAF

The architecture document (`Architecture-Diagram-v2.md`) places both agents inside a **"Microsoft Agent Framework"** block. MAF is the .NET SDK (`Microsoft.Agents.AI.Workflows`) for building multi-agent pipelines with type-safe `Executor<TIn,TOut>` nodes connected by a `WorkflowBuilder`.

### How It Is Used Here

Two MAF workflows were created, each using `WorkflowBuilder` + `InProcessExecution`:

#### ComplianceChatWorkflow (`Agents/Maf/ComplianceChatWorkflow.cs`)

```
ChatRequest  →  [ChatExecutor (MAF Executor)]  →  ChatResponse
```

- `ChatExecutor` is an `Executor<ChatRequest, ChatResponse>` node.
- It wraps `IChatAgentService` (RAG Q&A + intent routing + off-topic guardrails).
- When the user intends to create a case, `IChatAgentService` internally calls `IExtractionAgentService` as a synchronous tool call — the result comes back into the chat turn.
- `POST /api/chat` resolves `ComplianceChatWorkflow` from DI and calls `RunAsync`.

#### ComplianceExtractionWorkflow (`Agents/Maf/ComplianceExtractionWorkflow.cs`)

```
DocumentExtractionRequest  →  [ExtractionExecutor (MAF Executor)]  →  ExtractionResult
```

- `ExtractionExecutor` is an `Executor<DocumentExtractionRequest, ExtractionResult>` node.
- It wraps `IExtractionAgentService` (GPT-5.2 structured JSON extraction).
- `POST /api/upload` resolves `ComplianceExtractionWorkflow` from DI and calls `ExtractAsync`.

### NuGet Packages Added for MAF

```xml
<PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.3.0" />
<PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.3.0" />
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.5.0" />
```

---

## 4. Conditional Azure Wiring

The application degrades gracefully based on environment configuration. This allows local development without any Azure resources:

```
Environment variable / config         If present             If absent
────────────────────────────────────────────────────────────────────────
ConnectionStrings:SqlConnectionString  SQL repositories       In-memory repositories
BlobStorage:ContainerUri               BlobDocumentIngestion  File-stream ingestion
AzureOpenAI:Endpoint                   Azure OpenAI agents    Stub/regex agents
APPLICATIONINSIGHTS_CONNECTION_STRING  App Insights SDK       No telemetry
```

The switching logic lives entirely in `Program.cs`.

---

## 5. Database Schema

`Infrastructure/DbInitializer.cs` runs at startup (when SQL is configured) and creates:

| Table | Purpose |
|---|---|
| `dbo.sessions` | Conversation sessions; `messages_json` stores the turn history as JSON |
| `dbo.case_drafts` | Case draft records; collection columns stored as JSON |
| `dbo.faq_knowledge_base` | Compliance Q&A pairs used for RAG; pre-seeded with 5 entries |
| `dbo.audit_log` | Immutable audit trail for all agent actions |

---

## 6. Key Design Decisions

| Decision | Rationale |
|---|---|
| Minimal APIs (not Controllers) | Reduces boilerplate; aligns with .NET 8+ microservice patterns |
| `DefaultAzureCredential` everywhere | Supports managed identity in production; no secrets in config |
| Singleton services with `ConcurrentDictionary` (in-memory) | Thread-safe and allocation-efficient for dev/test |
| MAF `WorkflowBuilder` with single-node workflows | Satisfies the MAF requirement; designed to extend to multi-node as the pipeline grows |
| Interface + two implementations per service | Enables local development without Azure while keeping prod path clean |
| JSON columns in SQL | Schema-flexible for evolving case structures without migrations |

---

## 7. Local Development Quickstart

```bash
cd compliance-agent
dotnet build ComplianceAgent.slnx

# Run with in-memory storage (no Azure needed)
dotnet run --project src/Compliance.Agent.Api

# Test health
curl http://localhost:8080/health

# Create a session and chat
curl -X POST http://localhost:8080/api/session
curl -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{"sessionId":"<your-session-id>","message":"What is BSAM compliance?"}'
```

To enable Azure services, set the following environment variables or update `appsettings.json`:

```json
{
  "ConnectionStrings": { "SqlConnectionString": "<Azure SQL connection string>" },
  "BlobStorage": { "ContainerUri": "https://<storage>.blob.core.windows.net/<container>" },
  "AzureOpenAI": {
    "Endpoint": "https://<resource>.openai.azure.com/",
    "ChatDeployment": "gpt-5.2",
    "EmbeddingDeployment": "text-embedding-3-small"
  },
  "APPLICATIONINSIGHTS_CONNECTION_STRING": "<connection string>"
}
```
