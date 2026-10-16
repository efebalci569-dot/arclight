import { useLanguage } from '../services/language';
import { findInstaller, findPortable, formatDate, formatSize, useLatestRelease } from '../services/releases';
import { repository } from '../services/site';
import { Icon } from './Icon';

/**
 * Shows the real latest release from GitHub. Until one exists, it says so plainly and points to
 * building from source — there is never a download button without a file behind it.
 */
export function ReleaseCard() {
  const state = useLatestRelease();
  const { t, locale } = useLanguage();
  const text = t.release;

  if (state.kind === 'loading') {
    return (
      <div className="release release--loading" aria-busy="true">
        <p>{text.loading}</p>
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
          <p>{text.released(formatDate(release.publishedAt, locale))}</p>
        </div>
        <div className="release__actions">
          {installer && (
            <a className="button button--primary button--large" href={installer.url}>
              <Icon name="windows" size={18} />
              {text.installer(formatSize(installer.size))}
            </a>
          )}
          {portable && (
            <a className="button button--ghost button--large" href={portable.url}>
              {text.portable(formatSize(portable.size))}
            </a>
          )}
        </div>
        <p className="release__links">
          <a href={release.url} rel="noreferrer" target="_blank">
            {text.releasePage}
          </a>
          <a href="#/changelog">{t.common.releaseNotes}</a>
        </p>
      </div>
    );
  }

  return (
    <div className="release release--empty">
      <div className="release__head">
        <h2>{state.kind === 'none' ? text.noneTitle : text.errorTitle}</h2>
        <p>{state.kind === 'none' ? text.noneBody : text.errorBody}</p>
      </div>
      <div className="release__actions">
        <a className="button button--primary button--large" href={repository.releasesUrl} rel="noreferrer" target="_blank">
          <Icon name="github" size={18} />
          {text.openReleases}
        </a>
        <a className="button button--ghost button--large" href={repository.url} rel="noreferrer" target="_blank">
          {text.viewSource}
        </a>
      </div>
      <pre className="code" aria-label={text.buildLabel}>
        <code>{`git clone ${repository.url}.git
cd arclight
dotnet run --project src/Arclight.Desktop`}</code>
      </pre>
      <p className="release__hint">{text.buildHint}</p>
    </div>
  );
}
