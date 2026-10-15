import { useState } from 'react';
import { WindowFrame } from './WindowFrame';

const base = import.meta.env.BASE_URL;

const screens = [
  { id: 'library', label: 'Library', file: 'library.png', alt: 'My Library in grid view with filter chips, sort and view options.' },
  { id: 'detail', label: 'Game page', file: 'detail.png', alt: 'A game page with playtime, last played, sessions, rating and personal notes.' },
  { id: 'stats', label: 'Statistics', file: 'stats.png', alt: 'Statistics with totals, a 30-day activity chart and most played games.' },
  { id: 'collections', label: 'Collections', file: 'collections.png', alt: 'Collections shown as tiles made from the covers of the games inside them.' },
];

export function Screens() {
  const [active, setActive] = useState(screens[0].id);
  const current = screens.find((s) => s.id === active)!;

  return (
    <section className="section" id="screens" aria-labelledby="screens-title">
      <div className="section__head">
        <h2 id="screens-title">See it in action</h2>
        <p>Real screenshots of Arclight 0.1, taken with its built-in sample library of made-up games.</p>
      </div>
      <div className="tabs" role="tablist" aria-label="Screenshots">
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
            {screen.label}
          </button>
        ))}
      </div>
      <div id="screen-panel" role="tabpanel" aria-labelledby={`tab-${current.id}`} className="screens__panel">
        <WindowFrame key={current.id} src={`${base}screenshots/${current.file}`} alt={current.alt} />
      </div>
    </section>
  );
}
