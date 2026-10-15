export type SupportStatus = 'supported' | 'experimental' | 'manual';

export interface PlatformSupport {
  name: string;
  status: SupportStatus;
  /** What Arclight reads on the PC to find games. */
  source: string;
  launch: string;
}

export interface Feature {
  id: string;
  title: string;
  body: string;
  icon: IconName;
}

export type IconName =
  | 'library'
  | 'play'
  | 'clock'
  | 'stack'
  | 'note'
  | 'chart'
  | 'shield'
  | 'palette';

export interface ReleaseAsset {
  name: string;
  size: number;
  url: string;
}

export interface Release {
  tag: string;
  name: string;
  publishedAt: string;
  url: string;
  notes: string;
  assets: ReleaseAsset[];
}

export type ReleaseState =
  | { kind: 'loading' }
  | { kind: 'available'; release: Release }
  | { kind: 'none' }
  | { kind: 'error' };

export interface ChangelogEntry {
  version: string;
  date: string;
  summary: string;
  groups: { title: string; items: string[] }[];
}
