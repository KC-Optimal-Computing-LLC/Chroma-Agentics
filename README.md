# Chroma Agentics

Chroma Agentics is an independent fork and planned evolution of Roo Code, focused on local-first, orchestrated AI development workflows.

The core architecture aims to preserve Roo Code’s VS Code agent experience while adding:

- A C#/.NET backend service for durable agent execution
- Microsoft Agent Framework 1.0+ for multi-agent orchestration
- PostgreSQL and pgvector for persistent state, memory, and retrieval-augmented generation
- Ollama support for private local model inference
- Optional Next.js dashboards for team and small-business workflows

In later phases, we may evaluate a hybrid orchestration approach using LangGraph for complex stateful business processes and n8n for visual integrations. These are not part of the initial core runtime.

Chroma Agentics is maintained by KC Optimal Computing LLC as part of a broader mission to make practical, private, open-source AI tooling accessible to Kansas City developers and small businesses.

---

## Reader Guide

- **Developers**: Start with Project Status, Core vs Experimental Scope, Getting Started, Near-Term Priorities, and Architecture Decisions.
- **Business / non-technical readers**: Start with Why Chroma Agentics?, Core vs Experimental Scope, and About KC Optimal Computing.
- **Security reviewers**: Start with Security & Privacy Model, Security Roadmap, Maintainer & Support Policy, Versioning, and Non-Goals.

---

## Project Status

**Chroma Agentics is currently in early development.**

This README describes the intended architecture and development direction. Many components are planned or in progress. Features listed below may not yet be available in the repository.

**Current implementation status:**

- [x] Roo Code fork baseline imported
- [ ] Verify full extension build, launch, and baseline behavior
- [ ] C#/.NET backend service shell
- [ ] Ollama provider adapter + streaming
- [ ] PostgreSQL schema + migrations
- [ ] pgvector RAG indexing pipeline
- [ ] Microsoft Agent Framework 1.0+ orchestration integration
- [ ] Extension ↔ Backend API bridge (HTTP/WebSocket)
- [ ] Optional Next.js web dashboard (roadmap)
- [ ] LangGraph + n8n hybrid evaluation (exploratory, later phases)

We will update this status as work progresses. Check the repository issues and commits for the latest implementation state.

---

## What This Is / What This Is Not

### What this is

Chroma Agentics is an early-stage independent fork and planned evolution of Roo Code. Its core goal is to preserve the VS Code-based agent experience while adding a local-first backend runtime for durable orchestration, persistent memory, retrieval, and private model execution.

### What this is not

Chroma Agentics is **not** currently:

- A production-ready agent platform
- A replacement for Roo Code’s core editor experience
- A one-command local deployment
- A bundled LangGraph or n8n distribution
- A guarantee of private operation if cloud model providers are configured
- A tool that removes human approval from sensitive file or terminal actions

---

## Fork Notice

Chroma Agentics is an independent fork and derivative work of Roo Code. It is not officially affiliated with, endorsed by, or sponsored by Roo Code or its original maintainers unless explicitly stated.

- Original Roo Code copyright, license notices, and attribution are preserved in accordance with the Apache License 2.0.
- Chroma Agentics-specific modifications, additions, and branding are maintained by KC Optimal Computing LLC and its contributors.
- All derivative work requirements under Apache 2.0 (license inclusion, NOTICE preservation, marking of changes) are followed.

If you are a contributor to the original Roo Code project and have concerns about attribution or usage, please open an issue — we will review and address them promptly.

---

## Core vs Experimental Scope

