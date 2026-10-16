import { useId } from 'react';
import { useLanguage } from '../services/language';
import { Icon } from './Icon';
import { WindowFrame } from './WindowFrame';

const base = import.meta.env.BASE_URL;

/**
 * The page's one big moment: an arc of light — the crossbar of the Arclight mark —
 * draws itself once behind a real screenshot of the app.
 */
export function Hero() {
  const { t } = useLanguage();
  const gradientId = useId();
  const glowId = useId();
  return (
    <section className="hero" aria-labelledby="hero-title">
      <div className="hero__copy">
        {/* The tagline is the brand line and stays in English, as in the app. */}
        <h1 id="hero-title" className="hero__title" lang="en">
          <span>Your Games.</span>
          <span>One Universe.</span>
        </h1>
        <p className="hero__lead">{t.hero.lead}</p>
        <div className="hero__actions">
          <a className="button button--primary button--large" href="#/download">
            <Icon name="windows" size={18} />
            {t.common.downloadForWindows}
          </a>
          <a className="button button--ghost button--large" href="#/features">
            {t.hero.explore}
          </a>
        </div>
        <p className="hero__note">{t.hero.note}</p>
      </div>

      <div className="hero__stage">
        <svg className="hero__arc" viewBox="0 0 1200 560" preserveAspectRatio="none" aria-hidden="true" focusable="false">
          <defs>
            <linearGradient id={gradientId} x1="0" y1="0" x2="1" y2="0">
              <stop offset="0" stopColor="#8B5CF6" />
              <stop offset="1" stopColor="#38BDF8" />
            </linearGradient>
            <filter id={glowId} x="-20%" y="-50%" width="140%" height="200%">
              <feGaussianBlur stdDeviation="18" />
            </filter>
          </defs>
          <path className="hero__arc-glow" d="M 30 560 A 570 500 0 0 1 1170 560" stroke={`url(#${gradientId})`} filter={`url(#${glowId})`} pathLength={1} />
          <path className="hero__arc-line" d="M 30 560 A 570 500 0 0 1 1170 560" stroke={`url(#${gradientId})`} pathLength={1} />
        </svg>
        <WindowFrame src={`${base}screenshots/home.png`} alt={t.hero.screenshotAlt} eager />
      </div>
    </section>
  );
}
