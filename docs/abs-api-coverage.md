# ABS API Coverage

Map of every Audiobookshelf HTTP API endpoint and the `abs-cli` command (if
any) that implements it.

- **Reference:** ABS server source at `temp/audiobookshelf/server/routers/`
  (routes) and `server/controllers/` (handlers). Tested range:
  `2.33.1 – 2.35.1` (`AbsApiClient.cs`).
- **Permission** column uses ABS's tokens (`admin` / `update` / `upload` /
  `download` / `delete`); blank = any authenticated user. `?` = not visible at
  the router layer.
- ✅ = covered by a CLI command · — = not implemented · 🔒 = internal-only
  (no user-facing verb).

## Coverage summary

| Resource | Covered / Total |
|----------|-----------------|
| Libraries | 4 / 27 |
| Items | 13 / 26 |
| Me (current user) | 5 / 18 |
| Collections | 9 / 9 |
| Authors | 7 / 7 |
| Series | 1 / 2 |
| Backup | 6 / 7 |
| Search | 4 / 6 |
| Cache | 2 / 2 |
| Tools | 4 / 4 |
| Misc | 3 / 16 |
| Playlists | 0 / 10 |
| Podcasts | 0 / 13 |
| Users | 0 / 9 |
| Sessions | 0 / 9 |
| Notifications | 0 / 8 |
| Email | 0 / 5 |
| RSS feeds | 0 / 5 |
| API keys | 0 / 4 |
| Custom metadata providers | 0 / 3 |
| Shares | 0 / 2 |
| Stats | 0 / 2 |
| Filesystem | 0 / 2 |

## Libraries

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| POST | `/api/libraries` | Create library | ? | — |
| GET | `/api/libraries` | List libraries | | `libraries list` ✅ |
| GET | `/api/libraries/:id` | Get library | | `libraries get` ✅ |
| PATCH | `/api/libraries/:id` | Update library | update | — |
| DELETE | `/api/libraries/:id` | Delete library | delete | — |
| POST | `/api/libraries/order` | Reorder libraries | ? | — |
| GET | `/api/libraries/:id/items` | List items in library | | `items list` ✅ |
| DELETE | `/api/libraries/:id/issues` | Remove items with issues | delete | — |
| GET | `/api/libraries/:id/episode-downloads` | Podcast download queue | | — |
| GET | `/api/libraries/:id/series` | List series | | `series list` ✅ |
| GET | `/api/libraries/:id/series/:seriesId` | Get series (library-scoped) | | — |
| GET | `/api/libraries/:id/collections` | List collections | | `collections list` ✅ |
| GET | `/api/libraries/:id/playlists` | List playlists | | — |
| GET | `/api/libraries/:id/personalized` | Personalized shelves | | — |
| GET | `/api/libraries/:id/filterdata` | Valid filter values | | — |
| GET | `/api/libraries/:id/search` | Search within library | | `search` ✅ |
| GET | `/api/libraries/:id/stats` | Library statistics | | — |
| GET | `/api/libraries/:id/authors` | List authors | | `authors list` ✅ |
| GET | `/api/libraries/:id/narrators` | List narrators | | — |
| PATCH | `/api/libraries/:id/narrators/:narratorId` | Update narrator | update | — |
| DELETE | `/api/libraries/:id/narrators/:narratorId` | Remove narrator | delete | — |
| GET | `/api/libraries/:id/matchall` | Match all items to metadata | update | — |
| POST | `/api/libraries/:id/scan` | Scan library | update | `libraries scan` ✅ |
| GET | `/api/libraries/:id/recent-episodes` | Recent episodes | | — |
| GET | `/api/libraries/:id/opml` | Export as OPML | | — |
| POST | `/api/libraries/:id/remove-metadata` | Remove all metadata files | delete | — |
| GET | `/api/libraries/:id/podcast-titles` | Podcast titles | | — |
| GET | `/api/libraries/:id/download` | Download multiple items | download | — |

