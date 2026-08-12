# StreamerSongList API v2

This migration is based on the StreamerSongList API reference version 2.0.0 at <https://dev.staging.streamersonglist.com/api-reference> and its authentication guide at <https://dev.staging.streamersonglist.com/docs/authentication>.

## Implemented contract

The typed client currently uses:

- `GET /queue?streamer_name={name}&platform={platform}`
- `GET /play_history?streamer_name={name}&platform={platform}&limit=100&order_by=played_at&order_dir=desc`
- `POST /queue/{queueId}/play` to promote the selected winner to now-playing
- `POST /queue/played?position=playing` to record the promoted winner in play history

Supported platform values are `twitch`, `youtube`, `kick`, and `none`. Queue and play-history transport DTOs are internal to the v2 client and are mapped to the existing `SpinnerQueueItem` and `PlayHistoryItem` models.

The `day`, `week`, and `month` history settings add an RFC3339 `played_after` value using rolling UTC windows. API v2 does not expose the legacy `period=stream` parameter, so the `stream` setting currently means the most recent page of history.

## Authentication

API v2 requires an access token. A streamer access token is the primary desktop credential because it is created by the streamer, bound to one channel, grants full read/write access to that channel, and requires no OAuth flow.

The client supports all three documented authorization headers:

- `Authorization: Streamer <token>` — default and recommended for a personal, single-channel installation
- `Authorization: User <token>` — for a user who owns or administrates multiple channels
- `Authorization: Bearer <token>` — OAuth access token; an optional `Client-Id` header is also sent

Tokens entered on the Settings page are stored using Windows secure storage. The app no longer needs Twitch OAuth or Twitch chatbot access for queue automation.

Use **Save and test connection** on the Settings page after entering a token, streamer name, and platform. The result reports the active API endpoint and either the number of queue entries returned or the API error. Dashboard request and refresh failures are also shown on screen and written to the debugger output without logging the token.

Environment-variable fallback is available for development:

```text
SONGLISTSPINNER_SSL_API_BASE_URL=https://api.staging.streamersonglist.com/
SONGLISTSPINNER_SSL_ACCESS_TOKEN=<token>
SONGLISTSPINNER_SSL_TOKEN_TYPE=streamer
SONGLISTSPINNER_SSL_CLIENT_ID=<oauth-client-id>
```

## Promotion checklist

- Confirm the production v2 server URL before changing the checked-in default.
- Exercise the client against a real staging account and token; automated tests currently verify the published request and response contract with HTTP fixtures.
- Implement cursor traversal when the first 100 play-history entries are insufficient.
- Revisit the `stream` history option if the API publishes a session boundary or equivalent filter.
- Add OAuth with PKCE only if a future distribution model needs third-party user sign-in instead of a streamer-supplied token.
