# 03 — API Design

Read `00-overview.md` and `02-data-model-erd.md` first.

## Conventions
- Base path: `/api/v1/...`. Bump to `/api/v2` only on breaking changes;
  additive fields never require a version bump.
- Resource-oriented REST, plural nouns: `/employees`, `/leave-requests`.
  Verb-shaped actions only where REST doesn't fit naturally, as a sub-path:
  `/employees/{id}/deactivate`, `/workflow-requests/{id}/approve`.
- **Tenant resolution:** subdomain on the host header identifies the tenant
  for browser traffic; for direct API/integration use, an `X-Tenant`
  header or a tenant-scoped API key is required. The resolved tenant is
  always cross-checked against the JWT's `tenant_id` claim — mismatch = 403.
- **Auth header:** `Authorization: Bearer <jwt>`. Access token short-lived
  (~15 min), refresh token (httpOnly cookie, rotated on use) for renewal via
  `POST /api/v1/auth/refresh`.
- **Pagination:** query params `page` (1-based) + `pageSize` (default 25,
  max 100). Response envelope:
  ```json
  { "data": [...], "meta": { "page": 1, "pageSize": 25, "totalItems": 134, "totalPages": 6 } }
  ```
- **Filtering/sorting:** `?sort=-createdAt,lastName` (prefix `-` = desc),
  simple field filters as query params (`?status=Active&departmentId=...`);
  move to a structured filter query param only if/when simple filters prove
  insufficient.
- **Errors:** RFC 7807 `application/problem+json`:
  ```json
  { "type": "https://peoplehq.app/errors/validation", "title": "Validation failed",
    "status": 400, "detail": "...", "errors": { "email": ["Email is required"] } }
  ```
- **Idempotency:** state-changing POSTs that must not double-submit (leave
  request, approval action) accept an `Idempotency-Key` header; server
  dedupes on `(tenant_id, key)` for 24h.
- **File uploads:** client requests a pre-signed Blob Storage URL from the
  API (`POST /files/upload-url`), uploads directly to Blob Storage, then
  sends the resulting blob reference to the actual resource endpoint. API
  never proxies file bytes.
- **Rate limiting:** per-tenant + per-user token bucket at the gateway/
  middleware layer; 429 with `Retry-After`.
- **RBAC enforcement:** every endpoint declares required permission(s) via
  an attribute (`[RequirePermission("employee.write")]`); a policy handler
  checks the user's resolved permissions (role_permissions) — never inline
  `if (role == "Admin")` checks scattered in controllers.

## Endpoint catalogue (v1 scope)

### Auth & Tenant
| Method | Path | Notes |
|---|---|---|
| POST | `/auth/signup` | Public. Creates tenant + admin user. |
| POST | `/auth/login` | |
| POST | `/auth/refresh` | |
| POST | `/auth/logout` | Revokes refresh token. |
| POST | `/auth/mfa/enable`, `/auth/mfa/verify` | |
| GET/PUT | `/tenant` | Org details (Admin only for PUT). |
| GET/PUT | `/tenant/plan` | Current plan + usage; upgrade triggers billing flow. |

### Users & Roles
| GET/POST | `/users` | List/invite users. |
| GET/PUT/DELETE | `/users/{id}` | |
| POST | `/users/bulk-invite` | CSV upload. |
| GET | `/roles`, `/permissions` | |
| POST/PUT | `/roles` | Custom role CRUD (Admin). |

### Org Structure
| GET/POST | `/locations`, `/departments`, `/designations` | |
| GET/PUT/DELETE | `/{resource}/{id}` | |
| POST | `/{resource}/bulk-import` | CSV with validation-preview endpoint first: `/​{resource}/bulk-import/preview`. |
| GET | `/org-chart` | Tree payload for the org chart component. |

### Employees
| GET | `/employees` | List/search/filter, directory view. |
| POST | `/employees` | Create (Admin/HR). |
| GET/PUT | `/employees/{id}` | Field-level write permission enforced server-side, not just hidden in UI. |
| DELETE | `/employees/{id}` | Soft-delete/exit, not hard delete. |
| GET/POST | `/employees/{id}/documents` | |
| GET/POST | `/employees/{id}/skills` | |
| GET | `/employees/{id}/reportees` | For manager views. |
| GET/POST | `/custom-field-definitions` | Admin-configured Employee fields. |

### Onboarding
| GET/POST | `/candidates` | |
| PUT | `/candidates/{id}/stage` | Pipeline transition. |
| POST | `/candidates/{id}/convert` | → creates Employee. |
| GET/POST | `/onboarding-templates` | |
| GET | `/onboarding-tasks?employeeId=` | |
| PUT | `/onboarding-tasks/{id}/complete` | |

