# Privacy

Arclight is local-first. There is no Arclight account, no Arclight server, no analytics and no telemetry.

## What is stored, and where

Everything is stored on your PC in `%LOCALAPPDATA%\Arclight` (or in the `Data` folder next to `Arclight.exe` for the portable build):

- `arclight.db` — your games, tags, collections, notes, ratings, play sessions and settings (SQLite)
- `Cache\Artwork` — artwork copied from launchers or downloaded (safe to delete)
- `Images` — images you picked for covers and collections
- `Logs` — plain-text application logs, kept for 14 days
- `Backups` — database copies made before upgrades

You can export everything to a JSON file (Settings › Privacy › Export my data), reset statistics, clear play history, clear logs and clear the image cache at any time.

## When Arclight uses the network

Only in these cases:

1. **Missing Steam artwork.** For Steam games without artwork in Steam's own local cache, Arclight requests the cover, hero and header images for that app id from Steam's public image server (`shared.steamstatic.com`). This reveals the app ids of those games to Valve's CDN. Turn it off in Settings › Library › Download missing artwork.
2. **Update checks.** When you press *Check for updates*, or if you turned on *Check for updates on startup*, Arclight requests the latest release information from the public GitHub API. No library data is sent.

Launching a game through a launcher link (for example `steam://`) hands the request to that launcher, which may of course contact its own servers.

## Playtime tracking

To recognise running games, Arclight reads the names and file paths of running programs every few seconds — only while a game launched from Arclight is expected or running, or while the optional background tracker is on. It does not read window titles, screen contents, keystrokes, network traffic or anything else about other programs, and this information is never stored except as the start and end time of sessions for games in your library.

## Uninstalling

Uninstalling removes the program. Your library folder is kept so a reinstall continues where you left off; delete `%LOCALAPPDATA%\Arclight` to remove it. Arclight never deletes, moves or modifies your games.
