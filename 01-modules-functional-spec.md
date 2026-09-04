# 01 — Functional Specification, by Module

Read `00-overview.md` first for principles, roles, and tenancy model.
Each module lists: purpose, key screens, business rules, and edge cases to
handle. "Beyond Zoho" callouts are intentional enhancements — build these,
don't skip them as scope creep.

---

## A. Platform & Tenant Onboarding
**Purpose:** turn a visitor into a running tenant with zero human intervention.

- Public marketing/signup page → **Create Organization** form: org name,
  admin name/email/password, company size band, industry (optional,
  for future benchmarking features), chosen subdomain (live-validated for
  availability as they type).
- On submit: create `Tenant`, `User` (TenantAdmin role), seed defaults
  (one `Location` = "Head Office", one `Department` = "General", one
  `Designation` = "Employee", standard `LeaveType`s: Casual, Sick, Earned/
  Annual — all editable/deletable after setup).
- Email verification required before the tenant can send outbound email
  (mirrors the "verify your account" banner seen in the reference recording)
  but should **not** block core app usage — only outbound comms and domain
  addition, exactly as scoped in the recording.
- **Org Setup Wizard** (post-signup, skippable, resumable): Organization
  Details (legal name, logo, industry, timezone, work week) → Locations →
  Departments → Designations → Invite first few employees → Done. Each step
  writes immediately (no giant final submit) so partial completion is safe.
- **Plan/Billing:** Starter (free/trial, seat-capped), Growth, Enterprise.
  Stripe or a payment gateway integration is a Phase 4 item; v1 can run on a
  manually-assigned plan with feature flags already wired to it.
- **Beyond Zoho:** self-serve plan upgrade/downgrade screen with a clear
  seat-usage meter; a "sandbox/demo tenant" toggle for prospects to explore
  with seeded fake data before committing real org data.

## B. Identity & Access Management
**Purpose:** authn/authz for every user in every tenant.

- Login (email + password, tenant resolved by subdomain), "forgot password"
  flow, MFA (TOTP) optional-then-required-by-plan.
- **Invite-based user creation** (not open self-registration inside a
  tenant): Admin/Manager invites by email → invitee sets password on first
  login. Bulk invite via CSV upload.
- Role assignment (system roles + custom roles per `00-overview.md` §5).
- Session/refresh-token handling; "sign out of all devices" action.
- Delegation: an employee can delegate their approval authority to a
  colleague for a date range (seen in the recording's Home → Delegation
  tab) — delegated approvals are clearly marked as such in the approver's
  queue ("Approved by X on behalf of Y").
- **Beyond Zoho:** SSO (SAML/OIDC) as an Enterprise-plan feature; a visible
  "active sessions" list per user for security self-service; login
  anomaly notification (new device/location) via email.

## C. Organization Structure
**Purpose:** the skeleton every other module hangs off.

- **Locations:** name, address, timezone, work week/holiday-calendar
  assignment.
- **Departments:** name, parent department (self-referencing, supports
  sub-departments), head/lead (an Employee reference).
- **Designations:** title, level/grade (for future compensation bands).
- **Org Chart:** interactive tree/graph view built from the Employee →
  Manager relationship; search-within-chart; export as image (nice-to-have).
- Bulk import (CSV) for all of the above with a validation-preview step
  before commit (never silently partial-import).
- **Beyond Zoho:** versioned org structure — changing a department's parent
  or an employee's manager records an effective-dated history row so
  "who reported to whom on 1 March" is answerable later (useful for audits
  and for correct historical approval-chain resolution).

## D. Employee Database
**Purpose:** the single source of truth for a person's employment record.

- **Employee profile** sections: Personal (name, DOB, contact, emergency
  contact), Employment (designation, department, location, manager, employee
  ID, join date, employment type — full-time/part-time/contractor),
  Documents (ID proofs, contracts — stored in Blob Storage, metadata in DB),
  Bank/statutory details (fields only, no payroll processing in v1),
  Custom fields (tenant-defined, see below).
- **Field-level edit permissions:** some fields are employee-editable
  (phone, address, emergency contact), others are HR-only (designation,
  salary-adjacent, manager) — model as a permission matrix per field group,
  not hardcoded per-field ifs scattered in code.
- **Custom fields:** Tenant Admin can define additional fields (text/number/
  date/dropdown/checkbox) attached to the Employee entity without a schema
  migration — use an EAV-lite `EmployeeCustomFieldValue` table keyed by a
  tenant-defined `CustomFieldDefinition` (see ERD). Needed because every
  company wants one or two fields Zoho-shaped systems don't anticipate.