## Items

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/items/:id` | Get item (`--expanded`, `--include`) | | `items get` ✅ |
| DELETE | `/api/items/:id` | Delete item (`--hard`) | delete | `items delete` ✅ |
| GET | `/api/items/:id/download` | Download item | download | — |
| PATCH | `/api/items/:id/media` | Update item metadata | update | `items update` ✅ |
| GET | `/api/items/:id/cover` | Get cover | | `items cover get` ✅ |
| POST | `/api/items/:id/cover` | Upload cover (url/file) | upload | `items cover set` ✅ |
| PATCH | `/api/items/:id/cover` | Set cover from server path | update | `items cover set --server-path` ✅ |
| DELETE | `/api/items/:id/cover` | Remove cover | delete | `items cover remove` ✅ |
| POST | `/api/items/:id/match` | Match item to metadata | update | — |
| POST | `/api/items/:id/play` | Start playback session | | — |
| POST | `/api/items/:id/play/:episodeId` | Start episode playback | | — |
| PATCH | `/api/items/:id/tracks` | Update audio tracks | update | — |
| POST | `/api/items/:id/scan` | Scan item | update | `items scan` ✅ |
| GET | `/api/items/:id/metadata-object` | Get metadata object | | — |
| POST | `/api/items/:id/chapters` | Update chapters | update | `items chapters set` ✅ |
| GET | `/api/items/:id/ffprobe/:fileid` | FFprobe data for file | | — |
| GET | `/api/items/:id/file/:fileid` | Get library file | | — |
| DELETE | `/api/items/:id/file/:fileid` | Delete library file | delete | — |
| GET | `/api/items/:id/file/:fileid/download` | Download library file | download | — |
| GET | `/api/items/:id/ebook/:fileid?` | Get ebook file | | — |
| PATCH | `/api/items/:id/ebook/:fileid/status` | Toggle ebook primary/supplementary | update | `items toggle-ebook-status` ✅ |
| POST | `/api/items/batch/delete` | Batch delete | delete | `items batch-delete` ✅ |
| POST | `/api/items/batch/update` | Batch update | update | `items batch-update` ✅ |
| POST | `/api/items/batch/get` | Batch get | | `items batch-get` ✅ |
| POST | `/api/items/batch/quickmatch` | Batch quick-match | update | — |
| POST | `/api/items/batch/scan` | Batch scan | update | — |

## Me (current user)

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/me` | Current user profile | | `me` ✅ |
| GET | `/api/me/listening-sessions` | My listening sessions | | — |
| GET | `/api/me/item/listening-sessions/:id/:episodeId?` | Item listening sessions | | — |
| GET | `/api/me/listening-stats` | My listening stats | | — |
| GET | `/api/me/progress/:id/remove-from-continue-listening` | Remove from continue listening | update | — |
| GET | `/api/me/progress/:id/:episodeId?` | Get media progress | | `items progress get` ✅ |
| PATCH | `/api/me/progress/batch/update` | Batch update progress | update | `items batch-update-progress` ✅ |
| PATCH | `/api/me/progress/:libraryItemId/:episodeId?` | Set/create media progress | update | `items progress set` ✅ |
| DELETE | `/api/me/progress/:id` | Delete media progress | delete | `items progress remove` ✅ |
| POST | `/api/me/item/:id/bookmark` | Create bookmark | update | — |
| PATCH | `/api/me/item/:id/bookmark` | Update bookmark | update | — |
| DELETE | `/api/me/item/:id/bookmark/:time` | Delete bookmark | delete | — |
| PATCH | `/api/me/password` | Change my password | | — |
| GET | `/api/me/items-in-progress` | Items in progress | | — |
| GET | `/api/me/series/:id/remove-from-continue-listening` | Remove series from continue listening | update | — |
| GET | `/api/me/series/:id/readd-to-continue-listening` | Re-add series to continue listening | update | — |
| GET | `/api/me/stats/year/:year` | My stats for year | | — |
| POST | `/api/me/ereader-devices` | Update eReader devices | update | — |

## Collections

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| POST | `/api/collections` | Create collection | update | `collections create` ✅ |
| GET | `/api/collections` | List collections | | `collections list` ✅ |
| GET | `/api/collections/:id` | Get collection | | `collections get` ✅ |
| PATCH | `/api/collections/:id` | Update / reorder | update | `collections update` / `reorder` ✅ |
| DELETE | `/api/collections/:id` | Delete collection | delete | `collections delete` ✅ |
| POST | `/api/collections/:id/book` | Add item | update | `collections add` ✅ |
| DELETE | `/api/collections/:id/book/:bookId` | Remove item | update | `collections remove` ✅ |
| POST | `/api/collections/:id/batch/add` | Batch add | update | `collections batch-add` ✅ |
| POST | `/api/collections/:id/batch/remove` | Batch remove | update | `collections batch-remove` ✅ |

## Authors

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/authors/:id` | Get author | | `authors get` ✅ |
| PATCH | `/api/authors/:id` | Update author | update | `authors update` ✅ |
| DELETE | `/api/authors/:id` | Delete author | delete | `authors delete` ✅ |
| POST | `/api/authors/:id/match` | Match author to metadata | update | `authors match` ✅ |
| GET | `/api/authors/:id/image` | Get author image | | `authors image get` ✅ |
| POST | `/api/authors/:id/image` | Upload author image | upload | `authors image set` ✅ |
| DELETE | `/api/authors/:id/image` | Delete author image | delete | `authors image remove` ✅ |

## Series

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/series/:id` | Get series | | `series get` ✅ |
| PATCH | `/api/series/:id` | Update series | update | — |