| Area                    | Tooling                        | Scope       | Purpose                                                                                                     |
| ----------------------- | ------------------------------ | ----------- | ----------------------------------------------------------------------------------------------------------- |
| Editor agent experience | Roo Code fork                  | Core        | VS Code-based coding workflows and human interaction                                                        |
| Backend runtime         | C# / .NET                      | Core        | Durable service layer and API bridge                                                                        |
| Agent orchestration     | Microsoft Agent Framework 1.0+ | Core        | Multi-agent workflows and durability inside the backend                                                     |
| State and memory        | PostgreSQL + pgvector          | Core        | Agent state, checkpoints, memory, and retrieval                                                             |
| Local inference         | Ollama                         | Core        | Private local model execution                                                                               |
| Business process graphs | LangGraph                      | Exploratory | Complex long-running workflows with branching and pause/resume (later phases)                               |
| Visual integrations     | n8n                            | Exploratory | Connecting business tools and non-developer workflow automation (later phases, subject to licensing review) |
| Web dashboard           | Next.js                        | Roadmap     | Admin, audit, and team interfaces                                                                           |

**Note on n8n**: Integration is exploratory and subject to licensing review (n8n uses a Sustainable Use License with commercial restrictions) before any bundled, redistributed, or commercial deployment model is considered.

---

## Why Chroma Agentics?

Roo Code already delivers a strong in-editor AI agent experience. The goal of this fork is to extend that experience with backend capabilities teams need when moving from experimentation to real workflows:

- Durable, stateful agent execution
- Structured multi-agent orchestration with clear control flow
- Persistent memory and retrieval over codebases and documents
- Local-first operation that respects data control and privacy-sensitive workflows

In later phases we may evaluate LangGraph for complex, long-running business processes that benefit from explicit state machines, branching, and strong human-in-the-loop patterns, and n8n as a visual layer so non-technical users can connect agents to existing business systems.

This layered approach aims to give developers a familiar surface in VS Code, operations teams reliable backend services they can host, and business users practical ways to participate — without forcing one tool to solve every problem.

---

## Key Features (Intended)

### Preserved from Roo Code

- Specialized modes (Code, Architect, Ask, Debug, Custom, Orchestrator)
- Natural language code generation, refactoring, and codebase Q&A
- Checkpoint navigation and granular approval flows
- MCP server integration and terminal tooling (with user approval)
- Webview UI and configuration system

### Planned Core Additions

- Microsoft Agent Framework 1.0+ orchestration (sequential, concurrent, handoff, group collaboration workflows)
- C#/.NET backend service for durable execution and state management
- PostgreSQL + pgvector for conversation state, agent memory, and RAG
- Ollama-first local inference with support for other providers
- Optional Next.js layer for dashboards and non-editor interfaces

### Exploratory (Later Phases)

- LangGraph for complex stateful business graphs
- n8n for visual workflow composition and business system integration

---

## Architecture (Planned)

**Core runtime:**

```
VS Code Extension (Roo fork)
          │
          ▼  (Extension Bridge API — HTTP / WebSocket)
C# Backend Service
  ├── Microsoft Agent Framework 1.0+ (core orchestration)
  ├── PostgreSQL (state, checkpoints, pgvector embeddings)
  └── Model Provider Layer
          │
          ▼
Ollama (default local)  |  Other providers (configurable)
```

**Future / Experimental layers** (evaluated in later phases):

- LangGraph services for specialized long-running business workflows
- n8n for visual workflow integrations

Integration boundaries that still need definition and implementation:

- Extension ↔ Backend communication protocol and auth model
- How file/terminal actions remain under user control
- RAG chunking, embedding model, and retrieval strategy

---

## Architecture Decisions

### Why keep Roo Code as the editor foundation?

Roo Code already provides the in-editor agent surface, approval flows, modes, webview UI, and developer workflow foundation. Chroma Agentics extends that foundation rather than replacing it.

### Why add a C#/.NET backend?

The backend is intended to move durable execution, orchestration, persistence, and provider integration outside the editor process. This creates a cleaner boundary between the VS Code extension and long-running agent workflows.

### Why Microsoft Agent Framework?

Microsoft Agent Framework is the planned core orchestration layer because it fits the .NET backend direction and supports structured multi-agent workflow patterns.

### Why PostgreSQL and pgvector?

PostgreSQL provides durable state, checkpoints, and conversation storage. pgvector adds vector search support for memory and retrieval-augmented generation without requiring a separate vector database in the initial architecture.

