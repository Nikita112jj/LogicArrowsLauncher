namespace LogicArrowsLauncher;

public static class MapBridgeScript
{
    public const string Channel = "logic-arrows-launcher-map-v1";

    public const string Source = """
(() => {
  'use strict';

  const CHANNEL = 'logic-arrows-launcher-map-v1';
  const EXPECTED_VERSION = '1_4';
  const EXPORT_BUTTON_ID = 'logic-arrows-launcher-export-map';
  const IMPORT_CARD_ID = 'logic-arrows-launcher-import-map';
  const IMPORT_INPUT_ID = 'logic-arrows-launcher-import-map-input';
  const IMPORT_STATUS_ID = 'logic-arrows-launcher-import-map-status';
  const ACTIONS_ID = 'logic-arrows-launcher-map-actions';
  const MAX_DATA_LENGTH = 2_000_000;
  const MAX_FILE_LENGTH = 2_100_000;
  let pendingLobbyImport = null;

  function post(message) {
    if (globalThis.chrome?.webview?.postMessage) {
      globalThis.chrome.webview.postMessage({ channel: CHANNEL, ...message });
    }
  }

  function setImportStatus(message, isError = false) {
    const status = document.getElementById(IMPORT_STATUS_ID);
    if (status && typeof message === 'string') {
      status.textContent = message;
      status.style.color = isError ? '#a33' : '#777';
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

  function stageLobbyImport(payload) {
    if (!payload || typeof payload.data !== 'string' || payload.data.length === 0 || payload.data.length > MAX_DATA_LENGTH) {
      throw new Error('Данные карты пустые или слишком большие.');
    }
    pendingLobbyImport = { data: payload.data };
    setImportStatus('Файл выбран. Открываю новую карту…');
    return { ok: true, staged: true };
  }

  function tryPendingLobbyImport() {
    if (!pendingLobbyImport || !/^\/map-[^/]+$/.test(globalThis.location.pathname)) return;
    try {
      const result = importMap(pendingLobbyImport);
      pendingLobbyImport = null;
      post({ type: 'map-imported', imported: result.imported });
    } catch (error) {
      const message = String(error?.message || error);
      if (message === 'Редактор карты ещё не готов.') return;
      pendingLobbyImport = null;
      post({ type: 'bridge-error', message });
    }
  }

  globalThis.__logicArrowsLauncherExport = () => {
    try { return exportCurrentMap(); }
    catch (error) { return { error: String(error?.message || error) }; }
  };

  globalThis.__logicArrowsLauncherStageLobbyImport = (payload) => {
    try { return stageLobbyImport(payload); }
    catch (error) { return { ok: false, error: String(error?.message || error) }; }
  };

  globalThis.__logicArrowsLauncherOpenNewMap = () => {
    try {
      if (globalThis.location.pathname !== '/maps') throw new Error('Вкладка карт лобби недоступна.');
      const newMap = Array.from(document.querySelectorAll('.maps-page .ui-new-item'))
        .find((element) => element.id !== IMPORT_CARD_ID);
      if (!newMap) throw new Error('Кнопка новой карты ещё не готова.');
      newMap.click();
      return { ok: true };
    } catch (error) {
      return { ok: false, error: String(error?.message || error) };
    }
  };

  globalThis.__logicArrowsLauncherNotify = (message, isError) => {
    const exportButton = document.getElementById(EXPORT_BUTTON_ID);
    if (exportButton) exportButton.dataset.busy = '0';
    const importCard = document.getElementById(IMPORT_CARD_ID);
    if (importCard) importCard.dataset.busy = '0';
    if (typeof message === 'string') {
      setImportStatus(message, Boolean(isError));
      const status = document.querySelector('.ui-menu-panel .ui-menu-saving');
      if (status) status.textContent = message;
    }
    if (isError) post({ type: 'bridge-error', message: String(message || 'Операция не выполнена') });
  };

  function addLobbyImportCard() {
    if (globalThis.location.pathname !== '/maps') return;
    const mapsPage = document.querySelector('.maps-page');
    if (!mapsPage || document.getElementById(IMPORT_CARD_ID)) return;

    const card = document.createElement('div');
    card.id = IMPORT_CARD_ID;
    card.className = 'ui-new-item';
    card.setAttribute('role', 'button');
    card.setAttribute('tabindex', '0');
    card.setAttribute('aria-label', 'Импортировать карту из файла .map');
    card.dataset.busy = '0';
    card.style.flexDirection = 'column';
    card.style.gap = '0.5em';
    card.style.boxSizing = 'border-box';

    const title = document.createElement('div');
    title.className = 'ui-maps-menu-item-name';
    title.textContent = 'Импорт .map';

    const status = document.createElement('div');
    status.id = IMPORT_STATUS_ID;
    status.textContent = 'Выбрать файл';
    status.style.fontFamily = 'var(--font)';
    status.style.fontSize = '0.95em';
    status.style.color = '#777';
    status.style.textAlign = 'center';
    status.style.pointerEvents = 'none';

    const input = document.createElement('input');
    input.id = IMPORT_INPUT_ID;
    input.type = 'file';
    input.accept = '.map,application/json';
    input.hidden = true;

    const chooseFile = () => {
      if (card.dataset.busy === '1') return;
      input.click();
    };
    card.addEventListener('click', chooseFile);
    card.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        chooseFile();
      }
    });
    input.addEventListener('change', async () => {
      const file = input.files?.[0];
      input.value = '';
      if (!file) return;
      card.dataset.busy = '1';
      setImportStatus('Читаю файл…');
      try {
        const text = await file.text();
        if (text.length > MAX_FILE_LENGTH) throw new Error('Файл .map слишком большой.');
        post({ type: 'import-request', text });
      } catch (error) {
        card.dataset.busy = '0';
        setImportStatus(String(error?.message || error), true);
      }
    });

    card.append(title, status);
    mapsPage.append(card, input);
  }

  function addExportButton() {
    if (!/^\/map-[^/]+$/.test(globalThis.location.pathname)) return;
    const panel = document.querySelector('.ui-menu-panel');
    const nameInput = panel?.querySelector('.ui-menu-map-name-input');
    const backButton = panel?.querySelector('.ui-menu-back-button');
    if (!panel || !nameInput || !backButton || document.getElementById(EXPORT_BUTTON_ID)) return;

    const actions = document.createElement('div');
    actions.id = ACTIONS_ID;
    actions.style.position = 'absolute';
    actions.style.left = '1vmin';
    actions.style.right = '1vmin';
    actions.style.bottom = '2vmin';
    actions.style.display = 'flex';
    actions.style.alignItems = 'center';
    actions.style.justifyContent = 'center';
    actions.style.flexWrap = 'wrap';
    actions.style.gap = '1vmin';
    actions.style.boxSizing = 'border-box';

    backButton.style.position = 'static';
    backButton.style.left = 'auto';
    backButton.style.bottom = 'auto';
    backButton.style.margin = '0';

    const button = document.createElement('div');
    button.id = EXPORT_BUTTON_ID;
    button.className = 'ui-menu-back-button';
    button.setAttribute('role', 'button');
    button.setAttribute('tabindex', '0');
    button.setAttribute('aria-label', 'Экспортировать карту в файл .map');
    button.dataset.busy = '0';
    button.textContent = 'Экспорт .map';
    button.style.position = 'static';
    button.style.left = 'auto';
    button.style.bottom = 'auto';
    button.style.margin = '0';
    button.style.minWidth = '0';
    button.style.whiteSpace = 'nowrap';
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

    actions.append(backButton, button);
    panel.append(actions);
  }

  function syncUi() {
    addLobbyImportCard();
    addExportButton();
    tryPendingLobbyImport();
  }

  function startObserver() {
    if (!document.documentElement) {
      globalThis.setTimeout(startObserver, 25);
      return;
    }
    const observer = new MutationObserver(syncUi);
    observer.observe(document.documentElement, { childList: true, subtree: true });
    globalThis.setInterval(syncUi, 500);
    syncUi();
  }

  startObserver();
})();
""";
}
