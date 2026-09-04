# 04 — Frontend Architecture (React + TypeScript)

Read `00-overview.md`, `01-modules-functional-spec.md`, and `03-api-design.md`
first — screens below map directly to those endpoints/modules.

## Project structure (feature-folder, not layer-folder)
```
src/
  app/                # App shell: providers, router, theme, layout
  auth/                # Login, signup, MFA, session state
  tenant/              # Org setup wizard, plan/billing screens
  org-structure/       # Locations, Departments, Designations, Org chart
  employees/           # Directory, profile, documents, custom fields
  onboarding/          # Candidates pipeline, checklists
  leave/               # Apply, balances, team calendar, admin config
  attendance/          # Check-in widget, regularization, shifts
  performance/         # Goals, OKR, feedback notes
  workflow/            # Unified approvals inbox, "My Requests" tracker
  notifications/       # Bell dropdown, preferences
  timesheet/           # Timesheet entry, submission, projects/tasks admin
  payroll/             # Salary structures, payroll runs, payslips, tax/investment declarations
  reports/             # Dashboards, exportable report views
  admin/               # Users/roles, integrations, audit log, branding
  shared/
    components/        # DataTable, ApprovalTimeline, OrgChartView, StatCard...
    hooks/              # useTenant, usePermission, usePagination...
    api/                 # Generated OpenAPI client + React Query hooks per resource
    theme/               # MUI theme tokens (own brand, per artifact-design guidance if ever published as an artifact)
    utils/
  types/                # Shared TS types generated from OpenAPI
```
Each feature folder owns its screens, its React Query hooks, and its
Zod schemas — no cross-feature imports except through `shared/`.

## State management
- **Server state:** TanStack Query exclusively. No manual `useEffect` +
  `fetch` + local state for data that lives on the server — every list/
  detail screen is a `useQuery`, every mutation a `useMutation` with cache
  invalidation scoped to the affected resource keys.
- **Client/UI state:** Zustand for cross-component UI state that isn't
  server data (e.g. "which approvals-inbox filter is active", sidebar
  collapsed state). Component-local `useState` for anything not shared.
- **Forms:** React Hook Form + Zod resolver. Zod schemas mirror backend
  validation rules (documented per-field in `01-modules-functional-spec.md`)
  so client and server never disagree about what's valid.
- **Auth/tenant context:** a top-level `AuthProvider` holds the decoded JWT
  claims (user id, tenant id, roles/permissions) and exposes `usePermission("leave.approve")`
  — every conditionally-rendered action button goes through this hook, not
  ad-hoc role string comparisons.

## Routing & access control
- React Router v6, route tree grouped by feature folder above.
- A `RequirePermission` route wrapper redirects/403s server-side-consistent
  with the API's own RBAC — the frontend check is a UX convenience, **never**
  the actual security boundary (the API enforces it independently, per
  `03-api-design.md`).
- Tenant resolution happens before the router mounts (subdomain → tenant
  config fetch) so the app never flashes the wrong tenant's branding.

## Design system
- MUI v5 with a fully custom theme (palette, typography, spacing) —
  explicitly not Zoho's visual language; this is a from-scratch brand.
  If a visual design pass or mockup is ever produced as a Claude
  **Artifact**, load the `artifact-design` skill first per house style —
  it is not needed for the production React app itself, only for
  design-exploration artifacts.
- Dark mode from v1 (MUI theme mode toggle) — cheap to add now, expensive
  to retrofit once hundreds of components assume light-only colors.
- Shared component library: `DataTable` (server-side pagination/sort/filter
  built-in, used by every list screen), `ApprovalTimeline` (renders a
  `workflow_requests` + steps as a vertical stepper), `OrgChartView`
  (virtualized tree/graph for large tenants), `StatCard`, `EmptyState`,
  `ConfirmDialog`. Build these once in `shared/components`, reuse
  everywhere — the functional spec deliberately unified Leave/HR-Process/
  Travel/Exit around one workflow engine specifically so the frontend only
  needs one `ApprovalTimeline` and one unified inbox, not five bespoke UIs.
- Charts (headcount trends, attrition, engagement, Bradford score
  breakdowns): load the `dataviz` skill before building any chart or
  dashboard screen — it defines the color system, accessibility rules, and
  component patterns to keep every chart consistent.

