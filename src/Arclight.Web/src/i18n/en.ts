import type { Route } from '../services/router';
import type { ChangelogVersion } from '../services/site';
import type { ChangelogNotes, FeatureId, PlatformId, PlatformText, ScreenId, SupportStatus } from '../types/site';

/**
 * Every visible string on the site, in English. Other languages are typed as `Messages`,
 * so a missing or extra string there fails the build.
 * The tagline "Your Games. One Universe." is the brand line and stays in English everywhere, as in the app.
 */
export const en = {
  common: {
    download: 'Download',
    downloadForWindows: 'Download for Windows',
    releaseNotes: 'Release notes',
  },

  layout: {
    skipToContent: 'Skip to content',
    homeLabel: 'Arclight home',
    mainNav: 'Main',
    features: 'Features',
    platforms: 'Platforms',
    privacy: 'Privacy',
    github: 'Arclight on GitHub',
    language: 'Language',
    footerNav: 'Footer',
    sourceCode: 'Source code',
    license: 'MIT License',
    legal:
      'Arclight is an independent open-source project. It is not affiliated with Valve, Epic Games, Microsoft, CD PROJEKT, Ubisoft or Electronic Arts. Game names and trademarks belong to their owners.',
  },

  titles: {
    home: 'Arclight — Your Games. One Universe.',
    download: 'Download Arclight for Windows',
    changelog: 'Release notes — Arclight',
    'not-found': 'Page not found — Arclight',
  } satisfies Record<Route, string>,

  notFound: {
    title: 'That page doesn’t exist',
    body: 'The link may be old. Head back to the start.',
    action: 'Go to the home page',
  },

  hero: {
    lead: 'Bring your PC games together in one beautiful library.',
    explore: 'Explore Features',
    note: 'Free and open source, for Windows 10 and 11.',
    screenshotAlt: 'Arclight’s Home page: a featured game, quick stats, Continue Playing and Recently Added.',
  },

  platforms: {
    title: 'Every launcher, one library',
    intro:
      'Scan for Games reads the install records each launcher already keeps on your PC. It never asks for launcher passwords, never changes launcher files and never crawls your whole disk. Here is exactly what works today.',
    columns: {
      platform: 'Platform',
      source: 'What Arclight reads',
      launch: 'How games start',
      status: 'Detection',
    },
    status: {
      supported: 'Detected automatically',
      experimental: 'Experimental',
      manual: 'Add manually',
    } satisfies Record<SupportStatus, string>,
    rows: {
      steam: { name: 'Steam', source: 'Library folders and app manifests in your Steam install', launch: 'Through Steam' },
      epic: { name: 'Epic Games', source: 'The Epic Games Launcher’s install manifests', launch: 'Through the Epic Games Launcher' },
      gog: { name: 'GOG', source: 'Install records written by GOG installers and GOG Galaxy', launch: 'Directly from the game’s executable' },
      xbox: { name: 'Xbox (PC Game Pass)', source: 'Games in your XboxGames folders', launch: 'As a Windows app' },
      ubisoft: { name: 'Ubisoft Connect', source: 'Ubisoft Connect’s install records', launch: 'Through Ubisoft Connect' },
      ea: { name: 'EA app', source: 'Detected, but its game list can’t be read yet', launch: 'Add games manually' },
      other: { name: 'Any other game', source: 'Folders you choose, or a single .exe', launch: 'Directly, or with a launcher link' },
    } satisfies Record<PlatformId, PlatformText>,
    footnote:
      'Experimental scanners depend on launcher data that isn’t documented and may miss games. Anything Arclight can’t detect can be added with Add a Game — pick the .exe or a launcher link.',
  },

  features: {
    title: 'Built to be used every day',
    intro: 'A launcher you open first: fast to search, honest about what it knows, and yours to arrange.',
    counts: 'Counts',
    countsBody: 'The game’s own process, while it runs',
    neverCounts: 'Never counts',
    neverCountsBody: 'Launchers, crash reporters, anti-cheat helpers, time the PC was asleep',
    chartLabel: 'Example week of tracked playtime',
    chartCaption: 'Example week. Real totals come only from sessions Arclight verified on your PC.',
    /** Monday first, matching the app's activity chart. */
    weekdays: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
    items: {
      library: {
        title: 'Unified game library',
        body: 'Steam, Epic Games, GOG, Xbox, Ubisoft Connect and standalone games side by side, with big covers, instant search and filters you can combine.',
      },
      launch: {
        title: 'Quick game launching',
        body: 'Play starts the game through its own launcher or straight from its executable. If a game moved or was uninstalled, Arclight tells you what happened and how to fix it.',
      },
      tracking: {
        title: 'Playtime tracking',
        body: 'Time is counted only while the game’s own process is running. Launchers, crash reporters and sleep time never count, and nothing is estimated or imported.',
      },
      collections: {
        title: 'Custom collections',
        body: 'Backlog, Completed, Weekend Games — make your own, drag them into order and put a game in as many as you like.',
      },
      notes: {
        title: 'Personal game notes',
        body: 'Where you left off, quests to finish, settings that work. Notes save as you type and stay on your PC.',
      },
      stats: {
        title: 'Game statistics',
        body: 'Weekly and monthly totals, a 30-day activity chart, your most played games and your library by platform.',
      },
      local: {
        title: 'Local-first library',
        body: 'No account, no cloud. Everything lives in a local database you can export to JSON at any time.',
      },
      custom: {
        title: 'Customizable interface',
        body: 'Grid, compact grid or list. Seven accent colors, an OLED-black theme, card sizes and a reduced-motion mode.',
      },
    } satisfies Record<FeatureId, { title: string; body: string }>,
  },

  screens: {
    title: 'See it in action',
    intro: 'Real screenshots of Arclight 0.1, taken with its built-in sample library of made-up games.',
    tabsLabel: 'Screenshots',
    items: {
      library: { label: 'Library', alt: 'My Library in grid view with filter chips, sort and view options.' },
      detail: { label: 'Game page', alt: 'A game page with playtime, last played, sessions, rating and personal notes.' },
      stats: { label: 'Statistics', alt: 'Statistics with totals, a 30-day activity chart and most played games.' },
      collections: { label: 'Collections', alt: 'Collections shown as tiles made from the covers of the games inside them.' },
    } satisfies Record<ScreenId, { label: string; alt: string }>,
  },

  privacy: {
    title: 'Your library stays on your PC',
    intro:
      'Arclight is local-first. There is no Arclight account and no Arclight server: your games, notes, tags, collections and playtime live in a local database that you can export to JSON or delete whenever you want.',
    columns: [
      {
        title: 'Stored only on this PC',
        items: ['Your game list, artwork cache and personal notes', 'Play sessions and statistics', 'Settings and application logs (kept for 14 days)'],
      },
      {
        title: 'The only times Arclight goes online',
        items: [
          'Downloading missing cover art from Steam, Epic and Microsoft Store image servers (other games are matched by name in the Steam store). You can turn this off.',
          'Checking GitHub for a new version — only when you press Check for updates, or turn on the startup check.',
        ],
      },
      {
        title: 'What the tracker looks at',
        items: [
          'The names and file paths of running programs, to see whether one of your games is running.',
          'Never keystrokes, screen contents, window titles or anything about other programs.',
        ],
      },
    ],
  },

  closing: {
    title: 'Bring every game home.',
    body: 'Arclight is free, open source and runs entirely on your PC.',
  },

  download: {
    title: 'Download Arclight for Windows',
    lead: 'Free and open source. No account, no sign-in, no ads.',
    requirements: {
      title: 'Requirements',
      items: [
        'Windows 10 (version 1809 or later) or Windows 11, 64-bit',
        'About 200 MB of disk space; the .NET runtime is included',
        'No administrator rights needed to run Arclight',
      ],
    },
    installing: {
      title: 'Installing',
      steps: [
        'Download the installer and run it. It installs for your user only.',
        'Arclight isn’t code-signed yet, so Windows SmartScreen may show a warning. Choose More info, then Run anyway — only for files downloaded from the official GitHub release.',
        'Open Arclight and press Scan for Games, or add a game yourself.',
      ],
    },
    portable: {
      title: 'Portable version',
      body: 'Unzip anywhere — a USB drive works — and run Arclight.exe. The portable build keeps its library in a Data folder next to the app instead of your user profile.',
    },
    uninstalling: {
      title: 'Uninstalling',
      body: 'Remove Arclight from Windows Settings › Apps. Your games are never touched. Your library database stays in %LOCALAPPDATA%\\Arclight so a reinstall picks up where you left off; delete that folder to remove it too.',
    },
  },

  release: {
    loading: 'Checking GitHub for the latest release…',
    released: (date: string) => `Released ${date} for Windows 10 and 11 (64-bit).`,
    installer: (size: string) => `Download installer (${size})`,
    portable: (size: string) => `Portable .zip (${size})`,
    releasePage: 'Release page and checksums',
    noneTitle: 'No release has been published yet',
    noneBody:
      'The first installer will appear here as soon as it is published on GitHub. Until then you can build Arclight from source in a few minutes.',
    errorTitle: 'GitHub could not be reached',
    errorBody: 'The latest release couldn’t be loaded right now. You can find every published build on the GitHub releases page.',
    openReleases: 'Open GitHub releases',
    viewSource: 'View source',
    buildLabel: 'Build from source',
    buildHint: 'Requires the .NET 10 SDK on Windows.',
  },

  changelog: {
    /** The intro wraps a link: before + link + after. */
    introBefore: 'What changed in each version. Builds are published on ',
    introLink: 'GitHub Releases',
    introAfter: '.',
    entries: {
      '0.1.0': {
        summary: 'The first public build of Arclight: a working library, launcher and playtime tracker for Windows.',
        groups: [
          {
            title: 'Library',
            items: [
              'Add games manually or scan Steam, Epic Games, GOG, Xbox (experimental) and Ubisoft Connect (experimental).',
              'Grid, compact grid and list views with search, combinable filters and eight sort orders.',
              'Favorites, collections with drag-and-drop ordering, tags, personal notes and 5-star ratings.',
              'Scans never add duplicates; removed games can be hidden from future scans.',
              'Official covers and backgrounds for Steam, Epic, Xbox and other games (online lookups can be turned off).',
            ],
          },
          {
            title: 'Playing',
            items: [
              'Launch through Steam, Epic, Ubisoft and Windows app links or directly from an executable.',
              'Clear explanations when a game can’t start, with Locate game and Run as administrator options.',
              'Verified playtime tracking with crash recovery and sleep detection, plus an optional background tracker.',
            ],
          },
          {
            title: 'App',
            items: [
              'Home dashboard, Recently Played, Statistics and Settings.',
              'English and Turkish interface that follows your Windows language and switches instantly.',
              'Local SQLite storage with automatic backups before upgrades and JSON export.',
              'Installer and portable builds for Windows 10 and 11 (x64).',
            ],
          },
        ],
      },
    } satisfies Record<ChangelogVersion, ChangelogNotes>,
  },
};

export type Messages = typeof en;
