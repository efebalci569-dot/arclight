import { useLanguage } from '../services/language';
import { platforms } from '../services/site';
import { Icon } from './Icon';

const statusIcon = { supported: 'check', experimental: 'flask', manual: 'hand' } as const;

export function PlatformTable() {
  const { t } = useLanguage();
  const text = t.platforms;
  return (
    <section className="section" id="platforms" aria-labelledby="platforms-title">
      <div className="section__head">
        <h2 id="platforms-title">{text.title}</h2>
        <p>{text.intro}</p>
      </div>
      <div className="table-wrap">
        <table className="platforms">
          <thead>
            <tr>
              <th scope="col">{text.columns.platform}</th>
              <th scope="col">{text.columns.source}</th>
              <th scope="col">{text.columns.launch}</th>
              <th scope="col">{text.columns.status}</th>
            </tr>
          </thead>
          <tbody>
            {platforms.map((platform) => (
              <tr key={platform.id}>
                <th scope="row">{text.rows[platform.id].name}</th>
                <td>{text.rows[platform.id].source}</td>
                <td>{text.rows[platform.id].launch}</td>
                <td>
                  <span className={`status status--${platform.status}`}>
                    <Icon name={statusIcon[platform.status]} size={15} />
                    {text.status[platform.status]}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="section__footnote">{text.footnote}</p>
    </section>
  );
}
