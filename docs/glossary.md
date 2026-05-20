# Glossary & Concepts

A living reference of terms, patterns, and tools encountered while building this project. Grouped by topic, alphabetical within each group. Each entry has a **definition** and a **why it matters here** so the meaning sticks to the project rather than floating abstractly.

> **Convention:** when a concept is introduced in conversation or in code, add it here in the same PR. If a term appears in this file but you can't recall what it means in this project's context, that's a bug — improve the entry.

---

## Table of Contents
- [Architecture Patterns](#architecture-patterns)
- [Microservices Concepts](#microservices-concepts)
- [.NET & Blazor](#net--blazor)
- [Messaging & Events](#messaging--events)
- [Data & Storage](#data--storage)
- [Observability](#observability)
- [Infrastructure & Platform](#infrastructure--platform)
- [Security & Identity](#security--identity)
- [Source Control & CI/CD (GitHub)](#source-control--cicd-github)
- [Domain Concepts (Personal Finance)](#domain-concepts-personal-finance)

---

## Architecture Patterns

### BFF (Backend for Frontend)
A backend service that exists specifically to serve one frontend. It aggregates calls to downstream services, shapes responses for the UI, and holds sensitive things (like OAuth tokens) the browser shouldn't see.
**Why it matters here:** Our Blazor WASM client talks only to the BFF; the BFF talks to the domain services. This keeps the UI ignorant of the microservices fan-out and lets us evolve services without breaking the client.

### Event-Driven Architecture
A style where services communicate by publishing and consuming events rather than calling each other synchronously.
**Why it matters here:** A new transaction triggers a chain — categorization, valuation update, net-worth recompute — none of which the ingestion service has to know about. Each consumer subscribes independently.

### Outbox Pattern
To publish an event reliably when you also write to your database, you insert the event into an `outbox` table in the *same DB transaction* as the business write. A separate relay process reads the outbox and publishes to the bus.
**Why it matters here:** Without this, a crash between "DB commit" and "publish event" causes silent inconsistency (e.g., a transaction exists but net worth never updates).

### Saga Pattern
A way to coordinate a multi-step workflow across services without distributed transactions. Each step has a compensating action that runs if a later step fails.
**Why it matters here:** Useful for things like "reconcile this month" that touch transaction, valuation, and net-worth services and need an all-or-nothing feel without 2-phase commit.

### Service Boundary
The line between what one service owns vs. another. Drawn around a *bounded context* (a coherent piece of the domain — accounts, transactions, valuations).
**Why it matters here:** Drawing these wrong is the #1 way microservices projects fail. Our boundaries follow the domain (Account, Transaction, Valuation, NetWorth, Credit), not technical layers.

---

## Microservices Concepts

### Backpressure
When downstream consumers can't keep up, the system needs a way to slow producers (or buffer/drop) instead of melting down. Messaging systems give you this for free if you use them right.
**Why it matters here:** If categorization is slow, transactions pile up in NATS — that's fine because NATS JetStream persists them. The system degrades gracefully instead of dropping data.

### Eventual Consistency
Different services see updates at slightly different times. After all events are processed, they converge.
**Why it matters here:** When you add a transaction, your net worth may lag by a second or two. The UI must show this gracefully (loading states, "as of" timestamps) rather than pretending writes are instant everywhere.

### Idempotency
An operation can be safely retried without causing duplicate effects. Usually achieved via an idempotency key the server remembers.
**Why it matters here:** Plaid will replay webhooks. Our transaction service must not double-count when the same event arrives twice.

### Independent Deployability
Each service can be built, tested, and deployed without coordinating with others.
**Why it matters here:** This is the actual *point* of microservices. Path-filtered GitHub Actions workflows give us this — editing the Plaid connector doesn't rebuild the Blazor client.

### Polyglot
Services written in different languages, talking via a common wire protocol.
**Why it matters here:** Most services will be C# (so we can share contracts with Blazor); one or two will be Go or Python deliberately, to learn how gRPC + Protobuf bridges languages.

### Schema Evolution
Changing the shape of an event or API in a way that older consumers still understand.
**Why it matters here:** Protobuf's "never reuse field numbers, never remove required fields" rules force you to think about this from day one — much better than discovering it in production.

---

## .NET & Blazor

### Blazor Hosting Models
- **Blazor Server** — UI runs on server, DOM diffs sent over SignalR. Stateful server.
- **Blazor WebAssembly (WASM)** — UI runs in browser via .NET WebAssembly runtime. Calls APIs over HTTP.
- **Blazor United / Auto** (.NET 8+) — Server-rendered first, hydrates to WASM.
- **MAUI Blazor Hybrid** — Native app shell hosting Blazor.

**Why it matters here:** We picked **WASM** because it forces a clean HTTP boundary between UI and backend, which matches the microservices grain.

### EF Core DbContext
Entity Framework Core's unit-of-work + identity-map object. Maps C# classes to a database.
**Why it matters here:** Each service has its own DbContext and schema. Do **not** share DbContexts across services — that's the #1 microservices anti-pattern in .NET land.

### MudBlazor
Free, opinionated Material-design Blazor component library (data grids, charts, dialogs, forms).
**Why it matters here:** Saves writing primitive UI components so we can focus on the architecture side.

### SignalR
ASP.NET Core library for realtime server-to-client messaging over WebSockets (with fallbacks).
**Why it matters here:** The BFF subscribes to NATS events from domain services and pushes them to the Blazor client via a SignalR Hub. The Blazor component re-renders reactively.

### `.slnx`
The newer XML-based Visual Studio solution file format (VS 17.10+). Replaces the legacy `.sln` text format.
**Why it matters here:** Our solution is `.slnx`, so CI runners need .NET SDK **8.0.300+** or newer to recognize it.

---

## Messaging & Events

### gRPC
A high-performance RPC framework using Protobuf over HTTP/2. Strongly typed, multi-language.
**Why it matters here:** Synchronous internal service-to-service calls use gRPC. External-facing APIs use REST/JSON.

### Kafka
Heavy-duty distributed event streaming platform. Excellent, but operationally expensive.
**Why it matters here:** We're **not** using Kafka — overkill for one user. Worth knowing about so you understand what NATS gives up.

### NATS JetStream
Lightweight messaging system with optional persistent streams, acks, replay. Much smaller operational footprint than Kafka.
**Why it matters here:** Our message bus. Persistent, durable, supports replay — enough for everything we need.

### Protobuf (Protocol Buffers)
Google's binary, schema-first message format. Used by gRPC and as our event payload format.
**Why it matters here:** A `.proto` file is the *contract* between services. Generated code keeps C#, Go, and Python services in sync.

---

## Data & Storage

### Database-per-Service
Each service owns its own database/schema; no other service may read it directly.
**Why it matters here:** Forces communication through APIs/events. Trying to "just join the tables" is the path back to a distributed monolith.

### TimescaleDB
PostgreSQL extension optimized for time-series data (hypertables, automatic partitioning by time).
**Why it matters here:** Valuation Service stores time series of account values. Regular Postgres works fine until you have years of data; TimescaleDB scales the query side cleanly.

---

## Observability

### Distributed Tracing
Following a single request as it hops across services, with timing at each step. Each hop gets a *span*; spans share a *trace ID*.
**Why it matters here:** When "add transaction" is slow, tracing tells you *which* service or hop. Without it, debugging microservices is guesswork.

### Grafana
Visualization layer for metrics, logs, and traces. Reads from Prometheus, Loki, Tempo, etc.
**Why it matters here:** Dashboards for net worth lag, event throughput, error rates per service.

### Loki
Log aggregation system from Grafana Labs. Indexes labels, not log contents — cheap to run.
**Why it matters here:** Centralized logs from every service, searchable by service/level/trace ID.

### OpenTelemetry (OTel)
Vendor-neutral standard for emitting metrics, logs, and traces. Has SDKs for .NET, Go, Python, etc.
**Why it matters here:** One instrumentation API across our polyglot services. We send OTel data to Prometheus/Loki/Tempo.

### Prometheus
Pull-based metrics database with its own query language (PromQL).
**Why it matters here:** Numeric metrics (request rate, latency, queue depth). Each service exposes a `/metrics` endpoint.

### Tempo
Grafana Labs' distributed tracing backend; OTel-compatible.
**Why it matters here:** Stores traces so Grafana can render them.

---

## Infrastructure & Platform

### ArgoCD
GitOps controller for Kubernetes. Watches a Git repo and reconciles cluster state to match.
**Why it matters here:** Later-phase deployment story. Push to Git → ArgoCD applies to cluster. No `kubectl apply` from your laptop.

### GitOps
Pattern where the desired state of infrastructure is defined in Git, and a controller reconciles reality to match. Rollback = revert commit.
**Why it matters here:** Pairs naturally with GitHub as the source of truth and with ArgoCD as the reconciler.

### k3s / kind
Lightweight Kubernetes distributions for local/learning use. `kind` runs in Docker; `k3s` is a real lightweight cluster.
**Why it matters here:** Run a real cluster on your laptop or a single home server without the operational cost of full Kubernetes.

### Kubernetes (k8s)
Container orchestrator. Manages scheduling, scaling, networking, health checks across many machines.
**Why it matters here:** The canonical microservices runtime. We'll move from `docker-compose` to k3s in Phase 5.

### Service Mesh (Linkerd)
Sidecar proxies injected next to every service. They handle mTLS, retries, traffic shifting, and observability transparently.
**Why it matters here:** Optional later-phase addition. Teaches "I get features without writing code in every service."

---

## Security & Identity

### Duende IdentityServer
Commercial (free for personal use) OIDC/OAuth 2.0 server for .NET.
**Why it matters here:** Issues tokens our BFF and services trust. Alternative: Keycloak (open source).

### Keycloak
Open-source identity provider — OIDC, SAML, social logins, user federation.
**Why it matters here:** Heavier but free alternative to Duende. Either teaches you the same concepts.

### OIDC (OpenID Connect)
Identity layer on top of OAuth 2.0. Issues ID tokens proving who the user is, in addition to access tokens.
**Why it matters here:** Our auth standard. The BFF holds tokens server-side (the "BFF pattern" Microsoft documents) so they never reach the browser.

### Vault (HashiCorp)
Secrets management — dynamic database credentials, encryption-as-a-service, PKI.
**Why it matters here:** Centralizes secrets across services. Teaches secret rotation and least-privilege properly.

---

## Source Control & CI/CD (GitHub)

### Branch Protection / Rulesets
GitHub feature that enforces rules on a branch — required PRs, required status checks, no force-push, linear history.
**Why it matters here:** Forces every change through a PR with CI checks, even for a solo developer. Builds the discipline.

### CodeQL
GitHub's semantic code analysis engine for finding security vulnerabilities and bugs. Free for public repos.
**Why it matters here:** Catches things like SQL injection, hard-coded secrets, unsafe deserialization across the polyglot codebase.

### CODEOWNERS
A file GitHub looks for that maps **file paths** to **people or teams** who own those files. When a PR touches a matched path, GitHub auto-requests a review from the listed owner(s).

**Locations GitHub checks** (use any one): `.github/CODEOWNERS`, `/CODEOWNERS`, `/docs/CODEOWNERS`.

**Syntax:** `<path pattern>  <owner1> <owner2> ...`
- Patterns work like `.gitignore` (`*`, `*.cs`, `/src/Web/`, `/src/Services/Accounts/**`).
- Owners are GitHub usernames (`@username`) or teams (`@org/team-name`).
- Later rules override earlier ones — most specific path should come last.

**Example (future-state):**
```
*                              @armenac132
/src/Web/                      @armenac132
/.github/                      @armenac132
/src/Shared/Contracts/         @armenac132
```

**Why it matters here:** Even solo, it (1) teaches the mechanic before collaborators arrive, (2) forces a deliberate diff re-read on the PR page (a different view than VS), and (3) pairs with branch protection's "Require review from Code Owners" rule to route every PR through the right person automatically.

**Gotchas:**
- Usernames are case-sensitive and the `@` is required. `armenac132` (no @) or wrong casing silently fails to match.
- Only the **last matching line** for a given file applies — order matters when patterns overlap.
- The owner must have **write access** to the repo or the auto-review request won't fire.

### Dependabot
GitHub's automated dependency update bot. Opens PRs to bump versions; can group related updates.
**Why it matters here:** Keeps NuGet packages and Actions versions current without manual work.

### GHCR (GitHub Container Registry)
GitHub's Docker image registry, integrated with repo permissions.
**Why it matters here:** Each service's CI builds and pushes its image to GHCR. Auth uses the built-in `GITHUB_TOKEN`.

### GitHub Actions
GitHub's CI/CD system. Workflows are YAML files in `.github/workflows/`.
**Why it matters here:** Every service gets its own workflow; path filters scope each workflow to its own folder for independent CI.

### Linear History
Git history with no merge commits — only fast-forward, squash, or rebase merges.
**Why it matters here:** Required by our branch protection. Easier to read `git log`, easier to bisect, easier to revert.

### Monorepo vs Polyrepo
- **Monorepo:** all services in one repo, one shared history.
- **Polyrepo:** each service in its own repo.

**Why it matters here:** We chose **monorepo**. Microservices deployment story comes from path-filtered workflows, not separate repos. Avoids cross-repo PRs for shared contract changes.

### OIDC Deploy (Cloud)
GitHub Actions can act as an OIDC identity provider to cloud accounts (AWS, Azure, GCP) — no long-lived secrets stored in GitHub.
**Why it matters here:** When we deploy, the workflow authenticates to the cloud via OIDC, not via stored access keys.

### Path-Filtered Workflows
A workflow `on.push.paths:` clause that only runs when files in certain paths change.
**Why it matters here:** Editing `src/Web/Client/` shouldn't rebuild every Go service. Path filters give per-service CI in a monorepo.

### Reusable Workflows
A workflow file that other workflows can call (`uses: ./.github/workflows/reusable-x.yml`). Like a function.
**Why it matters here:** Every .NET service's CI looks similar — restore, build, test, package, push. Write it once.

### Trunk-Based Development
All work happens on short-lived branches off `main`; merge frequently. No long-lived `develop` branch.
**Why it matters here:** Simpler than GitFlow, matches modern practice, and pairs well with continuous deployment.

---

## Domain Concepts (Personal Finance)

### Plaid
US financial data aggregator — connects to banks, brokerages, credit cards via a single API. Free sandbox.
**Why it matters here:** One Plaid connector service replaces dozens of bank-specific integrations.

### Net Worth Snapshot
Point-in-time aggregation of all asset and liability values.
**Why it matters here:** The NetWorth service precomputes daily snapshots so the UI doesn't recompute on every page load.

### Reconciliation
Periodically comparing recorded balances/transactions against the source of truth (the institution) and resolving differences.
**Why it matters here:** Drives the saga-pattern example — a multi-service workflow that has to either fully succeed or cleanly roll back.

### Valuation
The current value of an asset at a point in time. For cash it's the balance; for stocks the price × shares; for real estate the Zillow estimate.
**Why it matters here:** Valuations are time-series and feed net-worth calculation. They're separate from transactions.
