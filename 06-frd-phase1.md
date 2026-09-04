# 06 — Functional Requirements Document (FRD): Phase 1

**Scope of this document:** Org Structure & Reportees, Employee Onboarding,
Attendance, Leave Management, Timesheet Management, Payroll (Calculation,
Processing, Payslips, Tax Computation), and the two cross-cutting services
every one of those depends on — the Approval Workflow Engine and
Notifications. This is the complete "must-work-before-launch" surface.

This FRD is a requirements catalogue; the *why* and design rationale for
each item live in `01-modules-functional-spec.md`, the schema in
`02-data-model-erd.md`, the contract in `03-api-design.md`, and the UI
inventory in `04-frontend-architecture.md`. Read this document alongside
those, not instead of them.

**ID scheme:** `FR-<MODULE>-<NN>`. **Priority:** `M` = Must have for Phase 1
launch, `S` = Should have, acceptable to slip to a fast-follow if time-boxed.

**Actors:** Employee (E), Manager (M), HR Admin (HRA), Finance/Payroll Admin
(FIN), Recruiter (REC), Tenant Admin (TA — superset of HRA). See
`00-overview.md` §5 for the full role model.

---

## 1. Org Structure & Reportees

| ID | Title | Description | Actors | Priority |
|---|---|---|---|---|
| FR-ORG-01 | Manage Locations | CRUD on Location (name, address, timezone, assigned holiday calendar). Deleting a Location with active employees is blocked with a clear error, not a silent cascade. | HRA/TA | M |
| FR-ORG-02 | Manage Departments | CRUD on Department, including parent-department (sub-department support) and an optional head-of-department employee reference. | HRA/TA | M |
| FR-ORG-03 | Manage Designations | CRUD on Designation (title, optional grade/level). | HRA/TA | M |
| FR-ORG-04 | Assign/Change Reporting Manager | Set or change an employee's manager. A manager cannot be set to create a reporting cycle (A→B→A) — validated server-side before save. | HRA/TA | M |
| FR-ORG-05 | View Org Chart | Interactive tree/graph built from the manager relationship; supports search-by-name and expand/collapse. Must render usably at 500+ employee scale (virtualized, not all-nodes-in-DOM). | E/M/HRA | M |
| FR-ORG-06 | Bulk Import Org Structure | CSV import for Locations/Departments/Designations/Employees with a **validation-preview step** — the system reports every row's pass/fail *before* any row commits; the import is all-or-nothing per file, or partial with an explicit downloadable error report of skipped rows (tenant setting). | HRA/TA | M |
| FR-ORG-07 | Reportees Panel | A manager sees a live list of direct (and, toggleable, indirect) reportees with today's check-in status and on-leave flag at a glance. | M | M |
| FR-ORG-08 | Org-structure change history | Changing an employee's department/designation/manager/location is timestamped and attributable (who, when, old value, new value) and queryable later ("who was X's manager on date Y"). | HRA/TA | S |

## 2. Employee Onboarding

| ID | Title | Description | Actors | Priority |
|---|---|---|---|---|
| FR-ONB-01 | Manage Candidate Records | Create/edit a Candidate (name, contact, resume file, applied designation, source). | REC/HRA | M |
| FR-ONB-02 | Candidate Pipeline Stages | Move a candidate through Offer Sent → Accepted → Documents Collected → Ready to Onboard → Converted/Rejected. Stage changes are timestamped. | REC/HRA | M |
| FR-ONB-03 | Onboarding Checklist Templates | Define a checklist template (task list, each with an owner role and a due-date offset relative to join date), optionally scoped to a department/designation. | HRA/TA | M |
| FR-ONB-04 | Auto-Instantiate Checklist | Converting a Candidate to Employee (or directly adding a new Employee) instantiates the applicable template's tasks against the new hire with concrete due dates. | System | M |
| FR-ONB-05 | Convert Candidate to Employee | One action creates the Employee record pre-filled from Candidate data, creates the linked User account (Invited status), and sends a welcome/setup email. | HRA/TA | M |
| FR-ONB-06 | Onboarding Dashboard | A single view per new hire showing checklist completion status across all owner roles (IT/HR/Manager), not siloed per department. | HRA/M | M |
| FR-ONB-07 | Buddy/Mentor Assignment | A checklist template can name a buddy/mentor role assignment, resolved to a specific colleague at instantiation time. | HRA/M | S |
| FR-ONB-08 | Day-30/Day-90 Pulse Task | Auto-scheduled follow-up task at day 30 and day 90 post-join, surfaced to the manager, to catch early attrition risk. | System/M | S |

