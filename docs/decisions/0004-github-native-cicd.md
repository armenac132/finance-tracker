# 0004 — Use GitHub-native tooling for CI/CD

## Status
Accepted
Date: 2026-05-20

## Context
Learning GitHub + GitHub Actions is a stated secondary goal of the project. The CI/CD landscape has many credible options — Jenkins, GitLab CI, Azure DevOps, CircleCI, TeamCity, Buildkite — each teaches similar concepts (workflows, jobs, runners, artifacts) but with different ergonomics.

Since the stated goal names GitHub specifically, the value of "deeply learn one platform" outweighs "compare platforms."

## Decision
Use the **GitHub-native** stack end-to-end:
- **GitHub Actions** for all CI and CD workflows.
- **GHCR (GitHub Container Registry)** for Docker images.
- **Dependabot** for dependency updates (grouped, weekly).
- **CodeQL** for static analysis / SAST.
- **Secret scanning + push protection** for accidental credential commits.
- **GitHub Environments** for deploy gating (`dev`, `prod`) with manual approval.
- **OIDC** from Actions to whatever cloud is eventually picked, instead of long-lived secrets.
- **Branch protection rulesets** (PR required, linear history, status checks required, no force-push).

CI workflow patterns we will use:
- **Path-filtered workflows** so editing one service doesn't rebuild the world.
- **Reusable workflows** (`workflow_call`) so each new service inherits the same CI shape with ~10 lines of YAML.
- **Concurrency groups** to cancel superseded PR runs.
- **Artifacts** for test results and coverage uploads.

## Consequences

**Positive**
- Single integrated system. No moving parts to glue together (no Jenkins server, no separate registry, no separate SAST tool to wire in).
- Tightly integrated with PRs — CodeQL findings appear as PR comments, Dependabot opens PRs, status checks gate merges. Real CI/CD feedback loop, not "go check the other tool."
- GitHub Actions YAML knowledge transfers directly to the broader Actions ecosystem (reusable marketplace actions, etc.).
- OIDC cloud auth removes a whole class of secret-management problems.

**Negative**
- Vendor lock-in to GitHub. Migrating to another platform later would mean rewriting workflows.
- Actions's YAML has rough edges (no native loops over service matrix without contortions, reusable workflow inputs are stringly-typed, expression syntax is its own language).
- GitHub-hosted runners can be slow for some workloads. Self-hosted runners are an option later if it matters.

**Neutral**
- We deliberately **do not learn** Jenkins, GitLab CI, or Azure DevOps on this project. Skill is portable but specific syntax is not.

## Alternatives Considered

- **Jenkins.** Rejected. Long-running self-hosted server, separate Groovy DSL, integration with GitHub PRs requires plugin glue. Industry-relevant but high friction for a personal learning project.
- **GitLab CI on a self-hosted GitLab.** Rejected. Excellent CI but requires running GitLab itself; learning value is in the wrong place.
- **Azure DevOps Pipelines.** Rejected. Reasonable choice for .NET shops but a separate ecosystem from where the repo lives. Cross-pollination with the stated GitHub goal would be confusing.
- **Multi-platform (e.g., Actions + a parallel Buildkite).** Rejected as silly for a solo project.