- **Employee list/directory:** searchable, filterable (department, location,
  status), with saved views; directory is visible to all employees
  (contact-card level fields only) unless the tenant restricts it.
- **Employee lifecycle status:** Invited → Active → On Leave → Suspended →
  Exited, driving what actions are available (e.g. an Exited employee can't
  check in).
- **Beyond Zoho:** a lightweight **skills/certifications** list per employee
  (name, level, expiry for certifications) — small addition, high value for
  compliance-heavy industries and future internal-mobility features.

## E. Onboarding (pre-boarding + new hire)
**Purpose:** everything from "offer accepted" to "fully set up on day one".

- **Candidate pipeline:** Candidate record (name, contact, resume file,
  designation applied for, source), stages (Offer Sent → Accepted →
  Documents Collected → Ready to Onboard → Converted to Employee).
- **Onboarding checklist templates** per department/designation: a list of
  tasks with an owner role (IT: laptop/accounts, HR: paperwork, Manager:
  first-week plan), due-relative-to-join-date, and completion tracking
  visible on a single onboarding dashboard per new hire.
- Converting a Candidate to an Employee auto-creates the Employee record
  pre-filled from candidate data, triggers account creation + welcome email,
  and instantiates the checklist.
- **Beyond Zoho:** a **buddy/mentor assignment** field on the checklist
  template, and a day-30/day-90 pulse-check task auto-scheduled to catch
  early attrition risk (ties into engagement surveys in §L).

## F. Attendance & Shifts
**Purpose:** track who worked when.

- **Check-in/Check-out:** single button, records timestamp + optional
  geolocation (if the tenant enables geo-fencing) + optional selfie capture
  (Enterprise/compliance-heavy tenants) + IP.
- **Shifts:** named shift definitions (start/end, break rules, grace
  period for late marking); **shift assignment** per employee or bulk by
  department/location; support rotating shift patterns.
- **Regularization:** employee requests a correction to a missed/incorrect
  check-in/out with a reason; goes through the generic approval workflow
  (§J below); manager approves/rejects.
- **Attendance summary/report:** daily/monthly view per employee and
  aggregate per team; late-arrival and early-departure flags; overtime
  calculation (configurable rules per tenant, e.g. >8h/day or >40h/week).
- **Beyond Zoho:** anomaly flagging — e.g., a pattern of Friday sick-leave
  or persistently late check-ins surfaces as an insight on the manager's
  dashboard (not punitive by default, informational), configurable off.

## G. Leave Management
**Purpose:** request, approve, and track time off, plus compliance signals.

- **Leave types:** tenant-configurable (Casual, Sick, Earned/Annual,
  Unpaid, Maternity/Paternity, Bereavement, …), each with accrual rule
  (fixed annual grant vs. monthly accrual), carry-forward rule (cap, expiry),
  encashment eligibility (flag only in v1), and whether it requires
  supporting documents (e.g. medical certificate above N days).
- **Leave policy:** assigns leave types + entitlement rules to a group of
  employees (by location, department, or employment type) — supports
  different policies for different offices/countries.
- **Holiday calendar:** per-location public holidays; leave requests
  spanning a holiday auto-exclude that day from the day-count.
- **Leave balance:** real-time balance per employee per leave type, with
  accrual history visible (not a black box).
- **Apply for leave:** date range or half-day, leave type, reason, optional
  attachment; conflict warning if teammates are already out (visibility into
  team calendar); goes through the generic approval workflow.
- **Leave summary/team calendar:** "who's out today/this week" view for
  managers and the whole team (helps planning, reduces surprise absences).
- **Bradford Factor score:** computed per employee from frequency × total
  days of *unplanned* absence over a rolling period, surfaced to HR/managers
  as an early attrition/wellbeing signal (present in the reference product;
  keep it, it's genuinely useful, but make the formula and period
  tenant-configurable and clearly explained via a tooltip — don't ship an
  opaque score).
- **Beyond Zoho:** a **leave forecast** widget showing projected balance at
  year-end given approved-but-future leave, so employees don't over-request;
  configurable **blackout periods** (e.g. no leave during month-end close)
  that warn (not necessarily block) at request time.

## H. ESS / MSS Self-Service Portal
**Purpose:** the employee/manager "home" — the highest-traffic surface in
the whole app, so it deserves the most UX care.

- **Employee Home/Overview:** profile summary card, check-in widget,
  quick links (apply leave, view payslip [future], raise a request),
  announcements/feed, "my reportees" panel (managers only) with each
  reportee's check-in status at a glance.
- **HR Process requests** (generic workflow instances, see §J): Department
  Change, Location Change, Designation Change, Travel Request, Travel
  Expense, Exit/Resignation — each a small form + approval chain.