## 3. Attendance & Shifts

| ID | Title | Description | Actors | Priority |
|---|---|---|---|---|
| FR-ATT-01 | Check-in / Check-out | Single-action check-in and check-out, timestamped server-side (client-supplied timestamps are never trusted). | E | M |
| FR-ATT-02 | Geo-capture (optional) | If the tenant enables it, capture geolocation at check-in/out; if geo-fencing is configured, flag (not necessarily block) an out-of-perimeter check-in. | E/System | S |
| FR-ATT-03 | Shift Definition | Define named shifts: start/end time, grace period (minutes late still counted on-time), break duration. | HRA/TA | M |
| FR-ATT-04 | Shift Assignment | Assign a shift to an employee or in bulk by department/location, effective-dated (supports rotating patterns). | HRA/TA | M |
| FR-ATT-05 | Attendance Regularization Request | Employee requests correction of a missed/incorrect check-in/out with a mandatory reason. Submits as a workflow request (§ Workflow Engine, FR-WF-01). | E | M |
| FR-ATT-06 | Regularization Approval | Manager approves/rejects via the unified approvals inbox; approval updates the underlying attendance record. | M | M |
| FR-ATT-07 | Attendance Summary | Daily and monthly attendance view, self and (for managers) team aggregate; late-arrival/early-departure flags computed from shift + grace period. | E/M/HRA | M |
| FR-ATT-08 | Overtime Calculation | Hours beyond the assigned shift length (configurable threshold, e.g. >8h/day or >40h/week) computed as overtime per tenant-configured rule. | System | M |
| FR-ATT-09 | Attendance → Payroll Feed | Unpaid/absent days for a pay period are computable as **Loss-of-Pay (LOP) days** and made available to the Payroll module for that employee/period (FR-PAY-09). | System | M |
| FR-ATT-10 | Anomaly Insight (informational) | Surface attendance patterns (e.g. repeated late check-ins) as a non-punitive insight on the manager dashboard; tenant can disable. | System | S |

## 4. Leave Management

