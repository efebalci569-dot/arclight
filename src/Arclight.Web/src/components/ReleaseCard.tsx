import { findInstaller, findPortable, formatDate, formatSize, useLatestRelease } from '../services/releases';
import { repository } from '../services/site';
import { Icon } from './Icon';

/**
 * Shows the real latest release from GitHub. Until one exists, it says so plainly and points to
 * building from source — there is never a download button without a file behind it.
 */
export function ReleaseCard() {
  const state = useLatestRelease();

  if (state.kind === 'loading') {
    return (
      <div className="release release--loading" aria-busy="true">
        <p>Checking GitHub for the latest release…</p>
      </div>
    );
  }

  if (state.kind === 'available') {
    const { release } = state;
    const installer = findInstaller(release);
    const portable = findPortable(release);
    return (
      <div className="release">
        <div className="release__head">
          <h2>Arclight {release.tag.replace(/^v/i, '')}</h2>
          <p>Released {formatDate(release.publishedAt)} for Windows 10 and 11 (64-bit).</p>
        </div>
        <div className="release__actions">
          {installer && (
            <a className="button button--primary button--large" href={installer.url}>
              <Icon name="windows" size={18} />
              Download installer ({formatSize(installer.size)})
            </a>
          )}
          {portable && (
            <a className="button button--ghost button--large" href={portable.url}>
              Portable .zip ({formatSize(portable.size)})
            </a>
          )}
        </div>
        <p className="release__links">
          <a href={release.url} rel="noreferrer" target="_blank">
            Release page and checksums
          </a>
          <a href="#/changelog">Release notes</a>
        </p>
      </div>
    );
  }

  return (
    <div className="release release--empty">
      <div className="release__head">
        <h2>{state.kind === 'none' ? 'No release has been published yet' : 'GitHub could not be reached'}</h2>
        <p>
          {state.kind === 'none'
            ? 'The first installer will appear here as soon as it is published on GitHub. Until then you can build Arclight from source in a few minutes.'
            : 'The latest release couldn’t be loaded right now. You can find every published build on the GitHub releases page.'}
        </p>
      </div>
      <div className="release__actions">
        <a className="button button--primary button--large" href={repository.releasesUrl} rel="noreferrer" target="_blank">
          <Icon name="github" size={18} />
          Open GitHub releases
        </a>
        <a className="button button--ghost button--large" href={repository.url} rel="noreferrer" target="_blank">
          View source
        </a>
      </div>
      <pre className="code" aria-label="Build from source">
        <code>{`git clone ${repository.url}.git
cd arclight
dotnet run --project src/Arclight.Desktop`}</code>
      </pre>
      <p className="release__hint">Requires the .NET 10 SDK on Windows.</p>
    </div>
  );
}
