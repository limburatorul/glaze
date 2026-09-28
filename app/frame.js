// Runs on every page Glaze shows (the app injects it on DOMContentLoaded): the window has no caption of
// its own, so this draws minimise / maximise / close over the top right corner. On YouTube the top bar
// is the title bar and drags the window (style.css); anywhere else - Google's sign-in - there is no such
// bar, so a Glaze one is drawn here, with the icon, in the same glass.
(() => {
  if (window.__glazeFrame) return;
  const post = msg => window.chrome.webview.postMessage(msg);
  const youtube = location.hostname === 'www.youtube.com';

  const css = document.createElement('style');
  css.textContent = `
    #yp-caption { position: fixed; top: 0; right: 0; z-index: 2147483647; display: flex; height: ${youtube ? 56 : 40}px;
      -webkit-app-region: no-drag; app-region: no-drag; }
    #yp-caption button { width: 46px; height: 100%; border: 0; padding: 0; background: transparent; color: #fff;
      font: 10px "Segoe Fluent Icons", "Segoe MDL2 Assets"; cursor: default; }
    #yp-caption button:hover { background: rgba(255, 255, 255, 0.1); }
    #yp-caption .yp-caption-close:hover { background: #c42b1c; }
    #yp-caption button:focus-visible { outline: 2px solid #8b5cf6; outline-offset: -2px; }
    #yp-titlebar { position: fixed; top: 0; left: 0; right: 0; z-index: 2147483646; height: 40px; box-sizing: border-box;
      display: flex; align-items: center; gap: 10px; padding: 0 16px; color: #f2f2f5;
      font: 600 13px "Segoe UI Variable", "Segoe UI", sans-serif;
      background: rgba(14, 14, 18, 0.55); backdrop-filter: blur(33.6px) saturate(2) brightness(1.05);
      border-bottom: 1px solid rgba(255, 255, 255, 0.08); -webkit-app-region: drag; app-region: drag; }
    #yp-titlebar img { width: 18px; height: 18px; }
    html.yp-fullscreen #yp-caption, html.yp-fullscreen #yp-titlebar,
    html:has(:fullscreen) #yp-caption, html:has(:fullscreen) #yp-titlebar { display: none; }`;
  document.documentElement.append(css);

  if (!youtube) {
    const bar = document.createElement('div');
    bar.id = 'yp-titlebar';
    const icon = document.createElement('img');
    icon.alt = '';
    icon.src = '%ICON%';
    bar.append(icon, 'Glaze');
    document.documentElement.append(bar);
  }

  const caption = document.createElement('div');
  caption.id = 'yp-caption';
  const button = (action, label, glyph) => {
    const b = document.createElement('button');
    b.className = 'yp-caption-' + action; b.title = label; b.setAttribute('aria-label', label);
    b.textContent = glyph;
    b.onclick = () => post({ type: 'window', action });
    caption.append(b);
    return b;
  };
  button('minimize', 'Minimize', '\uE921');
  const max = button('maximize', 'Maximize', '\uE922');
  button('close', 'Close', '\uE8BB');
  document.documentElement.append(caption);

  window.__glazeFrame = {
    maximized(on) {
      max.textContent = on ? '\uE923' : '\uE922';
      max.title = on ? 'Restore' : 'Maximize';
      max.setAttribute('aria-label', max.title);
    },
    // F11 fills the screen with the window itself; there is no title bar to draw then.
    fullscreen(on) {
      document.documentElement.classList.toggle('yp-fullscreen', on);
    },
  };
})();