- **Approvals inbox:** a manager's single queue across *all* request types
  (leave, regularization, HR process, expense) — do not scatter approvals
  across separate module-specific inboxes; one inbox, filterable by type.
- **Notifications center:** in-app bell + email digest, per-user
  notification preferences (which events trigger email vs. in-app only).
- **Beyond Zoho:** a unified **"My Requests" tracker** showing every request
  the employee has ever submitted across all types with live status,
  because chasing "what happened to my request from 3 weeks ago" across
  different tabs is a common real complaint with tab-per-request-type UIs.

## I. Performance & Goals (incl. OKR)
**Purpose:** align individual work to team/company objectives and support
review cycles.

- **Goals:** simple goal records (title, description, target date, progress
  %, owner), viewable this-week/all, manager can add goals for reportees.
- **OKR:** Objectives (qualitative, time-boxed to a cycle) with child Key
  Results (quantitative, measurable, 0–100% or numeric target/current);
  OKR cycles (quarterly by default, tenant-configurable); company-level
  Objectives can be linked as parents to team/individual Objectives so
  alignment is visible top-down.
- **Appraisal cycles** (Phase 2+): self-review → manager review → optional
  calibration → final rating; configurable review templates (competency
  questions, rating scale).
- **Beyond Zoho:** lightweight **continuous feedback** — anyone can post a
  short praise/feedback note to a colleague, visible on their profile
  timeline, separate from formal appraisal (cheap to build, high adoption,
  strongly requested in most modern HR tools because annual-only review
  feels stale).

## J. Approval Workflow Engine (cross-cutting, build once)
**Purpose:** one engine backing Leave, Regularization, HR Process requests,
Travel, Exit, and Onboarding-step approvals — not five bespoke ones.

- **Request** = polymorphic record: `request_type`, `requester_id`,
  `payload` (JSONB — type-specific fields), `status`
  (Draft/Pending/Approved/Rejected/Cancelled/Withdrawn), timestamps.
  `request_type` values in Phase 1: `LeaveRequest`, `AttendanceRegularization`,
  `TimesheetApproval`, `PayrollRunApproval`; Phase 2 adds `DepartmentChange`,
  `LocationChange`, `DesignationChange`, `TravelRequest`, `TravelExpense`,
  `ExitRequest`.
- **Approval chain resolution:** rule-based per `request_type` + tenant
  config — e.g. "direct manager only", "manager then skip-level for >5
  days", "department head for designation changes". Store the resolved
  chain as `WorkflowApprovalStep` rows at submission time (not
  re-resolved live) so later org changes don't retroactively alter a
  request already in flight.
- Each step: approver, sequence, status, acted-at, comment. Support both
  **sequential** (one after another) and **parallel** (any-one-of / all-of)
  approval steps for future flexibility, even if v1 only uses sequential.
- **Notifications** fire on: submitted, each step actioned, fully
  approved/rejected, and a configurable reminder if a step is pending > N
  days (nudge the approver).
- **Beyond Zoho:** a tenant-facing **no-code approval-chain builder**
  (simple visual "if X then approver Y" rule list, not a full BPMN editor)
  so HR Admins configure this themselves instead of filing a support
  ticket — this is a genuine differentiator vs. rigid legacy HR tools.

## K. Notifications
- Channels: in-app, email (always available), push (PWA/mobile, Phase 4).
- Digest vs. real-time preference per notification category, per user.
- Template management (Admin can edit email templates with placeholders)
  so tenants can adjust tone/branding of system emails.

## L. Reports & Analytics
- Standard reports: headcount (by dept/location/designation over time),
  attrition rate, leave utilization, attendance summary, onboarding
  time-to-productivity, approval SLA (average time-to-approve by type).
- Export to CSV/XLSX for every list screen and every report, not just a
  curated subset.
- Dashboard widgets configurable per role (Admin dashboard ≠ Manager
  dashboard ≠ Employee dashboard).
- **Beyond Zoho:** an **eNPS/pulse-survey** module (single-question
  recurring survey, anonymous by default) feeding an engagement trend chart
  on the Admin dashboard — cheap to build, and "engagement" is the single
  most-requested HR-suite feature not present in the recorded flow.

## M. Admin / Settings
- Organization Details, Organization Policy, Organization Structure
  (Locations/Departments/Designations — cross-ref §C), Domains &
  Rebranding (custom domain, logo, color accent — NOT copying Zoho's
  literal theme, just the *capability*), Email Authentication (SPF/DKIM
  setup for outbound mail domains), Users & Roles, integrations.