### Why Ollama first?

Ollama supports local model execution and aligns with the project’s local-first privacy direction. Other providers may be added through a provider abstraction.

### Why are LangGraph and n8n exploratory?

LangGraph and n8n may be useful for specialized business workflows and visual integrations, but they are not part of the initial core runtime. They will be evaluated only after the core developer runtime is stable.

---

## Security & Privacy Model

Chroma Agentics is designed with a local-first default.

**Default privacy posture:**

- Local model inference via Ollama when configured
- Local PostgreSQL for state and memory
- No mandatory cloud model providers
- Human-in-the-loop approval for file changes and terminal commands (inherited from Roo Code)
- Clear separation between extension UI, backend orchestration, and persistent storage

**Important limitations:**

- Privacy guarantees depend on the model providers you enable. Cloud providers will receive prompts if used.
- Logs, traces, embeddings, and database records may contain sensitive code or business information.
- Users are responsible for securing PostgreSQL instances, backend ports, and Ollama endpoints.
- The backend service will expose APIs; network exposure should be limited to localhost or properly authenticated/trusted networks in production.

A more detailed threat model and hardening guide will be added as the backend implementation matures.

---

## Security Roadmap

Planned security work includes:

- Localhost-only default backend binding
- Explicit configuration for any network-exposed backend mode
- Authentication strategy for extension-to-backend communication
- Clear handling of secrets and provider API keys
- Sensitive log redaction strategy
- Documentation for securing PostgreSQL and Ollama endpoints
- Threat model for local, LAN, and small-business deployment scenarios
- Audit logging for agent actions, tool calls, file changes, and terminal commands

---

## Getting Started

Because the project is in early development, we offer two realistic paths.

### Option A: Work on the Roo Code Extension Layer Only

If you only want to modify the VS Code extension (branding, modes, UI, etc.):

```bash
git clone https://github.com/KC-optimal-computing-llc/ChromaAgentics.git
cd ChromaAgentics
pnpm install
# Build and development commands will be documented once the baseline is verified
```

### Option B: Full Stack (Backend + Services) — Work in Progress

The first backend foundation slice is available under `backend/`. It includes health endpoints, local configuration, PostgreSQL/Ollama dependency status, and a minimal authenticated WebSocket event stream.

Start with `docs/GETTING_STARTED_BACKEND.md`, `.env.example`, and `docker-compose.yml`. Database migrations, durable workflows, model chat, RAG, and extension bridge behavior are still planned work.

**Do not expect a one-command “it just works” experience today.** This is an active development repository.

---

## Known Limitations

Chroma Agentics is in early development. Current limitations include:

- The full backend stack is not implemented yet.
- The extension/backend API contract has a Sprint 1 starter slice but is not finalized.
- PostgreSQL and Ollama dependency health checks exist; pgvector, durable schema, RAG, and Ollama chat are planned but not currently available.
- Microsoft Agent Framework integration is planned but not yet wired into the runtime.
- LangGraph and n8n are exploratory and not part of the initial core runtime.
- Installation instructions are limited until the Roo Code fork baseline is verified.
- Security hardening guidance is not complete yet.

---

## Non-Goals

Chroma Agentics is not intended to:

- Replace the Roo Code user experience with a completely separate editor workflow
- Require hosted/cloud model providers
- Bundle n8n or LangGraph before architecture and licensing review
- Provide production deployment guarantees during early development
- Bypass human approval for sensitive file changes or terminal commands
- Become a general-purpose business automation suite before the core developer runtime is stable

---

## Repository Layout

```text
/
├── src/                    # VS Code extension source (inherited from Roo Code)
├── backend/                # Planned C#/.NET backend service
├── db/                     # Planned PostgreSQL migrations and schema
├── frontend/               # Optional future Next.js dashboard (roadmap)
├── docs/                   # Architecture, setup, and deployment documentation
└── .env.example            # Backend local development configuration
```

Some folders may not exist yet while the project is in early development.

---

## Documentation Roadmap

Planned documentation includes:

