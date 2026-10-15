# Releasing

1. Update `<Version>` in `Directory.Build.props`, `version` in `src/Arclight.Web/package.json`, the changelog entry in `src/Arclight.Web/src/services/site.ts` and `CHANGELOG.md`.
2. Run the tests and a local release build:

   ```bash
   dotnet test
   powershell -ExecutionPolicy Bypass -File scripts/publish.ps1
   ```

3. Install the generated `Setup.exe` on a clean Windows 10 or 11 machine (or VM): add a game, scan, launch, play for a few minutes, close and reopen Arclight, then uninstall and confirm the games are untouched.
4. Commit, then tag and push:

   ```bash
   git tag v0.1.0
   git push origin v0.1.0
   ```

   The **Release** workflow builds the self-contained exe, the portable zip and the installer (Inno Setup), writes `SHA256SUMS.txt`, and creates a GitHub release using the matching `CHANGELOG.md` section as release notes.

5. The website (GitHub Pages) picks up the new release automatically: the download page reads the latest release from the GitHub API.

## Code signing

Builds are **not** code-signed. Windows SmartScreen may warn on first run; the website and release notes say so. If a signing certificate becomes available, sign `Arclight.exe` and the installer in the Release workflow before uploading, and only then describe the builds as signed.

## GitHub Pages

Enable Pages for the repository with **Source: GitHub Actions**. The **Website** workflow builds `src/Arclight.Web` with `SITE_BASE=/<repo>/` and deploys it on every push to `main` that touches the site.
