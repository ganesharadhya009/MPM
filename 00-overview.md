# PeopleHQ — Product & Engineering Bible

> Working name: **PeopleHQ** (rename freely). A multi-tenant HR management SaaS,
> inspired by the *flows* of Zoho People (not its branding, copy, or visual design).
> This document set is the single source of truth for building the product end to end.
> Read `00` through `05` in order before writing code for a new module.

## Documents in this set
| # | File | Purpose |
|---|------|---------|
| 00 | `00-overview.md` | Vision, principles, tech stack, tenancy, roles, NFRs (this file) |
| 01 | `01-modules-functional-spec.md` | Functional requirements per module — the "what" |
| 02 | `02-data-model-erd.md` | ERD + table-by-table schema — the "data" |
| 03 | `03-api-design.md` | REST conventions + endpoint catalogue — the "contract" |
| 04 | `04-frontend-architecture.md` | React app structure + screen inventory — the "UI" |
| 05 | `05-enhancements-and-roadmap.md` | Phasing, backlog, differentiators beyond Zoho |
| 06 | `06-frd-phase1.md` | Functional Requirements Document — the numbered FR catalogue for Phase 1 |
| 07 | `07-nfrd-phase1.md` | Non-Functional Requirements Document — the numbered NFR catalogue for Phase 1 |

## 1. Vision
A modern, self-serve HR platform that any company can sign up for, configure in
minutes, and grow into — starting with core HR (employee records, org structure),
attendance & leave, employee/manager self-service, and performance/OKR tracking,
with a clear runway to payroll-adjacent, engagement, and AI-assisted features.

Design principle: **every screen a small/mid-size company actually needs, none of
the bloat.** Favor sane defaults and progressive disclosure over exposing every
configuration knob up front.

## 2. Tech stack (decided)
| Layer | Choice | Notes |
|---|---|---|
| Frontend | React 18 + TypeScript + Vite | SPA, feature-folder structure |
| UI kit | MUI (Material UI) v5+ | Own theme — do not reuse Zoho's visual design |
| Server state | TanStack Query (React Query) | Caching, retries, optimistic updates |
| Client state | Zustand | Small, avoids Redux boilerplate |
| Forms | React Hook Form + Zod | Shared validation schemas, mirrored server-side |
| Backend | ASP.NET Core 8 (LTS) Web API | Minimal APIs or controllers — controllers for this size of surface |
| ORM | EF Core 8 + Npgsql | Code-first migrations |
| Database | PostgreSQL 16 | Azure Database for PostgreSQL – Flexible Server in prod |
| Auth | ASP.NET Core Identity + JWT (access + refresh) | Tenant claim embedded in token |
| File storage | Azure Blob Storage | Pre-signed upload URLs, never proxy large files through API |
| Background jobs | Hangfire (Postgres storage) or Azure Functions | Leave accrual, reminders, notification digests |
| Caching | Redis (Azure Cache for Redis) | Session/lookup caching, rate limiting store |
| Search (later) | Postgres full-text → Elasticsearch if needed | Start simple |
| Hosting | Azure App Service (API) + Azure Static Web Apps or App Service (React) | Container Apps if you outgrow App Service |
| CI/CD | GitHub Actions → Azure | Separate pipelines per app |
| Observability | Application Insights + Serilog (structured logs) | Correlate by TenantId + RequestId |

## 3. Architecture principles
1. **API-first.** The React app is just one client; the API must be fully usable
   without it (mobile app later, integrations, Zapier-style automation).
2. **Clean/vertical-slice architecture** on the backend: `Domain`, `Application`
   (use cases/CQRS-lite with MediatR), `Infrastructure` (EF Core, external
   services), `Api` (controllers, DTOs, auth). Keep business rules out of
   controllers and out of React.
3. **Multi-tenant from day one** (see §4) — retrofitting tenancy later is far
   more expensive than building it in now.
4. **Everything auditable.** Any create/update/delete on a tenant-scoped
   business entity writes an audit record (who, when, what changed). Approval
   workflows keep full history, never overwrite state in place.
5. **One generic workflow engine**, not five bespoke ones. Leave requests, HR
   process changes (department/location/designation), travel requests/expenses,
   exit requests, and onboarding approvals are all instances of the same
   "request → approval chain → status" pattern. Model it once (§ in
   `01-modules-functional-spec.md` and `02-data-model-erd.md`).
6. **Config over code for tenant customization.** Leave types, approval chains,
   holiday calendars, custom fields, and branding are tenant-configurable data,
   not per-tenant code branches.
7. **Additive, not a Zoho clone.** Where this spec adds fields, screens, or
   automation beyond what the recording showed, that's intentional — the goal
   is a better product using Zoho's flow only as a reference point, never its
   UI, copy, or trademarks.

