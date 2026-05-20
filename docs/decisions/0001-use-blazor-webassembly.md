# 0001 — Use Blazor WebAssembly for the frontend

## Status
Accepted
Date: 2026-05-20

## Context
The frontend must be .NET Blazor (a user-stated learning goal). Blazor offers four hosting models — Server, WebAssembly, United/Auto, and MAUI Hybrid — each with different implications for the rest of the architecture.

Because the larger goal is **learning microservices**, the frontend's relationship to the backend matters more than usual: we want a clean, observable HTTP boundary between UI and services, not an implicit server-side coupling.

## Decision
Use **Blazor WebAssembly** (WASM), running the .NET runtime in the browser and calling backend APIs over HTTP.

## Consequences

**Positive**
- Forces a clean HTTP boundary between UI and backend — the UI is "just another HTTP client," which matches the microservices grain.
- The BFF (Backend for Frontend) pattern slots in naturally: BFF holds OIDC tokens server-side, WASM uses cookie auth against the BFF. This is the Microsoft-documented "BFF pattern" for SPAs.
- Realtime is handled cleanly via SignalR from BFF → client (BFF subscribes to NATS, pushes to client).
- Strongly-typed DTOs can be shared between UI and BFF via a `Web.Contracts` project — large DX win.
- Stateless frontend; trivially horizontally scalable (irrelevant for one user, relevant for learning patterns).

**Negative**
- Cold-start latency (~1–3s for the WASM runtime to download and warm up). Acceptable for a personal app; would matter more for public-facing.
- Larger initial download than Blazor Server. Mitigate with AOT compilation in release builds.
- SignalR with multiple BFF replicas would require a Redis backplane and sticky sessions. (Not relevant until we scale the BFF.)

**Neutral**
- All UI logic runs client-side. Don't put business logic in components — push it to the BFF.

## Alternatives Considered

- **Blazor Server.** UI runs on server, DOM diffs over SignalR. Easiest to start with, but ties UI to a stateful server and blurs the line between frontend and backend — works *against* the microservices learning goal.
- **Blazor United / Auto (.NET 8+).** Server-rendered first, hydrates to WASM. Best UX, but more moving parts. Revisit if cold-start becomes painful — the migration from pure WASM is relatively contained.
- **MAUI Blazor Hybrid.** Native shell. Out of scope; can be added later as a companion app reusing the same Razor components and `Web.Contracts`.
- **Non-Blazor (React/Vue/Svelte).** Rejected — Blazor is a stated learning goal.
