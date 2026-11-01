# Exception Uploader

A small tray app for Windows and Mac that sends your leveling journal from the Exception WoW addon to
the Exception guild portal when you log out. WoW addons can't reach the internet, and the game writes
an addon's saved data only when you log out or `/reload`, so a program outside the game has to send it.

The source is public so anyone can check what it does. Every release is built from this repository by
GitHub Actions, and each download carries a signed build attestation that ties it to the exact commit.

## What it does, and what it doesn't

- It reads only `WTF/Account/<account>/<realm>/<character>/SavedVariables/Exception.lua` under the World
  of Warcraft folder you choose. See [`WowFolder.cs`](src/Exception.Uploader.Core/WowFolder.cs).
- From those files it takes only the journal string the addon stored for upload (`EXJ1:...`), found by
  one regular expression in [`SavedVariables.cs`](src/Exception.Uploader.Core/SavedVariables.cs).
- It sends that string, and nothing else, to the portal with your upload token. See
  [`PortalClient.cs`](src/Exception.Uploader.Core/PortalClient.cs).
- It never touches the game, its process or any other file. It doesn't read your WoW account or password.
- The upload token can only upload your own characters' journals. You make it on your Profile page on the
  portal, and revoke it there at any time.

## How it behaves

- Checks every 15 seconds, and sooner when a file changes. A file must be quiet for 5 seconds first,
  because the game writes it in pieces. A journal it already sent isn't sent again.
- Retries network and server errors with growing delays. If the portal refuses an export, it waits for
  the file to change. If the token is refused, it stops until you paste a new one.
- Uploading doesn't need the window. A Mac can't open the window while its display is asleep, so the app
  carries on without one and restarts itself every 5 minutes until it can.
- One copy runs per user. Opening it again shows the window of the copy already running.
- On a Mac it only starts at login when it runs from Applications.

## Files it keeps

In your app data folder, under `Exception Uploader` (`~/Library/Application Support` or `%APPDATA%`):

| File | Holds |
|---|---|
| `token.dat` | Your upload token. Only your user can read it, and on Windows it's encrypted with DPAPI. |
| `settings.json` | Your WoW folder and whether to start at login |
| `history.json` | Per character: a hash of the last upload, when, and the last message |
| `uploader.log` | Character names and outcomes. Never the token or the journal. |

## Check a download

Each release lists the commit it was built from. With the [GitHub CLI](https://cli.github.com):

```bash
gh attestation verify Exception-Uploader-Setup.exe --repo hkb-mats/exception-uploader
```

The downloads aren't code-signed yet, so Windows and macOS warn the first time you open them. On Windows
choose More info, then Run anyway. On a Mac open System Settings, then Privacy & Security, and choose
Open Anyway.

## Build it yourself

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet test tests/Exception.Uploader.Tests
dotnet run --project src/Exception.Uploader
```

To try it against a local copy of the portal without touching a real setup or adding a login item, set
`EXCEPTION_UPLOADER_DATA` to a test folder and `EXCEPTION_UPLOADER_API` to the local API's address.

Releases: push a tag like `v1.1.0`. [`release.yml`](.github/workflows/release.yml) tests, builds
self-contained single files for `win-x64`, `osx-arm64` and `osx-x64`, makes the installers (Inno Setup
for Windows, a disk image for Mac), attests them, and publishes the GitHub Release with `uploader.json`,
which installed copies read once a day to offer an update. They don't update themselves.

## License

[MIT](LICENSE)