## 4. Multi-tenancy model
- **Strategy:** shared database, shared schema, discriminator column. Every
  tenant-owned table carries a `tenant_id uuid NOT NULL`. EF Core applies a
  **global query filter** on `tenant_id` for every DbSet so a missing `WHERE`
  clause can never leak data across tenants — this is the single most
  important safety net in the whole system and must be unit-tested explicitly
  (a test that asserts tenant A can never read tenant B's rows through any
  repository method).
- **Rationale:** cheapest to run and scale for the target segment (SMB/mid-market);
  migrate a specific large customer to an isolated database later if a
  contract demands it — the schema doesn't need to change for that, only the
  connection resolution.
- **Tenant resolution:** subdomain-based — `acme.peoplehq.app`. Middleware
  resolves the subdomain → `tenant_id` before the request reaches
  controllers, and stamps it into an `ITenantContext` (scoped service) that
  EF Core's query filter reads. The JWT also carries `tenant_id` as a claim;
  the middleware cross-checks the subdomain against the token's tenant and
  rejects on mismatch (defense in depth against a stolen/misused token).
- **Signup flow (no tenant yet):** a public, unauthenticated
  `/signup` flow creates the `Tenant` row, the first `TenantAdmin` user, and
  a default set of lookup data (default leave types, a default location
  named after the org, default designation "Employee"), then redirects to
  `{subdomain}.peoplehq.app` to continue the org-setup wizard.
- **Plans & feature flags:** every tenant has a `PlanId` (Starter / Growth /
  Enterprise). Feature availability (e.g. OKR module, SSO, API access, seat
  count) is resolved from the plan + optional per-tenant overrides — never
  hardcoded per environment.

## 5. Roles & permissions
| Role | Scope | Typical access |
|---|---|---|
| **Platform Super Admin** | Cross-tenant | PeopleHQ's own ops team only. Tenant provisioning, billing, impersonation-for-support (audited), plan changes. Not a tenant-facing role. |
| **Tenant Admin (HR Admin)** | Single tenant, all data | Org structure, employee lifecycle, approval-chain config, integrations, branding, billing (if self-serve). |
| **Manager** | Own reporting line | Approve requests from direct/indirect reports, view reportee attendance/leave/goals, raise requests on their behalf where policy allows. |
| **Employee** | Self only | View/edit own profile (policy-gated fields), submit requests, check in/out, view own goals/leave/payslips (later). |
| **Recruiter** (optional, Onboarding module) | Candidates + own reqs | Manage candidate pipeline, send offers, doesn't see full employee HR data. |
| **Auditor / Read-only** (optional, Enterprise plan) | Single tenant, read-only | Compliance reviews, exports, no writes. |

Permissions are **not** hardcoded to roles in code — model as a
`Role → Permission[]` mapping in the database so Tenant Admins can create
custom roles later (see `02-data-model-erd.md`, `Roles`/`Permissions` tables).
Ship with the roles above as system-seeded defaults per tenant.

## 6. Non-functional requirements
- **Security:** OWASP ASVS baseline; password hashing via Identity defaults
  (PBKDF2/Argon2); MFA (TOTP) available from v1, enforced-by-plan later; all
  traffic TLS; secrets in Azure Key Vault, never in appsettings; parameterized
  EF Core queries only (no raw SQL string concatenation).
- **Tenant data isolation:** see §4 — must have an automated cross-tenant leak
  test suite that runs in CI, not just a design intention.
- **Auditability:** every workflow transition and every change to employee
  core fields (salary-adjacent fields especially, once payroll exists) is
  logged with actor, timestamp, before/after values.
- **Availability target:** 99.9% for v1 (single-region Azure, zone-redundant
  App Service + Postgres Flexible Server HA). Multi-region only if a customer
  contract requires it.
- **Performance:** P95 API response < 300ms for list/detail endpoints at
  10k-employee tenant scale; paginate everything; index every FK and every
  `(tenant_id, ...)` composite used in a WHERE/ORDER BY.
- **Data privacy/compliance:** GDPR-style data subject rights from v1 —
  an employee/admin can request data export and (post-employment, policy
  permitting) erasure; consent tracking for optional data (e.g. biometric
  attendance); data retention policy configurable per tenant.
- **Localization:** UI text externalized (i18next) even if only English ships
  first; date/time/currency formatting locale-aware; store all timestamps in
  UTC, render in the employee's/tenant's configured timezone.
- **Accessibility:** WCAG 2.1 AA target — keyboard navigation, proper ARIA on
  the custom components (org chart, approval timeline, calendar), color
  contrast checked against the theme (see `dataviz`/design guidance for any
  chart work).
- **Mobile:** responsive web from v1 (check-in/leave/approvals are
  mobile-heavy actions); native/PWA wrapper is a Phase 4 item, not required
  for MVP correctness.

## 7. Glossary
- **Tenant** — one customer organization using the platform.
- **Workflow / Request** — any submit-then-approve business action (leave,
  HR process change, travel, exit, onboarding step).
- **Org structure** — Locations, Departments, Designations, and the reporting
  hierarchy between Employees.
- **ESS / MSS** — Employee Self-Service / Manager Self-Service.
