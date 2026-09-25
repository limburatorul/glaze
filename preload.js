// Shared by the YouTube window (settings button) and the settings window.
// main.js validates every set-setting call, since YouTube's page can reach this too.
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('ytPlayer', {
  openSettings: () => ipcRenderer.send('open-settings'),
  getSettings: () => ipcRenderer.invoke('get-settings'),
  setSetting: (key, value) => ipcRenderer.send('set-setting', key, value),
});