### Leave
| GET/POST | `/leave-types`, `/leave-policies` | Admin config. |
| GET | `/leave-balances?employeeId=` | |
| GET/POST | `/leave-requests` | List (self or, for managers, `?scope=reportees`), submit. |
| GET | `/leave-requests/{id}` | |
| POST | `/leave-requests/{id}/cancel` | Requester withdraws. |
| GET | `/leave/team-calendar` | Who's out, date-range query. |
| GET | `/leave/bradford-score?employeeId=` | |
| GET | `/holidays?locationId=` | |

### Attendance
| POST | `/attendance/check-in`, `/attendance/check-out` | |
| GET | `/attendance?employeeId=&from=&to=` | |
| GET/POST | `/shifts`, `/shift-assignments` | |
| GET/POST | `/attendance/regularizations` | |

### Timesheet
| GET/POST | `/projects`, `/projects/{id}/tasks` | Admin-managed master data. |
| GET/POST | `/timesheets?employeeId=&scope=mine\|reportees` | |
| GET/PUT | `/timesheets/{id}` | Edit while Draft/Rejected. |
| POST | `/timesheets/{id}/submit` | → creates a `TimesheetApproval` workflow request. |
| GET | `/reports/timesheet-utilization`, `/reports/timesheet-compliance` | |

### Payroll & Compensation
| GET/POST | `/pay-components`, `/salary-structures` | Admin config. |
| GET/POST | `/employees/{id}/salary-assignments` | Effective-dated; POST creates a new dated row, never edits in place. |
| GET/PUT | `/statutory-settings` | PF/ESI/TDS config, country-scoped. |
| GET/POST | `/pt-slabs` | India Professional Tax state slabs. |
| GET/POST | `/employees/{id}/investment-declarations` | Employee submits. |
| PUT | `/investment-declarations/{id}/verify` | HR/Finance verify or reject. |
| POST | `/payroll-runs` | Initiate a run for a period + employee scope. |
| GET | `/payroll-runs`, `/payroll-runs/{id}` | |
| POST | `/payroll-runs/{id}/compute` | Pulls attendance/leave/timesheet inputs, produces draft items. |
| PUT | `/payroll-runs/{id}/items/{itemId}` | Manual override — `overrideReason` required, always audited. |
| GET | `/payroll-runs/{id}/exceptions` | Pre-check/exception report (§O "Beyond Zoho") — must be clean or acknowledged before submit-for-approval. |
| POST | `/payroll-runs/{id}/submit-for-approval` | → creates a `PayrollRunApproval` workflow request. |
| POST | `/payroll-runs/{id}/lock` | Post-approval; generates payslips; immutable after this. |
| GET | `/payroll-runs/{id}/bank-file` | Exports the disbursement file (Phase 1: manual upload by finance). |
| POST | `/payroll-runs/{id}/items/{itemId}/mark-paid` | Per-employee payment status, independent of the whole run. |
| GET | `/payslips?employeeId=&year=` | |
| GET | `/payslips/{id}/download` | PDF, via pre-signed Blob URL. |
| GET | `/employees/{id}/tax-summary?financialYear=` | Annual reconciliation / Form 16-equivalent. |
| POST | `/employees/{id}/full-final-settlement` | Triggered from the Exit workflow. |

### Performance
| GET/POST | `/goals` | |
| GET/POST | `/okr-cycles`, `/objectives`, `/key-results` | |
| GET/POST | `/feedback-notes` | |

### Generic Workflow (Approvals)
| GET | `/workflow-requests?scope=mine\|pending-my-approval&type=` | The unified inbox from §H. |
| POST | `/workflow-requests` | Generic submit `{requestType, payload}` — Leave/Attendance modules call their own dedicated endpoints which internally create the workflow_request; this generic one covers the simple HR-process types (Department/Location/Designation change, Travel). |
| POST | `/workflow-requests/{id}/approve` | Body: `{comment}`. |
| POST | `/workflow-requests/{id}/reject` | Body: `{comment}` (required). |
| POST | `/workflow-requests/{id}/withdraw` | Requester-only, while Pending. |
| GET/PUT | `/workflow-chain-rules` | Admin config for the no-code approval-chain builder. |
| POST/DELETE | `/delegations` | Approval delegation for a date range. |

### Notifications
| GET | `/notifications` | |
| PUT | `/notifications/{id}/read` | |
| GET/PUT | `/notification-preferences` | |

### Reports
| GET | `/reports/headcount`, `/reports/attrition`, `/reports/leave-utilization`, `/reports/attendance-summary`, `/reports/approval-sla` | Query params for date range/grouping; every report supports `?format=csv`. |

### Admin/Platform (Super Admin only, separate auth scope)
| GET | `/admin/tenants` | Platform ops. |
| POST | `/admin/tenants/{id}/suspend` | |
| POST | `/admin/tenants/{id}/impersonate` | Audited, time-boxed support access. |

## OpenAPI
Generate and commit an OpenAPI 3.1 spec from the ASP.NET Core app
(Swashbuckle/NSwag) — treat it as the contract React's typed API client is
generated from (`openapi-typescript` or similar), so frontend and backend
can never silently drift.