- `docs/GETTING_STARTED.md` — local setup and development workflow
- `docs/ARCHITECTURE.md` — system architecture and integration boundaries
- `docs/API_CONTRACT.md` — extension/backend API design
- `docs/SECURITY.md` — threat model and hardening guide
- `docs/MEMORY_AND_RAG.md` — PostgreSQL, pgvector, embeddings, and retrieval design
- `docs/MODEL_PROVIDERS.md` — Ollama and future provider abstraction
- `docs/TESTING.md` — extension, backend, integration, and security testing strategy
- `docs/ROADMAP.md` — detailed milestone tracking
- `docs/ADRs/` — architecture decision records

---

## Near-Term Priorities

Current contribution priorities:

- Verify and stabilize the Roo Code fork baseline (build, launch, core behavior)
- Define baseline test commands for the inherited extension layer
- Define the Extension ↔ Backend API contract
- Create the C# backend skeleton
- Add local development configuration (.env.example, docker-compose)
- Draft the PostgreSQL schema for state, checkpoints, and memory

These are the best starting points for new contributors. Planned issue labels include:

- `good first issue`
- `documentation`
- `fork-baseline`
- `backend`
- `api-contract`
- `database`
- `security`
- `architecture`
- `exploratory`

---

## Tech Stack & Implementation State

| Layer                        | Technology                            | Scope       | Implementation State           |
| ---------------------------- | ------------------------------------- | ----------- | ------------------------------ |
| Editor Integration           | TypeScript, VS Code APIs, pnpm        | Core        | Imported, pending verification |
| Orchestration                | Microsoft Agent Framework 1.0+ (.NET) | Core        | Planned                        |
| Backend                      | C# / .NET 8+                          | Core        | Planned                        |
| Persistence & RAG            | PostgreSQL + pgvector                 | Core        | Planned                        |
| Inference                    | Ollama (primary), pluggable providers | Core        | Planned                        |
| Observability                | OpenTelemetry-first                   | Core        | Planned                        |
| LangGraph experiment tracing | LangSmith optional                    | Exploratory | Exploratory                    |
| Optional Web Layer           | Next.js / React                       | Roadmap     | Roadmap                        |
| Advanced Business Logic      | LangGraph                             | Exploratory | Exploratory                    |
| Visual Integration           | n8n                                   | Exploratory | Exploratory                    |

---

## Roadmap

### Phase 1: Fork & Baseline (Current focus)

- Preserve Roo Code extension functionality and build process
- Update branding, metadata, and extension identity
- Add Fork Notice and license compliance documentation
- Establish contribution and code of conduct processes

### Phase 2: Backend Foundation

- C#/.NET backend service skeleton
- Extension ↔ Backend API definition (HTTP/WebSocket)
- Basic streaming of agent events
- Local development auth/binding

### Phase 3: Model Provider Layer

- Ollama chat adapter with streaming
- Local model discovery and capability metadata
- Provider abstraction for future cloud adapters

### Phase 4: Persistence & Memory

- PostgreSQL schema and Entity Framework migrations
- Conversation and agent state storage
- pgvector embeddings table and basic retrieval pipeline

### Phase 5: Microsoft Agent Framework Integration + Hybrid Exploration

- Sequential and concurrent workflow support via MAF
- Handoff and group collaboration patterns
- Begin evaluation of LangGraph for complex stateful business graphs
- Explore n8n as visual orchestration layer (subject to licensing review)

### Phase 6: Team & Business Layer (Later)

- Optional Next.js dashboard
- Basic admin / audit views
- Documentation and deployment guides aimed at KC small business teams

---

## Phase Exit Criteria

### Phase 1: Fork & Baseline is complete when

- The extension installs dependencies successfully.
- The extension builds without unexpected errors.
- A development host launches in VS Code.
- Core inherited Roo Code behaviors are verified.
- Branding and metadata changes are complete.
- License, NOTICE, fork attribution, and basic contribution docs exist.

### Phase 2: Backend Foundation is complete when

