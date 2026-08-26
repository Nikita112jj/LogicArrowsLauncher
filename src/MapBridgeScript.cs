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
  const THEME_STYLE_ID = 'logic-arrows-launcher-theme-style';
  const THEME_HEADING_ID = 'logic-arrows-launcher-settings-heading';
  const THEME_ROW_ID = 'logic-arrows-launcher-theme-row';
  const THEME_SELECT_ID = 'logic-arrows-launcher-theme-select';
  const THEME_STORAGE_KEY = 'logic-arrows-theme';
  const PATCHED_GAME_KEY = '__logicArrowsLauncherAdaptiveUpdate';
  const UPDATE_COUNTS = [1, 1, 1, 5, 20, 100];
  const SKIP_COUNTS = [20, 5, 1, 1, 1, 1];
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

  function patchGamePerformance() {
    const gamePage = findGamePage(globalThis.game);
    const game = gamePage?.game;
    if (!game || typeof game.updateFrame !== 'function' || game[PATCHED_GAME_KEY]) return;

    const originalUpdateFrame = game.updateFrame;
    game.updateFrame = function adaptiveUpdateFrame(callback = () => {}) {
      const level = Math.max(0, Math.min(UPDATE_COUNTS.length - 1, Number(this.updateSpeedLevel) || 0));
      if (level < 3 || !this.playing || this.frame % SKIP_COUNTS[level] !== 0) {
        return originalUpdateFrame.call(this, callback);
      }

      const targetTicks = UPDATE_COUNTS[level];
      const budgetMs = level >= 5 ? 8 : level >= 4 ? 9 : 10;
      const startedAt = globalThis.performance?.now?.() ?? Date.now();
      let completed = 0;
      while (completed < targetTicks) {
        this.updateTick(callback);
        this.updatesPerSecond++;
        completed++;
        const elapsed = (globalThis.performance?.now?.() ?? Date.now()) - startedAt;
        if (completed > 0 && elapsed >= budgetMs) break;
      }

      const now = globalThis.performance?.now?.() ?? Date.now();
      if (now - this.updateTime > 1000) {
        this.updateTime = now;
        this.tps = this.updatesPerSecond;
        this.updatesPerSecond = 0;
        this.onFPSUpdate();
      }
    };

    const originalDraw = game.draw;
    if (typeof originalDraw === 'function') {
      game.draw = function adaptiveDraw() {
        const level = Math.max(0, Math.min(UPDATE_COUNTS.length - 1, Number(this.updateSpeedLevel) || 0));
        if (level < 3) return originalDraw.call(this);
        const previousLevel = this.updateSpeedLevel;
        this.updateSpeedLevel = 0;
        try {
          return originalDraw.call(this);
        } finally {
          this.updateSpeedLevel = previousLevel;
        }
      };
    }
    game[PATCHED_GAME_KEY] = true;
  }

  const THEME_CSS = `;
    :root {
      --logic-background: #f7f8fb;
      --logic-surface: #ffffff;
      --logic-surface-strong: #eef2f8;
      --logic-ink: #202838;
      --logic-muted: #6d7584;
      --logic-border: rgba(32, 40, 56, 0.12);
    }
    html[data-logic-arrows-theme='dark'] {
      --logic-background: #111722;
      --logic-surface: #1d2636;
      --logic-surface-strong: #263247;
      --logic-ink: #edf2ff;
      --logic-muted: #aab4c7;
      --logic-border: rgba(237, 242, 255, 0.16);
      --blue: #345a9f;
      --dark-blue: #243f78;
      --accent-color: #80a8ff;
      color-scheme: dark;
    }
    @media (prefers-color-scheme: dark) {
      html:not([data-logic-arrows-theme]) {
        --logic-background: #111722;
        --logic-surface: #1d2636;
        --logic-surface-strong: #263247;
        --logic-ink: #edf2ff;
        --logic-muted: #aab4c7;
        --logic-border: rgba(237, 242, 255, 0.16);
        --blue: #345a9f;
        --dark-blue: #243f78;
        --accent-color: #80a8ff;
        color-scheme: dark;
      }
    }
    html[data-logic-arrows-theme='light'] {
      color-scheme: light;
    }
    body,
    #menu-page-main-div {
      background-color: var(--logic-background);
      color: var(--logic-ink);
      transition: background-color 180ms ease, color 180ms ease;
    }
    #logic-arrows-launcher-settings-heading {
      margin: clamp(24px, 5vh, 56px) 0 0 clamp(24px, 10vw, 140px);
      font-family: var(--font);
      color: var(--logic-ink);
      font-size: clamp(1.7rem, 3.2vw, 2.65rem);
      font-weight: 800;
      letter-spacing: -0.02em;
    }
    #logic-arrows-launcher-settings-subtitle {
      margin: 0 0 0 clamp(24px, 10vw, 140px);
      font-family: var(--font);
      color: var(--logic-muted);
      font-size: 0.98rem;
    }
    .settings-table {
      margin: 1.5rem 0 0 clamp(24px, 10vw, 140px);
      border-collapse: separate;
      border-spacing: 0 0.65rem;
      font-family: var(--font);
      font-size: 1.08rem;
      color: var(--logic-ink);
    }
    .settings-table td {
      background: var(--logic-surface);
      border-top: 1px solid var(--logic-border);
      border-bottom: 1px solid var(--logic-border);
    }
    .settings-table td:first-child {
      border-left: 1px solid var(--logic-border);
      border-radius: 0.8rem 0 0 0.8rem;
      padding: 0.85rem 0 0.85rem 1rem;
    }
    .settings-table td:last-child {
      border-right: 1px solid var(--logic-border);
      border-radius: 0 0.8rem 0.8rem 0;
      padding: 0.85rem 1rem;
    }
    .settings-table .setting-name {
      min-width: 15rem;
      padding-right: 2.5rem;
      font-weight: 700;
    }
    .settings-table .setting-value select,
    .settings-table .setting-value input[type='range'] {
      accent-color: var(--accent-color);
    }
    .settings-table .setting-value select {
      min-width: 12rem;
      padding: 0.45rem 2.2rem 0.45rem 0.7rem;
      border: 1px solid var(--logic-border);
      border-radius: 0.55rem;
      background: var(--logic-surface-strong);
      color: var(--logic-ink);
      font: inherit;
      cursor: pointer;
    }
    .settings-table .setting-value select:focus-visible {
      outline: 2px solid var(--accent-color);
      outline-offset: 2px;
    }
    .settings-table hr {
      border: 0;
      border-top: 1px solid var(--logic-border);
    }
    .settings-table .setting-divider {
      background: transparent;
      border: 0;
      padding: 0.25rem 0;
    }
    .logout-button {
      border: 1px solid rgba(255, 255, 255, 0.12);
      box-shadow: 0 0.35rem 1rem rgba(33, 63, 133, 0.18);
    }
    html[data-logic-arrows-theme='dark'] .ui-saved-item,
    html[data-logic-arrows-theme='dark'] .ui-new-item,
    html[data-logic-arrows-theme='dark'] .ui-menu-panel {
      background-color: var(--logic-surface);
      color: var(--logic-ink);
    }
    html[data-logic-arrows-theme='dark'] .ui-saved-item-tags,
    html[data-logic-arrows-theme='dark'] .ui-menu-saving,
    html[data-logic-arrows-theme='dark'] .ui-menu-back-text {
      color: var(--logic-muted);
    }
    html[data-logic-arrows-theme='dark'] .ui-menu-map-name-input {
      color: var(--logic-ink);
      border-bottom-color: var(--logic-muted);
    }
    #logic-arrows-launcher-theme-row td {
      background: var(--logic-surface-strong);
    }
    @media (max-width: 700px) {
      #logic-arrows-launcher-settings-heading,
      #logic-arrows-launcher-settings-subtitle,
      .settings-table {
        margin-left: 5vw;
      }
      .settings-table .setting-name {
        min-width: 0;
        padding-right: 1rem;
      }
      .settings-table .setting-value select {
        min-width: 9rem;
      }
    }
  `;

  function ensureThemeStyle() {
    let style = document.getElementById(THEME_STYLE_ID);
    if (!style) {
      style = document.createElement('style');
      style.id = THEME_STYLE_ID;
      document.head?.append(style);
    }
    if (style.textContent !== THEME_CSS) style.textContent = THEME_CSS;
  }

  function readTheme() {
    const value = globalThis.localStorage?.getItem(THEME_STORAGE_KEY);
    return value === 'dark' || value === 'light' || value === 'system' ? value : 'system';
  }

  function applyTheme(value = readTheme()) {
    const theme = value === 'dark' || value === 'light' || value === 'system' ? value : 'system';
    if (theme === 'system') document.documentElement.removeAttribute('data-logic-arrows-theme');
    else document.documentElement.setAttribute('data-logic-arrows-theme', theme);
  }

  function ensureSettingsTheme() {
    if (globalThis.location.pathname !== '/settings') return;
    const main = document.querySelector('.settings-page');
    const table = main?.querySelector('.settings-table');
    if (!main || !table) return;

    if (!document.getElementById(THEME_HEADING_ID)) {
      const heading = document.createElement('div');
      heading.id = THEME_HEADING_ID;
      heading.textContent = 'Настройки';
      const subtitle = document.createElement('div');
      subtitle.id = 'logic-arrows-launcher-settings-subtitle';
      subtitle.textContent = 'Внешний вид применяется сразу. Симуляция больших схем сама бережёт FPS.';
      main.insertBefore(heading, table);
      main.insertBefore(subtitle, table);
    }

    if (document.getElementById(THEME_ROW_ID)) return;
    const row = document.createElement('tr');
    row.id = THEME_ROW_ID;
    const label = document.createElement('td');
    label.className = 'setting-name';
    label.textContent = 'Тема:';
    const value = document.createElement('td');
    value.className = 'setting-value';
    const select = document.createElement('select');
    select.id = THEME_SELECT_ID;
    select.setAttribute('aria-label', 'Тема интерфейса');
    [['system', 'Системная'], ['dark', 'Тёмная'], ['light', 'Светлая']].forEach(([optionValue, optionText]) => {
      const option = document.createElement('option');
      option.value = optionValue;
      option.textContent = optionText;
      select.append(option);
    });
    select.value = readTheme();
    select.addEventListener('change', () => {
      const next = select.value;
      if (next !== 'dark' && next !== 'light' && next !== 'system') return;
      localStorage.setItem(THEME_STORAGE_KEY, next);
      applyTheme(next);
    });
    value.append(select);
    row.append(label, value);
    const interfaceSelect = table.querySelector('.interface-mode-select');
    const interfaceRow = interfaceSelect?.closest('tr');
    if (interfaceRow) interfaceRow.after(row);
    else table.append(row);
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
    ensureThemeStyle();
    applyTheme();
    patchGamePerformance();
    ensureSettingsTheme();
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
