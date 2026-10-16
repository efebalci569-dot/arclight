export type SupportStatus = 'supported' | 'experimental' | 'manual';

export type PlatformId = 'steam' | 'epic' | 'gog' | 'xbox' | 'ubisoft' | 'ea' | 'other';

export interface PlatformSupport {
  id: PlatformId;
  status: SupportStatus;
}

export interface PlatformText {
  name: string;
  /** What Arclight reads on the PC to find games. */
  source: string;
  launch: string;
}

export type FeatureId = 'library' | 'launch' | 'tracking' | 'collections' | 'notes' | 'stats' | 'local' | 'custom';

export interface Feature {
  id: FeatureId;
  icon: IconName;
}

export type ScreenId = 'library' | 'detail' | 'stats' | 'collections';

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
}

export interface ChangelogNotes {
  summary: string;
  groups: { title: string; items: string[] }[];
}
