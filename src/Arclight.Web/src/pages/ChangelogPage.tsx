import { changelog, repository } from '../services/site';
import { formatDate } from '../services/releases';

export function ChangelogPage() {
  return (
    <div className="page">
      <header className="page__head">
        <h1>Release notes</h1>
        <p>
          What changed in each version. Builds are published on{' '}
          <a href={repository.releasesUrl} rel="noreferrer" target="_blank">
            GitHub Releases
          </a>
          .
        </p>
      </header>
      <ol className="changelog">
        {changelog.map((entry) => (
          <li key={entry.version} className="changelog__entry">
            <div className="changelog__meta">
              <h2>{entry.version}</h2>
              <time dateTime={entry.date}>{formatDate(entry.date)}</time>
            </div>
            <div className="changelog__body">
              <p className="changelog__summary">{entry.summary}</p>
              {entry.groups.map((group) => (
                <section key={group.title}>
                  <h3>{group.title}</h3>
                  <ul className="plain-list">
                    {group.items.map((item) => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                </section>
              ))}
            </div>
          </li>
        ))}
      </ol>
    </div>
  );
}
