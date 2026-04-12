# Authentication

## Token Model (ABS 2.33.1)

ABS supports access + refresh token pairs. Both are fully implemented:

- **Access token** — 1 hour expiry (configurable via `ACCESS_TOKEN_EXPIRY` env var on ABS server)
- **Refresh token** — 30 days expiry (configurable via `REFRESH_TOKEN_EXPIRY` env var on ABS server)

The old non-expiring `user.token` still works but is deprecated in the ABS source
(`@deprecated` annotation, `isOldToken` tracking flag). API keys are also being deprecated.

## Auth Flow

1. **Login:** `POST /login` with `X-Return-Tokens: true` header — response includes
   `user.accessToken` (1h) and `user.refreshToken` (30d) in the JSON body
2. **Pre-request check:** Before every API call, decode the JWT payload (base64, no
   crypto needed) and check the `exp` claim. If the access token expires within 60
   seconds, proactively refresh before making the request.
3. **Proactive refresh:** `POST /auth/refresh` with `X-Refresh-Token: <refreshToken>`
   header — receive new `accessToken` + `refreshToken` pair, save to config, proceed
4. **Fallback:** If a request still returns 401 (e.g., server-side revocation), attempt
   one refresh and retry. If refresh also fails, report the error.
5. **Re-login:** After 30 days of inactivity (refresh token expired) —
   `Error: Session expired. Run: abs-cli login`

This is transparent to the user. They run `abs-cli login` once and the CLI handles
token refresh automatically. The 30-day window covers the "weeks between sessions"
usage pattern for agent workflows.

## Auth Commands

| Command | Description |
|---------|-------------|
| `abs-cli login` | Prompts for server URL, username, password. Stores tokens + server. |
| `abs-cli login --server https://...` | Server via flag, prompts for credentials only. |

## ABS Source Reference

The auth implementation is in `server/auth/TokenManager.js`:
- `generateTempAccessToken()` — creates access tokens with `exp` claim
- `generateRefreshToken()` — creates refresh tokens with 30d expiry
- `handleRefreshToken()` — validates refresh token, rotates both tokens
- `jwtAuthCheck()` — validates JWTs, checks expiration, handles old tokens

Login route is in `server/Auth.js`:
- `POST /login` — accepts `X-Return-Tokens: true` header for API clients
- `POST /auth/refresh` — accepts `X-Refresh-Token` header for non-cookie clients
