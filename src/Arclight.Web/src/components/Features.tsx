import { useLanguage } from '../services/language';
import { features } from '../services/site';
import { Icon } from './Icon';

/** A week of play (hours, Monday first) drawn as bars — mirrors the Statistics page's activity chart. */
const week = [1.2, 0, 2.4, 0.8, 3.1, 4.2, 2.6];

export function Features() {
  const { t } = useLanguage();
  const text = t.features;
  const tracking = features.find((f) => f.id === 'tracking')!;
  // Local-first gets its own section further down (Privacy).
  const others = features.filter((f) => f.id !== 'tracking' && f.id !== 'local');
  return (
    <section className="section" id="features" aria-labelledby="features-title">
      <div className="section__head">
        <h2 id="features-title">{text.title}</h2>
        <p>{text.intro}</p>
      </div>

      <div className="spotlight">
        <div className="spotlight__text">
          <span className="feature-icon">
            <Icon name={tracking.icon} />
          </span>
          <h3>{text.items.tracking.title}</h3>
          <p>{text.items.tracking.body}</p>
          <dl className="counts">
            <div>
              <dt>{text.counts}</dt>
              <dd>{text.countsBody}</dd>
            </div>
            <div>
              <dt>{text.neverCounts}</dt>
              <dd>{text.neverCountsBody}</dd>
            </div>
          </dl>
        </div>
        <figure className="spotlight__chart" aria-label={text.chartLabel}>
          <div className="bars">
            {week.map((hours, day) => (
              <div className="bars__col" key={day}>
                <div className="bars__bar" style={{ height: `${Math.max(3, (hours / 4.2) * 100)}%` }} data-empty={hours === 0 || undefined} />
                <span>{text.weekdays[day]}</span>
              </div>
            ))}
          </div>
          <figcaption>{text.chartCaption}</figcaption>
        </figure>
      </div>

      <ul className="feature-grid">
        {others.map((feature) => (
          <li key={feature.id}>
            <span className="feature-icon">
              <Icon name={feature.icon} />
            </span>
            <h3>{text.items[feature.id].title}</h3>
            <p>{text.items[feature.id].body}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
