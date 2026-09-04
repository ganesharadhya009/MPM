# 07 — Non-Functional Requirements Document (NFRD): Phase 1

Companion to `06-frd-phase1.md`. Where `00-overview.md` §6 states NFRs at
the whole-platform level, this document makes them **specific and
measurable for Phase 1**, and adds requirements that only exist because
Phase 1 now includes Payroll — payroll and tax data raise the stakes on
security, auditability, and compliance well above what a plain HR-records
module needs.

**ID scheme:** `NFR-<CATEGORY>-<NN>`. **Priority:** `M` = Must have for
Phase 1 launch, `S` = Should have.

---

## 1. Security

| ID | Requirement | Target / Acceptance Criteria | Priority |
|---|---|---|---|
| NFR-SEC-01 | All traffic encrypted in transit | TLS 1.2+ everywhere, no plain-HTTP endpoint reachable externally. | M |
| NFR-SEC-02 | Sensitive data encrypted at rest | Bank account numbers, tax IDs (PAN-equivalent), MFA secrets encrypted at the column level (not relying on disk-level encryption alone); Postgres transparent data encryption as a baseline floor, not the only control. | M |
| NFR-SEC-03 | Bank account numbers masked in UI | Full number never rendered after initial entry — show last 4 digits only; full value retrievable only for the bank-file export process, itself access-controlled and audited. | M |
| NFR-SEC-04 | Password & credential handling | ASP.NET Core Identity default hashing (PBKDF2/Argon2), no custom crypto; MFA (TOTP) available at launch. | M |
| NFR-SEC-05 | Role-based + field-level access control | Payroll/salary data restricted to Finance/HR Admin roles server-side (not just hidden in the UI); employee-editable vs. HR-only fields on the Employee record enforced by the API per `01-modules-functional-spec.md` §D, independent of any client-side check. | M |
| NFR-SEC-06 | Secrets management | All connection strings, API keys, and encryption keys in Azure Key Vault; none in source control or plain appsettings. | M |
| NFR-SEC-07 | Tenant data isolation | See NFR-COMP-01 — a cross-tenant data leak (including payroll data) is a Sev-1 defect class, verified by an automated test suite that runs in CI on every build, not just a design intention. | M |
| NFR-SEC-08 | Injection safety | 100% parameterized queries via EF Core; no raw SQL string concatenation anywhere in the Payroll module specifically, given its financial-data sensitivity. | M |
| NFR-SEC-09 | Audit trail integrity | Audit log entries (`audit_logs`) are append-only at the application layer — no update/delete code path exists against that table. | M |
| NFR-SEC-10 | Session security | JWT access tokens short-lived (~15 min); refresh tokens httpOnly, rotated on use, revocable ("sign out of all devices"). | M |
| NFR-SEC-11 | Login anomaly detection | New-device/new-location login triggers an email notification to the user. | S |

## 2. Data Privacy & Statutory Compliance

| ID | Requirement | Target / Acceptance Criteria | Priority |
|---|---|---|---|
| NFR-COMP-01 | Tenant isolation, provably | Automated tests assert that no repository/query path can return another tenant's rows, across every tenant-owned table including all new Payroll/Timesheet tables — run in CI, block merge on failure. | M |
| NFR-COMP-02 | Payroll statutory retention overrides erasure requests | A GDPR-style data-erasure request from an employee/former employee **cannot** delete payroll, tax, or statutory records within their legally mandated retention window (India: broadly 6–8 years across the Income Tax Act, PF Act, and Payment of Wages Act, exact figure confirmed against current law before go-live) — the erasure workflow must detect this conflict and retain those records while erasing everything not under a retention obligation, with the conflict clearly explained to the requester. | M |
| NFR-COMP-03 | Configurable retention period | Statutory retention duration is tenant/jurisdiction-configurable, not hardcoded, consistent with the pluggable-by-country design of the statutory engine (`02-data-model-erd.md`). | M |
| NFR-COMP-04 | Data export (subject access) | An employee or Admin can request a full export of an employee's data (profile, attendance, leave, timesheet, payslips) in a machine-readable format. | M |
| NFR-COMP-05 | Consent tracking | Optional data collection (geo-fencing location, biometric check-in if ever added) requires recorded consent per employee before collection begins. | S |
| NFR-COMP-06 | Statutory computation correctness | PF/ESI/PT/TDS calculations are unit-tested against known reference examples (hand-calculated or from an authoritative payroll calculator) for each statutory rule, with tests re-run whenever a statutory setting changes — a payroll miscalculation is a compliance incident, not just a bug. | M |
| NFR-COMP-07 | Immutable payslips | A generated payslip is never mutated in place; a correction produces a new payslip tied to an adjustment run, preserving the original for audit. | M |

## 3. Performance

| ID | Requirement | Target / Acceptance Criteria | Priority |
|---|---|---|---|
| NFR-PERF-01 | API latency, list/detail endpoints | P95 < 300ms at a 10,000-employee tenant scale, under normal load. | M |
| NFR-PERF-02 | Payroll run computation time | Computing a payroll run for a 1,000-employee tenant completes in under 2 minutes; for larger tenants, runs asynchronously with a visible progress indicator rather than blocking the UI request. | M |
| NFR-PERF-03 | Check-in/out response time | P95 < 500ms — this is the single highest-frequency write action in the system and must feel instant. | M |
| NFR-PERF-04 | Report generation | Standard reports (headcount, leave utilization, payroll summary) return in under 5 seconds for a 10,000-employee tenant, or are generated asynchronously with a download-when-ready pattern beyond that. | M |
| NFR-PERF-05 | Org chart rendering | Usable (interactive, no dropped frames) at 500+ node scale via virtualization. | M |

