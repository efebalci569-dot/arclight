import { platforms } from '../services/site';
import type { SupportStatus } from '../types/site';
import { Icon } from './Icon';

const statusText: Record<SupportStatus, string> = {
  supported: 'Detected automatically',
  experimental: 'Experimental',
  manual: 'Add manually',
};

const statusIcon = { supported: 'check', experimental: 'flask', manual: 'hand' } as const;

export function PlatformTable() {
  return (
    <section className="section" id="platforms" aria-labelledby="platforms-title">
      <div className="section__head">
        <h2 id="platforms-title">Every launcher, one library</h2>
        <p>
          Scan for Games reads the install records each launcher already keeps on your PC. It never asks for launcher passwords, never changes
          launcher files and never crawls your whole disk. Here is exactly what works today.
        </p>
      </div>
      <div className="table-wrap">
        <table className="platforms">
          <thead>
            <tr>
              <th scope="col">Platform</th>
              <th scope="col">What Arclight reads</th>
              <th scope="col">How games start</th>
              <th scope="col">Detection</th>
            </tr>
          </thead>
          <tbody>
            {platforms.map((platform) => (
              <tr key={platform.name}>
                <th scope="row">{platform.name}</th>
                <td>{platform.source}</td>
                <td>{platform.launch}</td>
                <td>
                  <span className={`status status--${platform.status}`}>
                    <Icon name={statusIcon[platform.status]} size={15} />
                    {statusText[platform.status]}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="section__footnote">
        Experimental scanners depend on launcher data that isn’t documented and may miss games. Anything Arclight can’t detect can be added
        with Add a Game — pick the .exe or a launcher link.
      </p>
    </section>
  );
}
