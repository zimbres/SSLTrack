# Authentication and configuration setup

## First administrator

Back up the SQLite database before upgrading. The application applies EF migrations at startup without replacing domain or agent rows. If you previously applied schema SQL manually without EF migration history, reconcile that history before starting the upgraded version; do not run an initial migration over existing tables.

Set `Authentication__Bootstrap__Email` and `Authentication__Bootstrap__Password` through environment variables or user secrets before the first startup. Choose a password of 12–128 characters. The app creates an Admin only when the Users table is empty and requires a password change on first login. No default login is shipped. Remove bootstrap values after successful setup. Restarting never replaces existing users.

For local development, set the variables in your shell, then run `dotnet run --project src/SSLTrack`. Set `ConnectionStrings__Data` if the default database path is unsuitable. The database directory must exist and be writable. For Docker Compose use `SSLTRACK_ADMIN_EMAIL` and `SSLTRACK_ADMIN_PASSWORD` in the deployment environment. `SSLTRACK_ENVIRONMENT` defaults to Production; set Development only for a local HTTP demo. Production needs HTTPS before sign-in or agent use.

## OIDC

Configure these environment variables (or equivalent appsettings keys):

| Variable | Meaning |
|---|---|
| `Authentication__Oidc__Enabled` | `true` to show organization sign-in |
| `Authentication__Oidc__Authority` | HTTPS issuer / discovery authority |
| `Authentication__Oidc__ClientId` | Registered client ID |
| `Authentication__Oidc__ClientSecret` | Client secret, supplied as a deployment secret |
| `Authentication__Oidc__TrustedCaPath` | Optional path inside the container to a PEM file containing trusted OIDC CA certificates |

Register `https://<your-ssltrack-host>/signin-oidc` as the provider callback. SSLTrack uses authorization code flow with PKCE and requests `openid profile email`. The validated ID token must include `email`, `email_verified=true`, and `sub`. Providers that do not supply a trusted verified email cannot admit users under this contract; configure their claim mapping accordingly.

### Private CA or self-signed provider certificates

For chiseled containers, mount the public CA certificate as a read-only PEM file; no shell or system trust-store installation is needed. Add the following to your SSLTrack service in Docker Compose:

```yaml
environment:
  Authentication__Oidc__TrustedCaPath: /certs/oidc-ca.pem
volumes:
  - ./certs/oidc-ca.pem:/certs/oidc-ca.pem:ro
```

Use the issuing root CA certificate, or the provider's own public certificate if it is self-signed. Obtain it through a trusted channel and verify its fingerprint. Do not mount a private key. The PEM file can contain multiple trusted certificates for rotation. The provider must serve any required intermediate certificates.

When configured, this file replaces system roots for OIDC backchannel HTTPS requests only (discovery, signing keys, and token exchange). Hostname, expiration, chain, and server-auth usage validation remain enabled; revocation checks are not performed. Other application HTTP clients keep their existing trust settings. An unset or blank path keeps standard system trust. A missing, unreadable, invalid, or empty certificate file stops startup when OIDC is enabled. Restart after replacing the certificate file. Browsers must separately trust the provider's certificate for the interactive sign-in redirect.

An Admin must create each human user on **Users**, select **OIDC**, and enter the matching email before login. SSLTrack links the validated issuer/subject on first admission and rejects a different identity later. Changing the user's authentication type deliberately clears that link and invalidates sessions. Local users cannot sign in through OIDC implicitly. Unknown, disabled, unverified, or mismatched identities see an admission message. OIDC passwords are managed by the provider.

## Roles and profiles

**Admin** manages monitoring data, accounts, SMTP, and Hangfire. **ReadOnly** views monitoring and updates their own display name, password, and picture. Each role can open **My profile**. Email is the permanent sign-in identifier and cannot be changed after the account is created. Users choose a default avatar, Gravatar, or a custom HTTPS picture URL. Gravatar and external image hosts receive browser requests when selected; images are not fetched by the server.

New local users can be required to change their password at first login. On **Users**, edit an existing local user, set a replacement password if needed, and select **Require password change at next sign-in**. They cannot access monitoring until the change is complete. Account changes invalidate sessions immediately on the next protected operation/request; connected Blazor sessions are also revalidated periodically. Keep at least one enabled administrator; the application prevents disabling or demoting the last one.

## Agent service accounts

Create the remote agent on **Agents**, then create its account on **Service accounts**. Accounts use an email identifier, a password of 12–128 characters, and one remote agent assignment. Configure the existing worker with:

```json
{
  "Configurations": {
    "SSLTrackApiAddress": "https://ssltrack.example.com",
    "AgentId": 2,
    "AuthType": "Basic",
    "Username": "agent-2@example.com",
    "Password": ""
  },
  "HttpClientConfiguration": { "IgnoreSsl": false }
}
```

