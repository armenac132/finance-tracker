# 0005 — Separate dev servers + CORS over hosted-WASM

## Status
Accepted
Date: 2026-05-27

Refines [ADR 0001](0001-use-blazor-webassembly.md) (Use Blazor WebAssembly).

## Context

ADR 0001 chose Blazor WebAssembly but left the **deployment topology between the WASM Client and the BFF** unspecified. Two options exist:

- **Hosted WASM.** The BFF serves the WASM client's static assets at `/` as well as its own API at `/api/*`. One process, one port. Historically Microsoft shipped this as the "ASP.NET Core Hosted" option in the Blazor WebAssembly template (`dotnet new blazorwasm --hosted`).
- **Separate dev servers + CORS.** The Client runs in its own dev server (the `Microsoft.AspNetCore.Components.WebAssembly.DevServer` package shipped with the standalone template). The BFF runs in its own Kestrel process. They talk over HTTP, and the BFF exposes a CORS policy that whitelists the Client's origin. Two processes, two ports.

When Phase 2 began (mid-May 2026) the obvious choice looked like hosted-WASM: simpler to reason about, single port, no CORS to configure. We started there.

**That choice turned out to be quietly broken in .NET 8.** Microsoft **removed** the `--hosted` option from `dotnet new blazorwasm` in .NET 8 and no longer ships a template that scaffolds the Server + Client + Shared trio with all the wiring. Microsoft's official path forward is either (a) the new **Blazor Web App** template (a unified SSR + WASM hybrid with render modes — a different architecture from pure-WASM-over-HTTP) or (b) standalone WASM + a separate API.

When we tried to assemble hosted-WASM by hand — taking an `ASP.NET Core Empty` BFF, adding `Microsoft.AspNetCore.Components.WebAssembly.Server`, and adding a `ProjectReference` to the Client — we ran into a cascading series of problems:

1. **Static-asset wiring was silently skipped.** Visual Studio's incremental build determined the BFF was "up to date" even when the Client's static-asset manifest had changed. The BFF would launch with an empty `staticwebassets.endpoints.json` and serve no `_framework/*` files. Forcing a `dotnet clean` + nuke of `bin/obj` would fix it for one launch.
2. **Hot Reload scripts 404 in WASM startup.** Debug builds of the WASM client dynamically `import()` two scripts that do not exist as files on disk: `aspnetcore-browser-refresh.js` and `blazor-hotreload.js`. Those scripts are **virtual endpoints** that only get registered when the host is launched via `dotnet watch`. Plain `dotnet run` (or VS F5 without watch) leaves the dynamic import unresolved, which throws inside `blazor.webassembly.js` and immediately surfaces as the WASM error UI: *"An unhandled error has occurred."* The user sees nothing useful in the BFF logs because the failure is entirely inside the browser.
3. **`Microsoft.AspNetCore.Components.WebAssembly.Server` does not register the Hot Reload endpoints.** Only the `dotnet watch` infrastructure does. A hand-assembled hosted-WASM setup is therefore only usable via `dotnet watch run` — which Visual Studio's F5 does not invoke by default.
4. **Hot Reload silently no-ops on top-level statements in `Program.cs`.** Even when wiring was healthy, edits to `Program.cs` in VS would report "Applied changes successfully" but the running process would still serve the old behavior (top-level statements only execute at startup, so an apply against a running process is a no-op).
5. **VS's Fast Up-to-Date Check skipped rebuilds** when files were edited outside the VS editor (e.g., via external tools), making external edits invisible to F5.

Each of these is individually surmountable. Stacked, they made the hosted-WASM workflow a constant fight against tooling that Microsoft has stopped supporting in this exact configuration.

We then took the separate-dev-servers path. It worked first try. `dotnet new blazorwasm` standalone ships with a working dev server out of the box. CORS is one `AddCors` + one `UseCors` call. F5 with multi-startup launches both projects. No virtual-endpoint magic, no incremental-build games.

## Decision

The Blazor WASM Client and the BFF run as **two separate dev servers**. The Client is served by its own `WebAssemblyDevServer` (the standalone template's default) on its own port. The BFF runs in its own Kestrel process on its own port. They communicate over HTTP. The BFF exposes a CORS policy that whitelists the Client's origin.

The BFF's `.csproj` does **not** reference the Client project. The BFF references `FinanceTracker.Web.Contracts` (for shared DTOs) and that is the only knowledge the BFF has of any HTTP caller.

In production, the topology becomes: WASM assets shipped from a static host (CDN, blob storage, or a thin static-file container), BFF deployed independently. CORS rules transfer cleanly from the dev origin to the production origin via configuration.

## Consequences

**Positive**

- **The Client really is "just another HTTP client"** from the BFF's perspective — matches the architectural intent ADR 0001 stated. Mobile, Postman, or a future second UI can hit the BFF the same way the WASM does.
- **No hosted-WASM dev tooling friction.** Each side uses standard, supported tooling. Hot Reload works on each side independently because each side ships with the infrastructure it expects.
- **Visual Studio F5 works the standard way** — multi-startup projects, both processes launched, both attachable to the debugger. No `dotnet watch` workarounds.
- **Production deployment is simpler and more flexible.** WASM is just static files; serve from a CDN. BFF scales independently. Either side can be replaced without touching the other.
- **CORS is real microservices knowledge.** Every microservices system has cross-origin HTTP traffic. Learning to configure it correctly here pays off later.

**Negative**

- **Two ports to manage locally** (`https://localhost:7106` BFF, `https://localhost:7230` Client). Slightly more cognitive overhead than "everything is on one URL."
- **CORS policy must be kept in sync** with the Client's actual origin across environments. A misconfigured policy fails loudly with a clear browser error, but it is still configuration to maintain.
- **The Client's API base URL is configuration** — currently hardcoded in `Program.cs`, will move to `wwwroot/appsettings.json` when we add a second environment.

**Neutral**

- **We will not learn the hosted-WASM dev workflow on this project.** That is a deliberate cut. If Microsoft revives a first-class hosted Blazor WASM template, we can re-evaluate; for .NET 8 the path is dead.

## Alternatives Considered

- **Hosted WASM (hand-assembled).** Rejected. See Context — the static-asset wiring, Hot Reload script gap, and the silent-incremental-build behavior combined into hours of friction with no real payoff over separate servers.
- **Blazor Web App template (.NET 8 unified SSR + WASM).** Rejected. Server-side rendering blurs the UI/backend line that ADR 0001 specifically wanted clean. The render-mode model (Server / WebAssembly / Auto per component) is genuinely useful but is a different architecture, not a deployment-topology choice. Worth a separate ADR if we ever revisit.
- **MAUI Blazor Hybrid.** Out of scope (covered in ADR 0001's "Alternatives Considered").

## Operational notes

- **CORS configuration** lives in `FinanceTracker.Web.Bff/Program.cs`. The Client's dev origin is `https://localhost:7230` (and `http://localhost:5130` for the HTTP profile). Production origins are added to the policy via configuration.
- **Client's BFF base URL** is currently hardcoded in `FinanceTracker.Web.Client/Program.cs`. Move to `wwwroot/appsettings.json` when a non-dev environment exists.
- **VS startup projects.** Solution Explorer → right-click solution → Configure Startup Projects → Multiple → set both Bff and Client to **Start**, with Bff above Client (so the BFF is listening when the Client first issues a request).
