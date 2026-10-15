import { features } from '../services/site';
import { Icon } from './Icon';

/** A week of play drawn as bars — mirrors the Statistics page's activity chart. */
const week = [
  { day: 'Mon', hours: 1.2 },
  { day: 'Tue', hours: 0 },
  { day: 'Wed', hours: 2.4 },
  { day: 'Thu', hours: 0.8 },
  { day: 'Fri', hours: 3.1 },
  { day: 'Sat', hours: 4.2 },
  { day: 'Sun', hours: 2.6 },
];

export function Features() {
  const tracking = features.find((f) => f.id === 'tracking')!;
  // Local-first gets its own section further down (Privacy).
  const others = features.filter((f) => f.id !== 'tracking' && f.id !== 'local');
  return (
    <section className="section" id="features" aria-labelledby="features-title">
      <div className="section__head">
        <h2 id="features-title">Built to be used every day</h2>
        <p>A launcher you open first: fast to search, honest about what it knows, and yours to arrange.</p>
      </div>

      <div className="spotlight">
        <div className="spotlight__text">
          <span className="feature-icon">
            <Icon name={tracking.icon} />
          </span>
          <h3>{tracking.title}</h3>
          <p>{tracking.body}</p>
          <dl className="counts">
            <div>
              <dt>Counts</dt>
              <dd>The game’s own process, while it runs</dd>
            </div>
            <div>
              <dt>Never counts</dt>
              <dd>Launchers, crash reporters, anti-cheat helpers, time the PC was asleep</dd>
            </div>
          </dl>
        </div>
        <figure className="spotlight__chart" aria-label="Example week of tracked playtime">
          <div className="bars">
            {week.map((d) => (
              <div className="bars__col" key={d.day}>
                <div className="bars__bar" style={{ height: `${Math.max(3, (d.hours / 4.2) * 100)}%` }} data-empty={d.hours === 0 || undefined} />
                <span>{d.day}</span>
              </div>
            ))}
          </div>
          <figcaption>Example week. Real totals come only from sessions Arclight verified on your PC.</figcaption>
        </figure>
      </div>

      <ul className="feature-grid">
        {others.map((feature) => (
          <li key={feature.id}>
            <span className="feature-icon">
              <Icon name={feature.icon} />
            </span>
            <h3>{feature.title}</h3>
            <p>{feature.body}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
