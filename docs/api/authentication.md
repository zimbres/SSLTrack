# SSLTrack authenticated API

The application's controller definitions generate OpenAPI at `/api/swagger/v1/swagger.json`. This document describes the authorization contract used by the integration tests. The Swagger UI and document require a human session.

## Credentials and failures

Human users sign in at `/login` using local credentials or configured OIDC; the browser receives an HttpOnly cookie. There is no public registration or automatic OIDC provisioning. Human API writes require an antiforgery token obtained from an authenticated page and sent in `RequestVerificationToken`, or its form field. ReadOnly users cannot write monitoring data.

Agents use HTTP Basic with a pre-created service account's email and password. Basic is accepted only over HTTPS outside Development/Testing. Standard Basic clients and SSLTrackAgent's existing `AuthType=Basic` are supported. Human passwords are not accepted as agent Basic credentials. Bearer tokens from an external reverse proxy are not interpreted by this application.

Missing, malformed, disabled, or invalid credentials return **401**. A valid identity without permission returns **403**. Cookie-based API failures do not redirect to HTML login pages. Required-password-change sessions receive **403** for monitoring APIs. Invalid/missing browser antiforgery tokens return **400**. Local login is rate limited and repeated bad passwords lock the account temporarily.

| Endpoint | Human access | Agent service account |
|---|---|---|
| `GET /api/health` | Anonymous | Anonymous |
| `GET /api/domains` | Admin, ReadOnly | Denied |
| `GET /api/domains/{domainName}` | Admin, ReadOnly | Denied |
| `GET /api/domains/agents/{agentId}` | Admin, ReadOnly | Assigned agent only |
| `POST /api/domains` | Admin | Denied |
| `POST /api/domains/update` | Admin | Denied |
| `PUT /api/domains/{domainName}` | Admin | Assigned domain certificate report only |
| `DELETE /api/domains/{domainId}` | Admin | Denied |
| `GET /api/logs`, `GET /api/logs/{id}`, `GET /api/logs/agent/{agentId}` | Admin, ReadOnly | Denied |
| `POST /api/logs` | Admin | Payload Agent must match assigned agent |
| `POST /api/logs/clear` | Admin | Denied |
| `HEAD /api/ping/{agentId}` | Admin | Assigned agent only |
| `GET /api/ping` | Admin, ReadOnly | Denied |

## Certificate report

The existing agent JSON payload is retained. The URL name must match `domainName`. Agent writes update only `certCN`, `issuer`, `expiryDate`, and `lastChecked`. IDs, agent assignment, port, user ID, silence, and public-prefix management fields cannot be changed by an agent. When the assigned agent has several matching domain names, `port` must identify exactly one assignment; ambiguous reports return **409**. An unassigned name returns **403**; success returns **204**.

```json
{
  "domainName": "example.com",
  "port": 443,
  "certCN": "example.com",
  "issuer": "Certificate authority",
  "expiryDate": "2027-01-01T00:00:00Z",
  "lastChecked": "2026-10-27T12:00:00Z"
}
```

Log payloads retain `agent`, `domain`, `dateTime`, and `message`. Logging returns **201** with the new log ID. Heartbeat returns **200**. Credentials should be stored in a deployment secret or environment variables; do not put them in URLs or committed collections.

## Verification

Run `dotnet test tests/SSLTrack.Tests`. Tests host the real application with isolated SQLite databases and exercise cookie login, antiforgery, role restrictions, credential revocation, forced changes, Basic credentials, cross-agent denial, allowlisted reports, ambiguity, rendered pages, and migration preservation. SMTP delivery tests use a local SMTP receiver. Live provider configuration is not established by these local tests.