Supply `Configurations__Password` securely at deployment. Service accounts have no website access and can only fetch their assigned domains, report certificate fields for them, submit their logs, and heartbeat their assigned ID. Disable an account or set its replacement password to revoke the old credentials on the next request. Existing agents configured with no authentication must be updated when upgrading.

The worker's existing Token/OAuth2 option remains for external deployments that already use it; this app does not issue or accept bearer tokens. Use Basic for its native service accounts. The worker now surfaces failed heartbeat, log, and report HTTP responses.

## HTTPS and proxies

Use HTTPS in production. Configure Kestrel TLS or terminate TLS at a trusted reverse proxy. Set `ReverseProxy__KnownProxies__0` (and subsequent indices) to exact proxy IP addresses when using a proxy; only these and framework loopback defaults are trusted for forwarded protocol/client-IP headers. Configure the proxy to overwrite forwarded headers and preserve the original host. Never add untrusted clients as proxies. Correct forwarding is required for secure cookies, rate limiting, HTTPS redirection, and OIDC callbacks.

## SMTP

Open **SMTP configuration** as an Admin. Set sender, recipients, host, port, TLS mode, and optional credentials; save, then send a test notification. Recipients and BCC use semicolon separators. STARTTLS is required when selected; use TLS on connect for implicit TLS servers or None only where deliberately appropriate.

On the first upgraded startup, existing `MailProperties` values are imported once when no database configuration exists. The database is authoritative thereafter. Remove legacy SMTP credentials from configuration after confirming migration. Blank password edits preserve the stored secret; **Clear saved SMTP password** removes it. Saved passwords are protected with ASP.NET Core Data Protection and never displayed in edit forms. Back up the persisted Data Protection keys with the database; restrict database access because it contains the key material. Background jobs load the current saved SMTP settings for every delivery.

## Verification

Run `dotnet test tests/SSLTrack.Tests`, `dotnet build src/SSLTrack/SSLTrack.csproj`, and `dotnet build src/SSLTrackAgent/SSLTrackAgent.csproj`. See [the API contract](api/authentication.md) for endpoint permissions and response semantics. Real OIDC and SMTP deployments still need checks with their own credentials and infrastructure.

Before deleting a remote agent, remove or reassign its service accounts and remove its assigned domains. Service accounts can be permanently removed from their edit screen.

## Keycloak example: public realm

**Authority** means the trusted OIDC issuer: the server and realm that authenticate users. For your realm, enter `https://keyclk.zimbres.com/realms/public`. Do not append `/protocol/openid-connect/auth` or `/.well-known/openid-configuration` to Authority. ASP.NET Core discovers the realm's endpoints automatically from `https://keyclk.zimbres.com/realms/public/.well-known/openid-configuration`.

Create an OpenID Connect client in Keycloak's **public** realm:

1. Choose a Client ID, for example `ssltrack`.
2. Enable **Client authentication** and **Standard flow** (authorization code). This is a server-side application with a client secret.
3. Set **Valid redirect URIs** to `https://<your-ssltrack-host>/signin-oidc`, using SSLTrack's real public URL, not Keycloak's host. Configure each actual deployment callback explicitly.
4. Copy the client secret from the client's **Credentials** tab into the deployment secret `Authentication__Oidc__ClientSecret`. This is the application's client secret, not a user's password.
5. Ensure the client can request the `profile` and `email` scopes and that the ID token contains `email`, `email_verified=true`, and `sub`. Users must have a verified email in Keycloak.

Example appsettings (provide the secret separately):

```json
{
  "Authentication": {
    "Oidc": {
      "Enabled": true,
      "Authority": "https://keycloak.example.com/realms/public",
      "ClientId": "ssltrack",
      "ClientSecret": ""
    }
  }
}
```

`ClientId` must match the actual Keycloak client you create; `ssltrack` above is an example. Enable OIDC only after the authority, client, secret, and callback are configured, then restart SSLTrack. Create each matching email on SSLTrack's **Users** page and select **OIDC** as the sign-in method. The account's Admin/ReadOnly role is managed in SSLTrack.

See [Keycloak endpoint discovery](https://www.keycloak.org/securing-apps/oidc-layers) and [Keycloak client configuration](https://www.keycloak.org/docs/latest/server_admin/index.html#_oidc_clients).

## Disabling users

On **Users**, click **Edit**, set **Account status** to **Disabled**, and save. Disabled users disappear from the normal list and cannot sign in. Select **Show disabled users** to include them, then edit and set the status back to **Enabled** to restore access. The last enabled administrator cannot be disabled.