## Playlists

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| POST | `/api/playlists` | Create playlist | | — |
| GET | `/api/playlists` | List my playlists | | — |
| GET | `/api/playlists/:id` | Get playlist | | — |
| PATCH | `/api/playlists/:id` | Update playlist | update | — |
| DELETE | `/api/playlists/:id` | Delete playlist | delete | — |
| POST | `/api/playlists/:id/item` | Add item | update | — |
| DELETE | `/api/playlists/:id/item/:itemId/:episodeId?` | Remove item | update | — |
| POST | `/api/playlists/:id/batch/add` | Batch add | update | — |
| POST | `/api/playlists/:id/batch/remove` | Batch remove | update | — |
| POST | `/api/playlists/collection/:collectionId` | Create from collection | | — |

## Podcasts

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| POST | `/api/podcasts` | Create / subscribe | update | — |
| POST | `/api/podcasts/feed` | Get podcast feed | | — |
| POST | `/api/podcasts/opml/parse` | Parse OPML text | | — |
| POST | `/api/podcasts/opml/create` | Bulk create from OPML | update | — |
| GET | `/api/podcasts/:id/checknew` | Check new episodes | update | — |
| GET | `/api/podcasts/:id/downloads` | Episode downloads | | — |
| GET | `/api/podcasts/:id/clear-queue` | Clear download queue | delete | — |
| GET | `/api/podcasts/:id/search-episode` | Search episode | | — |
| POST | `/api/podcasts/:id/download-episodes` | Download episodes | download | — |
| POST | `/api/podcasts/:id/match-episodes` | Quick-match episodes | update | — |
| GET | `/api/podcasts/:id/episode/:episodeId` | Get episode | | — |
| PATCH | `/api/podcasts/:id/episode/:episodeId` | Update episode | update | — |
| DELETE | `/api/podcasts/:id/episode/:episodeId` | Remove episode | delete | — |

## Sessions (playback)

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/sessions` | All sessions w/ user data | | — |
| DELETE | `/api/sessions/:id` | Delete session | delete | — |
| GET | `/api/sessions/open` | Open sessions | | — |
| POST | `/api/sessions/batch/delete` | Batch delete | delete | — |
| POST | `/api/session/local` | Sync local session | update | — |
| POST | `/api/session/local-all` | Sync all local sessions | update | — |
| GET | `/api/session/:id` | Get open session | | — |
| POST | `/api/session/:id/sync` | Sync session | update | — |
| POST | `/api/session/:id/close` | Close session | update | — |

## Backup

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/backups` | List backups | admin | `backup list` ✅ |
| POST | `/api/backups` | Create backup | admin | `backup create` ✅ |
| DELETE | `/api/backups/:id` | Delete backup | admin | `backup delete` ✅ |
| GET | `/api/backups/:id/download` | Download backup | admin | `backup download` ✅ |
| GET | `/api/backups/:id/apply` | Apply backup | admin | `backup apply` ✅ |
| POST | `/api/backups/upload` | Upload backup | admin | `backup upload` ✅ |
| PATCH | `/api/backups/path` | Update backup path | admin | — |

## Search

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/search/covers` | Search covers | | `metadata covers` ✅ |
| GET | `/api/search/books` | Search books | | `metadata search` ✅ |
| GET | `/api/search/podcast` | Search podcasts | | — |
| GET | `/api/search/authors` | Search authors | | `authors lookup` ✅ |
| GET | `/api/search/chapters` | Search chapters | | `items chapters lookup` ✅ |
| GET | `/api/search/providers` | Metadata providers | | `metadata providers` ✅ |

## Tools

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| POST | `/api/tools/item/:id/encode-m4b` | Encode to M4B | admin | `items encode-m4b start` ✅ |
| DELETE | `/api/tools/item/:id/encode-m4b` | Cancel M4B encode | admin | `items encode-m4b cancel` ✅ |
| POST | `/api/tools/item/:id/embed-metadata` | Embed metadata | admin | `items embed-metadata` ✅ |
| POST | `/api/tools/batch/embed-metadata` | Batch embed metadata | admin | `items batch-embed-metadata` ✅ |

## Cache

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| POST | `/api/cache/purge` | Purge all cache | admin | `cache purge` ✅ |
| POST | `/api/cache/items/purge` | Purge items cache | admin | `cache purge-items` ✅ |

## RSS feeds

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/feeds` | List feeds | admin | — |
| POST | `/api/feeds/item/:itemId/open` | Open feed for item | admin | — |
| POST | `/api/feeds/collection/:collectionId/open` | Open feed for collection | admin | — |
| POST | `/api/feeds/series/:seriesId/open` | Open feed for series | admin | — |
| POST | `/api/feeds/:id/close` | Close feed | admin | — |

