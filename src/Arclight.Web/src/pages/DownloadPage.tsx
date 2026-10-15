import { ReleaseCard } from '../components/ReleaseCard';

export function DownloadPage() {
  return (
    <div className="page">
      <header className="page__head">
        <h1>Download Arclight for Windows</h1>
        <p>Free and open source. No account, no sign-in, no ads.</p>
      </header>

      <ReleaseCard />

      <div className="page__grid">
        <section aria-labelledby="req-title">
          <h2 id="req-title">Requirements</h2>
          <ul className="plain-list">
            <li>Windows 10 (version 1809 or later) or Windows 11, 64-bit</li>
            <li>About 200 MB of disk space; the .NET runtime is included</li>
            <li>No administrator rights needed to run Arclight</li>
          </ul>
        </section>

        <section aria-labelledby="install-title">
          <h2 id="install-title">Installing</h2>
          <ol className="steps">
            <li>Download the installer and run it. It installs for your user only.</li>
            <li>
              Arclight isn’t code-signed yet, so Windows SmartScreen may show a warning. Choose More info, then Run anyway — only for files
              downloaded from the official GitHub release.
            </li>
            <li>Open Arclight and press Scan for Games, or add a game yourself.</li>
          </ol>
        </section>

        <section aria-labelledby="portable-title">
          <h2 id="portable-title">Portable version</h2>
          <p>
            Unzip anywhere — a USB drive works — and run Arclight.exe. The portable build keeps its library in a Data folder next to the app
            instead of your user profile.
          </p>
        </section>

        <section aria-labelledby="uninstall-title">
          <h2 id="uninstall-title">Uninstalling</h2>
          <p>
            Remove Arclight from Windows Settings › Apps. Your games are never touched. Your library database stays in
            %LOCALAPPDATA%\Arclight so a reinstall picks up where you left off; delete that folder to remove it too.
          </p>
        </section>
      </div>
    </div>
  );
}
