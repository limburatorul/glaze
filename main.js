const { app, BrowserWindow, Menu, ipcMain, nativeTheme, session, shell } = require('electron');
const { autoUpdater } = require('electron-updater');
const fs = require('fs');
const path = require('path');

// Chrome-like UA so YouTube doesn't serve a degraded page.
app.userAgentFallback = app.userAgentFallback.replace(/ (Electron|glaze)\/\S+/gi, '');

const extDir = path.join(__dirname, 'extensions');
const preload = path.join(__dirname, 'preload.js');

// "Info sub player" off → the player fills everything under the top bar.
// YouTube sizes this chain of containers from JS, so every level is forced to 100%.
const fullPlayerCss = `
ytd-watch-flexy #columns { display: none !important; }
ytd-watch-flexy #full-bleed-container { height: calc(100vh - 56px) !important; max-height: none !important; }
ytd-watch-flexy #player-container-outer, ytd-watch-flexy #player-container-inner,
ytd-watch-flexy #player-container, ytd-watch-flexy #player, ytd-watch-flexy #movie_player,
ytd-watch-flexy .html5-video-container { height: 100% !important; max-height: none !important; padding-top: 0 !important; }
ytd-watch-flexy video { width: 100% !important; height: 100% !important; left: 0 !important; top: 0 !important; object-fit: contain !important; }`;

// Blurred copy of the video behind it, filling the letterbox. A 32x18 canvas redrawn at
// 10fps is enough once it's blurred; CSS stretches it (see #yp-ambient in style.css).
const ambientJs = `(() => {
  const c = document.createElement('canvas');
  c.id = 'yp-ambient'; c.width = 32; c.height = 18;
  const ctx = c.getContext('2d');
  setInterval(() => {
    const host = document.querySelector('ytd-watch-flexy #full-bleed-container');
    const v = host && host.querySelector('video');
    if (!v) return;
    if (c.parentNode !== host) host.prepend(c);
    if (c.offsetWidth && v.readyState >= 2) ctx.drawImage(v, 0, 0, c.width, c.height);
  }, 100);
})()`;

// Settings button at the end of YouTube's top bar (which doubles as our title bar).
// Built with DOM calls, not innerHTML: YouTube enforces Trusted Types. Re-added if YouTube re-renders.
const settingsButtonJs = `(() => {
  const add = () => {
    const end = document.querySelector('ytd-masthead #end');
    if (!end || document.getElementById('yp-settings')) return;
    const b = document.createElement('button');
    b.id = 'yp-settings'; b.title = 'Settings'; b.setAttribute('aria-label', 'Settings');
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('viewBox', '0 0 24 24');
    const p = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    p.setAttribute('d', 'M19.14 12.94c.04-.3.06-.61.06-.94s-.02-.64-.07-.94l2.03-1.58a.49.49 0 0 0 .12-.61l-1.92-3.32a.49.49 0 0 0-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54a.48.48 0 0 0-.48-.41h-3.84a.48.48 0 0 0-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96a.49.49 0 0 0-.59.22L2.74 8.87a.48.48 0 0 0 .12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58a.49.49 0 0 0-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32a.48.48 0 0 0-.12-.61l-2.01-1.58zM12 15.6a3.6 3.6 0 1 1 0-7.2 3.6 3.6 0 0 1 0 7.2z');
    svg.append(p); b.append(svg);
    b.onclick = () => window.ytPlayer.openSettings();
    end.append(b);
  };
  add();
  setInterval(add, 1000);
})()`;

// Settings: defaults double as the schema. The YouTube page can reach set-setting through
// the preload too, so every write is checked against it.
const defaults = { related: false, comments: false, info: false, ambient: true, blur: 1, card: 300, onTop: false };
const limits = { blur: [0, 2], card: [180, 600] };