## Users

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| POST | `/api/users` | Create user | admin | — |
| GET | `/api/users` | List users | admin | — |
| GET | `/api/users/online` | Online users | | — |
| GET | `/api/users/:id` | Get user | admin | — |
| PATCH | `/api/users/:id` | Update user | admin | — |
| DELETE | `/api/users/:id` | Delete user | admin | — |
| PATCH | `/api/users/:id/openid-unlink` | Unlink OpenID | | — |
| GET | `/api/users/:id/listening-sessions` | User listening sessions | | — |
| GET | `/api/users/:id/listening-stats` | User listening stats | | — |

## Notifications

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/notifications` | Get settings | admin | — |
| PATCH | `/api/notifications` | Update settings | admin | — |
| GET | `/api/notificationdata` | Notification data | admin | — |
| GET | `/api/notifications/test` | Fire test event | admin | — |
| POST | `/api/notifications` | Create notification | admin | — |
| DELETE | `/api/notifications/:id` | Delete notification | admin | — |
| PATCH | `/api/notifications/:id` | Update notification | admin | — |
| GET | `/api/notifications/:id/test` | Send notification test | admin | — |

## Email

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/emails/settings` | Get email settings | admin | — |
| PATCH | `/api/emails/settings` | Update email settings | admin | — |
| POST | `/api/emails/test` | Send test email | admin | — |
| POST | `/api/emails/ereader-devices` | Update eReader devices | admin | — |
| POST | `/api/emails/send-ebook-to-device` | Send ebook to device | | — |

## API keys

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/api-keys` | List API keys | admin | — |
| POST | `/api/api-keys` | Create API key | admin | — |
| PATCH | `/api/api-keys/:id` | Update API key | admin | — |
| DELETE | `/api/api-keys/:id` | Delete API key | admin | — |

## Custom metadata providers

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/custom-metadata-providers` | List providers | admin | — |
| POST | `/api/custom-metadata-providers` | Create provider | admin | — |
| DELETE | `/api/custom-metadata-providers/:id` | Delete provider | admin | — |

## Shares

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| POST | `/api/share/mediaitem` | Create media share | | — |
| DELETE | `/api/share/mediaitem/:id` | Delete media share | | — |

Public (no auth, `/share/:slug…`, `/session/:id/track/:index`) and HLS
streaming routes are intentionally omitted — out of scope for a management CLI.

## Stats

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/stats/year/:year` | Server stats for year | admin | — |
| GET | `/api/stats/server` | Server statistics | admin | — |

## Filesystem

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| GET | `/api/filesystem` | List filesystem paths | | — |
| POST | `/api/filesystem/pathexists` | Check path exists | | — |

## Misc

| Method | Path | Description | Perm | CLI |
|--------|------|-------------|------|-----|
| POST | `/api/upload` | Upload file(s) | upload | `upload` ✅ |
| GET | `/api/tasks` | Background tasks | | `tasks list` ✅ |
| PATCH | `/api/settings` | Update server settings | admin | — |
| PATCH | `/api/sorting-prefixes` | Update sorting prefixes | admin | — |
| POST | `/api/authorize` | Authorize user | | 🔒 (login) |
| GET | `/api/tags` | List tags | | — |
| POST | `/api/tags/rename` | Rename tag | admin | — |
| DELETE | `/api/tags/:tag` | Delete tag | admin | — |
| GET | `/api/genres` | List genres | | — |
| POST | `/api/genres/rename` | Rename genre | admin | — |
| DELETE | `/api/genres/:genre` | Delete genre | admin | — |
| POST | `/api/validate-cron` | Validate cron expression | admin | — |
| GET | `/api/auth-settings` | Get auth settings | | — |
| PATCH | `/api/auth-settings` | Update auth settings | admin | — |
| POST | `/api/watcher/update` | Update watched path | admin | — |
| GET | `/api/logger-data` | Logger data | admin | — |

## Auth (non-`/api`)

| Method | Path | Description | CLI |
|--------|------|-------------|-----|
| POST | `/login` | Username/password login | `login` ✅ |
| POST | `/auth/refresh` | Refresh access token | 🔒 (automatic) |
