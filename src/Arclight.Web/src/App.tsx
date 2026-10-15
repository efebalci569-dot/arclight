import { useEffect } from 'react';
import { SiteLayout } from './layouts/SiteLayout';
import { ChangelogPage } from './pages/ChangelogPage';
import { DownloadPage } from './pages/DownloadPage';
import { HomePage } from './pages/HomePage';
import { useRoute } from './services/router';

const titles = {
  home: 'Arclight — Your Games. One Universe.',
  download: 'Download Arclight for Windows',
  changelog: 'Release notes — Arclight',
  'not-found': 'Page not found — Arclight',
};

export function App() {
  const { route, anchor } = useRoute();

  useEffect(() => {
    document.title = titles[route];
    if (route !== 'home') {
      window.scrollTo({ top: 0 });
    }
  }, [route]);

  return (
    <SiteLayout>
      {route === 'home' && <HomePage anchor={anchor} />}
      {route === 'download' && <DownloadPage />}
      {route === 'changelog' && <ChangelogPage />}
      {route === 'not-found' && (
        <div className="page page--center">
          <h1>That page doesn’t exist</h1>
          <p>The link may be old. Head back to the start.</p>
          <a className="button button--primary" href="#/">
            Go to the home page
          </a>
        </div>
      )}
    </SiteLayout>
  );
}