app.whenReady().then(async () => {
  // Installed build only: checks GitHub Releases, downloads in the background, installs on quit.
  // Dev runs aren't packaged and the portable exe can't replace itself (PORTABLE_EXECUTABLE_DIR is set there).
  if (app.isPackaged && !process.env.PORTABLE_EXECUTABLE_DIR) {
    autoUpdater.checkForUpdatesAndNotify().catch(e => console.error('Update check failed:', e.message));
  }

  // Every unpacked extension in ./extensions/<name>/ (must contain manifest.json) is loaded.
  if (fs.existsSync(extDir)) {
    for (const name of fs.readdirSync(extDir)) {
      const dir = path.join(extDir, name);
      if (!fs.existsSync(path.join(dir, 'manifest.json'))) continue;
      try {
        await session.defaultSession.extensions.loadExtension(dir, { allowFileAccess: true });
      } catch (e) {
        console.error(`Extension ${name} failed to load:`, e.message);
      }
    }
  }

  // YouTube follows the OS theme by default → force dark. Cookie "wide=1" = theater mode.
  nativeTheme.themeSource = 'dark';
  await session.defaultSession.cookies.set({ url: 'https://www.youtube.com', name: 'wide', value: '1', expirationDate: 2e9 });

  // No native title bar: YouTube's top bar is the title bar, Windows draws min/max/close over it.
  const win = new BrowserWindow({
    width: 1280, height: 800, backgroundColor: '#0e0e12',
    titleBarStyle: 'hidden',
    titleBarOverlay: { color: '#00000000', symbolColor: '#ffffff', height: 56 },
    webPreferences: { preload },
  });
  Menu.setApplicationMenu(null);

  let css = fs.readFileSync(path.join(__dirname, 'style.css'), 'utf8');
  // Optional wallpaper: background.jpg / .png / .webp next to main.js.
  const bg = ['jpg', 'png', 'webp'].map(e => path.join(__dirname, `background.${e}`)).find(fs.existsSync);
  if (bg) {
    const data = fs.readFileSync(bg).toString('base64');
    css += `html{--wallpaper:url("data:image/${path.extname(bg).slice(1).replace('jpg', 'jpeg')};base64,${data}")}`;
  }

  const settingsFile = path.join(app.getPath('userData'), 'settings.json');
  const settings = { ...defaults, ...(fs.existsSync(settingsFile) ? JSON.parse(fs.readFileSync(settingsFile, 'utf8')) : {}) };
  win.setAlwaysOnTop(settings.onTop);
  const optionsCss = () => [
    !settings.related && '#related { display: none !important; }',
    !settings.comments && 'ytd-comments#comments { display: none !important; }',
    !settings.info && fullPlayerCss,
    settings.ambient && '#yp-ambient { display: block !important; }',
    `html { --glass-blur-scale: ${settings.blur}; --yp-card: ${settings.card}px; }`,
  ].filter(Boolean).join('\n');
  let optionsKey = null;
  const applyOptions = async () => {
    const old = optionsKey;
    optionsKey = await win.webContents.insertCSS(optionsCss());
    if (old) win.webContents.removeInsertedCSS(old);
  };

  ipcMain.handle('get-settings', () => settings);
  ipcMain.on('set-setting', (_e, key, value) => {
    if (!(key in defaults) || typeof value !== typeof defaults[key]) return;
    if (limits[key]) value = Math.min(limits[key][1], Math.max(limits[key][0], value));
    settings[key] = value;
    fs.writeFileSync(settingsFile, JSON.stringify(settings));
    if (key === 'onTop') win.setAlwaysOnTop(value);
    else applyOptions();
  });

  let settingsWin = null;
  const openSettings = () => {
    if (settingsWin) return settingsWin.focus();
    // Acrylic per the Branding note: backgroundMaterial + transparent color, not transparent: true.
    settingsWin = new BrowserWindow({
      parent: win, width: 420, height: 641, useContentSize: true, resizable: false, minimizable: false, maximizable: false,
      title: 'Glaze settings', backgroundMaterial: 'acrylic', backgroundColor: '#00000000',
      webPreferences: { preload },
    });
    settingsWin.loadFile(path.join(__dirname, 'settings.html'));
    // The footer links open in the user's browser / mail client, never inside this window.
    settingsWin.webContents.on('will-navigate', (e, url) => {
      e.preventDefault();
      if (/^(https:\/\/protagonistlabs\.app\/|mailto:feedback@protagonistlabs\.app\?)/.test(url)) shell.openExternal(url);
    });
    settingsWin.on('closed', () => { settingsWin = null; });
  };
  ipcMain.on('open-settings', openSettings);

  win.webContents.on('dom-ready', () => {
    win.webContents.insertCSS(css);
    optionsKey = null;
    applyOptions();
    win.webContents.executeJavaScript(ambientJs);
    win.webContents.executeJavaScript(settingsButtonJs);
  });
  win.loadURL('https://www.youtube.com');

  // Links that try to open a new window stay in-app.
  win.webContents.setWindowOpenHandler(({ url }) => {
    win.loadURL(url);
    return { action: 'deny' };
  });

  const pip = () => win.webContents.executeJavaScript(
    `(() => { const v = document.querySelector('video');
      if (!v) return;
      document.pictureInPictureElement ? document.exitPictureInPicture() : v.requestPictureInPicture(); })()`,
    true // userGesture: required by requestPictureInPicture
  );

  // Shortcuts (there is no menu bar any more).
  const nav = win.webContents.navigationHistory;
  const shortcuts = {
    'Ctrl+P': pip,
    'Ctrl+,': openSettings,
    'Ctrl+T': () => {
      settings.onTop = !settings.onTop;
      fs.writeFileSync(settingsFile, JSON.stringify(settings));
      win.setAlwaysOnTop(settings.onTop);
    },
    'Alt+ArrowLeft': () => nav.goBack(),
    'Alt+ArrowRight': () => nav.goForward(),
    'F5': () => win.webContents.reload(),
    'F11': () => win.setFullScreen(!win.isFullScreen()),
    'Ctrl+Shift+I': () => win.webContents.toggleDevTools(),
  };
  win.webContents.on('before-input-event', (e, input) => {
    if (input.type !== 'keyDown') return;
    const combo = [input.control && 'Ctrl', input.shift && 'Shift', input.alt && 'Alt', input.key.length === 1 ? input.key.toUpperCase() : input.key]
      .filter(Boolean).join('+');
    const action = shortcuts[combo];
    if (action) { e.preventDefault(); action(); }
  });
});

app.on('window-all-closed', () => app.quit());
