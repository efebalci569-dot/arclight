import { useLanguage } from '../services/language';

export function Privacy() {
  const { t } = useLanguage();
  const text = t.privacy;
  return (
    <section className="section privacy" id="privacy" aria-labelledby="privacy-title">
      <div className="section__head">
        <h2 id="privacy-title">{text.title}</h2>
        <p>{text.intro}</p>
      </div>
      <div className="privacy__cols">
        {text.columns.map((column) => (
          <div key={column.title}>
            <h3>{column.title}</h3>
            <ul className="plain-list">
              {column.items.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </section>
  );
}