- **Integrations hub** (Phase 3+): Slack/Teams (notifications + slash
  commands like `/leave apply`), Google/Outlook calendar sync (leave and
  holidays as calendar events), biometric device import (CSV/API), generic
  webhook outbound events for anything not natively integrated.
- **Audit log viewer:** searchable, filterable by actor/entity/date —
  the UI surface for the audit trail required in `00-overview.md` §6.
- **Data export & deletion center:** self-serve GDPR-style data subject
  request handling (see NFRs).

## N. Timesheet Management
**Purpose:** track time spent against projects/tasks for utilization,
client billing, and — for hourly/contract employees — as direct payroll
input. Phase 1 scope (pulled forward because Payroll depends on it for
hourly/contract pay types).

- **Projects & Tasks:** lightweight master data only (name, code, optional
  client name, billable-by-default flag, active/inactive) — this is not a
  project-management suite, just enough structure to tag time meaningfully.
- **Timesheet entry:** daily or weekly grid, hours logged per project/task
  per day with an optional note. Two entry modes, tenant-configurable:
  **Simple** (total hours/day, no project breakdown) for tenants that don't
  bill by project, and **Detailed** (project + task required) for those
  that do.
- **Submission & approval:** employee submits a period's timesheet →
  routed through the generic Workflow Engine (§J) as a
  `TimesheetApproval` request → manager approves/rejects in the same
  unified inbox as every other request type. A rejected timesheet reopens
  for edits and resubmission.
- **Overtime & billable flags:** hours beyond the employee's assigned
  shift length are flagged overtime (configurable multiplier per tenant);
  each entry is billable or non-billable (defaults from the project, can
  be overridden per entry).
- **Payroll integration:** for employees whose salary structure is
  hourly/contract-based, that period's **approved** timesheet total feeds
  the payroll run as a variable earning line (§O); for salaried employees
  timesheets are utilization/billing data only by default, though a
  tenant can still require submission for compliance or client-billing
  reasons.
