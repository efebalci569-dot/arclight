import { useId } from 'react';
import { Icon } from './Icon';
import { WindowFrame } from './WindowFrame';

const base = import.meta.env.BASE_URL;

/**
 * The page's one big moment: an arc of light — the crossbar of the Arclight mark —
 * draws itself once behind a real screenshot of the app.
 */
export function Hero() {
  const gradientId = useId();
  const glowId = useId();
  return (
    <section className="hero" aria-labelledby="hero-title">
      <div className="hero__copy">
        <h1 id="hero-title" className="hero__title">
          <span>Your Games.</span>
          <span>One Universe.</span>
        </h1>
        <p className="hero__lead">Bring your PC games together in one beautiful library.</p>
        <div className="hero__actions">
          <a className="button button--primary button--large" href="#/download">
            <Icon name="windows" size={18} />
            Download for Windows
          </a>
          <a className="button button--ghost button--large" href="#/features">
            Explore Features
          </a>
        </div>
        <p className="hero__note">Free and open source, for Windows 10 and 11.</p>
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
        <WindowFrame
          src={`${base}screenshots/home.png`}
          alt="Arclight's Home page: a featured game, quick stats, Continue Playing and Recently Added."
          eager
        />
      </div>
    </section>
  );
}
