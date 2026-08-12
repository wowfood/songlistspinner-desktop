# StreamerSongList API v2

This migration is based on the StreamerSongList API reference version 2.0.0 at <https://dev.staging.streamersonglist.com/api-reference> and its authentication guide at <https://dev.staging.streamersonglist.com/docs/authentication>.

## Implemented contract

The typed client currently uses:

- `GET /queue?streamer_name={name}&platform={platform}`
- `GET /play_history?streamer_name={name}&platform={platform}&limit=200&order_by=played_at&order_dir=desc`

Supported platform values are `twitch`, `youtube`, `kick`, and `none`. Queue and play-history transport DTOs are internal to the v2 client and are mapped to the existing `SpinnerQueueItem` and `PlayHistoryItem` models.

The `day`, `week`, and `month` history settings add an RFC3339 `played_after` value using rolling UTC windows. API v2 does not expose the legacy `period=stream` parameter, so the `stream` setting currently means the most recent page of history.

## Authentication

API v2 requires an access token for these reads. The client supports all three documented authorization headers:

- `Authorization: Streamer <token>` — recommended for a personal, single-channel installation
- `Authorization: User <token>` — for a user who owns or administrates multiple channels
- `Authorization: Bearer <token>` — OAuth access token; an optional `Client-Id` header is also sent

Tokens entered on the Settings page are stored using Windows secure storage. StreamerSongList credentials use their own keys and are never reused as Twitch credentials.

Environment-variable fallback is available for development:

```text
SONGLISTSPINNER_SSL_API_BASE_URL=https://api.staging.streamersonglist.com/
SONGLISTSPINNER_SSL_ACCESS_TOKEN=<token>
SONGLISTSPINNER_SSL_TOKEN_TYPE=streamer
SONGLISTSPINNER_SSL_CLIENT_ID=<oauth-client-id>
```

## Promotion checklist

- Register a public desktop OAuth client and implement authorization code flow with PKCE and refresh-token rotation.
- Confirm the production v2 server URL before changing the checked-in default.
- Exercise the client against a real staging account and token; automated tests currently verify the published request and response contract with HTTP fixtures.
- Implement cursor traversal when the first 200 play-history entries are insufficient.
- Revisit the `stream` history option if the API publishes a session boundary or equivalent filter.
