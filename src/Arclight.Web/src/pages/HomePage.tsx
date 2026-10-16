import { useEffect } from 'react';
import { Features } from '../components/Features';
import { Hero } from '../components/Hero';
import { Icon } from '../components/Icon';
import { PlatformTable } from '../components/PlatformTable';
import { Privacy } from '../components/Privacy';
import { Screens } from '../components/Screens';
import { useLanguage } from '../services/language';

export function HomePage({ anchor }: { anchor?: string }) {
  const { t } = useLanguage();

  useEffect(() => {
    if (!anchor) {
      window.scrollTo({ top: 0 });
      return;
    }

    const target = document.getElementById(anchor);
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    target?.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'start' });
  }, [anchor]);

  return (
    <>
      <Hero />
      <PlatformTable />
      <Features />
      <Screens />
      <Privacy />
      <section className="closing" aria-labelledby="closing-title">
        <h2 id="closing-title">{t.closing.title}</h2>
        <p>{t.closing.body}</p>
        <a className="button button--primary button--large" href="#/download">
          <Icon name="windows" size={18} />
          {t.common.downloadForWindows}
        </a>
      </section>
    </>
  );
}
