import { useState } from 'react';
import { useLanguage } from '../services/language';
import type { ScreenId } from '../types/site';
import { WindowFrame } from './WindowFrame';

const base = import.meta.env.BASE_URL;

const screens: { id: ScreenId; file: string }[] = [
  { id: 'library', file: 'library.png' },
  { id: 'detail', file: 'detail.png' },
  { id: 'stats', file: 'stats.png' },
  { id: 'collections', file: 'collections.png' },
];

export function Screens() {
  const { t } = useLanguage();
  const text = t.screens;
  const [active, setActive] = useState(screens[0].id);
  const current = screens.find((s) => s.id === active)!;

  return (
    <section className="section" id="screens" aria-labelledby="screens-title">
      <div className="section__head">
        <h2 id="screens-title">{text.title}</h2>
        <p>{text.intro}</p>
      </div>
      <div className="tabs" role="tablist" aria-label={text.tabsLabel}>
        {screens.map((screen) => (
          <button
            key={screen.id}
            role="tab"
            id={`tab-${screen.id}`}
            aria-selected={screen.id === active}
            aria-controls="screen-panel"
            className="tabs__tab"
            onClick={() => setActive(screen.id)}
          >
            {text.items[screen.id].label}
          </button>
        ))}
      </div>
      <div id="screen-panel" role="tabpanel" aria-labelledby={`tab-${current.id}`} className="screens__panel">
        <WindowFrame key={current.id} src={`${base}screenshots/${current.file}`} alt={text.items[current.id].alt} />
      </div>
    </section>
  );
}
