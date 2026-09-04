# 05 — Roadmap & Future Enhancements

Read the other documents first. This file sequences the work and captures
ideas intentionally deferred past v1.

## Phasing

### Phase 0 — Foundations (nothing else works without this)
- Repo scaffold: .NET solution (Domain/Application/Infrastructure/Api),
  React app, Docker Compose (API + React + Postgres + Redis) for local dev.
- Multi-tenant middleware + EF Core global query filters + the cross-tenant
  leak test suite (`00-overview.md` §4/§6) — write this test **first**,
  before any feature, as a forcing function for correct tenancy from day one.
- Auth: signup, login, JWT + refresh, MFA, roles/permissions seeding.
- Org structure CRUD (Locations/Departments/Designations) + Employee CRUD
  + Org Chart view.
- CI/CD skeleton (build/test/deploy to Azure) so every subsequent PR ships
  through a real pipeline, not manual deploys.

### Phase 1 — Core HR + Payroll daily-use loop
This is the largest phase by design — it is the set of things a company
cannot run payroll or day-to-day HR without, per explicit scope decision to
pull these forward rather than defer them:
- **Org structure & reportees** (carried from Phase 0, dependency for
  everything below): Locations/Departments/Designations, Employee CRUD,
  manager/reportee hierarchy, Org Chart.
- **Employee Onboarding**: candidate pipeline, checklist templates,
  candidate→employee conversion (moved up from the original Phase 3 —
  needed in Phase 1 because payroll and attendance both need a clean
  "employee exists with a start date and salary structure" entry point).
- **Attendance & Shifts**: check-in/out, shift assignment, regularization
  requests (moved up from Phase 2 — regularization directly affects
  payroll's loss-of-pay calculation, so it belongs with the payroll-input
  chain, not deferred).
- **Leave Management**: types/policies/balances/requests/team calendar/
  holidays. Unpaid leave (LOP) days computed here feed payroll.
- **Timesheet Management**: projects/tasks, timesheet entry (daily/weekly),
  submission & approval, utilization/billable reporting; approved hours
  feed payroll for hourly/contract pay types.
- **Payroll & Compensation**: salary structure templates, employee salary
  assignment (effective-dated), statutory configuration (PF/ESI/
  Professional Tax/TDS — India default, pluggable per country), payroll
  run lifecycle (draft → compute → approve → lock → disburse), payslip
  generation, tax computation & investment declarations, Full & Final
  settlement on exit. Full detail in `01-modules-functional-spec.md` §O
  and the FRD (`06-frd-phase1.md`).
- **The generic Workflow Engine (§J)** built once here — Leave is its
  first consumer, but Attendance Regularization, Timesheet Approval, and
  Payroll Run Approval are all Phase 1 consumers too, so get the engine
  right before adding more request types on top of it.
- **Notifications** (in-app + email) wired to the workflow engine's events
  and to payroll events (payslip published, investment-proof deadline).

### Phase 2 — Self-service & manager experience
- Full Home/Overview (ESS/MSS), unified Approvals inbox, "My Requests"
  tracker, delegation, HR Process requests (Department/Location/
  Designation change, Travel).
- Custom fields on Employee.
- Bulk import/export across org structure and employees.

### Phase 3 — Performance & reporting
- Goals, OKR (cycles/objectives/key results with alignment), continuous
  feedback notes.
- Reports v1: headcount, attrition, leave utilization, attendance summary,
  approval SLA, payroll cost summary, tax summary (Form 16-equivalent).

### Phase 4 — SaaS growth, scale & compliance automation
- Self-serve billing/plan upgrade, seat-usage metering.
- SSO (SAML/OIDC), API keys + webhooks for tenants, integrations hub
  (Slack/Teams, Google/Outlook calendar).
- No-code approval-chain builder (replaces the Phase-1 static rules).
- **Statutory e-filing integrations**: direct PF ECR upload, ESI return,
  Professional Tax return, TDS/24Q e-filing to government portals (Phase 1
  only produces the correctly computed file/report for manual filing).
- **Direct bank/payment-gateway disbursement** (Phase 1 exports a
  bank-standard file for manual upload by the tenant's finance team).
- **Multi-country payroll rule sets** beyond the Phase 1 India default —
  additive rule sets against the same pluggable statutory engine, not a
  redesign (see `02-data-model-erd.md`).
- Mobile: PWA polish or a thin native wrapper (check-in/leave/approvals
  are the highest-value mobile actions — prioritize those three flows
  first, not full feature parity).
- Document e-signature, asset management, HR helpdesk, exit/offboarding
  clearance workflow (IT/Finance checklist — distinct from the Phase 1
  Full & Final settlement payroll calculation), appraisal cycles.

### Phase 5 — Differentiation / AI-assisted
- Attendance/leave anomaly insights (informational, opt-out) already
  seeded conceptually in Phase 1 (Bradford score) — extend with
  pattern-detection.
- eNPS/pulse surveys + engagement trend dashboard.
- Resume parsing for candidate intake (auto-fill from an uploaded resume).
- Attrition-risk scoring combining tenure, Bradford score, engagement
  survey trend, and manager-change frequency — presented as a
  manager-facing signal, never an automated action.
- Lightweight in-app HR FAQ chatbot (answer policy questions from the
  tenant's own configured leave/attendance policies — a genuinely useful,
  low-risk use of an LLM here since answers are grounded in the tenant's
  own structured policy data, not open-ended).

## Explicit non-goals for v1 (revisit later, don't build now)
- **Statutory e-filing / direct government portal integration** (PF ECR
  upload, TDS 24Q e-filing, ESI/PT e-returns) — Phase 1 payroll computes
  and produces the correct report/file for a human to file; direct filing
  integrations are Phase 4.
- **Multi-country payroll/tax rule sets** beyond the Phase 1 default
  (India) — the statutory engine is pluggable by design (§ in
  `02-data-model-erd.md`), but only one country's rules ship in Phase 1.
  Confirm target country before payroll engineering starts if it isn't
  India.
- **Direct bank/payment-gateway disbursement** — Phase 1 exports a
  bank-standard payment file for manual upload; automated disbursement via
  a payment gateway/banking API is Phase 4.
- Native mobile apps (PWA-first is enough for the initial market).
- Multi-region active-active hosting.
- Per-tenant isolated databases (start shared-schema; migrate a specific
  tenant later only if a contract requires it — the schema in
  `02-data-model-erd.md` already supports this move without redesign).

## How to use this document set when building
1. Before starting any module, re-read its section in
   `01-modules-functional-spec.md` in full, not just the table row.
2. Cross-check new tables against `02-data-model-erd.md` — extend it in
   place (keep it current) rather than letting the code and doc drift.
3. Any new endpoint must fit the conventions in `03-api-design.md`
   (pagination envelope, error format, permission attribute) — don't
   introduce a one-off response shape.
4. Any new screen must fit `04-frontend-architecture.md`'s feature-folder
   structure and reuse `shared/components` (`DataTable`, `ApprovalTimeline`,
   etc.) before building a bespoke equivalent.
5. When a decision here turns out wrong once real usage exists, **update
   these docs in the same PR that changes the code** — this document set
   only stays a useful "bible" if it's kept current, not treated as a
   one-time artifact.
