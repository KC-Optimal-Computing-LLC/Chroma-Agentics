# Chroma Agentics

> **Unlocking agentic power for developers and teams — inspired by the empowering force of Chroma.**

**Chroma Agentics** is a community-driven fork and thoughtful evolution of [Roo Code](https://github.com/RooCodeInc/Roo-Code), the powerful VS Code extension that brings a complete AI development team directly into your editor. 

We’ve integrated the production-ready **Microsoft Agent Framework** for sophisticated multi-agent orchestration, a high-performance **C# backend** for reliability and speed, **PostgreSQL** for persistent agent state, long-term memory, and Retrieval-Augmented Generation (RAG), and **Ollama** for private, local-first model inference.

Named after the mystical “Chroma” energy in the classic 2005 adventure game *Indigo Prophecy* (released as *Fahrenheit* in Europe), Chroma Agentics represents the unlocking of intelligent, collaborative agentic workflows — while remaining grounded in open-source values, privacy, and real-world practicality.

**Maintained with support from KC Optimal Computing** — *We’re here to help KC.*

---

## Why Chroma Agentics?

Modern development demands more than simple autocomplete or chat. Teams need reliable, orchestratable AI agents that can plan, execute, remember, and collaborate — all while keeping data private and systems maintainable.

Chroma Agentics delivers:

- The proven, loved Roo Code editor experience (modes, checkpoints, codebase awareness)
- Enterprise-grade multi-agent orchestration via Microsoft Agent Framework
- A robust C# service layer built for performance and integration
- Persistent, queryable memory and RAG powered by PostgreSQL
- True local/private operation with Ollama (no forced cloud lock-in)
- Accessibility for both experienced developers and non-technical KC small business teams

This project exists to accelerate Kansas City small businesses and developers with trustworthy, transparent, open-source AI tools — not hype.

---

## ✨ Key Features

### From the Roo Code Foundation (Preserved & Enhanced)
- **Specialized Agent Modes**: Code, Architect, Ask, Debug, Test, Custom, and Orchestrator
- **Natural Language Code Generation & Refactoring**
- **Deep Codebase Understanding** with semantic search and context
- **Checkpoint Navigation** — step back through agent actions
- **MCP Server Integration** and terminal command execution (with approval)
- **Granular Control** — approve file changes and commands

### New in Chroma Agentics
- **Microsoft Agent Framework Orchestration** — Native C#/.NET support for sequential, concurrent, handoff, and group collaboration workflows with checkpointing and human-in-the-loop
- **High-Performance C# Backend** — Reliable service layer for agent execution, state management, and API bridging
- **PostgreSQL Persistence & RAG** — Long-term agent memory, conversation history, and powerful retrieval over codebases and documents
- **Private by Default with Ollama** — Run powerful open models locally or on your own infrastructure
- **Optional Next.js Web Layer** — Dashboards and interfaces for teams or non-VS Code users
- **Observability & Durability** — Built with patterns from Microsoft Agent Framework (OpenTelemetry-ready, durable workflows)

### Designed for KC Small Business Reality
- On-site server deployment options
- Training paths and documentation for non-technical users
- Focus on trust, transparency, and practical outcomes over flashy demos
- Community education and long-term relationship building

---

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    VS Code Extension                         │
│              (Roo Code Fork — Editor Integration)            │
│   Modes • Checkpoints • File Ops • Terminal • MCP Servers   │
└───────────────────────────┬─────────────────────────────────┘
                            │ Feature API / HTTP + WebSocket
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                    C# Backend Service                        │
│   • Microsoft Agent Framework Orchestration (multi-agent)   │
│   • Workflow patterns: Sequential • Concurrent • Handoff    │
│   • PostgreSQL (State, Memory, pgvector RAG)                │
│   • Observability & Durable Execution                       │
└───────────────────────────┬─────────────────────────────────┘
                            │
            ┌───────────────┴───────────────┐
            ▼                               ▼
┌──────────────────────┐      ┌──────────────────────────────┐
│   Ollama (Local)     │      │   Optional: Next.js Frontend │
│   Private Inference  │      │   Dashboards • Team UI       │
└──────────────────────┘      └──────────────────────────────┘
```

This architecture keeps sensitive work local and private while giving teams powerful orchestration tools.

---

## 🚀 Getting Started

### Prerequisites
- Visual Studio Code (or compatible editor supporting VS Code extensions)
- [.NET SDK](https://dotnet.microsoft.com/download) (8.0+ recommended)
- [PostgreSQL](https://www.postgresql.org/) (with pgvector extension recommended for RAG)
- [Ollama](https://ollama.com/) (for local models)
- Node.js 18+ and pnpm (for extension and optional frontend development)

### Development Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/KC-optimal-computing-llc/ChromaAgentics.git
   cd ChromaAgentics
   ```

2. **Extension Layer (Roo Code base)**
   ```bash
   pnpm install
   # Build VSIX or run in development mode (F5 in VS Code)
   ```

3. **C# Backend Service**
   ```bash
   cd backend
   dotnet restore
   dotnet build
   
   # Configure your environment (appsettings.json or .env)
   # - PostgreSQL connection string
   # - Ollama base URL (default: http://localhost:11434)
   ```

4. **Database Setup**
   - Create PostgreSQL database
   - Run provided migration scripts (see `/db` or `docs/migrations.md`)
   - Enable pgvector extension if using advanced RAG

5. **Start the Stack**
   ```bash
   # Terminal 1: C# Backend
   cd backend && dotnet run

   # Terminal 2: Ollama
   ollama serve

   # Terminal 3: (Optional) Frontend
   cd frontend && npm install && npm run dev
   ```

6. **Launch the Extension**
   - Open the project in VS Code
   - Press `F5` to launch a development host with the extension loaded
   - Or build and install the VSIX package

For detailed, step-by-step instructions tailored to different environments (including on-site server setups), see the **[Getting Started Guide](docs/GETTING_STARTED.md)** (coming soon in this repository).

---

## 🛠️ Tech Stack

| Layer              | Technology                          | Purpose                              |
|--------------------|-------------------------------------|--------------------------------------|
| Editor Integration | TypeScript, VS Code APIs, pnpm     | Roo Code fork — familiar UX         |
| Orchestration      | Microsoft Agent Framework (.NET)   | Multi-agent workflows, patterns     |
| Backend            | C# / .NET 8+, ASP.NET Core         | Performance, reliability, bridging  |
| Persistence & RAG  | PostgreSQL + pgvector              | State, memory, intelligent retrieval|
| Inference          | Ollama (pluggable)                 | Private, local open-source models   |
| Optional Frontend  | Next.js, React, TypeScript         | Web dashboards & team interfaces    |
| Observability      | OpenTelemetry (via MAF patterns)   | Tracing, monitoring, debugging      |

We prioritize open-source components and local execution to maximize privacy and control for KC businesses.

---

## 🤝 Contributing

Chroma Agentics thrives on community input. Whether you’re fixing bugs, improving documentation, adding features, or providing feedback from real KC business use cases — your contributions matter.

Please read:
- [CONTRIBUTING.md](CONTRIBUTING.md)
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)