- The C#/.NET backend starts locally.
- A health endpoint is available.
- The extension can connect to the backend.
- Basic event streaming is demonstrated.
- Localhost binding and development auth behavior are documented.
- Backend health endpoint is covered by an automated test.

### Phase 3: Model Provider Layer is complete when

- Ollama connectivity is implemented.
- Streaming model responses work through the backend.
- Local model discovery is supported.
- Provider capability metadata is documented.

### Phase 4: Persistence & Memory is complete when

- PostgreSQL schema and migrations exist.
- Agent state and conversation state can be persisted.
- pgvector is enabled for embeddings.
- A basic retrieval pipeline is demonstrated.

### Phase 5: Microsoft Agent Framework integration is complete when

- Sequential workflow orchestration is demonstrated.
- Concurrent workflow orchestration is demonstrated.
- Handoff workflow behavior is demonstrated.
- Human-in-the-loop checkpoints are wired into the approval model.
- LangGraph and n8n evaluation outcomes are documented separately from the core runtime.

---

## Contributing

We welcome developers who want to help build a local-first agent platform rather than another thin wrapper around hosted models.

Please read [CONTRIBUTING.md](CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

We especially value contributions that improve documentation, add tests, clarify integration boundaries, or help make the system easier for smaller teams to run on their own infrastructure.

---

## Required Repository Files

Before broader contribution, the repository should include the following files to support contribution and security:

- `LICENSE`
- `NOTICE`
- `CONTRIBUTING.md`
- `CODE_OF_CONDUCT.md`
- `SECURITY.md`

Backend local development files (`.env.example`, `docker-compose.yml`) now exist for the Sprint 1 foundation slice. Issue templates should be added before broader contribution begins.

---

## License & Attribution

This project is licensed under the Apache License 2.0, inherited from the Roo Code base.

See [LICENSE](LICENSE) for the full license text.

As an independent fork and derivative work, Chroma Agentics will preserve applicable upstream copyright notices, license notices, and NOTICE file contents. Chroma Agentics-specific modifications, additions, and branding are maintained by KC Optimal Computing LLC and contributors.

Third-party integrations may have separate licenses. In particular, n8n integration is exploratory and subject to licensing review before any bundled, redistributed, or commercial deployment model is considered.

---

## Maintainer & Support Policy

**Chroma Agentics is maintained by KC Optimal Computing LLC.**

During early development:

- GitHub Issues are the primary channel for bug reports, feature requests, and questions.
- No production support or SLA is provided.
- Security vulnerabilities should be reported privately following the process in [SECURITY.md](SECURITY.md).
- We aim to respond to issues in a reasonable timeframe but cannot guarantee immediate resolution while the project is in active early development.

---

## Versioning

Chroma Agentics does not yet have a stable public release.

- APIs, configuration formats, database schemas, and documentation are subject to change.
- Breaking changes may occur without a major version bump during the early development phase.
- Stable versioning policy and semantic versioning expectations will be documented before the first public release.

---

## About KC Optimal Computing

KC Optimal Computing is a Kansas City, Missouri company building practical AI and automation tools for local small businesses and developers.

Our focus is on open-source models, private/local deployment options (on-site servers or secure cloud), and interfaces that non-technical users can actually use after reasonable training.

Chroma Agentics is one expression of that mission: extending a strong existing tool (Roo Code) with the backend capabilities needed for more serious workflows, while keeping control in the hands of the people running it.

"We're here to help KC. That's the mission."

More about our work: https://kcoptimal.com

---

## Acknowledgments

- The Roo Code team and community for building a genuinely useful in-editor agent platform and open-sourcing it.
- Microsoft for Microsoft Agent Framework and continued .NET AI investment.
- The PostgreSQL, pgvector, Ollama, Next.js, LangGraph, and n8n communities.
- Early reviewers and contributors who help keep this honest and buildable.

---

_Chroma Agentics — Building a credible, local-first agentic development platform on top of a proven foundation._

For questions, issues, or involvement in the Kansas City AI tooling community, open a GitHub issue.
