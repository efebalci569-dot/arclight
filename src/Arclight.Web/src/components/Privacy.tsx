export function Privacy() {
  return (
    <section className="section privacy" id="privacy" aria-labelledby="privacy-title">
      <div className="section__head">
        <h2 id="privacy-title">Your library stays on your PC</h2>
        <p>
          Arclight is local-first. There is no Arclight account and no Arclight server: your games, notes, tags, collections and playtime
          live in a local database that you can export to JSON or delete whenever you want.
        </p>
      </div>
      <div className="privacy__cols">
        <div>
          <h3>Stored only on this PC</h3>
          <ul className="plain-list">
            <li>Your game list, artwork cache and personal notes</li>
            <li>Play sessions and statistics</li>
            <li>Settings and application logs (kept for 14 days)</li>
          </ul>
        </div>
        <div>
          <h3>The only times Arclight goes online</h3>
          <ul className="plain-list">
            <li>Downloading missing cover art for Steam games from Steam’s public image server. You can turn this off.</li>
            <li>Checking GitHub for a new version — only when you press Check for updates, or turn on the startup check.</li>
          </ul>
        </div>
        <div>
          <h3>What the tracker looks at</h3>
          <ul className="plain-list">
            <li>The names and file paths of running programs, to see whether one of your games is running.</li>
            <li>Never keystrokes, screen contents, window titles or anything about other programs.</li>
          </ul>
        </div>
      </div>
    </section>
  );
}
