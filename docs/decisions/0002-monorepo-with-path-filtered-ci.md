# 0002 — Monorepo with path-filtered CI workflows

## Status
Accepted
Date: 2026-05-20

## Context
A microservices project can be organized as:
- **Polyrepo** — one Git repo per service. The classical "microservices" arrangement.
- **Monorepo** — all services in one repo with one shared history.

The standard argument for polyrepo is "services should be independently deployable." But independent deployability comes from the *deploy pipeline*, not the repo layout — a monorepo with path-filtered CI gives the same property without the cross-repo PR pain.

For this project specifically, there's a strong learning argument for monorepo: a single contracts change (e.g., editing a `.proto` file) needs to atomically update producers and consumers. In polyrepo that's a coordinated multi-PR dance. In a monorepo it's one PR.

## Decision
Single GitHub repository, `armenac132/finance-tracker`, containing all services, the frontend, deploy manifests, and shared contracts. Independent deployability is achieved via **path-filtered GitHub Actions workflows** (one workflow per service, scoped to its directory).

Directory layout (planned, will evolve):
```
finance-tracker/
├── src/
│   ├── Web/             # Blazor client, BFF, shared web contracts
│   ├── Services/        # Domain services (Accounts, Transactions, etc.)
│   ├── Connectors/      # Plaid, manual, eventually crypto/realestate
│   └── Shared/          # Shared contracts (DTOs, .proto files)
├── tests/
├── deploy/              # docker-compose, k8s manifests
├── proto/               # .proto schemas
└── .github/workflows/   # One workflow per service + cross-cutting ones
```

## Consequences

**Positive**
- Atomic contract changes: edit `.proto`, update producer, update consumer, single PR.
- Single CI configuration to learn and maintain.
- Cross-service refactors are mechanical (rename across the whole tree at once).
- Easier to enforce shared standards (one set of branch protections, one CodeQL config, one Dependabot config).

**Negative**
- Without discipline, services can become tightly coupled because the temptation to "just call into the other project" is constant. Mitigated by `CLAUDE.md` rules and code review.
- CI configuration grows in complexity as services are added (one workflow per service + filters).
- Repository size grows over time. Not a concern at the scale of this project.

**Neutral**
- Doesn't match what real-world "microservices at scale" companies do (most use polyrepo or Bazel-style monorepos). The learning value of path-filtered Actions is the relevant lesson here, not industry mimicry.

## Alternatives Considered

- **Polyrepo (one repo per service).** Rejected. Cross-repo PR coordination is real pain for a solo developer, and the "independent deployability" benefit can be had via path filters anyway.
- **Hybrid (one repo for app code, one for infra).** Rejected as premature. Can split later if infra grows complex enough to warrant it.
- **Bazel / Nx / Turborepo monorepo tooling.** Rejected as over-engineering. GitHub Actions path filters are sufficient. Revisit if build times become painful.
