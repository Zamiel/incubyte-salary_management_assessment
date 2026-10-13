### Demo credentials

For local development only:

- Email: `hr@acme.org`
- Password: `Demo123!`

These credentials exist because the app seeds a single HR user on first
run so a reviewer can log in immediately without provisioning anything.
They are read from `Seed:HrEmail` / `Seed:HrPassword` in user-secrets and
are not present in the repository.

In a real deployment this pattern would not be used. Credentials would
never be committed, documented in a public README, or hard-coded in a
seed step. Instead:

- Production seeds run with credentials supplied via environment variables
  (`Seed__HrEmail`, `Seed__HrPassword`) or a secrets manager
  (Azure Key Vault, AWS Secrets Manager), never via user-secrets.
- The seeding step is disabled entirely outside Development
  (`if (app.Environment.IsDevelopment())`).
- Real user onboarding goes through an invite/reset flow, not a seeded
  account with a known password.

The goal here is to make the project runnable end-to-end for evaluation
without leaking anything that would matter in production. If this code
were ever deployed, the seed block would be removed before the first
production deploy.