// Runs in youtube.com once per document (the app injects it on DOMContentLoaded).
// Everything is built with DOM calls, not innerHTML: YouTube enforces Trusted Types.
(() => {
  if (window.__glaze) return;
  const post = msg => window.chrome.webview.postMessage(msg);

  // Two style elements: the fixed look (style.css) and the part the settings rewrite.
  const style = id => {
    const s = document.createElement('style');
    s.id = id;
    document.documentElement.append(s);
    return s;
  };
  const base = style('glaze-base'), options = style('glaze-options');

  // Blurred copy of the video behind it, filling the letterbox. A 32x18 canvas redrawn at
  // 10fps is enough once it's blurred; CSS stretches it (see #yp-ambient in style.css).
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

  const svg = d => {
    const s = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    s.setAttribute('viewBox', '0 0 24 24');
    const p = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    p.setAttribute('d', d);
    s.append(p);
    return s;
  };

  // Settings button at the end of YouTube's top bar. Re-added if YouTube re-renders it.
  const gear = 'M19.14 12.94c.04-.3.06-.61.06-.94s-.02-.64-.07-.94l2.03-1.58a.49.49 0 0 0 .12-.61l-1.92-3.32a.49.49 0 0 0-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54a.48.48 0 0 0-.48-.41h-3.84a.48.48 0 0 0-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96a.49.49 0 0 0-.59.22L2.74 8.87a.48.48 0 0 0 .12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58a.49.49 0 0 0-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32a.48.48 0 0 0-.12-.61l-2.01-1.58zM12 15.6a3.6 3.6 0 1 1 0-7.2 3.6 3.6 0 0 1 0 7.2z';
  const addGear = () => {
    const end = document.querySelector('ytd-masthead #end');
    if (!end || document.getElementById('yp-settings')) return;
    const b = document.createElement('button');
    b.id = 'yp-settings'; b.title = 'Settings'; b.setAttribute('aria-label', 'Settings');
    b.append(svg(gear));
    b.onclick = () => post({ type: 'openSettings' });
    end.append(b);
  };
  addGear();
  setInterval(addGear, 1000);

  // Minimise / maximise / close. The window has no title bar of its own: YouTube's top bar is it,
  // dragged through app-region (style.css), and these sit over its right end like Windows' own.
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

  // Ctrl+P is caught here rather than by the app: picture-in-picture needs a real key press.
  document.addEventListener('keydown', e => {
    if (!e.ctrlKey || e.shiftKey || e.altKey || e.key.toLowerCase() !== 'p') return;
    e.preventDefault();
    const v = document.querySelector('video');
    if (!v) return;
    document.pictureInPictureElement ? document.exitPictureInPicture() : v.requestPictureInPicture();
  }, true);

  window.__glaze = {
    css(baseCss, optionsCss) {
      if (baseCss !== null) base.textContent = baseCss;
      options.textContent = optionsCss;
    },
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
