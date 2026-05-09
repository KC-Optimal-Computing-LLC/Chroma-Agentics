# Chroma Agentics

Chroma Agentics is an independent fork and planned evolution of Roo Code, focused on local-first, orchestrated AI development workflows.

The project aims to preserve Roo Code’s VS Code agent experience while adding:

- A C#/.NET backend service for durable agent execution
- Microsoft Agent Framework 1.0+ for multi-agent orchestration
- PostgreSQL and pgvector for persistent state, memory, and retrieval-augmented generation
- Ollama support for private local model inference
- Optional Next.js dashboards for team and small-business workflows

Chroma Agentics is maintained by KC Optimal Computing LLC as part of a broader mission to make practical, private, open-source AI tooling accessible to Kansas City developers and small businesses.

---

## Project Status

**Chroma Agentics is currently in early development.**

This README describes the intended architecture and development direction. Many components are planned or in progress. Features listed below may not yet be available in the repository.

Current status (as of May 2026):

- [x] Roo Code fork baseline (extension layer preserved)
- [ ] C#/.NET backend service shell
- [ ] Ollama provider adapter + streaming
- [ ] PostgreSQL schema + migrations
- [ ] pgvector RAG indexing pipeline
- [ ] Microsoft Agent Framework 1.0+ orchestration integration
- [ ] Extension ↔ Backend API bridge (HTTP/WebSocket)
- [ ] Optional Next.js web dashboard (roadmap)

We will update this status as work progresses. Check the repository issues and commits for the latest implementation state.

---

## Fork Notice

Chroma Agentics is an independent fork and derivative work of Roo Code. It is not officially affiliated with, endorsed by, or sponsored by Roo Code or its original maintainers unless explicitly stated.

- Original Roo Code copyright, license notices, and attribution are preserved in accordance with the Apache License 2.0.
- Chroma Agentics-specific modifications, additions, and branding are maintained by KC Optimal Computing LLC and its contributors.
- All derivative work requirements under Apache 2.0 (license inclusion, NOTICE preservation, marking of changes) are followed.

If you are a contributor to the original Roo Code project and have concerns about attribution or usage, please open an issue — we take compliance seriously.

---

## Why Chroma Agentics?

Roo Code already delivers a strong in-editor AI agent experience. The goal of this fork is to extend that experience with production-oriented backend capabilities that many teams need as they move from experimentation to real workflows:

- Durable, stateful agent execution outside the editor process
- Structured multi-agent orchestration patterns
- Persistent memory and retrieval over codebases and documents
- Local-first operation that respects data sovereignty

This direction aligns with KC Optimal Computing’s focus on practical tools that Kansas City small businesses and developers can actually run and maintain.

---

## Key Features (Intended)

### Preserved from Roo Code
- Specialized modes (Code, Architect, Ask, Debug, Custom, Orchestrator)
- Natural language code generation, refactoring, and codebase Q&A
- Checkpoint navigation and granular approval flows
- MCP server integration and terminal tooling (with user approval)
- Webview UI and configuration system

### Planned Additions
- Microsoft Agent Framework 1.0+ orchestration (sequential, concurrent, handoff, group collaboration workflows)
- C#/.NET backend service for durable execution and state management
- PostgreSQL + pgvector for conversation state, agent memory, and RAG
- Ollama-first local inference with support for other providers
- Optional Next.js layer for dashboards and non-editor interfaces

---

## Architecture (Planned)

```
VS Code Extension (Roo fork)
          │
          ▼  (Extension Bridge API — HTTP / WebSocket)
C# Backend Service
  ├── Microsoft Agent Framework 1.0+ (Workflow Engine)
  ├── PostgreSQL (state, checkpoints, pgvector embeddings)
  └── Model Provider Layer
          │
          ▼
Ollama (default local)  |  Other providers (configurable)
```

