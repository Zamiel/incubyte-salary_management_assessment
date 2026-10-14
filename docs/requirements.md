# Requirements

## Goal

Give ACME's HR team a web-based tool to manage salary data for 10,000
employees across multiple countries, replacing spreadsheet-based workflows
and enabling them to answer questions about how the organization pays people.

## Scope & Features

### In scope

**Authentication**
- HR Manager logs in with email + password.
- Credentials are hashed (PBKDF2 via ASP.NET Core `PasswordHasher`).
- Failed attempts are throttled: 5 consecutive failures within 15 minutes
  locks the account for 15 minutes. Stale failures outside that window are
  discarded so a typo last month doesn't lock you out today.
- Successful login returns a JWT carrying the user id, email, and role.
- Tokens expire after 8 hours.

**Authorization**
- Two roles exist: `Hr` and (reserved for future use) `Employee`.
- All employee-management endpoints require the `Hr` role.

**Employee management**
- List employees, paginated. Default page size 50, max 200.
- Filter terminated employees out by default; `?includeInactive=true` shows
  them.
- Create an employee with name, email, job title, department, country,
  currency, base salary, bonus, hire date.
- Terminate an employee (soft delete — sets `IsActive = false` and stamps
  `TerminationDate`).
- Emails are unique across the organization.

**Seeding**
- On first run, seed 10,000 employees with plausible names, departments,
  countries, salaries, and hire dates. Roughly 5% are terminated.
- Seed one HR user, credentials supplied via user-secrets in development.

### Out of scope (deliberately)

**Salary history / temporal records.** The brief asks for "how the org pays
people" — that is satisfiable with a current snapshot. A `SalaryRecord`
temporal table would let us answer "how has Alice's salary changed?" and
"what was our payroll last year?", but it requires a rewrite of the current
salary query to a join, a data migration, and a history endpoint. Deferred
until HR explicitly needs historical reporting. The current model can be
migrated to a temporal one without breaking the list endpoint.

**Salary edits.** Only create and terminate are implemented. Updating base
salary or bonuses isn't in the brief and would be handled by a `PATCH
/employees/{id}/salary` endpoint that also writes to `SalaryRecord` — which
is the reason it's deferred with salary history.

**Role management.** Only one role (`Hr`) exists. No admin UI for creating
users or changing roles. New HR users would be provisioned out-of-band
(seed, script, or direct DB insert) until a proper user management feature
is needed.

**External identity providers.** No OAuth, no SSO, no 2FA. If ACME uses
Google Workspace or Okta, integration would be added via ASP.NET Core
Identity — the current custom auth is intentionally minimal.

**Currency conversion.** Each employee's salary is stored in their local
currency (USD, EUR, GBP, etc.). Comparisons across countries require a
conversion layer that isn't built. Aggregations group by currency rather
than summing across.

**Audit log.** No record of who changed what. Would be needed for a real
salary system; skipped because the brief focuses on HR's read/query use case.

**Frontend.** Not yet implemented. See the README for status.

## Data model

Two entities.

**User** — represents a logged-in HR manager.
- `Id` (string, GUID)
- `Email` (unique, lowercased)
- `DisplayName`
- `PasswordHash`
- `Role` (`Hr` | `Employee`)
- `AccessFailedCount`, `LastFailedAt`, `LockoutEnd` — lockout state

**Employee** — represents a person on payroll.
- `Id` (string, GUID)
- `FirstName`, `LastName`, `Email` (unique)
- `JobTitle`, `Department`
- `Country`, `Currency`
- `BaseSalary`, `Bonuses` (decimal)
- `HireDate` (`DateOnly`), `TerminationDate` (`DateOnly?`)
- `IsActive` (bool)

## Non-functional

- **Persistence:** SQLite. Zero-config, file-based, fine for 10,000 rows.
  Would move to Postgres for multi-instance deployment.
- **API style:** REST, JSON. Timestamps in ISO 8601, dates as `yyyy-MM-dd`.
- **Auth:** JWT bearer tokens, HMAC-SHA256.
- **Secrets:** Never committed. `dotnet user-secrets` in development,
  environment variables in production.
- **Pagination:** Required on any list endpoint. No endpoint returns an
  unbounded result set.
- **Testing:** Unit tests for service-layer business rules and controller
  pagination behavior. Integration tests for the authorization pipeline
  (deferred).

## Open questions

- Should terminated employees appear in payroll analytics? Current design
  excludes them by default.
- Should HR be allowed to delete employees permanently? Current design is
  soft-delete only.
- Multi-tenancy — is ACME the only organization? Current design assumes one.