| ID | Title | Description | Actors | Priority |
|---|---|---|---|---|
| FR-LVE-01 | Configure Leave Types | Define leave types with accrual rule (fixed annual grant or monthly accrual), carry-forward cap, and document-required-above-N-days rule. | HRA/TA | M |
| FR-LVE-02 | Configure Leave Policy | Assign leave types + entitlement to a group of employees, filterable by department/location/employment type — supports differing policies per office. | HRA/TA | M |
| FR-LVE-03 | Holiday Calendar | Per-location public holiday list; a leave request spanning a holiday auto-excludes that day from the day-count. | HRA/TA | M |
| FR-LVE-04 | View Leave Balance | Real-time balance per employee per leave type with visible accrual history (not an opaque number). | E/M/HRA | M |
| FR-LVE-05 | Apply for Leave | Submit a leave request: type, date range or half-day, reason, optional attachment (required automatically once FR-LVE-01's document-required threshold is crossed). Submits as a workflow request. | E | M |
| FR-LVE-06 | Leave Approval | Manager approves/rejects with optional comment via the unified inbox; balance is provisionally reserved on submission and finalized on approval (rejected/withdrawn requests release the reservation). | M | M |
| FR-LVE-07 | Cancel/Withdraw Leave | Requester withdraws a Pending or already-Approved-but-future request; balance is restored accordingly. | E | M |
| FR-LVE-08 | Team Leave Calendar | "Who's out" view across a date range for a manager's team / the whole org (visibility scope tenant-configurable). | E/M | M |
| FR-LVE-09 | Bradford Factor Score | Compute a Bradford-style score from frequency × total days of unplanned absence over a tenant-configurable rolling period; shown with a tooltip explaining the formula (never opaque). | HRA/M | M |
| FR-LVE-10 | Leave → Payroll Feed | Approved unpaid leave for a period contributes to LOP days consumed by Payroll (FR-PAY-09), alongside attendance-derived LOP. | System | M |
| FR-LVE-11 | Leave Forecast | Employee-facing widget projecting year-end balance given approved-but-future leave. | E | S |
| FR-LVE-12 | Blackout Period Warning | Tenant-configurable date ranges (e.g. month-end close) show a warning (non-blocking by default) at request time. | System | S |

## 5. Timesheet Management

| ID | Title | Description | Actors | Priority |
|---|---|---|---|---|
| FR-TS-01 | Manage Projects & Tasks | CRUD on Project (name, code, optional client, billable-default flag) and its Tasks. | HRA/TA | M |
| FR-TS-02 | Timesheet Entry | Daily/weekly entry of hours; **Simple** mode (total hours/day) or **Detailed** mode (project + task required), per tenant configuration. | E | M |
| FR-TS-03 | Submit Timesheet | Submitting a period's timesheet creates a `TimesheetApproval` workflow request; a Submitted timesheet is locked from further edits until acted on. | E | M |
| FR-TS-04 | Approve/Reject Timesheet | Manager approves/rejects in the unified inbox; a rejected timesheet reopens for edit and resubmission. | M | M |
| FR-TS-05 | Overtime/Billable Flags | Entries beyond assigned shift hours flag as overtime (configurable multiplier); each entry flags billable/non-billable, defaulting from the project. | System/E | M |
| FR-TS-06 | Utilization & Billing Reports | Utilization % per employee/team and billable-hours-by-project reports. | M/HRA | M |
| FR-TS-07 | Timesheet Compliance Report | Report of who has not submitted by a configured cutoff. | HRA/M | M |
| FR-TS-08 | Timesheet → Payroll Feed | For employees on Hourly/Contract pay type, that period's **Approved** timesheet total hours × rate is available to Payroll as a variable earning line (FR-PAY-09). Unapproved timesheets never feed payroll. | System | M |
| FR-TS-09 | Copy-Last-Week / Quick Fill | One-click duplication of the prior period's entries as a starting point. | E | S |

## 6. Payroll & Compensation (Calculation, Processing, Payslips, Tax)

> Statutory logic below defaults to **India** (PF/ESI/Professional Tax/TDS)
> per the scope assumption in `01-modules-functional-spec.md` §O — confirm
> before implementation if the target market differs.

| ID | Title | Description | Actors | Priority |
|---|---|---|---|---|
| FR-PAY-01 | Define Pay Components | CRUD on Earning/Deduction components with calculation rule (Flat / %-of-Basic / %-of-CTC / Formula) and taxable flag. Ship India-default components pre-seeded, editable. | FIN/TA | M |
| FR-PAY-02 | Define Salary Structure Templates | Group pay components into a named, reusable structure template. | FIN/TA | M |
| FR-PAY-03 | Assign Employee Salary | Assign a structure + CTC + pay type (Salaried/Hourly/Contract) to an employee with an **effective date**; a revision inserts a new dated row, never overwrites — full salary history is retained and viewable. | FIN/HRA | M |
| FR-PAY-04 | Configure Statutory Settings | Tenant-level PF (employee/employer %, wage ceiling), ESI (threshold, %), and TDS regime defaults; India Professional Tax state-slab table management. | FIN/TA | M |
| FR-PAY-05 | Investment Declaration Submission | Employee declares planned tax-saving investments (80C/80D/HRA/etc.) with an amount, and uploads proof documents before a tenant-configured deadline. | E | M |
| FR-PAY-06 | Investment Declaration Verification | Finance/HR reviews each declaration → Verified or Rejected; a Rejected/unverified declaration reverts to the regime default and forces a TDS recompute on the next run. | FIN/HRA | M |
| FR-PAY-07 | Tax Regime Selection | Employee selects Old vs. New tax regime (India), revisable once per financial year per statutory rule. | E | M |
| FR-PAY-08 | Initiate Payroll Run | Create a run for a pay period + employee scope (all, or filtered by department/location). | FIN | M |
| FR-PAY-09 | Compute Payroll Run | Pulls: approved attendance/leave LOP days (FR-ATT-09, FR-LVE-10), approved timesheet hours for hourly/contract employees (FR-TS-08), and any one-off additions/deductions entered for the run (bonus, arrears, reimbursement, loan/advance recovery); computes gross, statutory deductions, and net pay per employee into a **Draft** state. | System/FIN | M |
| FR-PAY-10 | Manual Line-Item Override | A draft payroll line can be manually adjusted; a reason is **mandatory** and the change is fully audited (actor, timestamp, before/after). | FIN | M |
| FR-PAY-11 | Payroll Exception Report | Before a run can be submitted for approval, an automated exception report flags: salary change >configurable % vs. last run, an employee with no attendance data for the period, duplicate bank account numbers across employees, and any negative net pay. Exceptions must be resolved or explicitly acknowledged before submission proceeds. | System/FIN | M |
| FR-PAY-12 | Payroll Run Approval | Submitting a computed, exception-clear run creates a `PayrollRunApproval` workflow request routed to the configured Finance/HR Admin approver(s). | FIN | M |
| FR-PAY-13 | Lock Run & Generate Payslips | On approval, the run is locked (immutable) and a payslip is generated per employee for that period. Any post-lock correction requires a new adjustment run, never an edit to the locked one. | System | M |
| FR-PAY-14 | Bank Disbursement File | Generate a bank-standard payment file (e.g. NEFT-compatible CSV) for the finance team to upload manually (Phase 1; direct integration is Phase 4). | FIN | M |
| FR-PAY-15 | Per-Employee Payment Status | Track Pending/Paid/Failed per employee independent of the run as a whole; a failed transfer is reprocessable without re-running payroll for everyone else. | FIN | M |
| FR-PAY-16 | View/Download Payslip | Employee views and downloads their own payslip (PDF) via ESS, including YTD figures; payslips are never editable post-generation. | E | M |
| FR-PAY-17 | Annual Tax Reconciliation | Year-end reconciliation of total TDS deducted vs. actual liability per employee; generates an India Form-16-equivalent statement. | FIN/E | M |
| FR-PAY-18 | Full & Final Settlement | Triggered by an approved Exit request: computes pro-rated final salary, leave encashment per policy, pending reimbursements/recoveries, and gratuity where applicable, as a distinct settlement payslip. | FIN | M |
| FR-PAY-19 | Payroll & Tax Summary Reports | Payroll cost summary (by period/department/location) and tax summary reports, exportable. | FIN/HRA | M |

## 7. Cross-Cutting: Approval Workflow Engine

| ID | Title | Description | Actors | Priority |
|---|---|---|---|---|
| FR-WF-01 | Generic Request Submission | Any of Leave, Attendance Regularization, Timesheet, Payroll Run, (Phase 2: HR Process/Travel/Exit) submits through one polymorphic request record — see `02-data-model-erd.md`. | System | M |
| FR-WF-02 | Approval Chain Resolution | The approver chain for a request type is resolved from tenant-configured rules (e.g. direct manager; direct manager then skip-level above N days) at **submission time** and does not change if the org structure changes later while the request is in flight. | System | M |
| FR-WF-03 | Sequential Approval Processing | Steps are actioned in order; a rejection at any step ends the request as Rejected. (Parallel any-of/all-of steps are modeled in schema for future use, not required to be exercised by any Phase 1 request type.) | System | M |
| FR-WF-04 | Approve / Reject / Withdraw | An approver acts with an optional (Approve) or mandatory (Reject) comment; a requester may withdraw while still Pending. | M/FIN/E | M |
| FR-WF-05 | Unified Approvals Inbox | One inbox surfaces every pending request of every type awaiting the current user's action, filterable by type. | M/FIN | M |
| FR-WF-06 | Approval Delegation | A user delegates their approval authority to a named colleague for a date range; delegated actions are visibly attributed ("Approved by X on behalf of Y"). | M/FIN | M |
| FR-WF-07 | Pending-Approval Reminder | A configurable reminder notification fires if a step sits un-actioned beyond N days. | System | S |

## 8. Cross-Cutting: Notifications

| ID | Title | Description | Actors | Priority |
|---|---|---|---|---|
| FR-NOT-01 | In-App Notifications | Bell/inbox of events relevant to the user (request submitted/actioned, payslip published, declaration deadline). | E/M/FIN/HRA | M |
| FR-NOT-02 | Email Notifications | Same event set available as email, respecting per-category preference. | System | M |
| FR-NOT-03 | Notification Preferences | Per-user, per-category channel preference (In-app / Email / Both / None where appropriate). | E | M |

---

## Traceability
Every FR above maps 1:1 to a section in `01-modules-functional-spec.md`
(by module letter) and to specific tables in `02-data-model-erd.md` and
endpoints in `03-api-design.md`. When implementing an FR, confirm its
schema and API contract exist in those documents before writing code; if
they don't yet, add them there first so the documents and the system never
drift apart, per the working agreement in `05-enhancements-and-roadmap.md`.
