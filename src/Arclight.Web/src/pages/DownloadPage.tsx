import { ReleaseCard } from '../components/ReleaseCard';
import { useLanguage } from '../services/language';

export function DownloadPage() {
  const { t } = useLanguage();
  const text = t.download;
  return (
    <div className="page">
      <header className="page__head">
        <h1>{text.title}</h1>
        <p>{text.lead}</p>
      </header>

      <ReleaseCard />

      <div className="page__grid">
        <section aria-labelledby="req-title">
          <h2 id="req-title">{text.requirements.title}</h2>
          <ul className="plain-list">
            {text.requirements.items.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </section>

        <section aria-labelledby="install-title">
          <h2 id="install-title">{text.installing.title}</h2>
          <ol className="steps">
            {text.installing.steps.map((step) => (
              <li key={step}>{step}</li>
            ))}
          </ol>
        </section>

        <section aria-labelledby="portable-title">
          <h2 id="portable-title">{text.portable.title}</h2>
          <p>{text.portable.body}</p>
        </section>

        <section aria-labelledby="uninstall-title">
          <h2 id="uninstall-title">{text.uninstalling.title}</h2>
          <p>{text.uninstalling.body}</p>
        </section>
      </div>
    </div>
  );
}