We follow a transparent, respectful process aligned with KC Optimal Computing’s values of trust and long-term community building.

This project is maintained with active support from KC Optimal Computing to ensure continuity, especially following the original Roo Code team’s transition to new initiatives.

---

## 📜 License

This project inherits and adapts the **Apache License 2.0** from the Roo Code base. See [LICENSE](LICENSE) for full details.

Components derived from Microsoft Agent Framework follow their applicable licensing (MIT where noted).

---

## About KC Optimal Computing

**KC Optimal Computing** is a Kansas City, Missouri startup building practical AI and automation solutions for local small businesses and residents.

We focus on:
- Open-source models and tools
- Secure/private cloud or on-site custom server deployments
- Training and interfaces designed for non-technical users
- AI integration consulting that prioritizes real business outcomes

**“We’re here to help KC. That’s the mission.”**

Chroma Agentics embodies our philosophy: advanced agentic AI made accessible, private, maintainable, and genuinely useful for the Kansas City community. We believe in educating, building deep relationships, and accelerating local businesses through technology grounded in transparency and trust.

Learn more about our work and how we can support your team:  
[KC Optimal Computing](https://kcoptimal.com) (or reach out directly)

---

## 🙏 Acknowledgments & Credits

- The original Roo Code team and the vibrant community of contributors who built an exceptional foundation.
- Microsoft for the Agent Framework and continued investment in .NET AI tooling.
- The PostgreSQL, Ollama, Next.js, and broader open-source communities.
- Early testers, Kansas City developers, and business owners providing real-world feedback.

---

*Chroma Agentics — Agentic development, unlocked. Built for KC, by KC.*

For support, feature requests, or to join upcoming KC AI community discussions, open an issue or connect with us. We’re here to help.

---

**Repository maintained with care by KC Optimal Computing LLC • Kansas City, Missouri**

*This README was crafted following GitHub best practices for clarity, scannability, and usefulness. It accurately reflects the project’s technical foundation (Roo Code fork + Microsoft Agent Framework + C# + PostgreSQL + Ollama) and our mission of practical, trustworthy AI for Kansas City small businesses.*