# Architecture

Arclight is a Windows desktop app (WPF, .NET 10) with a separate static website (React + Vite). The desktop app works fully offline; the website never talks to it.

## Projects

```
Arclight.Desktop  ──►  Arclight.Platforms  ──►  Arclight.Core
     (WPF, MVVM)        (Windows specifics)      (pure .NET, no UI)
```

| Project | Responsibility |
|---|---|
| **Arclight.Core** (`net10.0`) | Domain models, SQLite repositories and migrations, `LibraryService` (shared in-memory state), library search/filter/sort, scan merging and de-duplication, the playtime `SessionTracker` state machine and `TrackingService`, statistics, launch validation, artwork cache. No Windows or UI dependencies, so it is fully unit-tested. |
| **Arclight.Platforms** (`net10.0-windows`) | One `IPlatformScanner` per launcher, a read-only registry abstraction, the Windows process snapshot (via `QueryFullProcessImageName`) and `WindowsGameLauncher`. |
| **Arclight.Desktop** (`net10.0-windows`, WPF) | Views, view models (CommunityToolkit.Mvvm), theme resources, dialogs, notifications, tray icon, single-instance guard, startup registration, file logging and the composition root (`App.xaml.cs`). |
| **Arclight.Web** | Marketing and download site, deployed to GitHub Pages. It reads release information from the public GitHub API at runtime. |

## Data

SQLite database at `%LOCALAPPDATA%\Arclight\arclight.db` (or `.\Data` in portable mode), WAL journaling, foreign keys on. Schema versions are tracked with `PRAGMA user_version`; before an upgrade the database is copied with SQLite's online backup into `Backups\`. A database created by a newer version is refused with a clear message instead of being modified.

| Table | Contents |
|---|---|
| `games` | One row per game. Every field except the name is optional. |
| `tags`, `game_tags` | Tags and their assignment to games. |
| `collections`, `collection_games` | Collections, their order and membership (deleting a collection never deletes games). |
| `play_sessions` | Verified sessions: start, end, last heartbeat, duration, source, status. Unique on `(game_id, started_at)`. |
| `settings` | `AppSettings` as one JSON document, so new settings get defaults automatically. |
| `ignored_games` | Scan results the user chose to hide. |
| `image_cache` | Artwork download bookkeeping (failed URLs are retried after a cool-down, never on every start). |

Playtime totals on `games` are written only by `SessionRepository`, inside the same transaction that completes a session, and only if the session was still `active`. Editing a game can therefore never overwrite tracked time, and a session can never be counted twice.

Files: `Cache\Artwork` (copied or downloaded artwork, safe to clear), `Images` (images the user picked — never removed by clearing the cache), `Logs` (plain text, 14 days), `Backups`.

## Threading

`LibraryService` is the single source of truth for games, collections and settings. Database work runs on the thread pool; in-memory updates and change events are marshalled to the UI thread through a dispatcher delegate supplied by the app. The game list is replaced copy-on-write so the tracker can read it from its polling thread. Scans run all scanners in parallel with a 60-second timeout each.

## Playtime tracking

1. Launching a game from Arclight registers an *expectation* with the `SessionTracker`.
2. While there is anything to watch, `TrackingService` takes a process snapshot every 5 seconds.
3. `GameProcessMatcher` decides which games are running: the configured executable always counts; any other `.exe` counts only if it runs from the game's install folder and doesn't match helper patterns (launchers, updaters, crash reporters, anti-cheat, installers) or user exclusions. Install folders that aren't specific enough (drive roots, `Program Files`, anything under `Windows`) are never used for matching.
4. The first time the process is seen, a session starts and is written as `active`. Heartbeats update it every 30 seconds.
5. The session ends at the last time the game was seen once it has been gone for 20 seconds (this tolerates launcher-to-game hand-offs). Sessions shorter than 30 seconds are discarded.
6. A gap of more than 90 seconds between polls means the PC slept; the session is closed at the last poll and a new one starts if the game is still running.
7. On start-up, sessions left `active` by a crash are closed at their last heartbeat, so time while Arclight wasn't running is never counted.

The optional background tracker adds every installed game with tracking enabled as a watch target, so games started from Steam or a desktop shortcut are tracked too.

## Launching

`WindowsGameLauncher` validates the configuration first (`LaunchValidator`): absolute paths, allowed file types, no control characters in arguments, and launcher URIs only for an allow-list of schemes whose handler is registered. Executables are started directly without a shell, so a game that needs administrator rights fails with `ElevationRequired` and the UI asks the user before retrying with elevation. Windows (Xbox) apps are activated through `explorer.exe shell:AppsFolder\…`.

## Artwork

Resolution order: images the user picked → artwork the launcher already stores locally (Steam library cache, Xbox package logos, GOG `.ico`, executable icons) → the platform's own public images for the exact game (Steam CDN by app id, Epic links from the launcher's `catcache.bin`, Microsoft Store catalog by package family name) → for games that still have no cover, a strict name match in the Steam store (`Arclight.Core.Artwork`) → a generated placeholder (gradient, icon and title). Everything online can be turned off with one setting. Lookups that find nothing are remembered for a week; downloads are limited in size and concurrency, serialized per image, written to a temp file first and cached forever.

## Localization

All interface text lives in `src/Arclight.Desktop/Resources/Strings/Strings.en.xaml` and `Strings.tr.xaml`, one `sys:String` per key. `Loc.Apply` merges the chosen file into the application resources, so XAML that uses `{DynamicResource Key}` updates the moment the language changes. Code reads strings with `Loc.Get` / `Loc.Format` / `Loc.Count` (`Key.One` / `Key.Other` for plurals), and view models rebuild their computed text on `Loc.LanguageChanged`. Core stays UI-free: `Format` (playtime, relative dates) reads its words and culture from a swappable `FormatStrings`, and `LaunchValidator` returns error codes that map to `Validation.*` keys.

The setting is `auto` (Windows display language; Turkish when it is Turkish, English otherwise), `en` or `tr`. `--lang tr` overrides it for one run. To add a language, copy `Strings.en.xaml` to `Strings.<code>.xaml`, translate the values (keep `{0}` placeholders and leading/trailing spaces), and add the code to `Loc.Resolve` and the Settings language list. Tests fail if a key, placeholder or edge space is missing in any language.

## Testing

- `Arclight.Core.Tests`: filtering and sorting, scan merging, the session state machine, process matching, statistics windows (including midnight and month boundaries), launch validation, repositories against a real temporary SQLite file, crash recovery and the tracking service with a fake process list.
- `Arclight.Platforms.Tests`: VDF parsing, Steam/Epic/GOG/Ubisoft/Xbox/EA/folder scanners against temporary folders and an in-memory registry, Windows package family names, launcher error handling.
- `Arclight.Desktop.Tests`: start-up switches, converters, editor helpers and localization (same keys and placeholders in every language, no untranslated text, every key the app uses exists, and live switching inside a real WPF application).