- **Reports:** utilization % per employee/team, billable-hours-by-project,
  and a timesheet-compliance report (who hasn't submitted, by cutoff).
- **Beyond Zoho:** a "copy last week" quick-fill action and an optional
  running start/stop timer as an alternative to manual entry — both are
  small additions that measurably improve submission compliance in
  practice, which is the single biggest real-world failure mode of
  timesheet features.

## O. Payroll & Compensation
**Purpose:** compute, approve, disburse pay accurately and compliantly,
and give every employee a clear, always-available payslip. Phase 1 scope
(pulled forward from a v1 non-goal — see `05-enhancements-and-roadmap.md`
for what's still deferred to later phases).

> **Scope assumption to confirm before engineering starts:** statutory
> rules default to **India** (Provident Fund, ESI, Professional Tax, TDS/
> Income Tax) since that's what the reference recording and this program
> are grounded in. The engine below is architected as a **pluggable
> rule set keyed by country/state** specifically so a second country is
> an additive rule set later, not a rewrite — but only India ships in
> Phase 1. If the actual target market isn't India, say so before the
> statutory calculation logic is built.

- **Pay components:** tenant-defined building blocks, each an Earning or
  Deduction, Fixed or Variable, Taxable or Non-taxable, with a calculation
  rule (Flat amount / % of Basic / % of CTC / Formula referencing other
  components). Ship India-sensible defaults — Basic, HRA, Conveyance/
  Transport Allowance, Special Allowance, Employer PF Contribution,
  Employer ESI Contribution — all tenant-editable, none hardcoded in code.
- **Salary structure templates:** named groupings of pay components with
  default percentages/formulas, assignable to employees or designations.
- **Employee salary assignment:** assign a CTC + structure to an employee
  with an **effective date**. A salary change creates a new dated record;
  it never overwrites the prior one — salary history is a compliance
  requirement, not a nice-to-have, and Full & Final settlement and tax
  computation both depend on it being accurate.
- **Statutory configuration (tenant-level, India defaults):**
  - **Provident Fund (PF):** employee + employer contribution %, wage
    ceiling, opt-out eligibility above ceiling.
  - **ESI:** eligibility wage threshold, employee + employer contribution
    %, automatic drop when salary crosses the threshold per statutory rule.
  - **Professional Tax (PT):** state-wise slab table — tenant selects the
    applicable state per work location (locations already carry an
    address/state, §C).
  - **Income Tax (TDS):** annual projected income computed from the
    employee's salary structure and declared exemptions; monthly TDS =
    (projected annual tax − tax already deducted) ÷ remaining months in
    the financial year, **recalculated every run** as declarations or
    salary change — never a one-time-at-joining calculation.
  - **Investment / exemption declarations:** employee declares planned
    investments (80C, 80D, HRA exemption backed by rent receipts, etc.) at
    year start or joining, submits proof documents before the tenant's
    configured deadline; HR/Finance verifies each declaration
    (Declared → Proof Submitted → Verified/Rejected); an unverified or
    rejected declaration falls back to the regime default and triggers a
    TDS recalculation on the next run. Employee also chooses their
    **tax regime** (Old vs New, India-specific) with the option to
    revise once per financial year per statutory rule.
- **Payroll run lifecycle (monthly cycle):**
  1. **Initiate** — a run for a pay period + a set of employees (by
     location/department or all).
  2. **Pull inputs** — approved attendance/leave (unpaid/LOP days reduce
     pay pro-rata), approved timesheet hours (hourly/contract pay types,
     §N), and one-off additions/deductions entered for that run (bonus,
     arrears, reimbursement, loan/advance recovery).
  3. **Compute** — gross, statutory deductions, net pay per employee. A
     computed run is a **draft**: line items are editable with a
     mandatory audit trail (who changed what, and why) — payroll
     overrides must never be silent.
  4. **Approve** — Finance/HR Admin sign-off via the same generic
     Workflow Engine as a `PayrollRunApproval` request; a payroll run is
     itself a workflow instance, not a special case.
  5. **Lock & generate payslips** — an approved run is locked and becomes
     immutable; corrections happen via a follow-up adjustment run, never
     by editing a locked run.
  6. **Disburse** — export a bank-standard payment file (e.g. an
     NEFT-compatible CSV) for the tenant's finance team to upload in
     Phase 1 (direct bank/gateway integration is Phase 4); track
     per-employee payment status (Pending/Paid/Failed) independently so a
     single failed transfer can be reprocessed without re-running payroll
     for everyone.
- **Payslips:** generated per employee per pay period at run lock —
  earnings/deductions breakdown, net pay, year-to-date figures, employer
  PF/ESI contribution shown separately as informational (not part of the
  employee's own net-pay math); available to the employee via ESS as a
  downloadable PDF, never editable after generation.
- **Tax computation & year-end statement:** an annual reconciliation of
  total TDS deducted vs. actual liability per employee; generates the
  India Form 16-equivalent statement at financial year-end. Statutory
  *e-filing* itself (PF ECR, ESI return, PT return, TDS/24Q) is out of
  Phase 1 scope — see `05-enhancements-and-roadmap.md`.
- **Full & Final settlement:** triggered from the Exit request workflow
  (§H/§J) — computes final pay pro-rated to the last working day, leave
  encashment per tenant policy, pending reimbursements/recoveries, and
  gratuity where applicable; produces a distinct final-settlement payslip
  rather than folding into the regular monthly cycle.
- **Beyond Zoho:** an automatic **payroll pre-check/exception report**
  runs before a payroll run can be submitted for approval — flags a
  salary changing more than a configurable % since the last run, an
  employee with no attendance data for the period, a duplicate bank
  account number across employees, or a negative net pay — surfaced to
  the approver *before* sign-off. Payroll incidents in practice are almost
  always caught too late; catching them at approval time instead of after
  disbursement is one of the highest-value things this module can do.

---

## "Most needed options" — recommended additions not in the recording
These are commonly expected in a serious HR SaaS and are cheap relative to
their value; include them in scope even though they weren't in the captured
screens:

1. **Document e-signature** for offer letters/contracts (own simple
   click-to-sign flow in v1; DocuSign/Adobe Sign integration later).
2. **Asset management** — track laptops/equipment issued to an employee,
   return on exit (small table, ties into offboarding checklist).
3. **HR Helpdesk/case management** — employees raise HR questions as
   tickets with SLA tracking, instead of email/Slack chaos.
4. *(Timesheet management moved into core Phase 1 scope — see §N.)*
5. **Exit/offboarding workflow** — mirror of onboarding: clearance
   checklist (IT, Finance, Manager), exit interview form, final
   settlement status tracking (amounts computed elsewhere, just status
   here in v1).
6. **Bulk actions everywhere** — bulk approve, bulk import, bulk role
   assignment; single-row-at-a-time UIs age badly as tenants grow.
7. **Audit log + data export/deletion center** (also listed under §M —
   worth calling out as non-negotiable for any SaaS handling employee PII).
8. **Configurable dashboards** per role rather than one fixed layout.
9. **API keys + webhooks** for the tenant itself (not just internal
   integrations) — mid-market customers expect to be able to connect their
   own tools.
10. **In-app product tour/checklist** for new tenants (mirrors the "Home"
    tour seen in the recording) — keep this pattern, it measurably improves
    activation.