## Accessibility & i18n
- All user-facing strings via i18next from v1, even English-only initially
  — retrofitting i18n after hundreds of hardcoded strings is expensive.
- Keyboard navigation and ARIA labeling required on custom components
  (`OrgChartView`, `ApprovalTimeline`, calendar pickers) specifically,
  since these are the ones a UI kit doesn't give you for free.

## Screen inventory (v1 scope, mapped to modules)
| Route | Module | Notes |
|---|---|---|
| `/signup` | Tenant | Public. |
| `/login`, `/forgot-password`, `/mfa` | Auth | Public. |
| `/setup/*` | Tenant onboarding wizard | Org details → Locations → Departments → Designations → Invite. |
| `/home` | ESS/MSS | Overview (profile card, check-in, reportees, tabs: Activities/Approvals/Leave/Goals/HR Process/Related). |
| `/home/dashboard`, `/home/calendar`, `/home/delegation` | ESS/MSS | |
| `/directory` | Employees | Searchable list. |
| `/employees/{id}` | Employees | Profile tabs: Personal/Employment/Documents/Skills/Custom Fields. |
| `/org-chart` | Org Structure | |
| `/settings/locations`, `/departments`, `/designations` | Org Structure | Admin. |
| `/onboarding/candidates` | Onboarding | Pipeline board/list. |
| `/onboarding/candidates/{id}` | Onboarding | Detail + checklist. |
| `/onboarding/templates` | Onboarding | Admin config. |
| `/leave/apply`, `/leave/summary`, `/leave/team-calendar` | Leave | |
| `/leave/settings/types`, `/policies`, `/holidays` | Leave | Admin. |
| `/attendance` | Attendance | Personal log + check-in widget (also embedded on Home). |
| `/attendance/settings/shifts` | Attendance | Admin. |
| `/attendance/regularizations` | Attendance | |
| `/timesheets` | Timesheet | Own timesheets, weekly grid entry. |
| `/timesheets/{id}` | Timesheet | Detail/edit while Draft or Rejected. |
| `/timesheets/team` | Timesheet | Manager view of reportees' timesheets. |
| `/admin/projects` | Timesheet | Project/task master data. |
| `/payroll/my-payslips` | Payroll | ESS — employee's own payslip history/download. |
| `/payroll/investment-declarations` | Payroll | ESS — employee declares + uploads proofs. |
| `/employees/{id}` → **Salary** tab | Payroll | Salary assignment + history (HR/Finance only, permission-gated per `01-modules-functional-spec.md` §D field-level rules). |
| `/admin/payroll/pay-components`, `/salary-structures` | Payroll | Admin config. |
| `/admin/payroll/statutory-settings`, `/pt-slabs` | Payroll | Admin config, India defaults editable. |
| `/payroll/runs` | Payroll | Run list + status. |
| `/payroll/runs/{id}` | Payroll | Compute → review/override line items → exception report → submit for approval → lock → bank-file export → mark-paid. This is the single most complex screen in the app — build it as a guided multi-step flow, not one long form. |
| `/goals`, `/okr` | Performance | |
| `/approvals` | Workflow | Unified inbox — every request type, filterable. |
| `/my-requests` | Workflow | "Beyond Zoho" unified tracker, §H. |
| `/notifications` | Notifications | |
| `/reports/*` | Reports | One route per report, shared `ReportLayout`. |
| `/admin/users`, `/roles`, `/branding`, `/integrations`, `/audit-log`, `/billing` | Admin | |

## API client generation
Generate a typed client + React Query hooks from the OpenAPI spec
(`03-api-design.md`) via `openapi-typescript` + a thin codegen wrapper, run
as a pre-build script — never hand-write fetch calls for typed endpoints;
hand-written fetch is a smell that the OpenAPI spec and the call have
drifted.

## Testing
- Unit: Vitest + React Testing Library for components/hooks.
- Integration: mock the API at the network boundary (MSW) so tests exercise
  real React Query + component behavior against realistic responses.
- E2E: Playwright covering the golden paths — signup → org setup → invite
  employee → apply leave → approve leave, at minimum, before calling any
  release "done."
