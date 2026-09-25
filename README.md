# Glaze

A desktop player for YouTube on Windows. It opens youtube.com in its own window and restyles it: frosted glass over a wallpaper, a player that fills the window with the rest of the page out of the way, and SponsorBlock built in.

![Glaze playing a video, with its settings window open](https://protagonistlabs.app/glaze/img/watch-settings.png)

**Page:** [protagonistlabs.app/glaze](https://protagonistlabs.app/glaze/) · **Download:** [latest release](https://github.com/limburatorul/glaze/releases/latest)

Glaze is not affiliated with, endorsed by or sponsored by YouTube or Google. It shows the real youtube.com, signed in or not, and changes only how it looks on your screen.

## What it does

- **The player fills the window.** Suggestions, comments and the title block are hidden by default, so a video gets everything under the top bar. Each one comes back with a switch in Settings.
- **Blurred video in the margins.** When the window is wider than the video, a blurred copy of the picture fills the sides instead of black bars.
- **Glass.** The top bar, side menu, menus and the player's own controls are frosted glass over a wallpaper, with a strength slider from off to 200%.
- **YouTube's top bar is the title bar.** Drag the window by it; Windows draws minimise, maximise and close over its right end.
- **SponsorBlock** ([ajayyy/SponsorBlock](https://github.com/ajayyy/SponsorBlock), 6.1.7) skips the sponsor segments, intros and reminders its users have marked inside videos.
- **Less clutter.** Shorts shelves, promo banners, ad cards in the feed and the voice search button are hidden. It is not an ad blocker: the ads YouTube plays in videos still play. Dark theme and theater mode are on from the start.
- **Video grid you can size.** A card width slider (180–600 px); the number of columns follows.
- **Always on top**, from Settings or `Ctrl+T`.
- **Updates itself** when installed: it checks this repo's releases at start, downloads in the background and installs when you quit. The portable build doesn't update.

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

Running from source, put a `background.jpg`, `.png` or `.webp` next to `main.js` and Glaze uses it instead of the gradient. The portable exe unpacks itself to a new temporary folder on every start, so there it is always the gradient.

## Install

From the [latest release](https://github.com/limburatorul/glaze/releases/latest), Windows 11, x64 (the settings window uses Windows 11's acrylic):

- `Glaze-Setup-<version>.exe` installs for your user only, with no questions, adds Glaze to the Start menu and keeps itself up to date.
- `Glaze-<version>-portable.exe` runs without installing anything. It doesn't update itself.

Settings are kept in `%APPDATA%\Glaze`.

It is not code-signed, so SmartScreen shows *"Windows protected your PC"* the first time, with the run button behind **More info → Run anyway**.

## Build from source

```
npm install
npm start          # run it
npm run dist       # release\Glaze-Setup-<version>.exe and Glaze-<version>-portable.exe
npm run release    # same, and publishes them to GitHub Releases (needs GH_TOKEN)
```

Electron 38. Any unpacked Chrome extension placed in `extensions\<name>\` (with its `manifest.json`) is loaded at start.

## Licence

GPL-3.0, because it ships SponsorBlock, which is GPL-3.0. SponsorBlock's source for the bundled version is at [github.com/ajayyy/SponsorBlock/tree/6.1.7](https://github.com/ajayyy/SponsorBlock/tree/6.1.7); the third-party notices it carries are in `extensions/SponsorBlock/oss-attribution`.

Made by [Protagonist Labs](https://protagonistlabs.app/).
