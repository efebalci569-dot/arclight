import { useEffect, useState } from 'react';

export type Route = 'home' | 'download' | 'changelog' | 'not-found';

/**
 * Tiny hash router: GitHub Pages serves static files only, so routes live after the "#".
 * In-page anchors use "#/section" style ids handled by the home page.
 */
export function parseRoute(hash: string): { route: Route; anchor?: string } {
  const path = hash.replace(/^#\/?/, '');
  if (path === '' || path.startsWith('features') || path.startsWith('platforms') || path.startsWith('privacy') || path.startsWith('screens')) {
    return { route: 'home', anchor: path || undefined };
  }

  if (path.startsWith('download')) return { route: 'download' };
  if (path.startsWith('changelog')) return { route: 'changelog' };
  return { route: 'not-found' };
}

export function useRoute() {
  const [state, setState] = useState(() => parseRoute(window.location.hash));

  useEffect(() => {
    const onChange = () => setState(parseRoute(window.location.hash));
    window.addEventListener('hashchange', onChange);
    return () => window.removeEventListener('hashchange', onChange);
  }, []);

  return state;
}
