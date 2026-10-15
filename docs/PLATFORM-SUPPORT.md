# Platform support

Arclight only reads records that launchers already keep on your PC. It never asks for launcher credentials, never reads cookies or tokens, never changes launcher files and never needs administrator rights. Scanners are read-only and isolated in `src/Arclight.Platforms`, one per launcher.

| Platform | Status | What is read | Launch | Notes |
|---|---|---|---|---|
| Steam | Supported | `HKCU\Software\Valve\Steam\SteamPath`, `steamapps\libraryfolders.vdf`, `appmanifest_*.acf` in every library | `steam://rungameid/<appid>` | Only installed (or downloading) games are visible locally; owned-but-not-installed games would need the Steam Web API, which Arclight doesn't use. A few known tools and runtimes (Steamworks Redistributables, Proton, SteamVR, Wallpaper Engine…) are marked "probably not a game" and left unchecked. Artwork comes from Steam's local library cache, then Steam's public CDN. |
| Epic Games | Supported | `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item` (location from the launcher's registry key) | `com.epicgames.launcher://apps/<ns>%3A<item>%3A<app>?action=launch` | DLC and non-game items (for example Unreal Engine) are skipped. Covers and backgrounds come from the image links in the launcher's own catalog cache (`Data\Catalog\catcache.bin`). |
| GOG | Supported | `HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\<id>` | The game's executable with its launch parameters | Works for games installed with or without GOG Galaxy. DLC entries are skipped. |
| Xbox / PC Game Pass | Experimental | The `.GamingRoot` file on each fixed drive and `<root>\<game>\Content\appxmanifest.xml` / `MicrosoftGame.config` | `shell:AppsFolder\<PackageFamilyName>!<AppId>` | Only games installed into user-accessible `XboxGames` folders are found. Older Microsoft Store games in the protected `WindowsApps` folder are not read; add them manually. Covers come from the Microsoft Store catalog, looked up by package family name. |
| Ubisoft Connect | Experimental | `HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\<id>` and the matching uninstall entry for the name | `uplay://launch/<id>/0` | Undocumented layout; may miss games. |
| EA app | Manual only | Whether the EA app is installed | — | The EA app keeps its game list in encrypted storage, which Arclight doesn't read. Add EA games with Add a Game (executable or `link2ea://`/`origin2://` link). |
| Game folders | Experimental | Subfolders of folders you choose in Settings | The suggested executable | Each subfolder is suggested as a game with its most likely executable (named like the folder, not an installer or crash handler). You confirm every suggestion. |
| Manual | — | — | Executable, launcher link or custom command | Anything else, including emulators and mod launchers. |

## Duplicates

A detected game matches an existing library entry by platform id, then by executable path, then by install folder. Matches are listed under "In library" in the scan window and are never added again. When a launcher no longer lists a game it was scanned from, the game is marked *Not installed* (it is not removed). Games you remove can be hidden from future scans; Settings › Library lists them so you can show them again.

## Adding support for another launcher

1. Implement `IPlatformScanner` in `src/Arclight.Platforms/<Launcher>/`.
2. Read only documented or stable local data; never prompt for credentials or touch protected folders.
3. Return `PlatformScanStatus.LauncherNotFound` when the launcher isn't installed and `Unsupported` when you can detect it but not its games.
4. Add tests in `tests/Arclight.Platforms.Tests` using `FakeRegistry` and `TempFolder`.
5. Register the scanner in `App.xaml.cs` and document it here and in the website's platform table.
