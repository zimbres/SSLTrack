# Authentication, accounts, and SMTP configuration

## Intent
Protect SSLTrack's web interface and REST API while retaining the existing agent Basic authentication client. Administrators control who can access the application and which agent each service account can operate.

## Approaches
The user selected application-managed Basic service accounts, reusing the agent's existing Username and Password fields. Server-issued bearer tokens were considered but add token issuance and lifecycle management without being necessary for the selected workflow. Human users use cookie sessions with local passwords or OIDC.

## Accounts and initial setup
Store users in SQLite with a unique, normalized email as their login identifier, display name, Admin/ReadOnly role, enabled state, authentication type, avatar preference, and password-change requirement. Use ASP.NET Core Identity password hashing and account lockout rather than custom password cryptography. No public registration or OIDC auto-provisioning.

Provision the first administrator from explicit bootstrap configuration supplied through environment variables or user secrets. Bootstrap only when no human users exist; never ship a default password or expose anonymous administrator creation. Document bootstrap and migration steps.

Administrators can create and edit users, disable access, set/reset a local password, and require a password change on first or next login. Prevent disabling or demoting the final enabled administrator. Revalidate accounts so disabling accounts or changing permissions invalidates existing access, including established Blazor sessions before protected operations.

## Human authentication
Provide a login screen with email/password and an OIDC option when configured. Configure OIDC authority, client ID, and secret outside source control. Require validated provider email and map it only to an existing enabled OIDC account. Link provider issuer/subject on first successful login and enforce that binding thereafter; reject absent, unverified, disabled, or mismatched users with a useful sign-in message. Local users cannot implicitly become OIDC users.

Use protected HttpOnly cookies, local-only return URLs, antiforgery protection on browser writes, generic local credential errors, lockout/rate limits, and POST logout. A local account marked for password change can access only password change and logout until complete. OIDC password changes remain the provider's responsibility.

## Authorization
Admin can manage domains, agents, users, service accounts, SMTP settings, logs, and the Hangfire dashboard. ReadOnly can view monitoring information and update their own profile/password/avatar, but cannot mutate monitoring/configuration data. Enforce permissions in server operations and API handlers as well as hiding or disabling controls.

Protect the REST API, Swagger, and Hangfire; API failures return 401/403 rather than HTML login redirects. Keep minimal health/liveness endpoints anonymous for infrastructure, returning no monitoring data. Cookie-authenticated API mutations require antiforgery protection.

## Agent service accounts
Provide an Admin-only configuration page for service accounts, separate from human accounts. Use email as identifier, a hashed secret, enabled state, and one assigned agent. Support creation, credential rotation, and disabling. Service accounts use HTTP Basic through the existing agent AuthType=Basic, Username, and Password settings and cannot log into the web interface.

Allow only fetching the assigned agent's domains, reporting certificate results for assigned domains, posting logs for the assigned agent, and submitting its heartbeat. Reject mismatched agent IDs, unassigned domains, general domain CRUD, log reads/clears, and administrative endpoints. Certificate reports update only certificate result fields; never accept assignment, IDs, silence flags, or other management fields from an agent. Use unambiguous domain lookup and reject ambiguous assignments rather than updating another row. Require HTTPS for Basic outside local development and document agent configuration.

## Profile and pictures
Provide an authenticated profile page for display name, password changes for local accounts, and avatar selection. Email remains the stable login identifier and is managed by administrators. Offer Gravatar or a user-supplied HTTPS image URL, with a default avatar when unavailable. Do not fetch arbitrary image URLs on the server. Disclose Gravatar's external request when users select it.

## SMTP configuration
Replace the read-only Notifications settings with an Admin-only editable configuration screen backed by SQLite. Include sender name/address, recipients, BCC, host, port, username, password, and TLS mode, plus the existing test action. Protect the SMTP password using the application's persisted Data Protection keys; never send saved secrets back to the browser. Empty password input preserves the secret, with an explicit clear action.

Import existing MailProperties configuration once when no database settings exist, preserving existing deployments. Database settings become authoritative after import. Load current settings for each send so background jobs use saved changes. Preserve per-message subject/body behavior. Validate addresses, required connection fields, port, and TLS selections, and provide useful save/test feedback without exposing credentials.

## Implementation and validation
Use the current ASP.NET Core 10, Blazor Server, MudBlazor, EF Core, and SQLite architecture. Add an EF migration without replacing existing databases or modifying live data during development. Document configuration, deployment, migration, and agent setup. Tests must cover authorization boundaries, service-account scoping, password-change restrictions, local and OIDC account admission, bootstrap constraints, SMTP persistence/secret preservation, and migration behavior. Build both the server and agent; verify browser flows when the environment permits. Live OIDC/SMTP integrations require deployment credentials and are reported separately from local verification.

## Scope
No self-registration, user invitation/email reset workflow, new external identity provider, or server-issued agent tokens. The builder may choose implementation details that preserve these contracts and the existing application's visual conventions.
