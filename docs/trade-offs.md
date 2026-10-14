# Design Trade-offs

Each section documents a decision that could have gone another way,
the alternatives considered, and why we chose what we did.

## Custom auth instead of ASP.NET Core Identity

**Considered:** Full Identity (`IdentityUser`, `UserManager`, `SignInManager`,
`IdentityDbContext` with six supporting tables).

**Chosen:** A ~80-line `AuthService` using only `IPasswordHasher<User>` for
hashing and custom logic for lockout.

**Why:** The brief needs a login gate for a single persona. Identity provides
OAuth, external providers, 2FA, email confirmation, password reset tokens,
role/permission tables, and claim stores — none of which the requirement
mentions. It would add six tables and rewrite the auth layer we already
have tested. The custom version is 100% testable, understandable in one
sitting, and gives us full control over the lockout policy (stale-failure
window) which Identity doesn't express out of the box.

**Migration path if we ever need it:** swap `AuthService.LoginAsync` for
`SignInManager.PasswordSignInAsync`. Since both use the same PBKDF2 hasher,
existing hashes carry over without a password reset for users.

## User-secrets for configuration, not committed defaults

**Considered:** A development-only signing key checked into
`appsettings.Development.json`, so a reviewer can `git clone && dotnet run`
with zero setup.

**Chosen:** `dotnet user-secrets` for the JWT signing key and the seeded HR
credentials. The app fails fast at startup if the key is missing.

**Why:** Committing a signing key normalizes the wrong pattern. Even though
it's "just dev," the muscle memory of "secrets go in the repo" is the root
cause of a large fraction of production credential leaks. The cost is one
setup step documented in the README. We chose the friction.

In production, the same keys come from environment variables
(`Jwt__SigningKey`, `Seed__HrEmail`, `Seed__HrPassword`). Neither user-secrets
nor appsettings are used there.

## Soft delete over hard delete

**Considered:** `DELETE FROM Employees WHERE Id = ?`.

**Chosen:** `IsActive = false` plus a `TerminationDate` stamp.

**Why:** Terminating an employee is not the same as forgetting they existed.
Payroll history, audit trails, tax reporting, and "how much did we pay
engineers in 2024?" all need the row to stay. Deleting would silently change
historical aggregates and break any future feature that references former
employees. The terminated employee is excluded from the default list but
queryable via `?includeInactive=true`.

## Salary history deferred

**Considered:** A `SalaryRecord` temporal table with `EffectiveFrom` /
`EffectiveTo` per salary change, so we could answer "how has Alice's salary
changed?" and "what was our payroll six months ago?".

**Chosen:** A single current `BaseSalary` / `Bonuses` on the `Employee`
entity.

**Why:** The brief asks to "manage salaries" and "answer questions about how
the org pays people." Both are satisfiable with a current snapshot. A
temporal model costs: a new table, a rewrite of the current-salary query to
a join with `EffectiveTo IS NULL`, a data migration, a backfill seeder, a
new endpoint, and tests. That's a real feature, not a tweak.

**Migration path:** when HR asks for it, add `SalaryRecord`, backfill one
row per employee with `EffectiveFrom = HireDate`, change the list projection
to join on the current record, and add `GET /api/employees/{id}/salary-history`.
The list endpoint's response shape doesn't have to change.

## `DateOnly` for hire and termination dates

**Considered:** `DateTime` (which the initial entity used).

**Chosen:** `DateOnly`.

**Why:** A hire date is a calendar day. Using `DateTime` invites the classic
timezone bug: server stores `2024-03-15T00:00:00Z`, client in UTC-5 renders
`2024-03-14`, HR sees the wrong start date. `DateOnly` has no time component,
so there's nothing to shift. JSON serializes as `"2024-03-15"` and the
ambiguity is gone.

SQLite stores both as `TEXT`; the change is model-level, not storage-level.

## Role claim name `"role"`, not the WS-Fed URI

**Considered:** Leaving `RoleClaimType` at its default and letting ASP.NET
Core map to `ClaimTypes.Role` (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`).

**Chosen:** `RoleClaimType = "role"`, matching a short claim we emit ourselves
in `TokenService`.

**Why:** We already set `MapInboundClaims = false` to keep `sub` short and
readable in the token. The long URI is a legacy of WS-Federation that has no
meaning in a modern bearer-token flow. Setting `RoleClaimType` to the short
string keeps the JWT compact and the code grep-able: emitting `"role"` in
`TokenService` and reading `"role"` in `Program.cs` are two greps that match.

## Timing-equalization on unknown email

**Considered:** Returning 401 immediately when the email doesn't exist.

**Chosen:** Verifying against a pre-computed dummy hash before returning 401.

**Why:** Without it, a login attempt for a non-existent account responds in
~1ms while a real account responds in ~40ms (the PBKDF2 cost). The timing
difference is observable and leaks whether an email is registered — a
known account-enumeration vector. Burning the same PBKDF2 work against a
dummy hash equalizes the response time. The cost is one extra hash verify
per failed login, which is negligible.

## Commit conventions

Early commits use a `data:` prefix for entity and migration changes. `data:`
is not part of the official Conventional Commits spec. Later commits use
`feat:`, `refactor:`, and `test:` correctly. The earlier commits were not
rewritten because the branch is pushed and rewriting shared history is worse
than a non-standard prefix.