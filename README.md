# Glaze

A desktop player for YouTube on Windows. It opens youtube.com in its own window and restyles it: frosted glass over a wallpaper, a player that fills the window with the rest of the page out of the way, and SponsorBlock built in.

![Glaze playing a video, with its settings window open](https://protagonistlabs.app/glaze/img/watch-settings.png)

**Page:** [protagonistlabs.app/glaze](https://protagonistlabs.app/glaze/) · **Download:** [latest release](https://github.com/limburatorul/glaze/releases/latest)

Glaze is not affiliated with, endorsed by or sponsored by YouTube or Google. It shows the real youtube.com, signed in or not, and changes only how it looks on your screen.

## What it does

- **The player fills the window.** Suggestions, comments and the title block are hidden by default, so a video gets everything under the top bar. Each one comes back with a switch in Settings.
- **Blurred video in the margins.** When the window is wider than the video, a blurred copy of the picture fills the sides instead of black bars.
- **Glass.** The top bar, side menu, menus and the player's own controls are frosted glass over a wallpaper, with a strength slider from off to 200%.
- **YouTube's top bar is the title bar.** Drag the window by it; minimise, maximise and close sit at its right end.
- **SponsorBlock** ([ajayyy/SponsorBlock](https://github.com/ajayyy/SponsorBlock), 6.1.7) skips the sponsor segments, intros and reminders its users have marked inside videos.
- **Less clutter.** Shorts shelves, promo banners, ad cards in the feed and the voice search button are hidden. It is not an ad blocker: the ads YouTube plays in videos still play. Dark theme and theater mode are on from the start.
- **Video grid you can size.** A card width slider (180–600 px); the number of columns follows.
- **Always on top**, from Settings or `Ctrl+T`.
- **Signing in to Google works.** Glaze runs on Microsoft's WebView2 (the Edge engine built into Windows 11), which Google accepts for sign-in. Glaze 1.x ran on Electron, where Google refused it as "not a secure browser".
- **Updates itself**, installed or portable: it checks this repo's releases shortly after it starts and every 10 minutes, and asks before it updates. The download is checked against the size and SHA-256 GitHub publishes for it, then Glaze restarts on the new version.

## Shortcuts

| Keys | Does |
| --- | --- |
| `Ctrl+P` | Picture-in-picture |
| `Ctrl+,` | Settings |
| `Ctrl+T` | Always on top |
| `Alt+←` / `Alt+→` | Back / forward |
| `F5` | Reload |
| `F11` | Full screen |

## Your own wallpaper

Glaze comes with a wallpaper. Put a `background.jpg`, `.png` or `.webp` next to `Glaze.exe` and it uses that instead.

## Install

From the [latest release](https://github.com/limburatorul/glaze/releases/latest), Windows 11, x64 (the settings window uses Windows 11's acrylic):

- `Glaze-Setup-<version>.exe` installs for your user only, with no admin prompt, and adds Glaze to the Start menu. Installed over Glaze 1.x, it removes the old version first; your settings stay, but you sign in again once.
- `Glaze-<version>-portable.exe` runs without installing anything.

Both update themselves. Settings are kept in `%APPDATA%\Glaze`; the sign-in and the page files in `%LOCALAPPDATA%\Glaze`.

Glaze needs the Microsoft Edge WebView2 Runtime, which Windows 11 already has; if it is missing, Glaze says so and links to it.

It is not code-signed, so SmartScreen shows *"Windows protected your PC"* the first time, with the run button behind **More info → Run anyway**.

## Build from source

.NET 10 SDK and [Inno Setup 6](https://jrsoftware.org/isinfo.php):

```
dotnet run --project app                        # run it
powershell -File installer\build.ps1            # dist\Glaze-Setup-<version>.exe and Glaze-<version>-portable.exe
```

The version is `<Version>` in `app/Glaze.csproj`, and nowhere else. A release is `gh release create v<version>` with the three files from `dist\`: installed copies update from the `Glaze-Setup-` installer, portable ones from the `-portable.exe`, and copies of Glaze 1.x from `latest.yml`.

C#, WinForms and WebView2. `style.css`, `settings.html` and `app/page.js` are what Glaze puts into YouTube and its settings window; they and every unpacked extension in `extensions\<name>\` (with its `manifest.json`) are built into the exe.

## Licence

GPL-3.0, because it ships SponsorBlock, which is GPL-3.0. SponsorBlock's source for the bundled version is at [github.com/ajayyy/SponsorBlock/tree/6.1.7](https://github.com/ajayyy/SponsorBlock/tree/6.1.7); the third-party notices it carries are in `extensions/SponsorBlock/oss-attribution`.

Made by [Protagonist Labs](https://protagonistlabs.app/).