Integration boundaries that still need definition and implementation:
- Extension ↔ Backend communication protocol and auth model
- How file/terminal actions remain under user control
- RAG chunking, embedding model, and retrieval strategy
- Model provider abstraction and capability detection

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

## Getting Started

Because the project is in early development, we offer two realistic paths.

### Option A: Work on the Roo Code Extension Layer Only

If you only want to modify the VS Code extension (branding, modes, UI, etc.):

```bash
git clone https://github.com/KC-optimal-computing-llc/ChromaAgentics.git
cd ChromaAgentics
pnpm install
# Follow original Roo Code development instructions (F5 in VS Code, etc.)
```

### Option B: Full Stack (Backend + Services) — Work in Progress

The backend, database, and orchestration layers are still being built. Current steps are exploratory:

1. Clone the repository
2. Set up PostgreSQL (docker compose or local)
3. Configure environment (see `.env.example` — will be added)
4. Build and run the C# backend shell
5. Run Ollama and pull base models
6. Test extension-to-backend connectivity (once the bridge exists)

We will publish a working `docker-compose.yml`, `.env.example`, and step-by-step guide once the backend shell and database migrations are in the repository.

**Do not expect a one-command “it just works” experience today.** This is an active development repository.

---

## Tech Stack (Planned)

| Layer                  | Technology                              | Status      |
|------------------------|-----------------------------------------|-------------|
| Editor Integration     | TypeScript, VS Code APIs, pnpm         | Inherited from Roo Code |
| Orchestration          | Microsoft Agent Framework 1.0+ (.NET)  | Planned     |
| Backend                | C# / .NET 8+                           | Planned     |
| Persistence & RAG      | PostgreSQL + pgvector                  | Planned     |
| Inference              | Ollama (primary), pluggable providers  | Planned     |
| Optional Web Layer     | Next.js / React                        | Roadmap     |
| Observability          | OpenTelemetry patterns                 | Planned     |

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

### Phase 5: Microsoft Agent Framework Integration
- Sequential and concurrent workflow support
- Handoff and group collaboration patterns
- Human-in-the-loop checkpoints
- Durable execution where appropriate

### Phase 6: Team & Business Layer (Later)
- Optional Next.js dashboard
- Basic admin / audit views
- Documentation and deployment guides aimed at KC small business teams

---

## Contributing

We welcome developers who want to work on a real, local-first agent platform rather than another wrapper around hosted models.

Please read:
- [CONTRIBUTING.md](CONTRIBUTING.md) (to be added)
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) (to be added)

We especially value contributions that improve documentation, add tests, clarify integration boundaries, or help make the system easier for smaller teams to run on their own infrastructure.

---

## License & Attribution

This project is licensed under the Apache License 2.0, inherited from the Roo Code base.

See [LICENSE](LICENSE) for the full text.

We follow Apache 2.0 requirements for derivative works: preserving original notices, including the license, and clearly marking our own modifications.

---

## About KC Optimal Computing

KC Optimal Computing is a Kansas City, Missouri company building practical AI and automation tools for local small businesses and developers.

Our focus is on open-source models, private/local deployment options (on-site servers or secure cloud), and interfaces that non-technical users can actually use after reasonable training.

Chroma Agentics is one expression of that mission: extending a strong existing tool (Roo Code) with the backend capabilities needed for more serious workflows, while keeping control in the hands of the people running it.

"We're here to help KC. That's the mission."

More about our work: https://kcoptimal.com (or contact us directly).

---

## Acknowledgments

- The Roo Code team and community for building a genuinely useful in-editor agent platform and open-sourcing it.
- Microsoft for Microsoft Agent Framework and continued .NET AI investment.
- The PostgreSQL, pgvector, Ollama, and Next.js communities.
- Early reviewers and contributors who help keep this honest and buildable.

---

*Chroma Agentics — Building a credible, local-first agentic development platform on top of a proven foundation.*

For questions, issues, or to get involved in the Kansas City AI tooling community, open an issue or reach out. We’re here to help.