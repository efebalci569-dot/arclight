# Changelog

All notable changes to Arclight are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [0.1.0] - 2026-10-14

The first public build: a working library, launcher and playtime tracker for Windows.

### Library
- Add games manually (executable, launcher link or custom command) with cover, background, genre, year, notes and tags.
- Scan for Games reads Steam, Epic Games and GOG install records; Xbox (PC Game Pass) and Ubisoft Connect are experimental; the EA app is detected but its games must be added manually.
- Optional game folders for DRM-free games, with a suggested executable per folder.
- Scans never add duplicates (platform id, then executable, then install folder); removed games can be hidden from future scans; uninstalled games are marked instead of deleted.
- Grid, compact grid and virtualized list views; search across names, tags, collections, platforms and notes; combinable filters; eight sort orders.
- Favorites, collections with drag-and-drop ordering, tags, personal notes (auto-saved) and 5-star ratings.
- Official covers and backgrounds: Steam's local cache and public CDN, the image links in the Epic launcher's own catalog, the Microsoft Store catalog for Xbox games, and an exact-name match in the Steam store for any other game without a cover. Every online lookup can be turned off with one setting; icons come from executables and Xbox packages, with a designed placeholder otherwise.

### Playing
- Launch through Steam, Epic Games, Ubisoft Connect and Windows app links, or directly from an executable without a command shell.
- Clear messages for missing executables, uninstalled games, missing launchers, access problems and games that need administrator rights, with Locate game and Run as administrator actions.
- Playtime is counted only while the game's own process runs; launchers, crash reporters, anti-cheat helpers and sleep time are excluded. Sessions survive crashes (closed at their last heartbeat) and are never counted twice.
- Optional background tracker for games started outside Arclight, per-game opt-out and user-defined process exclusions.

### App
- Home dashboard (featured game — with a row of your own covers to pick from until you choose one — Continue Playing, Recently Added, quick stats), Recently Played, Statistics (30-day chart, most played, platforms) and Settings.
- Local SQLite storage with backups before schema upgrades, JSON export, statistics reset, play-history clearing and log clearing.
- English and Turkish interface. It follows the Windows display language by default and switches instantly from Settings › General, including dates, numbers and playtime units.
- Accent colors, OLED-black theme, card sizes, grid density and reduced motion.
- Close-to-tray, start minimized and start with Windows (per user, no admin rights).
- Self-contained installer and portable builds for Windows 10 and 11 (x64).

[0.1.0]: https://github.com/efebalci569-dot/arclight/releases/tag/v0.1.0
