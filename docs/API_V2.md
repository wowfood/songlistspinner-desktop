# StreamerSongList API v2

This migration is based on the StreamerSongList API reference version 2.0.0 at <https://dev.staging.streamersonglist.com/api-reference> and its authentication guide at <https://dev.staging.streamersonglist.com/docs/authentication>.

## Implemented contract

The typed client currently uses:

- `GET /streamers?streamer_name={name}&platform={platform}` to resolve the numeric streamer ID used by event channels
- `GET /queue?streamer_name={name}&platform={platform}` for both upcoming queue entries and the current `playing` entry
- `GET /play_history?streamer_name={name}&platform={platform}&limit=100&order_by=played_at&order_dir=desc`
- `POST /queue/played?queue_id={queueId}` to move the selected winner directly into play history when its popup closes
- `POST /queue/played?position=playing&streamer_id={streamerId}` to complete the current now-playing entry
- `POST /queue/{queueId}/play` to promote a selected winner to the now-playing slot

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
SONGLISTSPINNER_SSL_EVENTS_URL=wss://events.staging.streamersonglist.com/connection/websocket
SONGLISTSPINNER_SSL_ACCESS_TOKEN=<token>
SONGLISTSPINNER_SSL_TOKEN_TYPE=streamer
SONGLISTSPINNER_SSL_CLIENT_ID=<oauth-client-id>
```

## Realtime updates

The dashboard does not poll. After loading the initial REST snapshot, it opens an anonymous Centrifugo connection to the configured events endpoint and subscribes to:

```text
streamer:{streamerId}-queue
streamer:{streamerId}-play_history
```

`now_playing_update`, `queue_add`, `queue_clear`, `queue_remove`, `queue_reorder`, `queue_update`, and `play_history_add` are treated as invalidation signals. Closely spaced events are debounced into one queue-and-history REST refresh so a queue transition cannot produce competing UI updates. A successful initial connection or reconnection also triggers a complete refresh to cover changes that could have occurred while disconnected.

## Now Playing workflow

When **Display Now Playing** is disabled, the existing queue automation can continue to move a winner directly to play history. When it is enabled, closing the winner popup performs an ordered transition:

1. Read the current queue snapshot.
2. If the now-playing slot is occupied, explicitly mark that entry played.
3. Read the queue again because StreamerSongList may auto-promote the first queued item.
4. Promote the selected winner only when it was not the auto-promoted item.

The overlay displays the confirmed `playing` entry from the REST snapshot rather than optimistically displaying the winner. Its fields, font, width, and screen position are configurable in Settings.

The staging event endpoint is `wss://events.staging.streamersonglist.com/connection/websocket`. Its HTTPS root displays Centrifugo's password-protected administrative console; application clients connect to the WebSocket path anonymously and do not use that login.

## Promotion checklist

- Confirm the production v2 server URL before changing the checked-in default.
- Confirm the production event WebSocket URL before changing the checked-in default.
- Exercise the client against a real staging account and token; automated tests currently verify the published request and response contract with HTTP fixtures.
- Implement cursor traversal when the first 100 play-history entries are insufficient.
- Revisit the `stream` history option if the API publishes a session boundary or equivalent filter.
- Add OAuth with PKCE only if a future distribution model needs third-party user sign-in instead of a streamer-supplied token.
