import { useLanguage } from '../services/language';
import { formatDate } from '../services/releases';
import { changelog, repository } from '../services/site';

export function ChangelogPage() {
  const { t, locale } = useLanguage();
  const text = t.changelog;
  return (
    <div className="page">
      <header className="page__head">
        <h1>{t.common.releaseNotes}</h1>
        <p>
          {text.introBefore}
          <a href={repository.releasesUrl} rel="noreferrer" target="_blank">
            {text.introLink}
          </a>
          {text.introAfter}
        </p>
      </header>
      <ol className="changelog">
        {changelog.map((entry) => {
          const notes = text.entries[entry.version];
          return (
            <li key={entry.version} className="changelog__entry">
              <div className="changelog__meta">
                <h2>{entry.version}</h2>
                <time dateTime={entry.date}>{formatDate(entry.date, locale)}</time>
              </div>
              <div className="changelog__body">
                <p className="changelog__summary">{notes.summary}</p>
                {notes.groups.map((group) => (
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
          );
        })}
      </ol>
    </div>
  );
}
