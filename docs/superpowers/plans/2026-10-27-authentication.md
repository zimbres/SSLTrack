# Authentication and configuration implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for native execution or superpowers:subagent-driven-development for delegated execution. Track steps with checkboxes.

**Goal:** Implement the approved authentication, profiles, agent service accounts, and database SMTP configuration.

**Architecture:** SQLite persists human users, scoped service accounts, and protected SMTP configuration. Cookie/OIDC authentication serves humans; Basic authentication serves agents. Shared server authorization guards interactive operations and REST endpoints.

**Tech Stack:** ASP.NET Core 10, EF Core SQLite, MudBlazor, ASP.NET Core authentication and Identity password hashing, MailKit.

**Spec:** `docs/superpowers/specs/2026-10-27-authentication-design.md`

## Global constraints

- No public registration or OIDC auto-provisioning; normalized email is the login identifier.
- Roles are Admin and ReadOnly. Service accounts have one assigned agent and no web access.
- Existing databases must migrate without replacement; SMTP configuration imports once.
- Browser writes require antiforgery protection; production Basic authentication requires HTTPS.
- Secrets must not appear in responses, logs, or committed configuration.

## Review focus

- Established Blazor sessions must lose mutation permission when an administrator disables or demotes the user.
- An agent report must never change assignment, silence flags, domain identity, or another agent's certificate data.
- Case differences in email must not bypass uniqueness or OIDC admission checks.
- Blank SMTP password edits must preserve the saved password; explicit clearing must remove it.
- Bootstrap reruns must not replace accounts or recreate an administrator after installation.

## Task 1: Account persistence and admission

**Files:** create `Models/AppUser.cs`, `Models/ServiceAccount.cs`, `Services/AccountService.cs`, authentication tests under `tests/SSLTrack.Tests/`; modify `Data/ApplicationDbContext.cs`; add EF migration and snapshot changes.

**Interfaces:** AccountService owns normalized-email lookup, password verification, account lifecycle, OIDC issuer/subject linking, and bootstrap. It returns account records or explicit admission failures; passwords are hashed with PasswordHasher, never returned.

- [x] Add tests for duplicate normalized emails, wrong/locked passwords, disabled accounts, forced password changes, unknown/unverified OIDC users, issuer/subject mismatch, final-admin protection, and bootstrap reruns. Run `dotnet test tests/SSLTrack.Tests` and confirm missing-feature failures.
- [x] Implement persistence, unique indexes, validation, lockout, account versioning, and lifecycle methods; add explicit bootstrap settings and migration.
- [x] Run account tests against temporary SQLite databases, including applying old then new migrations without losing domain rows.

## Task 2: Human sessions and authorization

**Files:** create `Security/` authentication registration, cookie validation, circuit-aware access guard, OIDC events, and login/logout/password endpoints; modify `Program.cs`, `Components/Routes.razor`, `Models/AuthorizationFilter.cs`, and domain/agent mutation entry points.

**Interfaces:** AccessGuard.RequireHumanAsync and RequireAdminAsync reload current account state before protected service operations. Authentication registration creates cookie and optional OIDC schemes. Forced-change sessions cannot use monitoring operations.

- [x] Add failing integration tests for anonymous access, API 401/403 behavior, ReadOnly mutations, revoked sessions, forced-change bypass, antiforgery failures, and nonlocal return URLs.
- [x] Implement cookies, OIDC code flow, validated-email pre-provisioned admission, POST logout, rate limits, cascading authentication state, and protected routes. Protect Swagger and Hangfire; retain minimal public health.
- [x] Enforce authorization below UI controls, separating background maintenance from user-triggered operations. Run the authorization tests.

## Task 3: Agent authentication and scoped API contract

**Files:** create `Security/BasicAuthenticationHandler.cs`, `Services/ServiceAccountService.cs`, and API contract documentation; modify `Controllers/DomainsController.cs`, `Controllers/LogsController.cs`, `Controllers/RootController.cs`, agent HTTP response handling, and agent configuration examples.

**Interfaces:** Basic handler authenticates only enabled service accounts with hashed secrets; agent identity claims include assigned agent ID. Certificate reports preserve the current payload format but apply only certificate result fields to an unambiguous assigned domain.

- [x] Add failing tests for wrong/disabled credentials, HTTP outside development, cross-agent fetch/report/log/heartbeat, ambiguous domains, management-field tampering, and service-account administrative access.
- [x] Implement Basic authentication, scoped endpoint permissions, credential rotation/revocation, and allowlisted report updates. Preserve human Admin/ReadOnly API policies.
- [x] Reuse AuthType=Basic in the agent. Ensure failed HTTP writes are surfaced. Document each endpoint's credentials, allowed callers, and 401/403 behavior; run API tests and build the agent.

## Task 4: SMTP persistence and delivery

**Files:** create `Models/SmtpSettings.cs`, `Services/SmtpSettingsService.cs`, SMTP tests; modify `Services/MailService.cs`, context, and migration.

**Interfaces:** SmtpSettingsService.GetForEditAsync returns settings without saved secrets; SaveAsync accepts optional replacement password plus explicit clear flag. Delivery loads/decrypts current settings on each send. ImportFromConfigurationAsync runs once when no settings exist.

- [x] Add failing tests for one-time import, database precedence, secret protection, blank-password preservation, explicit clearing, invalid addresses/ports/TLS, and changed settings used by the next send.
- [x] Implement settings persistence and Data Protection; retain per-message subject/body. Use a local SMTP test server for delivery and failure evidence without external credentials.
- [x] Run SMTP and migration tests.

## Task 5: Login, profile, and administration screens

**Files:** create login/password/profile/users/service-account pages and focused form components; modify `Components/Layout/MainLayout.razor`, `NavMenu.razor`, `Pages/Notifications.razor`, existing mutation controls, and shared styles.

**Interfaces:** Pages invoke AccountService, ServiceAccountService, SmtpSettingsService, and server access guards. Login/logout/password HTTP flows use antiforgery-protected forms. Profile pictures use Gravatar or an HTTPS URL without server-side remote fetching.

- [x] Add failing browser/integration checks for first-login password change, profile ownership, role-restricted navigation, user creation, service-account rotation, and SMTP save validation.
- [x] Build screens in the existing MudBlazor visual style with labels, validation summaries, loading/error feedback, and mobile layouts. Add account menu/avatar; keep privileged controls unavailable to ReadOnly.
- [x] Run checks; inspect desktop and mobile layouts in one batched pass, fix defects, and confirm once.

## Task 6: Verification and handoff

**Files:** update `README.md`, server/agent example settings, and implementation checklist.

- [x] Document bootstrap environment variables, OIDC callback and trusted-email requirements, schema migration, SMTP import, roles, avatars, and agent HTTPS/Basic setup.
- [x] Run `dotnet test tests/SSLTrack.Tests`, `dotnet build src/SSLTrack/SSLTrack.csproj`, and `dotnet build src/SSLTrackAgent/SSLTrackAgent.csproj`.
- [x] Review the full diff for authorization omissions, secret leakage, and migration compatibility. Resolve findings and rerun affected tests.
- [x] Report concrete changes and verification results, distinguishing local checks from live OIDC/SMTP verification requiring user credentials.


Verification: 25/25 tests pass; server and agent builds pass. Postman local anonymous API check: 2 assertions passed. Desktop and mobile pages inspected. Independent review issues resolved and covered by regression tests. Live OIDC/external SMTP require deployment credentials.
