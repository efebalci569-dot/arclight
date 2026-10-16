import { useEffect } from 'react';
import { SiteLayout } from './layouts/SiteLayout';
import { ChangelogPage } from './pages/ChangelogPage';
import { DownloadPage } from './pages/DownloadPage';
import { HomePage } from './pages/HomePage';
import { useLanguage } from './services/language';
import { useRoute } from './services/router';

export function App() {
  const { route, anchor } = useRoute();
  const { t } = useLanguage();

  useEffect(() => {
    document.title = t.titles[route];
  }, [route, t]);

  useEffect(() => {
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
          <h1>{t.notFound.title}</h1>
          <p>{t.notFound.body}</p>
          <a className="button button--primary" href="#/">
            {t.notFound.action}
          </a>
        </div>
      )}
    </SiteLayout>
  );
}
