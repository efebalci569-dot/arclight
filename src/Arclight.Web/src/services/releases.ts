import { useEffect, useState } from 'react';
import type { Release, ReleaseState } from '../types/site';
import { repository } from './site';

const cacheKey = 'arclight.latest-release';

interface GitHubRelease {
  tag_name: string;
  name: string | null;
  published_at: string;
  html_url: string;
  body: string | null;
  draft: boolean;
  prerelease: boolean;
  assets: { name: string; size: number; browser_download_url: string }[];
}

/**
 * Asks GitHub for the latest published release. The site never invents a download:
 * if no release exists yet, the page says so and explains how to build from source.
 */
export async function fetchLatestRelease(signal?: AbortSignal): Promise<ReleaseState> {
  try {
    const cached = sessionStorage.getItem(cacheKey);
    if (cached) {
      return JSON.parse(cached) as ReleaseState;
    }
  } catch {
    // Storage can be unavailable (private mode); fall through to the network.
  }

  try {
    const response = await fetch(`https://api.github.com/repos/${repository.owner}/${repository.name}/releases/latest`, {
      headers: { Accept: 'application/vnd.github+json' },
      signal,
    });

    let state: ReleaseState;
    if (response.status === 404) {
      state = { kind: 'none' };
    } else if (!response.ok) {
      return { kind: 'error' };
    } else {
      const data = (await response.json()) as GitHubRelease;
      const release: Release = {
        tag: data.tag_name,
        name: data.name || data.tag_name,
        publishedAt: data.published_at,
        url: data.html_url,
        notes: data.body ?? '',
        assets: data.assets.map((asset) => ({ name: asset.name, size: asset.size, url: asset.browser_download_url })),
      };
      state = release.assets.length > 0 ? { kind: 'available', release } : { kind: 'none' };
    }

    try {
      sessionStorage.setItem(cacheKey, JSON.stringify(state));
    } catch {
      // Ignore storage failures.
    }

    return state;
  } catch (error) {
    if ((error as Error).name === 'AbortError') {
      return { kind: 'loading' };
    }

    return { kind: 'error' };
  }
}

export function useLatestRelease(): ReleaseState {
  const [state, setState] = useState<ReleaseState>({ kind: 'loading' });

  useEffect(() => {
    const controller = new AbortController();
    fetchLatestRelease(controller.signal).then((result) => {
      if (!controller.signal.aborted) {
        setState(result);
      }
    });
    return () => controller.abort();
  }, []);

  return state;
}

export function findInstaller(release: Release) {
  return release.assets.find((asset) => /setup.*\.exe$/i.test(asset.name)) ?? release.assets.find((asset) => asset.name.endsWith('.exe'));
}

export function findPortable(release: Release) {
  return release.assets.find((asset) => /portable.*\.zip$/i.test(asset.name)) ?? release.assets.find((asset) => asset.name.endsWith('.zip'));
}

export function formatSize(bytes: number) {
  return bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(0)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

export function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString('en', { year: 'numeric', month: 'long', day: 'numeric' });
}
