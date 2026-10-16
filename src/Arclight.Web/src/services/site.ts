import type { ChangelogEntry, Feature, PlatformSupport } from '../types/site';

// Only structure lives here; every visible string is in src/i18n.

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
  { id: 'steam', status: 'supported' },
  { id: 'epic', status: 'supported' },
  { id: 'gog', status: 'supported' },
  { id: 'xbox', status: 'experimental' },
  { id: 'ubisoft', status: 'experimental' },
  { id: 'ea', status: 'manual' },
  { id: 'other', status: 'manual' },
];

export const features: Feature[] = [
  { id: 'library', icon: 'library' },
  { id: 'launch', icon: 'play' },
  { id: 'tracking', icon: 'clock' },
  { id: 'collections', icon: 'stack' },
  { id: 'notes', icon: 'note' },
  { id: 'stats', icon: 'chart' },
  { id: 'local', icon: 'shield' },
  { id: 'custom', icon: 'palette' },
];

/** Newest first. Each version's notes are in src/i18n; the compiler checks every language has them. */
export const changelog = [{ version: '0.1.0', date: '2026-10-15' }] as const satisfies readonly ChangelogEntry[];

export type ChangelogVersion = (typeof changelog)[number]['version'];
