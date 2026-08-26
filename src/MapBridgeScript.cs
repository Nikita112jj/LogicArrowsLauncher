namespace LogicArrowsLauncher;

public static class MapBridgeScript
{
    public const string Channel = "logic-arrows-launcher-map-v1";

    public const string Source = """
(() => {
  'use strict';

  const CHANNEL = 'logic-arrows-launcher-map-v1';
  const EXPECTED_VERSION = '1_4';
  const BUTTON_ID = 'logic-arrows-launcher-export-map';
  const MAX_DATA_LENGTH = 2_000_000;

  function post(message) {
    if (globalThis.chrome?.webview?.postMessage) {
      globalThis.chrome.webview.postMessage({ channel: CHANNEL, ...message });
    }
  }

  function findGamePage(namespace) {
    const direct = namespace?.navigation?.gamePage;
    if (direct?.game?.gameMap) return direct;
    const candidates = Object.values(namespace?.navigation || {});
    return candidates.find((candidate) => candidate?.game?.gameMap) || null;
  }

  function getRuntime() {
    const namespace = globalThis.game;
    const gamePage = findGamePage(namespace);
    const game = gamePage?.game;
    const map = game?.gameMap;
    if (!namespace || !game || !map) throw new Error('Редактор карты ещё не готов.');
    if (globalThis.gameVersion !== EXPECTED_VERSION) {
      throw new Error(`Неподдерживаемая версия Logic Arrows: ${String(globalThis.gameVersion || 'unknown')}`);
    }
    return { namespace, gamePage, game, map };
  }

  function exportCurrentMap() {
    const { namespace, gamePage, map } = getRuntime();
    const buffer = namespace.save(map);
    const data = namespace.Utils.arrayBufferToBase64(buffer);
    if (typeof data !== 'string' || data.length === 0 || data.length > MAX_DATA_LENGTH) {
      throw new Error('Экспорт карты пустой или слишком большой.');
    }
    return {
      data,
      mapId: typeof gamePage.mapInfo?.id === 'string' ? gamePage.mapInfo.id : null,
      mapName: typeof gamePage.mapInfo?.name === 'string' ? gamePage.mapInfo.name : null,
      version: globalThis.gameVersion,
    };
  }

  function importMap(payload) {
    const { namespace, game, map } = getRuntime();
    if (!payload || typeof payload.data !== 'string' || payload.data.length === 0 || payload.data.length > MAX_DATA_LENGTH) {
      throw new Error('Данные карты пустые или слишком большие.');
    }
    let bytes;
    try {
      const decoded = globalThis.atob(payload.data);
      bytes = Array.from(decoded, (character) => character.charCodeAt(0));
    } catch {
      throw new Error('Данные карты не являются корректным Base64.');
    }
    if (bytes.length < 4) throw new Error('Экспорт карты слишком короткий.');
    namespace.load(map, bytes);
    game.screenUpdated = true;
    return { ok: true, imported: bytes.length };
  }

  globalThis.__logicArrowsLauncherExport = () => {
    try { return exportCurrentMap(); }
    catch (error) { return { error: String(error?.message || error) }; }
  };

  globalThis.__logicArrowsLauncherImport = (payload) => {
    try { return importMap(payload); }
    catch (error) { return { ok: false, error: String(error?.message || error) }; }
  };

  globalThis.__logicArrowsLauncherNotify = (message, isError) => {
    const button = document.getElementById(BUTTON_ID);
    if (button) button.dataset.busy = '0';
    const status = document.querySelector('.ui-menu-panel .ui-menu-saving');
    if (status && typeof message === 'string') status.textContent = message;
    if (isError) post({ type: 'bridge-error', message: String(message || 'Операция не выполнена') });
  };

  function addExportButton() {
    if (!/^\/map-[^/]+$/.test(globalThis.location.pathname)) return;
    const panel = document.querySelector('.ui-menu-panel');
    const nameInput = panel?.querySelector('.ui-menu-map-name-input');
    if (!panel || !nameInput || document.getElementById(BUTTON_ID)) return;

    const button = document.createElement('div');
    button.id = BUTTON_ID;
    button.className = 'ui-menu-back-button';
    button.setAttribute('role', 'button');
    button.setAttribute('tabindex', '0');
    button.setAttribute('aria-label', 'Экспортировать карту в файл .map');
    button.dataset.busy = '0';
    button.textContent = 'Экспорт .map';
    button.style.position = 'absolute';
    button.style.bottom = '1.5vh';
    button.style.left = '4vw';
    button.style.minWidth = '210px';
    button.style.justifyContent = 'center';
    button.style.fontFamily = 'var(--font)';
    button.style.fontSize = '3.5vmin';
    button.style.color = '#333';
    button.addEventListener('click', () => {
      if (button.dataset.busy === '1') return;
      button.dataset.busy = '1';
      post({ type: 'export-request' });
    });
    button.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        button.click();
      }
    });

    (panel).append(button);
  }

  function startObserver() {
    if (!document.documentElement) {
      globalThis.setTimeout(startObserver, 25);
      return;
    }
    const observer = new MutationObserver(addExportButton);
    observer.observe(document.documentElement, { childList: true, subtree: true });
    globalThis.setInterval(addExportButton, 750);
    addExportButton();
  }

  startObserver();
})();
""";
}
