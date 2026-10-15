import type { ReactNode } from 'react';
import { Icon } from '../components/Icon';
import { Logo } from '../components/Logo';
import { repository } from '../services/site';

export function SiteLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      <header className="site-header">
        <div className="site-header__inner">
          <a href="#/" className="site-header__brand" aria-label="Arclight home">
            <Logo />
          </a>
          <nav className="site-nav" aria-label="Main">
            <a href="#/features">Features</a>
            <a href="#/platforms">Platforms</a>
            <a href="#/privacy">Privacy</a>
            <a href="#/changelog">Release notes</a>
          </nav>
          <div className="site-header__actions">
            <a className="icon-link" href={repository.url} aria-label="Arclight on GitHub" rel="noreferrer" target="_blank">
              <Icon name="github" size={20} />
            </a>
            <a className="button button--small button--primary" href="#/download">
              Download
            </a>
          </div>
        </div>
      </header>
      <main id="main">{children}</main>
      <footer className="site-footer">
        <div className="site-footer__inner">
          <div className="site-footer__brand">
            <Logo />
            <p>Your Games. One Universe.</p>
          </div>
          <nav className="site-footer__links" aria-label="Footer">
            <a href="#/download">Download</a>
            <a href="#/changelog">Release notes</a>
            <a href={repository.url} rel="noreferrer" target="_blank">
              Source code
            </a>
            <a href={`${repository.url}/blob/main/LICENSE`} rel="noreferrer" target="_blank">
              MIT License
            </a>
            <a href={`${repository.url}/blob/main/docs/PRIVACY.md`} rel="noreferrer" target="_blank">
              Privacy
            </a>
          </nav>
          <p className="site-footer__legal">
            Arclight is an independent open-source project. It is not affiliated with Valve, Epic Games, Microsoft, CD PROJEKT, Ubisoft or
            Electronic Arts. Game names and trademarks belong to their owners.
          </p>
        </div>
      </footer>
    </>
  );
}