## 4. Scalability

| ID | Requirement | Target / Acceptance Criteria | Priority |
|---|---|---|---|
| NFR-SCAL-01 | Multi-tenant load isolation | One tenant running a large payroll batch does not degrade API responsiveness for other tenants — payroll computation runs as a background job (Hangfire/Azure Functions), never inline on the request thread. | M |
| NFR-SCAL-02 | Horizontal scale of the API tier | Stateless API instances behind Azure App Service autoscale; no in-memory session state that would break with multiple instances. | M |
| NFR-SCAL-03 | Database indexing discipline | Every FK and every `(tenant_id, ...)` composite used in a WHERE/ORDER BY is indexed before the feature ships, verified via query plan review on the largest Payroll/Attendance tables specifically (highest row-count growth). | M |

## 5. Availability & Reliability

| ID | Requirement | Target / Acceptance Criteria | Priority |
|---|---|---|---|
| NFR-AVAIL-01 | Platform uptime | 99.9% monthly, single-region Azure with zone-redundant App Service + Postgres Flexible Server HA. | M |
| NFR-AVAIL-02 | Payroll-window reliability | Elevated monitoring/alerting around tenant-configured payroll processing dates (typically month-end) — a payroll-run failure window is treated as a higher-severity incident class than an equivalent failure on a quiet day. | M |
| NFR-AVAIL-03 | Graceful degradation | If background job processing (payroll compute, notifications) is temporarily unavailable, the core check-in/leave-apply/approve flows continue to function; queued jobs process on recovery, nothing is silently dropped. | S |
| NFR-AVAIL-04 | Backup & disaster recovery | Automated daily Postgres backups with point-in-time restore; RPO ≤ 1 hour, RTO ≤ 4 hours for a full environment restore. Payroll data specifically must be included in restore-drill validation (a periodic test restore that confirms payroll figures reconcile), not just assumed covered. | M |

## 6. Auditability

| ID | Requirement | Target / Acceptance Criteria | Priority |
|---|---|---|---|
| NFR-AUD-01 | Full change history on financial data | Every create/update to `employee_salary_assignments`, `payroll_runs`, `payroll_run_items`, and `investment_declarations` is captured in `audit_logs` with actor, timestamp, and before/after diff. | M |
| NFR-AUD-02 | Manual override visibility | Any manually overridden payroll line item is visibly marked as such in every view where it appears (run detail, payslip audit view), never indistinguishable from a system-computed value. | M |
| NFR-AUD-03 | Audit log searchability | Admin-facing audit log viewer supports filtering by actor, entity type, entity id, and date range, per `01-modules-functional-spec.md` §M. | M |

## 7. Usability & Accessibility

| ID | Requirement | Target / Acceptance Criteria | Priority |
|---|---|---|---|
| NFR-UX-01 | Accessibility conformance | WCAG 2.1 AA across all Phase 1 screens, with particular attention to the custom `OrgChartView`, `ApprovalTimeline`, and calendar/date-range pickers, which don't get accessibility for free from the UI kit. | M |
| NFR-UX-02 | Guided complex flows | The Payroll Run screen (compute → review → exception report → approve → lock → disburse) is a guided multi-step flow with clear current-state indication, not a single dense form — this is the highest-risk screen in the product for user error. | M |
| NFR-UX-03 | Mobile-responsive | Check-in/out, leave apply/approve, and timesheet entry are fully usable on a mobile browser viewport at launch (native/PWA wrapper is a later phase per `05-enhancements-and-roadmap.md`). | M |
| NFR-UX-04 | Localization readiness | All UI strings externalized via i18next even though only one language ships at launch; dates/currency rendered per tenant locale/timezone. | M |

## 8. Maintainability & Extensibility

| ID | Requirement | Target / Acceptance Criteria | Priority |
|---|---|---|---|
| NFR-MAINT-01 | Pluggable statutory engine | Adding a second country's payroll rules must be achievable by adding a new `statutory_settings`/rule configuration, not by branching core calculation code per country. | M |
| NFR-MAINT-02 | Config over code for tenant customization | Leave types, approval chains, pay components, and custom fields are tenant-configurable data, never per-tenant code forks, per `00-overview.md` §3. | M |
| NFR-MAINT-03 | Contract-tested API | OpenAPI spec generated from the live API and the frontend's typed client generated from it, so a breaking change surfaces at build time, not in production. | M |
| NFR-MAINT-04 | Documentation currency | Any schema, endpoint, or scope change is reflected in `02-data-model-erd.md`/`03-api-design.md`/this FRD-NFRD pair in the same change set — enforced as a PR review checklist item, not left to memory. | M |

## 9. Interoperability

| ID | Requirement | Target / Acceptance Criteria | Priority |
|---|---|---|---|
| NFR-INT-01 | Bank file format | The Phase 1 disbursement export conforms to a standard bank-upload format (e.g. NEFT-compatible CSV) validated against at least one real bank's documented import spec before go-live. | M |
| NFR-INT-02 | Import/export ubiquity | Every list screen and every report supports CSV/XLSX export, per `01-modules-functional-spec.md` §L. | M |

---

## Priority summary for Phase 1 sign-off
Everything marked `M` above is a launch blocker. Items marked `S` may ship
in a fast-follow immediately after Phase 1 without changing the
architecture — none of them require a redesign if deferred, they were
chosen specifically because they're additive.
