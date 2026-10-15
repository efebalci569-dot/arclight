import type { ChangelogEntry, Feature, PlatformSupport } from '../types/site';

export const repository = {
  owner: 'efebalci569-dot',
  name: 'arclight',
  get url() {
    return `https://github.com/${this.owner}/${this.name}`;
  },
  get releasesUrl() {
    return `${this.url}/releases`;
  },
};

export const platforms: PlatformSupport[] = [
  {
    name: 'Steam',
    status: 'supported',
    source: 'Library folders and app manifests in your Steam install',
    launch: 'Through Steam',
  },
  {
    name: 'Epic Games',
    status: 'supported',
    source: 'The Epic Games Launcher’s install manifests',
    launch: 'Through the Epic Games Launcher',
  },
  {
    name: 'GOG',
    status: 'supported',
    source: 'Install records written by GOG installers and GOG Galaxy',
    launch: 'Directly from the game’s executable',
  },
  {
    name: 'Xbox (PC Game Pass)',
    status: 'experimental',
    source: 'Games in your XboxGames folders',
    launch: 'As a Windows app',
  },
  {
    name: 'Ubisoft Connect',
    status: 'experimental',
    source: 'Ubisoft Connect’s install records',
    launch: 'Through Ubisoft Connect',
  },
  {
    name: 'EA app',
    status: 'manual',
    source: 'Detected, but its game list can’t be read yet',
    launch: 'Add games manually',
  },
  {
    name: 'Any other game',
    status: 'manual',
    source: 'Folders you choose, or a single .exe',
    launch: 'Directly, or with a launcher link',
  },
];

export const features: Feature[] = [
  {
    id: 'library',
    title: 'Unified game library',
    body: 'Steam, Epic Games, GOG, Xbox, Ubisoft Connect and standalone games side by side, with big covers, instant search and filters you can combine.',
    icon: 'library',
  },
  {
    id: 'launch',
    title: 'Quick game launching',
    body: 'Play starts the game through its own launcher or straight from its executable. If a game moved or was uninstalled, Arclight tells you what happened and how to fix it.',
    icon: 'play',
  },
  {
    id: 'tracking',
    title: 'Playtime tracking',
    body: 'Time is counted only while the game’s own process is running. Launchers, crash reporters and sleep time never count, and nothing is estimated or imported.',
    icon: 'clock',
  },
  {
    id: 'collections',
    title: 'Custom collections',
    body: 'Backlog, Completed, Weekend Games — make your own, drag them into order and put a game in as many as you like.',
    icon: 'stack',
  },
  {
    id: 'notes',
    title: 'Personal game notes',
    body: 'Where you left off, quests to finish, settings that work. Notes save as you type and stay on your PC.',
    icon: 'note',
  },
  {
    id: 'stats',
    title: 'Game statistics',
    body: 'Weekly and monthly totals, a 30-day activity chart, your most played games and your library by platform.',
    icon: 'chart',
  },
  {
    id: 'local',
    title: 'Local-first library',
    body: 'No account, no cloud. Everything lives in a local database you can export to JSON at any time.',
    icon: 'shield',
  },
  {
    id: 'custom',
    title: 'Customizable interface',
    body: 'Grid, compact grid or list. Seven accent colors, an OLED-black theme, card sizes and a reduced-motion mode.',
    icon: 'palette',
  },
];

export const changelog: ChangelogEntry[] = [
  {
    version: '0.1.0',
    date: '2026-10-14',
    summary: 'The first public build of Arclight: a working library, launcher and playtime tracker for Windows.',
    groups: [
      {
        title: 'Library',
        items: [
          'Add games manually or scan Steam, Epic Games, GOG, Xbox (experimental) and Ubisoft Connect (experimental).',
          'Grid, compact grid and list views with search, combinable filters and eight sort orders.',
          'Favorites, collections with drag-and-drop ordering, tags, personal notes and 5-star ratings.',
          'Scans never add duplicates; removed games can be hidden from future scans.',
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
          'Local SQLite storage with automatic backups before upgrades and JSON export.',
          'Installer and portable builds for Windows 10 and 11 (x64).',
        ],
      },
    ],
  },
];
