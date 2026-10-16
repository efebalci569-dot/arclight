import type { ReactNode } from 'react';
import { Icon } from '../components/Icon';
import { LanguageSwitch } from '../components/LanguageSwitch';
import { Logo } from '../components/Logo';
import { useLanguage } from '../services/language';
import { repository } from '../services/site';

export function SiteLayout({ children }: { children: ReactNode }) {
  const { t } = useLanguage();
  return (
    <>
      <a className="skip-link" href="#main">
        {t.layout.skipToContent}
      </a>
      <header className="site-header">
        <div className="site-header__inner">
          <a href="#/" className="site-header__brand" aria-label={t.layout.homeLabel}>
            <Logo />
          </a>
          <nav className="site-nav" aria-label={t.layout.mainNav}>
            <a href="#/features">{t.layout.features}</a>
            <a href="#/platforms">{t.layout.platforms}</a>
            <a href="#/privacy">{t.layout.privacy}</a>
            <a href="#/changelog">{t.common.releaseNotes}</a>
          </nav>
          <div className="site-header__actions">
            <LanguageSwitch />
            <a className="icon-link site-header__github" href={repository.url} aria-label={t.layout.github} rel="noreferrer" target="_blank">
              <Icon name="github" size={20} />
            </a>
            <a className="button button--small button--primary" href="#/download">
              {t.common.download}
            </a>
          </div>
        </div>
      </header>
      <main id="main">{children}</main>
      <footer className="site-footer">
        <div className="site-footer__inner">
          <div className="site-footer__brand">
            <Logo />
            <p lang="en">Your Games. One Universe.</p>
          </div>
          <nav className="site-footer__links" aria-label={t.layout.footerNav}>
            <a href="#/download">{t.common.download}</a>
            <a href="#/changelog">{t.common.releaseNotes}</a>
            <a href={repository.url} rel="noreferrer" target="_blank">
              {t.layout.sourceCode}
            </a>
            <a href={`${repository.url}/blob/main/LICENSE`} rel="noreferrer" target="_blank">
              {t.layout.license}
            </a>
            <a href={`${repository.url}/blob/main/docs/PRIVACY.md`} rel="noreferrer" target="_blank">
              {t.layout.privacy}
            </a>
          </nav>
          <p className="site-footer__legal">{t.layout.legal}</p>
        </div>
      </footer>
    </>
  );
}
