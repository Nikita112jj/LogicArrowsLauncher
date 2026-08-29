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
  const PATCHED_DARK_RENDER_KEY = '__logicArrowsLauncherDarkRenderClear';
  const PATCHED_DARK_ARROW_KEY = '__logicArrowsLauncherDarkArrowCell';
  const PATCHED_DARK_GRID_TILE_KEY = '__logicArrowsLauncherDarkGridTile';
  const PATCHED_FOCUS_RECOVERY_KEY = '__logicArrowsLauncherFocusRecovery';
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
      status.dataset.error = isError ? '1' : '0';
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
      --logic-game-panel: #eef2f8;
      --logic-game-panel-strong: #ffffff;
      --logic-game-ink: #202838;
      --logic-game-muted: #596477;
    }
    html[data-logic-arrows-theme='dark'] {
      --logic-background: #111722;
      --logic-surface: #1d2636;
      --logic-surface-strong: #263247;
      --logic-ink: #edf2ff;
      --logic-muted: #aab4c7;
      --logic-border: rgba(237, 242, 255, 0.16);
      --logic-game-panel: #182131;
      --logic-game-panel-strong: #222f45;
      --logic-game-ink: #f3f6ff;
      --logic-game-muted: #c0cbe0;
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
        --logic-game-panel: #182131;
        --logic-game-panel-strong: #222f45;
        --logic-game-ink: #f3f6ff;
        --logic-game-muted: #c0cbe0;
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
    .ui-game-view canvas:focus,
    .ui-game-view canvas:focus-visible,
    .ui-game-view-canvas:focus,
    .ui-game-view-canvas:focus-visible,
    canvas.cnv:focus,
    canvas.cnv:focus-visible {
      outline: none !important;
      box-shadow: none !important;
      -webkit-tap-highlight-color: transparent !important;
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
    .ui-saved-item,
    .ui-new-item {
      background-color: var(--logic-surface);
      color: var(--logic-ink);
      border: 1px solid var(--logic-border);
      box-shadow: 0 0.45rem 1.2rem rgba(0, 0, 0, 0.14);
      transition: background-color 180ms ease, color 180ms ease, border-color 180ms ease, transform 180ms ease;
    }
    .ui-saved-item:hover,
    .ui-new-item:hover {
      transform: translateY(-2px);
    }
    .ui-saved-item-name,
    .ui-maps-menu-item-name {
      color: var(--logic-ink);
    }
    .ui-saved-item-tags,
    #logic-arrows-launcher-import-map-status {
      color: var(--logic-muted) !important;
    }
    #logic-arrows-launcher-import-map-status[data-error='1'] {
      color: #d96b78 !important;
    }
    html[data-logic-arrows-theme='dark'] #logic-arrows-launcher-import-map-status[data-error='1'] {
      color: #ff9ca8 !important;
    }
    html[data-logic-arrows-theme='dark'] .ui-menu-panel {
      background-color: var(--logic-surface);
      color: var(--logic-ink);
      border: 1px solid var(--logic-border);
    }
    html[data-logic-arrows-theme='dark'] .ui-menu-saving,
    html[data-logic-arrows-theme='dark'] .ui-menu-back-text {
      color: var(--logic-muted);
    }
    html[data-logic-arrows-theme='dark'] .ui-menu-map-name-input {
      color: var(--logic-ink);
      border-bottom-color: var(--logic-muted);
    }
    .settings-table tr:has(.setting-divider),
    .settings-table .setting-divider,
    .settings-table .setting-divider hr {
      display: none !important;
    }
    .settings-table tr:has(.logout-button) td {
      border-radius: 0.8rem !important;
      padding: 0.9rem 1rem !important;
    }
    .settings-table tr:has(.logout-button) .logout-button {
      display: inline-flex;
      align-items: center;
      min-height: 2.6rem;
      padding: 0.55rem 1rem;
      border-radius: 0.7rem !important;
      overflow: hidden;
      box-shadow: 0 0.35rem 0.9rem rgba(33, 63, 133, 0.24);
    }
    #logic-arrows-launcher-theme-row,
    #logic-arrows-launcher-theme-row td {
      background: transparent !important;
      box-shadow: none !important;
      border-color: transparent !important;
    }
    #logic-arrows-launcher-export-map {
      background-color: var(--logic-surface-strong);
      color: var(--logic-ink) !important;
      border: 1px solid var(--logic-border);
      box-shadow: 0 0.25rem 0.7rem rgba(0, 0, 0, 0.16);
    }
    #logic-arrows-launcher-export-map:hover {
      background-color: var(--accent-color);
      color: #fff !important;
    }
    @media (prefers-color-scheme: dark) {
      html:not([data-logic-arrows-theme='light']) .ui-menu-panel {
        background-color: var(--logic-surface);
        color: var(--logic-ink);
        border: 1px solid var(--logic-border);
      }
      html:not([data-logic-arrows-theme='light']) .ui-menu-saving,
      html:not([data-logic-arrows-theme='light']) .ui-menu-back-text {
        color: var(--logic-muted);
      }
      html:not([data-logic-arrows-theme='light']) .ui-menu-map-name-input {
        color: var(--logic-ink);
        border-bottom-color: var(--logic-muted);
      }
    }
    .settings-table input[type='checkbox'] {
      appearance: none;
      -webkit-appearance: none;
      position: relative;
      width: 3.1rem;
      height: 1.7rem;
      margin: 0;
      border: 1px solid var(--logic-border);
      border-radius: 999px;
      background: var(--logic-game-panel-strong);
      box-shadow: inset 0 1px 2px rgba(0, 0, 0, 0.16);
      cursor: pointer;
      vertical-align: middle;
      transition: background-color 180ms ease, border-color 180ms ease, box-shadow 180ms ease;
    }
    .settings-table input[type='checkbox']::after {
      content: '';
      position: absolute;
      top: 0.18rem;
      left: 0.18rem;
      width: 1.2rem;
      height: 1.2rem;
      border-radius: 50%;
      background: #ffffff;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.28);
      transition: transform 180ms cubic-bezier(0.2, 0, 0, 1);
    }
    .settings-table input[type='checkbox']:checked {
      border-color: var(--accent-color);
      background: var(--accent-color);
      box-shadow: 0 0 0 2px color-mix(in srgb, var(--accent-color) 22%, transparent);
    }
    .settings-table input[type='checkbox']:checked::after {
      transform: translateX(1.38rem);
    }
    .settings-table input[type='checkbox']:focus-visible {
      outline: 2px solid var(--accent-color);
      outline-offset: 3px;
    }
    .settings-table input[type='checkbox']:hover {
      border-color: var(--accent-color);
    }
    html[data-logic-arrows-theme='light'] .settings-table input[type='checkbox'] {
      border-color: #9aa9bf;
      background: #e8edf5;
      box-shadow: inset 0 1px 2px rgba(24, 38, 62, 0.2), 0 1px 2px rgba(24, 38, 62, 0.08);
    }
    html[data-logic-arrows-theme='light'] .settings-table input[type='checkbox']:checked {
      border-color: #315fd4;
      background: #315fd4;
      box-shadow: 0 0 0 2px rgba(49, 95, 212, 0.22);
    }
    @media (prefers-color-scheme: light) {
      html:not([data-logic-arrows-theme='dark']) .settings-table input[type='checkbox'] {
        border-color: #9aa9bf;
        background: #e8edf5;
        box-shadow: inset 0 1px 2px rgba(24, 38, 62, 0.2), 0 1px 2px rgba(24, 38, 62, 0.08);
      }
      html:not([data-logic-arrows-theme='dark']) .settings-table input[type='checkbox']:checked {
        border-color: #315fd4;
        background: #315fd4;
        box-shadow: 0 0 0 2px rgba(49, 95, 212, 0.22);
      }
    }
    .settings-table select {
      appearance: none;
      -webkit-appearance: none;
      min-height: 2.65rem;
      padding: 0.55rem 2.75rem 0.55rem 0.95rem;
      border: 1px solid #a8b4c8 !important;
      border-radius: 0.85rem !important;
      background-color: var(--logic-surface) !important;
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='14' height='14' viewBox='0 0 14 14'%3E%3Cpath d='m3.2 5.2 3.8 3.8 3.8-3.8' fill='none' stroke='%23485469' stroke-width='1.8' stroke-linecap='round' stroke-linejoin='round'/%3E%3C/svg%3E");
      background-repeat: no-repeat;
      background-position: right 0.85rem center;
      color: var(--logic-ink) !important;
      box-shadow: 0 1px 2px rgba(20, 34, 57, 0.08);
      cursor: pointer;
      transition: border-color 160ms ease, box-shadow 160ms ease, background-color 160ms ease;
    }
    .settings-table select:hover {
      border-color: var(--accent-color) !important;
    }
    .settings-table select:focus,
    .settings-table select:focus-visible {
      outline: none;
      border-color: var(--accent-color) !important;
      box-shadow: 0 0 0 3px color-mix(in srgb, var(--accent-color) 20%, transparent), 0 2px 8px rgba(20, 34, 57, 0.12);
    }
    .settings-table select option {
      padding: 0.65rem 0.8rem;
      border-radius: 0.65rem;
      background: var(--logic-surface);
      color: var(--logic-ink);
    }
    .logic-custom-select {
      position: relative;
      width: 100%;
      min-width: 10rem;
    }
    #logic-custom-select-layer {
      position: fixed;
      inset: 0;
      z-index: 2147483000;
      isolation: isolate;
      pointer-events: none;
    }
    html.logic-dropdown-open .logic-custom-select:not([data-open='1']) {
      visibility: hidden !important;
    }
    .logic-custom-select > select {
      position: absolute !important;
      width: 1px !important;
      height: 1px !important;
      opacity: 0 !important;
      pointer-events: none !important;
    }
    .logic-custom-select-button {
      position: relative;
      display: flex;
      align-items: center;
      justify-content: space-between;
      width: 100%;
      min-height: 2.65rem;
      padding: 0.55rem 2.75rem 0.55rem 0.95rem;
      border: 1px solid #a8b4c8 !important;
      border-radius: 0.85rem !important;
      background-color: var(--logic-surface) !important;
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='14' height='14' viewBox='0 0 14 14'%3E%3Cpath d='m3.2 5.2 3.8 3.8 3.8-3.8' fill='none' stroke='%23485469' stroke-width='1.8' stroke-linecap='round' stroke-linejoin='round'/%3E%3C/svg%3E");
      background-repeat: no-repeat;
      background-position: right 0.85rem center;
      color: var(--logic-ink) !important;
      font: inherit;
      text-align: left;
      cursor: pointer;
      box-shadow: 0 1px 2px rgba(20, 34, 57, 0.08);
      transition: border-color 160ms ease, box-shadow 160ms ease, background-color 160ms ease;
    }
    .logic-custom-select-button:hover,
    .logic-custom-select-button[aria-expanded='true'] {
      border-color: var(--accent-color) !important;
      box-shadow: 0 0 0 3px color-mix(in srgb, var(--accent-color) 13%, transparent), 0 2px 8px rgba(20, 34, 57, 0.12);
    }
    .logic-custom-select-button:focus,
    .logic-custom-select-button:focus-visible {
      outline: none;
      border-color: var(--accent-color) !important;
      box-shadow: 0 0 0 3px color-mix(in srgb, var(--accent-color) 22%, transparent), 0 2px 8px rgba(20, 34, 57, 0.14);
    }
    .logic-custom-select-menu {
      position: fixed;
      z-index: 2147483647;
      top: 0;
      left: 0;
      right: auto;
      display: none;
      pointer-events: auto;
      padding: 0.35rem;
      border: 1px solid var(--logic-border) !important;
      border-radius: 0.9rem;
      background: #ffffff !important;
      color: #202838 !important;
      opacity: 1 !important;
      box-shadow: 0 0.75rem 1.5rem rgba(20, 34, 57, 0.2);
    }
    .logic-custom-select-menu[data-open='1'] {
      display: grid;
      gap: 0.18rem;
    }
    .logic-custom-select-option {
      min-height: 2.2rem;
      display: flex;
      align-items: center;
      padding: 0.45rem 0.7rem;
      border-radius: 0.62rem;
      color: var(--logic-ink);
      cursor: pointer;
      transition: background-color 140ms ease, color 140ms ease;
    }
    .logic-custom-select-option:hover,
    .logic-custom-select-option:focus-visible,
    .logic-custom-select-option[data-selected='1'] {
      background: color-mix(in srgb, var(--accent-color) 17%, transparent);
      color: var(--logic-ink);
      outline: none;
    }
    html[data-logic-arrows-theme='dark'] .logic-custom-select-button {
      border-color: #53617a !important;
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='14' height='14' viewBox='0 0 14 14'%3E%3Cpath d='m3.2 5.2 3.8 3.8 3.8-3.8' fill='none' stroke='%23e8edf7' stroke-width='1.8' stroke-linecap='round' stroke-linejoin='round'/%3E%3C/svg%3E");
    }
    html[data-logic-arrows-theme='dark'] .logic-custom-select-menu {
      background: #1d2636 !important;
      color: #edf2ff !important;
      border-color: #53617a !important;
      box-shadow: 0 0.9rem 2rem rgba(0, 0, 0, 0.32);
    }
    html[data-logic-arrows-theme='dark'] .logic-custom-select-option {
      color: #edf2ff !important;
    }
    @media (prefers-color-scheme: dark) {
      html:not([data-logic-arrows-theme='light']) .logic-custom-select-menu {
        background: #1d2636 !important;
        color: #edf2ff !important;
      }
      html:not([data-logic-arrows-theme='light']) .logic-custom-select-option {
        color: #edf2ff !important;
      }
    }
    html[data-logic-arrows-theme='dark'] .logic-custom-select-option:hover,
    html[data-logic-arrows-theme='dark'] .logic-custom-select-option:focus-visible,
    html[data-logic-arrows-theme='dark'] .logic-custom-select-option[data-selected='1'] {
      background: rgba(121, 151, 255, 0.2);
    }
    html[data-logic-arrows-theme='dark'] .settings-table select {
      border-color: #53617a !important;
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='14' height='14' viewBox='0 0 14 14'%3E%3Cpath d='m3.2 5.2 3.8 3.8 3.8-3.8' fill='none' stroke='%23e8edf7' stroke-width='1.8' stroke-linecap='round' stroke-linejoin='round'/%3E%3C/svg%3E");
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.22);
    }
    html[data-logic-arrows-dark-ui='1'] .ui-toolbar-item-tooltip {
      background-color: var(--logic-game-panel) !important;
      color: var(--logic-game-ink) !important;
      border: 1px solid var(--logic-border) !important;
      border-radius: 1rem !important;
      box-shadow: 0 1rem 2.5rem rgba(0, 0, 0, 0.34) !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-toolbar-item-tooltip * {
      background-color: transparent !important;
      color: inherit !important;
      opacity: 1 !important;
      text-shadow: none !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-toolbar-item-tooltip .inline-key,
    html[data-logic-arrows-dark-ui='1'] .ui-toolbar-item-tooltip .inline-key-blue,
    html[data-logic-arrows-dark-ui='1'] .ui-toolbar-item-tooltip .inline-icon-blue {
      background-color: #1d2a3d !important;
      color: #f3f6ff !important;
    }
    html[data-logic-arrows-theme='light'] .ui-toolbar-item-tooltip {
      background-color: #ffffff !important;
      color: #333333 !important;
      border: 1px solid #d2d8e2 !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-arrow-info {
      background-color: var(--logic-game-panel) !important;
      color: var(--logic-game-ink) !important;
      border: 1px solid var(--logic-border) !important;
      box-shadow: 0 1rem 2.5rem rgba(0, 0, 0, 0.34) !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-arrow-info h1,
    html[data-logic-arrows-dark-ui='1'] .ui-arrow-info h2,
    html[data-logic-arrows-dark-ui='1'] .ui-arrow-info h3,
    html[data-logic-arrows-dark-ui='1'] .ui-arrow-info p,
    html[data-logic-arrows-dark-ui='1'] .ui-arrow-info span,
    html[data-logic-arrows-dark-ui='1'] .ui-arrow-info strong {
      background-color: transparent !important;
      color: var(--logic-game-ink) !important;
      opacity: 1 !important;
      text-shadow: none !important;
    }
    html[data-logic-arrows-theme='light'] .ui-arrow-info,
    html[data-logic-arrows-theme='light'] .ui-arrow-info h1,
    html[data-logic-arrows-theme='light'] .ui-arrow-info h2,
    html[data-logic-arrows-theme='light'] .ui-arrow-info h3,
    html[data-logic-arrows-theme='light'] .ui-arrow-info p,
    html[data-logic-arrows-theme='light'] .ui-arrow-info span,
    html[data-logic-arrows-theme='light'] .ui-arrow-info strong {
      color: #333 !important;
      opacity: 1 !important;
    }
    @media (prefers-color-scheme: light) {
      html:not([data-logic-arrows-theme='dark']) .ui-arrow-info,
      html:not([data-logic-arrows-theme='dark']) .ui-arrow-info h1,
      html:not([data-logic-arrows-theme='dark']) .ui-arrow-info h2,
      html:not([data-logic-arrows-theme='dark']) .ui-arrow-info h3,
      html:not([data-logic-arrows-theme='dark']) .ui-arrow-info p,
      html:not([data-logic-arrows-theme='dark']) .ui-arrow-info span,
      html:not([data-logic-arrows-theme='dark']) .ui-arrow-info strong {
        color: #333 !important;
        opacity: 1 !important;
      }
    }
    html[data-logic-arrows-dark-ui='1'] .level-side-panel {
      background-color: var(--logic-game-panel) !important;
      color: var(--logic-game-ink) !important;
      border: 1px solid var(--logic-border) !important;
    }
    html[data-logic-arrows-dark-ui='1'] .level-side-panel h1,
    html[data-logic-arrows-dark-ui='1'] .level-side-panel p,
    html[data-logic-arrows-dark-ui='1'] .level-side-panel span,
    html[data-logic-arrows-dark-ui='1'] .level-side-panel label {
      background-color: transparent !important;
      color: inherit !important;
      border-color: transparent !important;
    }
    html[data-logic-arrows-dark-ui='1'] .level-side-panel {
      box-shadow: 0 1rem 2.5rem rgba(0, 0, 0, 0.34) !important;
    }
    html[data-logic-arrows-dark-ui='1'] .level-side-panel .level-tutorial-back-button {
      background-color: var(--logic-game-panel-strong) !important;
      color: var(--logic-game-ink) !important;
      border: 1px solid var(--logic-border) !important;
    }
    html[data-logic-arrows-dark-ui='1'] .level-side-panel .inline-arrow {
      border-bottom-color: var(--logic-game-muted) !important;
    }
    html[data-logic-arrows-dark-ui='1'] .level-side-panel .inline-key,
    html[data-logic-arrows-dark-ui='1'] .level-side-panel .inline-key-blue,
    html[data-logic-arrows-dark-ui='1'] .level-side-panel .inline-icon-blue {
      background-color: #1d2a3d !important;
      color: #f3f6ff !important;
    }
    html[data-logic-arrows-dark-ui='1'] .level-side-panel .inline-spoiler-caption,
    html[data-logic-arrows-dark-ui='1'] .level-side-panel .inline-spoiler-text {
      color: var(--logic-game-muted) !important;
      border-color: var(--logic-game-muted) !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-inventory,
    html[data-logic-arrows-dark-ui='1'] .ui-inventory-items,
    html[data-logic-arrows-dark-ui='1'] .ui-toolbar-controller,
    html[data-logic-arrows-dark-ui='1'] .ui-toolbar-container,
    html[data-logic-arrows-dark-ui='1'] .ui-menu-button-container,
    html[data-logic-arrows-dark-ui='1'] .ui-undo-button-container {
      background: transparent !important;
      border: 0 !important;
      color: inherit !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-inventory-line,
    html[data-logic-arrows-dark-ui='1'] .ui-toolbar,
    html[data-logic-arrows-dark-ui='1'] .ui-speed-controller,
    html[data-logic-arrows-dark-ui='1'] .ui-menu-panel,
    html[data-logic-arrows-dark-ui='1'] .ui-ok-cancel-panel {
      background-color: var(--logic-game-panel) !important;
      color: var(--logic-game-ink) !important;
      border-color: var(--logic-border) !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-inventory-line,
    html[data-logic-arrows-dark-ui='1'] .ui-toolbar,
    html[data-logic-arrows-dark-ui='1'] .ui-speed-controller {
      border: 1px solid var(--logic-border);
    }
    html[data-logic-arrows-dark-ui='1'] .inventory-item,
    html[data-logic-arrows-dark-ui='1'] .ui-toolbar-item {
      background-color: var(--logic-game-panel-strong) !important;
      color: var(--dark-blue) !important;
      border: 1px solid var(--logic-border);
    }
    html[data-logic-arrows-dark-ui='1'] .ui-toolbar-item > span {
      color: var(--dark-blue) !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-menu-button,
    html[data-logic-arrows-dark-ui='1'] .ui-undo-button {
      background-color: var(--logic-game-panel-strong) !important;
      color: var(--logic-game-ink) !important;
      border: 1px solid var(--logic-border) !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-menu-button .menu-icon-line {
      background-color: #f3f6ff !important;
    }
    html[data-logic-arrows-theme='light'] .ui-menu-button .menu-icon-line {
      background-color: #172033 !important;
    }
    @media (prefers-color-scheme: light) {
      html:not([data-logic-arrows-theme='dark']) .ui-menu-button .menu-icon-line {
        background-color: #172033 !important;
      }
    }
    html[data-logic-arrows-dark-ui='1'] .ui-controls-hint,
    html[data-logic-arrows-dark-ui='1'] .ui-fps-display,
    html[data-logic-arrows-dark-ui='1'] .ui-menu-panel,
    html[data-logic-arrows-dark-ui='1'] .ui-ok-cancel-panel,
    html[data-logic-arrows-dark-ui='1'] .ui-menu-saving,
    html[data-logic-arrows-dark-ui='1'] .ui-menu-back-text,
    html[data-logic-arrows-dark-ui='1'] .ui-menu-map-name-input {
      color: var(--logic-game-ink) !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-controls-hint {
      color: var(--logic-game-ink) !important;
      text-shadow: none;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-controls-hint p {
      display: block;
      margin-block-start: 0.5em;
      margin-block-end: 0;
      padding: 0;
      border: 0;
      border-radius: 0;
      background: transparent !important;
      color: inherit !important;
      text-shadow: none;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-controls-hint .inline-key,
    html[data-logic-arrows-dark-ui='1'] .ui-controls-hint .inline-key-blue,
    html[data-logic-arrows-dark-ui='1'] .ui-controls-hint .inline-icon-blue {
      background-color: #1d2a3d !important;
      color: #f3f6ff !important;
      border-color: transparent !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-fps-display {
      padding: 0;
      border: 0;
      background: transparent !important;
      color: var(--logic-game-ink) !important;
      text-shadow: none;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-menu-button,
    html[data-logic-arrows-dark-ui='1'] .ui-undo-button {
      box-shadow: 0 0.25rem 0.7rem rgba(0, 0, 0, 0.18);
    }
    html[data-logic-arrows-dark-ui='1'] .ui-menu-button:hover,
    html[data-logic-arrows-dark-ui='1'] .ui-undo-button:hover {
      background-color: var(--logic-game-panel-strong) !important;
      border-color: var(--accent-color) !important;
    }
    html[data-logic-arrows-theme='light'] .ui-menu-button,
    html[data-logic-arrows-theme='light'] .ui-undo-button {
      background-color: #e6ebf4 !important;
      color: #243f78 !important;
      border-color: #aebbd0 !important;
    }
    html[data-logic-arrows-theme='light'] .ui-toolbar-item > span {
      color: #243f78 !important;
    }
    @media (prefers-color-scheme: light) {
      html:not([data-logic-arrows-theme='dark']) .ui-menu-button,
      html:not([data-logic-arrows-theme='dark']) .ui-undo-button {
        background-color: #e6ebf4 !important;
        color: #243f78 !important;
        border-color: #aebbd0 !important;
      }
      html:not([data-logic-arrows-theme='dark']) .ui-toolbar-item > span {
        color: #243f78 !important;
      }
    }
    html[data-logic-arrows-dark-ui='1'] .ui-speed-controller {
      border: 1px solid var(--logic-border);
    }
    html[data-logic-arrows-dark-ui='1'] .ui-range-tick {
      background-color: var(--logic-game-panel-strong) !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-text-message.active {
      color: #b9caff !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-text-message.error.active {
      color: #ff9ca8 !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-menu-map-name-input {
      border-bottom-color: var(--logic-game-muted) !important;
    }
    html[data-logic-arrows-dark-ui='1'] .ui-menu-panel,
    html[data-logic-arrows-dark-ui='1'] .ui-ok-cancel-panel {
      border: 1px solid var(--logic-border) !important;
      box-shadow: 0 1rem 2.5rem rgba(0, 0, 0, 0.25);
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
    if (isDarkTheme()) document.documentElement.setAttribute('data-logic-arrows-dark-ui', '1');
    else document.documentElement.removeAttribute('data-logic-arrows-dark-ui');
  }

  function isDarkTheme() {
    const theme = readTheme();
    if (theme === 'dark') return true;
    if (theme === 'light') return false;
    return Boolean(globalThis.matchMedia?.('(prefers-color-scheme: dark)')?.matches);
  }

  function installGameFocusRecovery() {
    if (!/^\/map-[^/]+$/.test(globalThis.location.pathname) || globalThis[PATCHED_FOCUS_RECOVERY_KEY]) return;

    const isInputFocused = () => {
      const active = document.activeElement;
      return Boolean(active && (
        active.tagName === 'INPUT' ||
        active.tagName === 'TEXTAREA' ||
        active.tagName === 'SELECT' ||
        active.isContentEditable ||
        active.classList?.contains('ui-menu-map-name-input')
      ));
    };

    const clearOfficialKeyboardState = () => {
      try {
        // Official KeyboardHandler clears its private key Sets on Control/Meta keyup.
        document.dispatchEvent(new KeyboardEvent('keyup', {
          code: 'ControlLeft',
          key: 'Control',
          bubbles: true,
          cancelable: false,
        }));
      } catch { }
    };
    const focusGameSurface = () => {
      if (document.visibilityState === 'hidden' || isInputFocused()) return;
      const canvas = document.querySelector('canvas');
      if (!canvas) return;
      if (!canvas.hasAttribute('tabindex')) canvas.setAttribute('tabindex', '-1');
      try {
        canvas.focus({ preventScroll: true });
      } catch {
        try { canvas.focus(); } catch { }
      }
    };
    const recover = () => {
      if (document.visibilityState === 'hidden' || isInputFocused()) return;
      globalThis.setTimeout(focusGameSurface, 0);
    };

    // Prevent game hotkeys from hijacking typing in text inputs (e.g. map rename, modal inputs)
    document.addEventListener('keydown', (event) => {
      if (isInputFocused()) {
        event.stopPropagation();
        if (event.key === 'Enter' && document.activeElement?.tagName === 'INPUT') {
          try { document.activeElement.blur?.(); } catch { }
        }
      }
    }, true);
    document.addEventListener('keyup', (event) => {
      if (isInputFocused()) {
        event.stopPropagation();
      }
    }, true);

    document.addEventListener?.('visibilitychange', () => {
      if (document.visibilityState === 'hidden') clearOfficialKeyboardState();
      else recover();
    }, { passive: true });
    globalThis.addEventListener?.('blur', clearOfficialKeyboardState, true);
    globalThis.addEventListener?.('pagehide', clearOfficialKeyboardState);
    globalThis.addEventListener?.('focus', recover, true);
    globalThis.addEventListener?.('pageshow', recover);
    globalThis[PATCHED_FOCUS_RECOVERY_KEY] = true;
    globalThis.__logicArrowsLauncherRecoverInput = recover;
  }

  function patchMapMenuPanel() {
    if (!/^\/map-[^/]+$/.test(globalThis.location.pathname)) return;
    const panel = document.querySelector('.ui-menu-panel');
    const nameInput = panel?.querySelector('.ui-menu-map-name-input');
    const publicCheckbox = panel?.querySelector('.ui-menu-public-checkbox');
    if (!panel || !nameInput) return;

    let runtime = null;
    try { runtime = getRuntime(); } catch { }
    const mapInfo = runtime?.gamePage?.mapInfo;
    const namespace = runtime?.namespace || globalThis.game;

    // 1. Map Name Renaming patch
    if (nameInput.dataset.launcherPatched !== '1') {
      nameInput.dataset.launcherPatched = '1';

      const commitName = async () => {
        const val = nameInput.value?.trim();
        if (!val || !mapInfo) return;
        mapInfo.name = val;
        document.title = `${val} | Logic Arrows`;
        const savingDiv = panel.querySelector('.ui-menu-saving');
        if (savingDiv) savingDiv.textContent = 'Сохранение...';

        try {
          if (namespace?.ArrowsDB) {
            const cached = await namespace.ArrowsDB.read('mapCache', mapInfo.id);
            if (cached) {
              await namespace.ArrowsDB.write('mapCache', {
                ...cached,
                name: val,
                version: (cached.version || 0) + 1
              });
            } else {
              await namespace.ArrowsDB.write('mapCache', {
                ...mapInfo,
                name: val,
                version: (mapInfo.version || 0) + 1
              });
            }
          }
        } catch { }

        try {
          if (namespace?.Routes?.saveMapInfo) {
            namespace.Routes.saveMapInfo(mapInfo, () => {});
          }
        } catch { }

        if (savingDiv) savingDiv.textContent = 'Сохранено';
      };

      nameInput.addEventListener('keydown', (event) => {
        event.stopPropagation();
        if (event.key === 'Enter') {
          event.preventDefault();
          commitName();
          nameInput.blur();
        }
      });
      nameInput.addEventListener('keyup', (event) => event.stopPropagation());
      nameInput.addEventListener('keypress', (event) => event.stopPropagation());
      nameInput.addEventListener('input', () => {
        const val = nameInput.value?.trim();
        if (val) document.title = `${val} | Logic Arrows`;
      });
      nameInput.addEventListener('blur', () => {
        commitName();
      });
      nameInput.addEventListener('change', () => {
        commitName();
      });
    }

    // 2. Public Link Container below map name
    const LINK_BOX_ID = 'logic-arrows-public-link-container';
    let linkBox = document.getElementById(LINK_BOX_ID);
    if (!linkBox) {
      linkBox = document.createElement('div');
      linkBox.id = LINK_BOX_ID;
      linkBox.style.width = 'calc(100% - 2vmin)';
      linkBox.style.margin = '1vmin auto';
      linkBox.style.padding = '0.8vmin 1.2vmin';
      linkBox.style.background = 'rgba(31, 111, 235, 0.15)';
      linkBox.style.border = '1px solid rgba(56, 139, 253, 0.4)';
      linkBox.style.borderRadius = '0.8vmin';
      linkBox.style.fontSize = '2.2vmin';
      linkBox.style.color = '#58a6ff';
      linkBox.style.display = 'none';
      linkBox.style.alignItems = 'center';
      linkBox.style.justifyContent = 'space-between';
      linkBox.style.cursor = 'pointer';
      linkBox.style.boxSizing = 'border-box';
      linkBox.style.userSelect = 'none';
      linkBox.title = 'Нажмите, чтобы скопировать ссылку на карту';

      const urlSpan = document.createElement('span');
      urlSpan.className = 'logic-public-url-text';
      urlSpan.style.overflow = 'hidden';
      urlSpan.style.textOverflow = 'ellipsis';
      urlSpan.style.whiteSpace = 'nowrap';
      urlSpan.style.maxWidth = '75%';
      urlSpan.style.fontFamily = 'var(--font)';

      const copyBtn = document.createElement('span');
      copyBtn.className = 'logic-public-copy-btn';
      copyBtn.style.fontSize = '1.8vmin';
      copyBtn.style.fontFamily = 'var(--font)';
      copyBtn.style.background = 'rgba(56, 139, 253, 0.3)';
      copyBtn.style.padding = '0.3vmin 0.8vmin';
      copyBtn.style.borderRadius = '0.5vmin';
      copyBtn.style.color = '#ffffff';
      copyBtn.textContent = 'Копировать 📋';

      linkBox.append(urlSpan, copyBtn);
      nameInput.parentElement?.insertBefore(linkBox, nameInput.nextSibling);
    }

    const updatePublicLink = () => {
      if (!mapInfo || !linkBox) return;
      const isPublic = Boolean(mapInfo.isPublic);
      const targetDisplay = isPublic ? 'flex' : 'none';
      if (linkBox.style.display !== targetDisplay) {
        linkBox.style.display = targetDisplay;
      }
      if (isPublic) {
        const cleanId = mapInfo.id?.replace(/^map-/, '') || mapInfo.id;
        const url = `https://logic-arrows.io/map-${cleanId}`;
        if (linkBox.dataset.renderedUrl !== url) {
          linkBox.dataset.renderedUrl = url;
          const urlSpan = linkBox.querySelector('.logic-public-url-text');
          if (urlSpan) {
            urlSpan.textContent = `🌐 ${url}`;
          }
        }
        linkBox.onclick = (e) => {
          e.stopPropagation();
          try {
            if (navigator.clipboard?.writeText) {
              navigator.clipboard.writeText(url);
            } else {
              const tempInput = document.createElement('input');
              tempInput.value = url;
              document.body.appendChild(tempInput);
              tempInput.select();
              document.execCommand('copy');
              tempInput.remove();
            }
            const copyBtn = linkBox.querySelector('.logic-public-copy-btn');
            if (copyBtn) {
              copyBtn.textContent = 'Скопировано! ✅';
              copyBtn.style.background = 'rgba(63, 185, 80, 0.4)';
              setTimeout(() => {
                copyBtn.textContent = 'Копировать 📋';
                copyBtn.style.background = 'rgba(56, 139, 253, 0.3)';
              }, 1800);
            }
          } catch { }
        };
      }
    };

    updatePublicLink();

    if (publicCheckbox && publicCheckbox.dataset.launcherPatched !== '1') {
      publicCheckbox.dataset.launcherPatched = '1';
      publicCheckbox.addEventListener('click', () => {
        setTimeout(updatePublicLink, 50);
      });
    }
  }

  function patchDarkArrowCellShader(source) {
    if (!isDarkTheme() || typeof source !== 'string') return source;
    if (
      !source.includes('const vec4 signal_colors[]') ||
      !source.includes('vec3 base = color.rgb + signal_colors')
    ) return source;

    return source
      .replace(
        'vec4(1.0, 1.0, 1.0, 1.0)',
        'vec4(0.0, 0.0, 0.0, 0.0)'
      )
      .replace(
        'alpha = mix(alpha, 0.75, scale);',
        'alpha = mix(alpha, 0.75 * color.a, scale);'
      );
  }

  function patchDarkGridGeneratorShader(source) {
    if (!isDarkTheme() || typeof source !== 'string') return source;
    if (
      !source.includes('uniform float u_show_chunk_borders') ||
      !source.includes('out_color = vec4(vec3(color), 1.0);')
    ) return source;

    // mirror light theme exactly: bg white(1.0) + lines 0.8 -> dark bg + lighter lines
    return source.replace(
      'float color = 1.0 - step(min(grid.x, grid.y), 0.0) * 0.2;',
      'float color = 0.16 + step(min(grid.x, grid.y), 0.0) * 0.34;'
    );
  }

  function patchDarkGridTileShader(source) {
    if (!isDarkTheme() || typeof source !== 'string') return source;
    if (
      !source.includes('uniform sampler2D u_texture') ||
      !source.includes('mix(vec3(0.98), color.rgb, scale)')
    ) return source;

    return source.replace(
      'mix(vec3(0.98), color.rgb, scale)',
      'mix(vec3(0.16, 0.18, 0.22), color.rgb, scale)'
    );
  }

  function installDarkArrowCellShaderHook() {
    const contexts = [globalThis.WebGL2RenderingContext, globalThis.WebGLRenderingContext];
    for (const Context of contexts) {
      const prototype = Context?.prototype;
      if (!prototype || prototype[PATCHED_DARK_ARROW_KEY] || typeof prototype.shaderSource !== 'function') continue;

      const originalShaderSource = prototype.shaderSource;
      const wrappedShaderSource = function (shader, source) {
        return originalShaderSource.call(
          this,
          shader,
          patchDarkGridTileShader(
            patchDarkGridGeneratorShader(patchDarkArrowCellShader(source)),
          ),
        );
      };
      Object.defineProperty(prototype, PATCHED_DARK_ARROW_KEY, { value: true });
      prototype.shaderSource = wrappedShaderSource;
    }
  }

  const PATCHED_DARK_DRAW_KEY = '__logicArrowsLauncherDarkDraw';
  function patchDarkBackgroundFiltering() {
    const gamePage = findGamePage(globalThis.game);
    const gr = gamePage?.game?.render;
    if (!gr || !gr.backgroundTexture || !gr.render || !gr.render.gl) return;
    if (!isDarkTheme()) return;
    try {
      if (gr.backgroundTexture.generateMipmaps && !gr.backgroundTexture.__patchedDarkFiltering) {
        gr.backgroundTexture.__patchedDarkFiltering = true;
      }
    } catch {}
  }
  function patchDarkScreenClear() {
    const gamePage = findGamePage(globalThis.game);
    const gr = gamePage?.game?.render;
    if (!gr || !gr.render || typeof gr.render.clear !== 'function' || gr.render.__patchedDarkClear) return;
    const origClear = gr.render.clear;
    gr.render.clear = function(r, g, b, a) {
      if (isDarkTheme() && arguments.length === 0) {
        return origClear.call(this, 1, 1, 1, 1);
      }
      if (isDarkTheme() && r === 1 && g === 1 && b === 1 && a === 1) {
        return origClear.call(this, 1, 1, 1, 1);
      }
      return origClear.apply(this, arguments);
    };
    gr.render.__patchedDarkClear = true;
  }
  function patchDarkDrawOrder() {
    const gamePage = findGamePage(globalThis.game);
    const game = gamePage?.game;
    if (!game || game.__patchedDarkDraw) return;
    const proto = Object.getPrototypeOf(game);
    const origDraw = proto.draw;
    if (typeof origDraw !== 'function' || origDraw.__patchedDark) return;
    const wrappedDraw = function(...args) {
      if (!isDarkTheme()) return origDraw.apply(this, args);
      if (!this.render || !this.render.isReady()) return;
      const e = this.render;
      this.updateFocus();
      const s = Math.floor(-this.offset[0] / 256 / 16) - 1;
      const i = Math.floor(-this.offset[1] / 256 / 16) - 1;
      const a = Math.floor(-this.offset[0] / 256 / 16 + this.width / this.scale / 16);
      const n = Math.floor(-this.offset[1] / 256 / 16 + this.height / this.scale / 16);
      if (this.drawPastedArrows || this.selectedMap.getSelectedArrows().length) this.screenUpdated = true;
      // keep original adaptive check
      const h = globalThis.game?.PlayerSettings;
      if (h && h.framesToUpdate && h.framesToUpdate[this.updateSpeedLevel] > 1) this.screenUpdated = true;

      let anyDirty = false;
      this.gameMap.chunks.forEach((ch, key) => {
        if (ch.x >= s && ch.x <= a && ch.y >= i && ch.y <= n) {
          if (ch.renderDirty || !e.hasChunkMesh(key)) {
            anyDirty = true;
          }
        }
      });

      if (this.screenUpdated) {
        e.clearRenderTextures();
      } else if (anyDirty) {
        e.render.setRenderTarget(e.mainRenderTexture);
        e.render.clear(0, 0, 0, 0);
        e.render.setRenderTarget(null);
      }

      const t = this.scale;
      e.startArrowsRendering();
      e.setChunkArrowSize(t);
      e.setChunkArrowAlpha(1);
      e.setChunkArrowOffset(this.offset[0] / 256, this.offset[1] / 256);
      this.gameMap.chunks.forEach((ch, key) => {
        if (!(ch.x >= s && ch.x <= a && ch.y >= i && ch.y <= n)) return;
        const need = ch.renderDirty || !e.hasChunkMesh(key);
        if (need) {
          const m = this.buildChunkMesh(ch);
          e.updateChunkMesh(key, m.vertices, m.indices);
          ch.renderDirty = false;
        }
        if (this.screenUpdated || anyDirty || need) e.drawChunkMesh(key);
      });
      if (performance.now() - this.drawTime > 1000) {
        this.drawTime = performance.now();
        this.fps = this.drawsPerSecond;
        this.drawsPerSecond = 0;
      }
      this.drawsPerSecond++;
      e.endArrowsRendering();
      e.removeMissingChunkMeshes(this.gameMap.chunks);
      if (this.screenUpdated) e.drawBackground(this.scale, [-this.offset[0] / 256, -this.offset[1] / 256]);
      // DARK: draw grid BEFORE arrows so gray lines stay behind arrows and don't hide them
      e.clear();
      e.drawGridRenderTexture();
      e.drawArrowsRenderTexture();
      e.setSolidColor(0.25, 0.5, 1, 0.25);
      this.selectedMap.getSelectedArrows().forEach(k => {
        const p = k.split(',').map(x => parseInt(x, 10));
        const x = p[0] * this.scale + this.offset[0] * this.scale / 256;
        const y = p[1] * this.scale + this.offset[1] * this.scale / 256;
        const sz = this.scale + 0.05 * this.scale;
        e.drawSolidColorRect(x, y, sz, sz);
      });
      e.startTransparentArrowsRendering();
      e.setArrowSize(t);
      this.drawSelectedArrows();
      if (this.isSelecting) {
        const sel = this.selectedMap.getCurrentSelectedArea();
        if (sel) {
          const x = sel[0] * this.scale + this.offset[0] * this.scale / 256;
          const y = sel[1] * this.scale + this.offset[1] * this.scale / 256;
          const w = (sel[2] - sel[0]) * this.scale;
          const h2 = (sel[3] - sel[1]) * this.scale;
          e.setSolidColor(0.5, 0.5, 0.75, 0.25);
          e.drawSolidColorRect(x, y, w, h2);
        }
      }
      this.screenUpdated = false;
      this.frame++;
      return;
    };
    wrappedDraw.__patchedDark = true;
    proto.draw = wrappedDraw;
    game.__patchedDarkDraw = true;
  }

  function patchDarkRenderClear() {
    const gamePage = findGamePage(globalThis.game);
    const game = gamePage?.game;
    const gameRender = game?.render;
    if (
      !gameRender ||
      typeof gameRender.clearRenderTextures !== 'function' ||
      gameRender[PATCHED_DARK_RENDER_KEY]
    ) return;

    const originalClearRenderTextures = gameRender.clearRenderTextures;
    gameRender.clearRenderTextures = function () {
      if (
        !isDarkTheme() ||
        !this.mainRenderTexture ||
        !this.gridRenderTexture ||
        !this.render ||
        typeof this.render.setRenderTarget !== 'function' ||
        typeof this.render.clear !== 'function'
      ) {
        return originalClearRenderTextures.call(this);
      }

      this.render.setRenderTarget(this.mainRenderTexture);
      this.render.clear(0, 0, 0, 0);
      this.render.setRenderTarget(this.gridRenderTexture);
      this.render.clear(1, 1, 1, 1);
      this.render.setRenderTarget(null);
    };
    Object.defineProperty(gameRender, PATCHED_DARK_RENDER_KEY, { value: true });
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

  function upgradeSettingsDropdowns() {
    if (!document.body) return;
    let layer = document.getElementById('logic-custom-select-layer');
    if (!layer) {
      layer = document.createElement('div');
      layer.id = 'logic-custom-select-layer';
      document.body.append(layer);
    }

    document.querySelectorAll('.settings-table select').forEach((select) => {
      if (select.dataset.logicCustomDropdown === '1') return;
      select.dataset.logicCustomDropdown = '1';

      const shell = document.createElement('div');
      shell.className = 'logic-custom-select';
      shell.dataset.open = '0';
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'logic-custom-select-button';
      button.setAttribute('aria-haspopup', 'listbox');
      button.setAttribute('aria-expanded', 'false');
      button.setAttribute('aria-label', select.getAttribute('aria-label') || 'Выбор значения');
      const menu = document.createElement('div');
      menu.className = 'logic-custom-select-menu';
      menu.dataset.open = '0';
      menu.setAttribute('role', 'listbox');
      button.setAttribute('aria-controls', `logic-custom-options-${Math.random().toString(36).slice(2)}`);
      menu.id = button.getAttribute('aria-controls');

      const close = () => {
        shell.dataset.open = '0';
        menu.dataset.open = '0';
        button.setAttribute('aria-expanded', 'false');
        document.documentElement.classList?.remove('logic-dropdown-open');
      };
      const positionMenu = () => {
        if (shell.dataset.open !== '1') return;
        const rect = button.getBoundingClientRect();
        const viewportPadding = 12;
        const width = Math.max(rect.width, 180);
        const availableWidth = Math.max(160, window.innerWidth - viewportPadding * 2);
        const menuWidth = Math.min(width, availableWidth);
        let left = Math.min(rect.left, window.innerWidth - menuWidth - viewportPadding);
        left = Math.max(viewportPadding, left);
        menu.style.width = `${Math.round(menuWidth)}px`;
        menu.style.left = `${Math.round(left)}px`;
        menu.style.maxHeight = `${Math.max(120, window.innerHeight - viewportPadding * 2)}px`;
        const menuHeight = Math.min(menu.scrollHeight || 180, window.innerHeight - viewportPadding * 2);
        const belowTop = rect.bottom + 6;
        const aboveTop = rect.top - menuHeight - 6;
        const top = belowTop + menuHeight <= window.innerHeight - viewportPadding
          ? belowTop
          : Math.max(viewportPadding, aboveTop);
        menu.style.top = `${Math.round(top)}px`;
      };
      const open = () => {
        document.querySelectorAll('.logic-custom-select[data-open="1"]').forEach((other) => {
          if (other !== shell) {
            other.dataset.open = '0';
            other.querySelector('.logic-custom-select-menu')?.setAttribute('data-open', '0');
            other.querySelector('.logic-custom-select-button')?.setAttribute('aria-expanded', 'false');
          }
        });
        shell.dataset.open = '1';
        menu.dataset.open = '1';
        button.setAttribute('aria-expanded', 'true');
        document.documentElement.classList?.add('logic-dropdown-open');
        positionMenu();
        menu.querySelector(`[data-value="${CSS.escape(select.value)}"]`)?.focus();
      };
      const sync = () => {
        const current = select.options[select.selectedIndex];
        button.textContent = current?.textContent || select.value;
        menu.querySelectorAll('[role="option"]').forEach((option) => {
          const selected = option.dataset.value === select.value;
          option.setAttribute('aria-selected', selected ? 'true' : 'false');
          option.dataset.selected = selected ? '1' : '0';
        });
      };
      const choose = (value) => {
        if (select.value !== value) {
          select.value = value;
          select.dispatchEvent(new Event('change', { bubbles: true }));
        }
        sync();
        close();
        button.focus();
      };

      Array.from(select.options).forEach((sourceOption) => {
        const option = document.createElement('div');
        option.className = 'logic-custom-select-option';
        option.dataset.value = sourceOption.value;
        option.textContent = sourceOption.textContent;
        option.setAttribute('role', 'option');
        option.tabIndex = -1;
        option.addEventListener('click', () => choose(sourceOption.value));
        option.addEventListener('keydown', (event) => {
          if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            choose(sourceOption.value);
          } else if (event.key === 'Escape') {
            event.preventDefault();
            close();
            button.focus();
          }
        });
        menu.append(option);
      });

      button.addEventListener('click', () => {
        if (shell.dataset.open === '1') close();
        else open();
      });
      button.addEventListener('keydown', (event) => {
        if (event.key === 'ArrowDown' || event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          open();
        } else if (event.key === 'Escape') {
          close();
        }
      });
      select.addEventListener('change', sync);
      document.addEventListener('click', (event) => {
        if (!shell.contains(event.target) && !menu.contains(event.target)) close();
      });
      globalThis.addEventListener?.('resize', positionMenu);
      globalThis.addEventListener?.('scroll', positionMenu, true);

      select.parentElement?.insertBefore(shell, select);
      shell.append(button, select);
      layer.append(menu);
      sync();
    });
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
    // КРИТИЧНО: load игры только создаёт/перезаписывает чанки из потока и НЕ удаляет
    // старые чанки за пределами импортируемых данных. Без очистки импорт перемешивает
    // импортируемую карту с содержимым открытой карты (см. pasteFromText игры —
    // там tempMap.clear() перед load).
    if (typeof map.clear === 'function') map.clear();
    else if (map.chunks && typeof map.chunks.clear === 'function') map.chunks.clear();
    else throw new Error('Не удалось очистить карту перед импортом.');
    namespace.load(map, bytes);
    game.screenUpdated = true;
    return { ok: true, imported: bytes.length };
  }

  function stageLobbyImport(payload) {
    if (!payload || typeof payload.data !== 'string' || payload.data.length === 0 || payload.data.length > MAX_DATA_LENGTH) {
      throw new Error('Данные карты пустые или слишком большие.');
    }
    const name = typeof payload.name === 'string' ? payload.name : (typeof payload.mapName === 'string' ? payload.mapName : null);
    pendingLobbyImport = { data: payload.data, name: name ? name.trim().slice(0, 32) : null };
    setImportStatus('Карта подготовлена. Открываю редактор…');
    return { ok: true, staged: true };
  }

  function tryPendingLobbyImport() {
    if (!pendingLobbyImport || !/^\/map-[^/]+$/.test(globalThis.location.pathname)) return;
    try {
      const { gamePage, namespace } = getRuntime();
      const staged = pendingLobbyImport;
      const result = importMap(staged);
      if (staged.name && gamePage?.mapInfo) {
        gamePage.mapInfo.name = staged.name;
        document.title = `${staged.name} | Logic Arrows`;
        const nameInput = document.querySelector('.ui-menu-map-name-input');
        if (nameInput) nameInput.value = staged.name;
        // имя в локальный mapCache (иначе после перезапуска — «New map»);
        // tryPendingLobbyImport синхронная, поэтому await — только внутри async IIFE
        void (async () => {
          try {
            if (namespace?.ArrowsDB && gamePage.mapInfo.id) {
              const cached = await namespace.ArrowsDB.read('mapCache', gamePage.mapInfo.id);
              const version = ((cached && cached.version) || 0) + 1;
              if (cached) await namespace.ArrowsDB.write('mapCache', { ...cached, name: staged.name, version });
              else await namespace.ArrowsDB.write('mapCache', { ...gamePage.mapInfo, name: staged.name, version });
            }
          } catch { }
          const saveInfo = () => {
            try { namespace.Backend?.saveMapInfo?.(gamePage.mapInfo, () => {}); } catch { }
            try { namespace.Routes?.saveMapInfo?.(gamePage.mapInfo, () => {}); } catch { }
            try { gamePage.saveMap?.(gamePage.mapInfo); } catch { }
          };
          saveInfo();
          // повторная запись после оседания игры (сохранение карты из редактора)
          setTimeout(() => { try { saveInfo(); } catch { } }, 800);
          setTimeout(() => { try { saveInfo(); } catch { } }, 2000);
        })();
      }
      pendingLobbyImport = null;
      post({ type: 'map-imported', imported: result.imported, name: staged.name });
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
    title.textContent = 'Импорт карты';

    const status = document.createElement('div');
    status.id = IMPORT_STATUS_ID;
    status.textContent = 'Файл .map или ID';
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

    const openImportModal = () => {
      if (card.dataset.busy === '1') return;
      const existing = document.getElementById('logic-arrows-import-modal');
      if (existing) { existing.remove(); return; }

      const modalOverlay = document.createElement('div');
      modalOverlay.id = 'logic-arrows-import-modal';
      modalOverlay.style.position = 'fixed';
      modalOverlay.style.inset = '0';
      modalOverlay.style.background = 'rgba(0, 0, 0, 0.72)';
      modalOverlay.style.display = 'flex';
      modalOverlay.style.alignItems = 'center';
      modalOverlay.style.justifyContent = 'center';
      modalOverlay.style.zIndex = '999999';
      modalOverlay.style.backdropFilter = 'blur(4px)';

      const modalBox = document.createElement('div');
      modalBox.style.background = 'var(--logic-game-panel, #182232)';
      modalBox.style.color = 'var(--logic-game-ink, #f0f4fc)';
      modalBox.style.border = '1px solid var(--logic-border, #32435f)';
      modalBox.style.borderRadius = '1rem';
      modalBox.style.padding = '1.75rem';
      modalBox.style.maxWidth = '460px';
      modalBox.style.width = '90%';
      modalBox.style.boxShadow = '0 1rem 3rem rgba(0, 0, 0, 0.6)';
      modalBox.style.display = 'flex';
      modalBox.style.flexDirection = 'column';
      modalBox.style.gap = '1rem';
      modalBox.style.boxSizing = 'border-box';

      const modalTitle = document.createElement('div');
      modalTitle.style.fontSize = '1.25rem';
      modalTitle.style.fontWeight = 'bold';
      modalTitle.textContent = 'Импорт карты в игру';

      const modalSub = document.createElement('div');
      modalSub.style.fontSize = '0.9rem';
      modalSub.style.color = 'var(--logic-game-muted, #8ea0be)';
      modalSub.textContent = 'Вставьте ID публичной карты (map-6ugjRgZm), ссылку, Base64-код карты (AAAB...) или выберите .map файл.';

      const idInput = document.createElement('input');
      idInput.type = 'text';
      idInput.placeholder = 'ID, ссылка или Base64 код (AAAB...)';
      idInput.style.padding = '0.65rem 0.85rem';
      idInput.style.borderRadius = '0.5rem';
      idInput.style.border = '1px solid var(--logic-border, #32435f)';
      idInput.style.background = 'var(--logic-game-panel-strong, #121a27)';
      idInput.style.color = 'var(--logic-game-ink, #f0f4fc)';
      idInput.style.fontFamily = 'inherit';
      idInput.style.fontSize = '1rem';
      idInput.style.outline = 'none';

      const btnCloud = document.createElement('button');
      btnCloud.textContent = '🌐 Загрузить по ID / коду / ссылке';
      btnCloud.style.padding = '0.65rem 1rem';
      btnCloud.style.borderRadius = '0.5rem';
      btnCloud.style.border = 'none';
      btnCloud.style.background = '#2563eb';
      btnCloud.style.color = '#fff';
      btnCloud.style.fontWeight = 'bold';
      btnCloud.style.fontSize = '0.95rem';
      btnCloud.style.cursor = 'pointer';

      const btnFile = document.createElement('button');
      btnFile.textContent = '📁 Выбрать файл с ПК (.map)';
      btnFile.style.padding = '0.65rem 1rem';
      btnFile.style.borderRadius = '0.5rem';
      btnFile.style.border = '1px solid var(--logic-border, #32435f)';
      btnFile.style.background = 'transparent';
      btnFile.style.color = 'var(--logic-game-ink, #f0f4fc)';
      btnFile.style.fontWeight = 'bold';
      btnFile.style.fontSize = '0.95rem';
      btnFile.style.cursor = 'pointer';

      const btnClose = document.createElement('button');
      btnClose.textContent = 'Отмена';
      btnClose.style.padding = '0.5rem 1rem';
      btnClose.style.borderRadius = '0.5rem';
      btnClose.style.border = 'none';
      btnClose.style.background = 'transparent';
      btnClose.style.color = 'var(--logic-game-muted, #8ea0be)';
      btnClose.style.cursor = 'pointer';

      const handleCloudImport = async () => {
        const raw = idInput.value.trim();
        if (!raw) {
          idInput.focus();
          return;
        }

        // 1. Direct Base64 code (e.g. AAAB...)
        if (raw.startsWith('AAAB') || (/^[A-Za-z0-9+/=]{20,}$/.test(raw) && !raw.startsWith('map-'))) {
          try {
            const decoded = globalThis.atob(raw);
            if (decoded.length >= 4) {
              card.dataset.busy = '1';
              setImportStatus('Импортирую карту из Base64…');
              modalOverlay.remove();
              stageLobbyImport({ data: raw, name: 'Импортированная карта' });
              globalThis.__logicArrowsLauncherOpenNewMap?.();
              return;
            }
          } catch { }
        }

        // 2. Direct JSON map code
        if (raw.startsWith('{')) {
          try {
            const parsed = JSON.parse(raw);
            if (parsed && typeof parsed.data === 'string' && parsed.data.length >= 4) {
              card.dataset.busy = '1';
              setImportStatus('Импортирую карту из JSON…');
              modalOverlay.remove();
              stageLobbyImport({ data: parsed.data, name: parsed.mapName || parsed.name || 'Импортированная карта' });
              globalThis.__logicArrowsLauncherOpenNewMap?.();
              return;
            }
          } catch { }
        }

        // 3. Cloud Map ID / URL
        const cleanId = raw.replace(/^https?:\/\/[^/]+\/(?:map-)?/, '').replace(/^map-/, '').replace(/[?#].*$/, '').trim();
        if (!cleanId) {
          idInput.focus();
          return;
        }
        card.dataset.busy = '1';
        setImportStatus('Загружаю карту ' + cleanId + '…');
        modalOverlay.remove();
        try {
          const res = await fetch('/api/mapguest', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ id: cleanId })
          });
          if (!res.ok) throw new Error('Сервер вернул статус ' + res.status);
          const json = await res.json();
          if (!json || !json.data) throw new Error('Карта не найдена или не является публичной.');
          stageLobbyImport({ data: json.data, name: json.name });
          globalThis.__logicArrowsLauncherOpenNewMap?.();
        } catch (error) {
          card.dataset.busy = '0';
          setImportStatus(String(error?.message || error), true);
        }
      };

      btnCloud.addEventListener('click', handleCloudImport);
      idInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') { e.preventDefault(); handleCloudImport(); }
        if (e.key === 'Escape') { modalOverlay.remove(); }
      });

      btnFile.addEventListener('click', () => {
        modalOverlay.remove();
        input.click();
      });

      btnClose.addEventListener('click', () => modalOverlay.remove());
      modalOverlay.addEventListener('click', (e) => {
        if (e.target === modalOverlay) modalOverlay.remove();
      });

      modalBox.append(modalTitle, modalSub, idInput, btnCloud, btnFile, btnClose);
      modalOverlay.append(modalBox);
      document.body.append(modalOverlay);
      setTimeout(() => idInput.focus(), 50);
    };

    card.addEventListener('click', openImportModal);
    card.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        openImportModal();
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
    button.style.color = 'var(--logic-ink)';
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

  // --- In-Game Preview & Optimization Studio ---
  const PREVIEW_SIDEBAR_ID = 'side-menu-preview-btn';
  const PREVIEW_CONTAINER_ID = 'logic-preview-studio-container';

  // --- Extensions (вкладка «Расширения» внизу сайдбара) ---
  const EXTENSIONS_SIDEBAR_ID = 'side-menu-extensions-btn';
  // Иконка — inline SVG с ЯВНЫМ цветом в атрибуте stroke: не наследует CSS-цвет темы
  // (currentColor пропадал при выключенном встроенном расширении) и не зависит от наследования.
  const EXT_ICON_COLOR = '#e8edf7';
  const EXT_ICON_SVG = `<svg viewBox="0 0 24 24" width="32" height="32" stroke="${EXT_ICON_COLOR}" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round" style="display:block;margin:auto;pointer-events:none;"><path d="M11 21.73a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73z"></path><path d="M12 22V12"></path><polyline points="3.29 7 12 12 20.71 7"></polyline><path d="m7.5 4.27 9 5.15"></path></svg>`;

  function makeExtensionIcon() {
    const icon = document.createElement('div');
    icon.className = 'side-menu-icon';
    icon.style.display = 'flex';
    icon.style.alignItems = 'center';
    icon.style.justifyContent = 'center';
    icon.innerHTML = EXT_ICON_SVG;
    return icon;
  }

  function decodeBase64Map(base64) {
    if (!base64 || typeof base64 !== 'string') throw new Error('Пустая строка');
    let clean = base64.trim().replace(/^["']|["']$/g, '');
    if (clean.startsWith('{')) {
      try {
        const parsed = JSON.parse(clean);
        if (parsed.data) clean = String(parsed.data).trim();
      } catch { }
    }
    clean = clean.replace(/-/g, '+').replace(/_/g, '/').replace(/\s+/g, '');
    while (clean.length % 4 !== 0) clean += '=';

    if (!/^[A-Za-z0-9+/=]+$/.test(clean)) {
      throw new Error('Код содержит недопустимые символы. Скопируйте Base64 код схемы (обычно начинается с AAAB...)');
    }

    let raw;
    try {
      raw = globalThis.atob(clean);
    } catch {
      throw new Error('Некорректный Base64. Проверьте правильность скопированного кода.');
    }

    if (!raw || raw.length < 4) {
      throw new Error('Код слишком короткий для карты Logic Arrows.');
    }

    const buf = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; i++) buf[i] = raw.charCodeAt(i);
    let offset = 0;
    const readU8 = () => {
      if (offset >= buf.length) throw new Error('Неожиданный конец данных карты.');
      return buf[offset++];
    };
    const readU16 = () => { const l = readU8(); const h = readU8(); return (h << 8) | l; };
    const readS16 = () => { const v = readU16(); return (v & 0x8000) ? -(v & 0x7FFF) : v; };

    const version = readU16();
    if (version !== 0) throw new Error('Неподдерживаемая версия схемы: ' + version);
    const chunkCount = readU16();
    const cells = [];
    for (let c = 0; c < chunkCount; c++) {
      const cx = readS16();
      const cy = readS16();
      const typeCount = readU8() + 1;
      for (let t = 0; t < typeCount; t++) {
        const type = readU8();
        const arrowCount = readU8() + 1;
        for (let a = 0; a < arrowCount; a++) {
          const pos = readU8();
          const rot = readU8();
          const lx = pos & 0xF;
          const ly = pos >> 4;
          const rotation = rot & 0x3;
          const flipped = (rot & 0x4) !== 0 || (rot & 0x8) !== 0;
          cells.push({ x: cx * 16 + lx, y: cy * 16 + ly, type, rotation, flipped });
        }
      }
    }
    return cells;
  }

  function encodeBase64Map(cells) {
    const bytes = [];
    const writeU8 = (v) => bytes.push(v & 0xFF);
    const writeU16 = (v) => { writeU8(v & 0xFF); writeU8((v >> 8) & 0xFF); };
    const writeS16 = (v) => { const enc = v < 0 ? ((-v) | 0x8000) : v; writeU16(enc); };

    writeU16(0);
    const chunks = new Map();
    for (const c of cells) {
      const cx = c.x >= 0 ? Math.floor(c.x / 16) : Math.floor((c.x - 15) / 16);
      const cy = c.y >= 0 ? Math.floor(c.y / 16) : Math.floor((c.y - 15) / 16);
      const key = `${cx},${cy}`;
      if (!chunks.has(key)) chunks.set(key, { cx, cy, cells: [] });
      chunks.get(key).cells.push(c);
    }

    writeU16(chunks.size);
    for (const chunk of chunks.values()) {
      writeS16(chunk.cx);
      writeS16(chunk.cy);
      const types = new Map();
      for (const c of chunk.cells) {
        if (!types.has(c.type)) types.set(c.type, []);
        types.get(c.type).push(c);
      }
      writeU8(types.size - 1);
      for (const [type, arr] of types) {
        writeU8(type);
        writeU8(arr.length - 1);
        for (const a of arr) {
          const lx = a.x - (chunk.cx * 16);
          const ly = a.y - (chunk.cy * 16);
          const pos = (ly << 4) | (lx & 0xF);
          const rot = (a.flipped ? 4 : 0) | (a.rotation & 3);
          writeU8(pos);
          writeU8(rot);
        }
      }
    }
    let binary = '';
    for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
    return globalThis.btoa(binary);
  }

  // === Умный оптимизатор v2 — точные правила движка игры (bundle.js v1.4) ===
  // Понимает механику всех типов стрелок: офсеты передачи, прыжки на 2 клетки
  // (сплиттеры и синие стрелки), детектор (читает клетку сзади), блокер (гасит
  // стрелку перед собой), NOT-гейт (без входа постоянно выдаёт сигнал —
  // скрытый источник), AND и защёлку (нужны 2 одновременных входа).
  const LA_OFF = {
    1: [[-1, 0]], 2: [[-1, 0], [0, 1], [1, 0], [0, -1]], 3: [], 4: [[-1, 0]], 5: [[-1, 0]],
    6: [[-1, 0], [1, 0]], 7: [[-1, 0], [0, 1]], 8: [[-1, 0], [0, 1], [0, -1]],
    9: [[-1, 0], [0, 1], [1, 0], [0, -1]], 10: [[-2, 0]], 11: [[-1, 1]], 12: [[-1, 0], [-2, 0]],
    13: [[-2, 0], [0, 1]], 14: [[-1, 0], [-1, 1]], 15: [[-1, 0]], 16: [[-1, 0]], 17: [[-1, 0]],
    18: [[-1, 0]], 19: [[-1, 0]], 20: [[-1, 0]], 21: [[-1, 0], [0, 1], [1, 0], [0, -1]],
    22: [[-1, 0]], 24: [[-1, 0]]
  };
  // Источники: сигнал есть без входа. 2 — источник, 9 — генератор (взводится сам),
  // 21/24 — кнопки (пользователь нажимает).
  const LA_SOURCES = new Set([2, 9, 21, 24]);
  // AND (16) и защёлка (18) требуют два одновременных входных импульса.
  const LA_MIN_INPUTS = { 16: 2, 18: 2 };

  // Защищённые типы: 23 — цель уровня, 25 — декоративная стрелка («Does
  // nothing» в бандле, голубая — из неё рисуют пиксель-арт). Любой НЕИЗВЕСТНЫЙ
  // тип тоже считаем декором: лучше сохранить лишнее, чем удалить схему.
  const LA_KNOWN_TYPES = new Set([...Object.keys(LA_OFF).map(Number), 25]);
  function laIsDecor(type) {
    return type === 25 || type === 23 || !LA_KNOWN_TYPES.has(type);
  }

  // Цель смещения в глобальных координатах (реплика h() из бандла игры).
  function laRelTarget(cell, dx, dy) {
    const c = cell.flipped ? -dy : dy;
    const r = cell.rotation & 3;
    if (r === 0) return [cell.x + c, cell.y + dx];
    if (r === 1) return [cell.x - dx, cell.y + c];
    if (r === 2) return [cell.x - c, cell.y - dx];
    return [cell.x + dx, cell.y - c];
  }

  // Смещения-«выходы» (что клетка делает с миром): передача + гашение блокера.
  function laOutOffsets(cell) {
    const list = (LA_OFF[cell.type] || []).slice();
    if (cell.type === 3) list.push([-1, 0]);
    return list;
  }

  // Все механически значимые смещения: выходы + вход детектора сзади.
  function laMechOffsets(cell) {
    const list = laOutOffsets(cell);
    if (cell.type === 5) list.push([1, 0]);
    return list;
  }

  function laKey(x, y) { return x + ',' + y; }

  function laBuildIndex(cells) {
    const byKey = new Map();
    for (const c of cells) byKey.set(laKey(c.x, c.y), c);
    return byKey;
  }

  function laOutTargets(cell, byKey) {
    const out = [];
    for (const [dx, dy] of laOutOffsets(cell)) {
      const [tx, ty] = laRelTarget(cell, dx, dy);
      const t = byKey.get(laKey(tx, ty));
      if (t) out.push(t);
    }
    return out;
  }

  // Клетки, которые могут хоть когда-нибудь сработать (сигнал === REQ типа).
  function laComputeFiring(cells, byKey, inEdges) {
    const live = new Set();
    let changed = true;
    while (changed) {
      changed = false;
      for (const c of cells) {
        if (live.has(c)) continue;
        let ok;
        if (LA_SOURCES.has(c.type) || c.type === 15 || c.type === 23) ok = true;
        else if (c.type === 5) {
          const [bx, by] = laRelTarget(c, 1, 0);
          const behind = byKey.get(laKey(bx, by));
          ok = !!behind && live.has(behind);
        } else {
          const senders = inEdges.get(c) || [];
          let active = 0;
          for (const s of senders) if (live.has(s)) active++;
          ok = active >= (LA_MIN_INPUTS[c.type] || 1);
        }
        if (ok) { live.add(c); changed = true; }
      }
    }
    return live;
  }

  function laBuildGraph(cells) {
    const byKey = laBuildIndex(cells);
    const inEdges = new Map();
    for (const c of cells) {
      for (const t of laOutTargets(c, byKey)) {
        if (!inEdges.has(t)) inEdges.set(t, []);
        inEdges.get(t).push(c);
      }
    }
    return { byKey, inEdges };
  }

  // Безопасная чистка: удалить клетки, которые никогда не сработают (декор не трогаем).
  function laSafePrune(cells) {
    let alive = cells.slice();
    const removed = [];
    while (true) {
      const { byKey, inEdges } = laBuildGraph(alive);
      const live = laComputeFiring(alive, byKey, inEdges);
      const next = alive.filter(c => live.has(c) || laIsDecor(c.type));
      if (next.length === alive.length) return { kept: alive, removed };
      for (const c of alive) if (!next.includes(c)) removed.push(c);
      alive = next;
    }
  }

  // Глубокая чистка: срезать клетки, чей сигнал ни к кому не приходит (декор не трогаем).
  function laDeepTrim(cells) {
    let alive = cells.slice();
    const removed = [];
    let changed = true;
    while (changed) {
      changed = false;
      const byKey = laBuildIndex(alive);
      const next = [];
      for (const c of alive) {
        let hasEffect = laIsDecor(c.type); // цель уровня и декор не срезаем
        if (!hasEffect) {
          for (const [dx, dy] of laOutOffsets(c)) {
            const [tx, ty] = laRelTarget(c, dx, dy);
            if (byKey.has(laKey(tx, ty))) { hasEffect = true; break; }
          }
        }
        if (hasEffect) next.push(c);
        else { removed.push(c); changed = true; }
      }
      alive = next;
    }
    return { kept: alive, removed };
  }

  // Защита пиксель-арта из декоративных стрелок: для каждой 8-связной
  // компоненты декора фиксируем её столбцы и ряды (bbox), чтобы сжатие
  // не искажало рисунок (цифры индикатора и т.п.).
  function laDecorProtection(cells) {
    const decor = cells.filter(c => laIsDecor(c.type) && c.type !== 23);
    const pos = new Set(decor.map(c => laKey(c.x, c.y)));
    const byKey = laBuildIndex(decor);
    const seen = new Set();
    const pCols = new Set(), pRows = new Set();
    for (const d of decor) {
      const startKey = laKey(d.x, d.y);
      if (seen.has(startKey)) continue;
      seen.add(startKey);
      const stack = [d];
      let minX = d.x, maxX = d.x, minY = d.y, maxY = d.y;
      while (stack.length > 0) {
        const c = stack.pop();
        if (c.x < minX) minX = c.x;
        if (c.x > maxX) maxX = c.x;
        if (c.y < minY) minY = c.y;
        if (c.y > maxY) maxY = c.y;
        for (let dx = -1; dx <= 1; dx++) {
          for (let dy = -1; dy <= 1; dy++) {
            const nk = laKey(c.x + dx, c.y + dy);
            if (pos.has(nk) && !seen.has(nk)) {
              seen.add(nk);
              stack.push(byKey.get(nk));
            }
          }
        }
      }
      for (let x = minX; x <= maxX; x++) pCols.add(x);
      for (let y = minY; y <= maxY; y++) pRows.add(y);
    }
    return { pCols, pRows };
  }

  // Сжатие пустот с сохранением ВСЕХ связей: удаляем пустые столбцы/ряды,
  // затем точно проверяем, что (а) каждая связь сохранила смещение и
  // (б) пустые цели смещений остались пустыми. Нарушения чиним возвратом
  // отдельных столбцов/рядов — расстояния-прыжки и тайминги не меняются.
  function laCompact(cells) {
    const n = cells.length;
    if (n === 0) return { ok: true, deletedCols: 0, deletedRows: 0 };
    const xs = new Set(), ys = new Set();
    for (const c of cells) { xs.add(c.x); ys.add(c.y); }
    const minX = Math.min(...xs), maxX = Math.max(...xs);
    const minY = Math.min(...ys), maxY = Math.max(...ys);

    const { pCols, pRows } = laDecorProtection(cells);
    const Sx = new Set(), Sy = new Set();
    for (let x = minX; x <= maxX; x++) if (!xs.has(x) && !pCols.has(x)) Sx.add(x);
    for (let y = minY; y <= maxY; y++) if (!ys.has(y) && !pRows.has(y)) Sy.add(y);

    const byKey = laBuildIndex(cells);
    const consOcc = [], consVoid = [];
    for (const c of cells) {
      for (const [dx, dy] of laMechOffsets(c)) {
        const [px, py] = laRelTarget(c, dx, dy);
        const K = byKey.get(laKey(px, py));
        if (K) consOcc.push({ a: c, b: K, ox: px - c.x, oy: py - c.y });
        else consVoid.push({ a: c, ox: px - c.x, oy: py - c.y });
      }
    }

    const less = (arr, v) => { let n2 = 0; for (const s of arr) if (s < v) n2++; return n2; };
    let arrX = [...Sx].sort((p, q) => p - q);
    let arrY = [...Sy].sort((p, q) => p - q);
    const Mx = v => v - less(arrX, v);
    const My = v => v - less(arrY, v);

    const repairBetween = (a, b) => {
      const loX = Math.min(a.x, b.x), hiX = Math.max(a.x, b.x);
      for (let g = loX; g < hiX; g++) if (Sx.has(g)) { Sx.delete(g); arrX = [...Sx].sort((p, q) => p - q); return true; }
      const loY = Math.min(a.y, b.y), hiY = Math.max(a.y, b.y);
      for (let g = loY; g < hiY; g++) if (Sy.has(g)) { Sy.delete(g); arrY = [...Sy].sort((p, q) => p - q); return true; }
      return false;
    };

    for (let iter = 0; iter < 200000; iter++) {
      let fail = null;
      for (const { a, b, ox, oy } of consOcc) {
        if (Mx(b.x) - Mx(a.x) !== ox || My(b.y) - My(a.y) !== oy) { fail = { pair: [a, b] }; break; }
      }
      if (!fail) {
        const img = new Map();
        for (const k of cells) img.set(Mx(k.x) + ',' + My(k.y), k);
        for (const { a, ox, oy } of consVoid) {
          const hit = img.get((Mx(a.x) + ox) + ',' + (My(a.y) + oy));
          if (hit) { fail = { pair: [a, hit] }; break; }
        }
      }
      if (!fail) break;
      if (!repairBetween(fail.pair[0], fail.pair[1])) return { ok: false, deletedCols: 0, deletedRows: 0 };
    }

    for (const c of cells) { c.x = Mx(c.x); c.y = My(c.y); }
    return { ok: true, deletedCols: arrX.length, deletedRows: arrY.length };
  }

  // Нормализация: сдвиг min(X,Y) в (1,1).
  function laNormalize(cells) {
    if (cells.length === 0) return;
    let minX = Infinity, minY = Infinity;
    for (const c of cells) { if (c.x < minX) minX = c.x; if (c.y < minY) minY = c.y; }
    for (const c of cells) { c.x -= minX - 1; c.y -= minY - 1; }
  }

  // «Дальние связи» (прыжки на 2 клетки), сохранённые в схеме.
  function laCountLongLinks(cells) {
    const byKey = laBuildIndex(cells);
    let n = 0;
    for (const c of cells) {
      for (const [dx, dy] of laMechOffsets(c)) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) >= 2) {
          const [tx, ty] = laRelTarget(c, dx, dy);
          if (byKey.has(laKey(tx, ty))) n++;
        }
      }
    }
    return n;
  }

  function laBBox(cells) {
    if (cells.length === 0) return { w: 0, h: 0 };
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const c of cells) {
      if (c.x < minX) minX = c.x; if (c.x > maxX) maxX = c.x;
      if (c.y < minY) minY = c.y; if (c.y > maxY) maxY = c.y;
    }
    return { w: maxX - minX + 1, h: maxY - minY + 1 };
  }

  function optimizeCells(cells, options) {
    const opts = options || {};
    const warnings = [];
    const stats = {
      origCells: cells.length, removedDead: 0, removedDangling: 0,
      prunedCancelled: false, deletedCols: 0, deletedRows: 0,
      longLinks: 0, origW: 0, origH: 0, optW: 0, optH: 0,
      reduction: 0, optCells: 0, duplicateCells: 0
    };

    if (!cells || cells.length === 0) return { cells: [], base64: '', stats, warnings };

    // 0. Дедупликация координат (последняя побеждает).
    const byCoord = new Map();
    for (const c of cells) byCoord.set(laKey(c.x, c.y), { ...c });
    let work = Array.from(byCoord.values());
    stats.duplicateCells = cells.length - work.length;

    const bbox0 = laBBox(work);
    stats.origW = bbox0.w; stats.origH = bbox0.h;
    const origArea = Math.max(1, bbox0.w * bbox0.h);

    // 1. Безопасная чистка: никогда не срабатывающие механизмы.
    let result = laSafePrune(work);
    stats.removedDead = result.removed.length;

    // Защита: если чистка удалила ВСЁ — в схеме нет источников сигнала.
    // Скорее всего это особенность схемы — отменяем удаление.
    if (result.kept.length === 0 && work.length > 0) {
      warnings.push('В схеме не найдено ни одного работающего источника сигнала (источник, генератор, кнопка или NOT без входа). Чистка отменена — удалён 0 блоков.');
      stats.prunedCancelled = true;
      result = { kept: work.slice(), removed: [] };
      stats.removedDead = 0;
    }

    // 2. Глубокая чистка (по галочке): висячие выходы.
    if (opts.deep && result.kept.length > 0) {
      const deep = laDeepTrim(result.kept);
      stats.removedDangling = deep.removed.length;
      result = { kept: deep.kept, removed: result.removed };
      if (deep.removed.length > 0) {
        warnings.push('Глубокая чистка срезала ' + deep.removed.length + ' блок(ов) с висячими выходами (сигнал ни к кому не приходил).');
      }
    }

    work = result.kept;

    // 3. Сжатие пустот с сохранением связей и таймингов.
    if (work.length > 0) {
      const comp = laCompact(work);
      if (comp.ok) {
        stats.deletedCols = comp.deletedCols;
        stats.deletedRows = comp.deletedRows;
      } else {
        warnings.push('Сжатие пропущено: не удалось гарантировать сохранность всех связей.');
      }
      laNormalize(work);
    }

    const bbox1 = laBBox(work);
    stats.optW = bbox1.w; stats.optH = bbox1.h;
    const optArea = Math.max(1, bbox1.w * bbox1.h);
    stats.reduction = Math.max(0, Math.round((1 - optArea / origArea) * 1000) / 10);
    stats.optCells = work.length;
    stats.longLinks = laCountLongLinks(work);

    const base64 = work.length > 0 ? encodeBase64Map(work) : '';
    return { cells: work, base64, stats, warnings };
  }

  function getArrowColor(type) {
    switch (type) {
      case 1: return '#f85149';
      case 2: return '#ff6b6b';
      case 3: return '#f85149';
      case 4: return '#58a6ff';
      case 5: return '#ffc107';
      case 6: case 7: case 8: return '#f85149';
      case 9: return '#ff4081';
      case 10: case 11: case 12: case 13: case 14: return '#58a6ff';
      case 15: case 16: case 17: case 18: case 19: return '#e3b341';
      case 20: case 21: case 22: return '#db6d28';
      case 23: return '#3fb950';
      case 24: return '#f85149';
      case 25: return '#00d2ff';
      default: return '#8b949e';
    }
  }

  function getArrowName(type) {
    switch (type) {
      case 1: return 'Стрелка (Красная)';
      case 2: return 'Источник (Source)';
      case 3: return 'Блокировщик (Blocker)';
      case 4: return 'Задержка (Delay)';
      case 5: return 'Детектор (Detector)';
      case 6: return 'Разветвитель Вверх-Вниз';
      case 7: return 'Разветвитель Вверх-Вправо';
      case 8: return 'Разветвитель Тройной';
      case 9: return 'Генератор импульсов';
      case 10: return 'Синяя быстрая стрелка';
      case 11: return 'Диагональная стрелка';
      case 15: return 'НЕ (NOT Gate)';
      case 16: return 'И (AND Gate)';
      case 17: return 'ИСКЛ-ИЛИ (XOR Gate)';
      case 18: return 'Защёлка (Latch)';
      case 19: return 'T-триггер (T-FF)';
      case 20: return 'Случайный (Random)';
      case 21: case 22: return 'Кнопка (Button)';
      case 25: return '7-сегментный дисплей';
      default: return 'Блок #' + type;
    }
  }

  const arrowSprites = new Map();
  function getArrowSprite(type, onLoaded) {
    if (arrowSprites.has(type)) {
      const existing = arrowSprites.get(type);
      if (onLoaded && (!existing.complete || existing.naturalWidth === 0)) {
        existing.addEventListener('load', onLoaded, { once: true });
      }
      return existing;
    }
    const img = new Image();
    img.src = `res/sprites/arrow${type}.png`;
    if (onLoaded) {
      img.addEventListener('load', onLoaded, { once: true });
    }
    arrowSprites.set(type, img);
    return img;
  }
  // Preload official arrow sprites 1..26
  for (let i = 1; i <= 26; i++) getArrowSprite(i);

  function renderInGamePreviewStudio(container) {
    container.innerHTML = `
      <div id="${PREVIEW_CONTAINER_ID}" style="display:flex;flex-direction:column;width:100%;height:100%;background:#161a22;color:#f0f6fc;font-family:var(--font,Roboto,-apple-system,sans-serif);box-sizing:border-box;">
        <!-- Top Toolbar (No AI Slop, clean GitHub/Linear UI with Lucide icons) -->
        <div style="display:flex;align-items:center;gap:8px;padding:8px 14px;background:#1f242e;border-bottom:1px solid #2d3544;flex-wrap:wrap;z-index:2;">
          <div style="display:flex;align-items:center;gap:6px;margin-right:4px;">
            <svg viewBox="0 0 24 24" width="18" height="18" stroke="#58a6ff" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"><path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"></path><circle cx="12" cy="12" r="3"></circle></svg>
            <span style="font-size:13px;font-weight:600;color:#f0f6fc;white-space:nowrap;">Превью схем</span>
          </div>
          <input type="text" id="logic-preview-input" placeholder="Вставьте Base64 код схемы (AAAB...) или JSON..." style="flex:1;min-width:180px;background:#12151b;color:#f0f6fc;border:1px solid #2d3544;border-radius:6px;padding:6px 10px;font-size:12.5px;font-family:Consolas,monospace;outline:none;">
          
          <button type="button" id="logic-preview-paste-btn" title="Вставить код из буфера обмена" style="display:flex;align-items:center;gap:5px;background:#282f3d;color:#c9d1d9;border:1px solid #3b4557;border-radius:6px;padding:5px 10px;cursor:pointer;font-size:12px;font-weight:500;">
            <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"><path d="M15 2H9a1 1 0 0 0-1 1v2a1 1 0 0 0 1 1h6a1 1 0 0 0 1-1V3a1 1 0 0 0-1-1Z"></path><path d="M8 4H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2h-2"></path><path d="M12 11v6"></path><path d="m9 14 3 3 3-3"></path></svg>
            Вставить
          </button>
          
          <button type="button" id="logic-preview-open-btn" title="Открыть локальный .map файл" style="display:flex;align-items:center;gap:5px;background:#282f3d;color:#c9d1d9;border:1px solid #3b4557;border-radius:6px;padding:5px 10px;cursor:pointer;font-size:12px;font-weight:500;">
            <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"><path d="m6 14 1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2"></path></svg>
            .map файл
          </button>
          <input type="file" id="logic-preview-file-input" accept=".map,.json,.txt" style="display:none;">
          
          <button type="button" id="logic-preview-opt-btn" title="Анализ связей: удалить мёртвые механизмы и сжать пустоты, не ломая схему" style="display:flex;align-items:center;gap:5px;background:#238636;color:#fff;border:1px solid #2ea043;border-radius:6px;padding:5px 12px;cursor:pointer;font-size:12px;font-weight:bold;">
            <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2.2" fill="none" stroke-linecap="round" stroke-linejoin="round"><path d="m15 15 6 6m-6-6v4.8m0-4.8h4.8"></path><path d="M9 15 3 21m6-6v4.8m0-4.8H4.2"></path><path d="M15 9l6-6m-6 6V4.2m0 4.8h4.8"></path><path d="M9 9 3 3m6 6V4.2m0 4.8H4.2"></path></svg>
            Уменьшить схему
          </button>
          
          <button type="button" id="logic-preview-save-btn" title="Сохранить схему в .map" style="display:flex;align-items:center;gap:5px;background:#282f3d;color:#c9d1d9;border:1px solid #3b4557;border-radius:6px;padding:5px 10px;cursor:pointer;font-size:12px;">
            <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
            Сохранить .map
          </button>
          
          <button type="button" id="logic-preview-center-btn" title="Центрировать камеру" style="display:flex;align-items:center;gap:5px;background:#282f3d;color:#c9d1d9;border:1px solid #3b4557;border-radius:6px;padding:5px 10px;cursor:pointer;font-size:12px;">
            <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"><line x1="2" y1="12" x2="5" y2="12"></line><line x1="19" y1="12" x2="22" y2="12"></line><line x1="12" y1="2" x2="12" y2="5"></line><line x1="12" y1="19" x2="12" y2="22"></line><circle cx="12" cy="12" r="7"></circle><circle cx="12" cy="12" r="3"></circle></svg>
            По центру
          </button>
        </div>

        <!-- Main Workspace -->
        <div style="display:flex;flex:1;overflow:hidden;position:relative;">
          <!-- Canvas Viewport -->
          <div id="logic-preview-canvas-wrap" style="flex:1;position:relative;background:#1a1e26;overflow:hidden;">
            <canvas id="logic-preview-canvas" style="display:block;width:100%;height:100%;cursor:grab;"></canvas>
            <div id="logic-preview-tooltip" style="position:absolute;bottom:12px;left:12px;background:rgba(26,31,41,0.94);border:1px solid #3b4557;padding:5px 10px;border-radius:6px;font-size:12px;color:#c9d1d9;pointer-events:none;display:none;backdrop-filter:blur(4px);"></div>
          </div>

          <!-- Sidebar Info Panel -->
          <div style="width:300px;background:#1a1f29;border-left:1px solid #2d3544;padding:12px;display:flex;flex-direction:column;gap:10px;overflow-y:auto;box-sizing:border-box;">
            <!-- Stats Card -->
            <div style="background:#13161d;border:1px solid #2d3544;border-radius:8px;padding:10px 12px;">
              <div style="display:flex;align-items:center;gap:6px;font-size:13px;font-weight:600;color:#f0f6fc;margin-bottom:6px;">
                <svg viewBox="0 0 24 24" width="14" height="14" stroke="#58a6ff" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="18" x="3" y="3" rx="2"></rect><path d="M3 9h18"></path><path d="M9 21V9"></path></svg>
                Параметры схемы
              </div>
              <div id="logic-preview-stats" style="font-size:12px;color:#8ea0be;line-height:1.5;">Вставьте Base64 код схемы для просмотра.</div>
            </div>

            <!-- Optimization Card -->
            <div style="background:#13161d;border:1px solid #2d3544;border-radius:8px;padding:10px 12px;">
              <div style="display:flex;align-items:center;gap:6px;font-size:13px;font-weight:600;color:#3fb950;margin-bottom:6px;">
                <svg viewBox="0 0 24 24" width="14" height="14" stroke="#3fb950" stroke-width="2.2" fill="none" stroke-linecap="round" stroke-linejoin="round"><path d="m15 15 6 6m-6-6v4.8m0-4.8h4.8"></path><path d="M9 15 3 21m6-6v4.8m0-4.8H4.2"></path><path d="M15 9l6-6m-6 6V4.2m0 4.8h4.8"></path><path d="M9 9 3 3m6 6V4.2m0 4.8H4.2"></path></svg>
                Оптимизация v2
              </div>
              <label style="display:flex;align-items:flex-start;gap:6px;font-size:11.5px;color:#8ea0be;cursor:pointer;margin-bottom:8px;line-height:1.4;">
                <input type="checkbox" id="logic-preview-deep-chk" style="accent-color:#3fb950;margin-top:1px;">
                <span>Срезать висячие выходы — <b style="color:#e3b341;">агрессивно</b>: удаляет цепочки, чей сигнал ни к кому не приходит (в т.ч. индикаторные концы)</span>
              </label>
              <div id="logic-preview-opt-info" style="font-size:12px;color:#8ea0be;line-height:1.5;">Анализирует сигнальные связи схемы: удаляет мёртвые механизмы и сжимает пустоты, не ломая соединения (прыжки, сплиттеры, тайминги).</div>
              <button type="button" id="logic-preview-copy-btn" style="display:none;width:100%;margin-top:8px;background:#1f6feb;color:#fff;border:none;border-radius:6px;padding:7px;cursor:pointer;font-size:12px;font-weight:bold;">📋 Скопировать код</button>
              <button type="button" id="logic-preview-undo-btn" style="display:none;width:100%;margin-top:6px;background:#282f3d;color:#c9d1d9;border:1px solid #3b4557;border-radius:6px;padding:7px;cursor:pointer;font-size:12px;">↩ Вернуть оригинал</button>
            </div>

            <!-- Explorer Guide Card -->
            <div style="background:#13161d;border:1px solid #2d3544;border-radius:8px;padding:10px 12px;">
              <div style="display:flex;align-items:center;gap:6px;font-size:13px;font-weight:600;color:#58a6ff;margin-bottom:6px;">
                <svg viewBox="0 0 24 24" width="14" height="14" stroke="#58a6ff" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"><path d="M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z"></path></svg>
                Куда поместить карту
              </div>
              <div style="font-size:11.5px;color:#8ea0be;line-height:1.45;margin-bottom:8px;">
                1. Нажмите <b>«Скопировать код»</b> и в разделе <b>«Карты»</b> нажмите <b>«Импорт карты»</b>.<br>
                2. Либо сохраните <code>.map</code> файл и откройте папку в Проводнике:
              </div>
              <button type="button" id="logic-preview-folder-btn" style="display:flex;align-items:center;justify-content:center;gap:6px;width:100%;background:#282f3d;color:#c9d1d9;border:1px solid #3b4557;border-radius:6px;padding:7px;cursor:pointer;font-size:12px;font-weight:500;">
                <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"><path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z"></path><circle cx="12" cy="13" r="2"></circle><path d="m14 15 2 2"></path></svg>
                Папка карт в Explorer
              </button>
            </div>
          </div>
        </div>
      </div>
    `;

    const input = document.getElementById('logic-preview-input');
    const pasteBtn = document.getElementById('logic-preview-paste-btn');
    const openBtn = document.getElementById('logic-preview-open-btn');
    const fileInput = document.getElementById('logic-preview-file-input');
    const optBtn = document.getElementById('logic-preview-opt-btn');
    const saveBtn = document.getElementById('logic-preview-save-btn');
    const centerBtn = document.getElementById('logic-preview-center-btn');
    const copyBtn = document.getElementById('logic-preview-copy-btn');
    const undoBtn = document.getElementById('logic-preview-undo-btn');
    const folderBtn = document.getElementById('logic-preview-folder-btn');
    const statsDiv = document.getElementById('logic-preview-stats');
    const optInfoDiv = document.getElementById('logic-preview-opt-info');
    const tooltip = document.getElementById('logic-preview-tooltip');

    const canvas = document.getElementById('logic-preview-canvas');
    const ctx = canvas.getContext('2d');

    let currentCells = [];
    let lastOpt = null;
    let lastOptOriginal = null;
    let cellSize = 32;
    let offsetX = 0;
    let offsetY = 0;
    let isDragging = false;
    let dragStartX = 0;
    let dragStartY = 0;

    function resizeCanvas() {
      const wrap = document.getElementById('logic-preview-canvas-wrap');
      if (!wrap) return;
      const rect = wrap.getBoundingClientRect();
      canvas.width = Math.max(100, Math.round(rect.width * window.devicePixelRatio));
      canvas.height = Math.max(100, Math.round(rect.height * window.devicePixelRatio));
      draw();
    }

    function resetView() {
      if (currentCells.length === 0) {
        cellSize = 32;
        offsetX = canvas.width / (2 * window.devicePixelRatio);
        offsetY = canvas.height / (2 * window.devicePixelRatio);
        draw();
        return;
      }
      let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
      for (const c of currentCells) {
        if (c.x < minX) minX = c.x;
        if (c.x > maxX) maxX = c.x;
        if (c.y < minY) minY = c.y;
        if (c.y > maxY) maxY = c.y;
      }
      const w = maxX - minX + 1;
      const h = maxY - minY + 1;
      const viewW = canvas.width / window.devicePixelRatio - 60;
      const viewH = canvas.height / window.devicePixelRatio - 60;
      cellSize = Math.max(16, Math.min(64, Math.min(viewW / Math.max(1, w), viewH / Math.max(1, h))));
      const midX = (minX + w / 2) * cellSize;
      const midY = (minY + h / 2) * cellSize;
      offsetX = (canvas.width / (2 * window.devicePixelRatio)) - midX;
      offsetY = (canvas.height / (2 * window.devicePixelRatio)) - midY;
      draw();
    }

    function draw() {
      if (!ctx) return;
      const dpr = window.devicePixelRatio || 1;
      ctx.save();
      ctx.scale(dpr, dpr);
      ctx.imageSmoothingEnabled = false;
      const width = canvas.width / dpr;
      const height = canvas.height / dpr;

      // Authentic Logic Arrows game canvas background
      ctx.fillStyle = '#1e222b';
      ctx.fillRect(0, 0, width, height);

      // 1. Grid lines and chunk borders (sharp 1px lines)
      const minX = Math.floor(-offsetX / cellSize) - 1;
      const maxX = Math.ceil((width - offsetX) / cellSize) + 1;
      const minY = Math.floor(-offsetY / cellSize) - 1;
      const maxY = Math.ceil((height - offsetY) / cellSize) + 1;

      // Cell Grid Lines
      ctx.beginPath();
      for (let x = minX; x <= maxX; x++) {
        if (x % 16 === 0 || x === 0) continue;
        const sx = Math.round(offsetX + x * cellSize) + 0.5;
        ctx.moveTo(sx, 0);
        ctx.lineTo(sx, height);
      }
      for (let y = minY; y <= maxY; y++) {
        if (y % 16 === 0 || y === 0) continue;
        const sy = Math.round(offsetY + y * cellSize) + 0.5;
        ctx.moveTo(0, sy);
        ctx.lineTo(width, sy);
      }
      ctx.strokeStyle = '#292f3b';
      ctx.lineWidth = 1;
      ctx.stroke();

      // Chunk Borders (every 16 cells)
      ctx.beginPath();
      for (let x = minX; x <= maxX; x++) {
        if (x % 16 !== 0 || x === 0) continue;
        const sx = Math.round(offsetX + x * cellSize) + 0.5;
        ctx.moveTo(sx, 0);
        ctx.lineTo(sx, height);
      }
      for (let y = minY; y <= maxY; y++) {
        if (y % 16 !== 0 || y === 0) continue;
        const sy = Math.round(offsetY + y * cellSize) + 0.5;
        ctx.moveTo(0, sy);
        ctx.lineTo(width, sy);
      }
      ctx.strokeStyle = '#434e63';
      ctx.lineWidth = 1.5;
      ctx.stroke();

      // Origin Axes (x=0, y=0)
      ctx.beginPath();
      if (0 >= minX && 0 <= maxX) {
        const sx = Math.round(offsetX) + 0.5;
        ctx.moveTo(sx, 0);
        ctx.lineTo(sx, height);
      }
      if (0 >= minY && 0 <= maxY) {
        const sy = Math.round(offsetY) + 0.5;
        ctx.moveTo(0, sy);
        ctx.lineTo(width, sy);
      }
      ctx.strokeStyle = '#388bfd';
      ctx.lineWidth = 1.5;
      ctx.stroke();

      // 2. Render Cells with official sprites
      for (const cell of currentCells) {
        const cx = offsetX + cell.x * cellSize;
        const cy = offsetY + cell.y * cellSize;
        if (cx + cellSize < 0 || cx > width || cy + cellSize < 0 || cy > height) continue;

        ctx.save();
        ctx.translate(Math.round(cx + cellSize / 2), Math.round(cy + cellSize / 2));
        ctx.rotate((cell.rotation * 90 * Math.PI) / 180);
        if (cell.flipped) ctx.scale(-1, 1);

        const img = getArrowSprite(cell.type, draw);
        if (img.complete && img.naturalWidth > 0) {
          ctx.drawImage(img, -Math.round(cellSize / 2), -Math.round(cellSize / 2), Math.round(cellSize), Math.round(cellSize));
        } else {
          // Fallback vector while sprite loads
          const s = cellSize * 0.85;
          const color = getArrowColor(cell.type);
          ctx.fillStyle = color;
          ctx.beginPath();
          ctx.moveTo(0, -s * 0.45);
          ctx.lineTo(s * 0.4, s * 0.4);
          ctx.lineTo(0, s * 0.2);
          ctx.lineTo(-s * 0.4, s * 0.4);
          ctx.closePath();
          ctx.fill();
        }
        ctx.restore();
      }

      ctx.restore();
    }

    function loadFromText(text) {
      const trimmed = (text || '').trim();
      if (!trimmed) {
        currentCells = [];
        statsDiv.textContent = 'Вставьте Base64 код схемы для просмотра.';
        draw();
        return;
      }
      try {
        let base64 = trimmed;
        if (trimmed.startsWith('{')) {
          const parsed = JSON.parse(trimmed);
          base64 = parsed.data || trimmed;
        }
        currentCells = decodeBase64Map(base64);
        lastOpt = null;
        lastOptOriginal = null;
        copyBtn.style.display = 'none';
        undoBtn.style.display = 'none';

        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const c of currentCells) {
          if (c.x < minX) minX = c.x;
          if (c.x > maxX) maxX = c.x;
          if (c.y < minY) minY = c.y;
          if (c.y > maxY) maxY = c.y;
        }
        const w = maxX - minX + 1;
        const h = maxY - minY + 1;
        const chunks = new Set(currentCells.map(c => `${Math.floor(c.x / 16)},${Math.floor(c.y / 16)}`)).size;
        statsDiv.innerHTML = `
          • Размер: <b>${w} × ${h}</b> клеток<br>
          • Блоков: <b>${currentCells.length}</b><br>
          • Занято чанков: <b>${chunks}</b><br>
          • Границы: X:[${minX}..${maxX}], Y:[${minY}..${maxY}]
        `;
        resetView();
      } catch (err) {
        currentCells = [];
        draw();
        statsDiv.innerHTML = `
          <div style="background:#2d1518;border:1px solid #6e1c24;border-radius:6px;padding:8px 10px;color:#ff7b72;font-size:11.5px;line-height:1.45;">
            <div style="font-weight:600;margin-bottom:3px;display:flex;align-items:center;gap:4px;">
              <span>⚠️ Ошибка кода схемы</span>
            </div>
            ${err.message || 'Не удалось распознать код.'}
          </div>
        `;
      }
    }

    input.addEventListener('input', () => loadFromText(input.value));
    pasteBtn.addEventListener('click', async () => {
      try {
        const text = await navigator.clipboard?.readText?.();
        if (text) { input.value = text; loadFromText(text); }
      } catch {
        const manual = prompt('Вставьте код схемы (Base64 или JSON):');
        if (manual) { input.value = manual; loadFromText(manual); }
      }
    });

    openBtn.addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', async () => {
      const file = fileInput.files?.[0];
      if (!file) return;
      const text = await file.text();
      input.value = text;
      loadFromText(text);
    });

    optBtn.addEventListener('click', () => {
      if (currentCells.length === 0) {
        alert('Сначала вставьте или загрузите код схемы.');
        return;
      }
      const deepChk = document.getElementById('logic-preview-deep-chk');
      lastOptOriginal = { input: input.value, cells: currentCells.map(c => ({ ...c })) };
      lastOpt = optimizeCells(currentCells, { deep: !!(deepChk && deepChk.checked) });
      currentCells = lastOpt.cells;
      input.value = lastOpt.base64 || input.value;
      const st = lastOpt.stats;
      let html = '';
      if (currentCells.length === 0) {
        html += '<div style="color:#ff7b72;font-weight:600;margin-bottom:4px;">Схема оптимизирована в пустоту</div>' +
          '<div style="font-size:11.5px;">Каждый её блок либо никогда не сработал бы, либо его сигнал никуда не приходил.</div>';
      } else {
        html += '• Размер: <b>' + st.origW + '×' + st.origH + '</b> ➔ <b>' + st.optW + '×' + st.optH + '</b>' +
          ' <b style="color:#3fb950">(-' + st.reduction + '%)</b><br>' +
          '• Блоков: <b>' + st.origCells + '</b> ➔ <b>' + st.optCells + '</b><br>';
        if (st.removedDead > 0) html += '• 🧹 Мёртвых механизмов удалено: <b style="color:#ff7b72">' + st.removedDead + '</b> (никогда не сработали бы)<br>';
        if (st.removedDangling > 0) html += '• ✂️ Висячих выходов срезано: <b style="color:#e3b341">' + st.removedDangling + '</b><br>';
        if (st.duplicateCells > 0) html += '• Дубликатов координат: <b>' + st.duplicateCells + '</b><br>';
        if (st.deletedCols > 0 || st.deletedRows > 0) html += '• Сжато: столбцов <b>' + st.deletedCols + '</b>, рядов <b>' + st.deletedRows + '</b><br>';
        if (st.longLinks > 0) html += '• Дальние связи (прыжки) сохранены: <b>' + st.longLinks + '</b><br>';
      }
      if (lastOpt.warnings && lastOpt.warnings.length > 0) {
        for (const wtext of lastOpt.warnings) {
          html += '<div style="background:#2d2513;border:1px solid #5a4a1c;border-radius:6px;padding:6px 8px;margin-top:6px;color:#e3b341;font-size:11.5px;line-height:1.4;">⚠️ ' + wtext + '</div>';
        }
      }
      optInfoDiv.innerHTML = html;
      copyBtn.style.display = lastOpt.base64 ? 'block' : 'none';
      undoBtn.style.display = 'block';
      resetView();
    });

    undoBtn.addEventListener('click', () => {
      if (!lastOptOriginal) return;
      currentCells = lastOptOriginal.cells;
      input.value = lastOptOriginal.input;
      lastOpt = null;
      lastOptOriginal = null;
      undoBtn.style.display = 'none';
      copyBtn.style.display = 'none';
      optInfoDiv.innerHTML = 'Оптимизация отменена, схема восстановлена.';
      resetView();
    });

    copyBtn.addEventListener('click', async () => {
      if (lastOpt?.base64) {
        try {
          await navigator.clipboard.writeText(lastOpt.base64);
          copyBtn.textContent = 'Скопировано! ✅';
          setTimeout(() => { copyBtn.textContent = '📋 Скопировать код'; }, 1800);
        } catch { }
      }
    });

    saveBtn.addEventListener('click', () => {
      if (currentCells.length === 0) return;
      const base64 = encodeBase64Map(currentCells);
      const envelope = JSON.stringify({ format: 'logic-arrows-map', formatVersion: 1, siteVersion: '1_4', data: base64 }, null, 2);
      const blob = new Blob([envelope], { type: 'application/json' });
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = 'optimized-map.map';
      a.click();
    });

    centerBtn.addEventListener('click', resetView);

    folderBtn.addEventListener('click', () => {
      post({ type: 'open-maps-folder' });
    });

    // Canvas Events
    canvas.addEventListener('mousedown', (e) => {
      isDragging = true;
      dragStartX = e.clientX;
      dragStartY = e.clientY;
      canvas.style.cursor = 'grabbing';
    });

    canvas.addEventListener('mousemove', (e) => {
      const dpr = window.devicePixelRatio || 1;
      const rect = canvas.getBoundingClientRect();
      const mouseX = e.clientX - rect.left;
      const mouseY = e.clientY - rect.top;

      if (isDragging) {
        offsetX += (e.clientX - dragStartX);
        offsetY += (e.clientY - dragStartY);
        dragStartX = e.clientX;
        dragStartY = e.clientY;
        draw();
      } else {
        const cellX = Math.floor((mouseX - offsetX) / cellSize);
        const cellY = Math.floor((mouseY - offsetY) / cellSize);
        const hit = currentCells.find(c => c.x === cellX && c.y === cellY);
        if (hit) {
          tooltip.style.display = 'block';
          tooltip.textContent = `(${cellX}, ${cellY}) • ${getArrowName(hit.type)}`;
        } else {
          tooltip.style.display = 'none';
        }
      }
    });

    window.addEventListener('mouseup', () => {
      if (isDragging) {
        isDragging = false;
        canvas.style.cursor = 'grab';
      }
    });

    canvas.addEventListener('wheel', (e) => {
      e.preventDefault();
      const rect = canvas.getBoundingClientRect();
      const mouseX = e.clientX - rect.left;
      const mouseY = e.clientY - rect.top;
      const oldCellSize = cellSize;
      const factor = e.deltaY < 0 ? 1.15 : 0.85;
      cellSize = Math.max(8, Math.min(100, cellSize * factor));

      const worldX = (mouseX - offsetX) / oldCellSize;
      const worldY = (mouseY - offsetY) / oldCellSize;
      offsetX = mouseX - worldX * cellSize;
      offsetY = mouseY - worldY * cellSize;
      draw();
    }, { passive: false });

    globalThis.addEventListener('resize', resizeCanvas);
    setTimeout(resizeCanvas, 30);
  }

  function patchMenuPageSideBar() {
    const sideBar = document.getElementById('menu-page-side-bar');
    if (!sideBar) return;

    if (!document.getElementById(PREVIEW_SIDEBAR_ID)) {
      const previewBtn = document.createElement('div');
      previewBtn.id = PREVIEW_SIDEBAR_ID;
      previewBtn.className = 'side-menu-element';
      previewBtn.setAttribute('role', 'button');
      previewBtn.setAttribute('tabindex', '0');
      previewBtn.style.cursor = 'pointer';

      const icon = document.createElement('div');
      icon.className = 'side-menu-icon';
      icon.style.display = 'flex';
      icon.style.alignItems = 'center';
      icon.style.justifyContent = 'center';
      icon.innerHTML = `<svg viewBox="0 0 24 24" width="32" height="32" stroke="white" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round" style="display:block;margin:auto;pointer-events:none;"><path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"></path><circle cx="12" cy="12" r="3"></circle></svg>`;

      const title = document.createElement('div');
      title.className = 'side-menu-title';
      title.textContent = 'Превью';

      previewBtn.append(icon, title);

      const guideBtn = Array.from(sideBar.children).find(c => {
        const t = c.querySelector('.side-menu-title')?.textContent || '';
        return t.includes('Гайд') || t.includes('Guide') || t.includes('Настройки') || t.includes('Settings');
      });
      if (guideBtn) sideBar.insertBefore(previewBtn, guideBtn);
      else sideBar.appendChild(previewBtn);

      const activatePreview = () => {
        sideBar.querySelectorAll('.side-menu-element').forEach(el => el.classList.remove('side-menu-element-selected'));
        previewBtn.classList.add('side-menu-element-selected');

        const extWrap = document.getElementById('logic-extensions-page-container');
        if (extWrap) extWrap.style.display = 'none';

        const content = document.getElementById('menu-page-content');
        if (content) {
          Array.from(content.children).forEach(child => {
            if (child.id !== 'logic-preview-page-container') {
              child.style.display = 'none';
            }
          });
          let previewWrap = document.getElementById('logic-preview-page-container');
          if (!previewWrap) {
            previewWrap = document.createElement('div');
            previewWrap.id = 'logic-preview-page-container';
            previewWrap.style.cssText = 'display:flex;width:100%;height:100%;';
            content.appendChild(previewWrap);
            renderInGamePreviewStudio(previewWrap);
          } else {
            previewWrap.style.display = 'flex';
          }
        }
      };

      previewBtn.addEventListener('click', activatePreview);
      previewBtn.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); activatePreview(); }
      });
    }

    sideBar.querySelectorAll('.side-menu-element').forEach(el => {
      // Исключаем ОБЕ наши вкладки: иначе общий хук (навешивается на каждом проходе)
      // срабатывает после activateExtensions и тут же прячет страницу расширений.
      if (el.id !== PREVIEW_SIDEBAR_ID && el.id !== EXTENSIONS_SIDEBAR_ID && !el.dataset.previewHooked) {
        el.dataset.previewHooked = '1';
        el.addEventListener('click', () => {
          const previewBtn = document.getElementById(PREVIEW_SIDEBAR_ID);
          const wasPreviewSelected = previewBtn?.classList.contains('side-menu-element-selected');
          previewBtn?.classList.remove('side-menu-element-selected');

          const previewWrap = document.getElementById('logic-preview-page-container');
          if (previewWrap) previewWrap.style.display = 'none';

          const extensionsWrap = document.getElementById('logic-extensions-page-container');
          if (extensionsWrap) extensionsWrap.style.display = 'none';

          const content = document.getElementById('menu-page-content');
          if (content) {
            Array.from(content.children).forEach(child => {
              if (child.id !== 'logic-preview-page-container' && child.id !== 'logic-extensions-page-container') {
                child.style.display = '';
              }
            });
          }

          if (wasPreviewSelected) {
            const titleText = el.querySelector('.side-menu-title')?.textContent || '';
            let route = 'maps';
            if (titleText.includes('Уровни') || titleText.includes('Levels')) route = 'levels';
            else if (titleText.includes('Карты') || titleText.includes('Maps')) route = 'maps';
            else if (titleText.includes('Гайд') || titleText.includes('Guide')) route = 'guide';
            else if (titleText.includes('Настройки') || titleText.includes('Settings')) route = 'settings';

            window.history.pushState(null, '', '/' + route);
            window.dispatchEvent(new PopStateEvent('popstate'));
          }
        });
      }
    });
  }

  // --- Extensions page (внизу сайдбара, иконка «package») ---
  function renderExtensionsList() {
    const listDiv = document.getElementById('logic-ext-list');
    if (!listDiv) return;
    const stateReceived = globalThis.__laExtensionsState !== undefined;
    const state = globalThis.__laExtensionsState && typeof globalThis.__laExtensionsState === 'object' ? globalThis.__laExtensionsState : {};
    const diagDiv = document.getElementById('logic-ext-diag');
    if (diagDiv) {
      let iconState = 'вкладки нет';
      const extBtn = document.getElementById(EXTENSIONS_SIDEBAR_ID);
      if (extBtn) {
        const icon = extBtn.querySelector('.side-menu-icon');
        iconState = icon && icon.querySelector('svg') ? 'svg ок' : 'иконка удалена, будет восстановлена';
      }
      const iconNote = globalThis.__laExtIconNote ? ' • ' + globalThis.__laExtIconNote : '';
      diagDiv.textContent = stateReceived
        ? `Диагностика: лаунчер ${state.version || '?'} • состояние получено • встроенное ${state.builtInActive ? 'включено' : 'выключено'} • расширений: ${Array.isArray(state.entries) ? state.entries.length : '?'} • иконка: ${iconState}${iconNote}`
        : `Диагностика: состояние от лаунчера ещё не получено — переоткройте вкладку. • иконка: ${iconState}${iconNote}`;
    }
    const entries = Array.isArray(state.entries) ? state.entries : [];
    const builtInActive = state.builtInActive === true;
    const builtInCard = `
      <div style="display:flex;align-items:center;gap:8px;background:#13161d;border:1px solid ${builtInActive ? '#2ea043' : '#2d3544'};border-radius:8px;padding:9px 12px;">
        <div style="flex:1;min-width:0;">
          <div style="display:flex;align-items:center;gap:7px;font-size:13px;font-weight:600;color:#f0f6fc;">Встроенное расширение лаунчера
            <span style="background:${builtInActive ? '#123018' : '#22272f'};color:${builtInActive ? '#3fb950' : '#8ea0be'};border:1px solid ${builtInActive ? '#2ea043' : '#3b4557'};border-radius:10px;padding:1px 8px;font-size:10.5px;font-weight:600;">${builtInActive ? 'Активно' : 'Выключено'}</span>
          </div>
          <div style="font-size:10.5px;color:#6e7d94;line-height:1.4;">Тёмная тема, оптимизация графики, «Превью схем», экспорт карт. Активно, когда ни одно стороннее расширение не включено.</div>
        </div>
        ${builtInActive ? '' : `<button type="button" data-ext-action="builtin" style="background:#238636;color:#fff;border:1px solid #2ea043;border-radius:6px;padding:4px 10px;cursor:pointer;font-size:11.5px;font-weight:600;">Включить</button>`}
      </div>`;
    if (entries.length === 0) {
      listDiv.innerHTML = builtInCard + `
        <div style="border:1px dashed #3b4557;border-radius:8px;padding:14px;text-align:center;color:#8ea0be;font-size:12px;line-height:1.5;">
          Сторонних расширений пока нет.<br>Нажмите <b style="color:#f0f6fc;">«Добавить расширение»</b> и выберите папку с .js файлами.
        </div>`;
      return;
    }
    listDiv.innerHTML = builtInCard + entries.map((entry) => {
      const missing = entry.missing === true;
      const active = entry.enabled === true && !missing;
      const badge = missing
        ? '<span style="background:#5a1d1d;color:#f88;border:1px solid #a33;border-radius:10px;padding:1px 8px;font-size:10.5px;font-weight:600;">Папка не найдена</span>'
        : active
          ? '<span style="background:#123018;color:#3fb950;border:1px solid #2ea043;border-radius:10px;padding:1px 8px;font-size:10.5px;font-weight:600;">Активно</span>'
          : '<span style="background:#22272f;color:#8ea0be;border:1px solid #3b4557;border-radius:10px;padding:1px 8px;font-size:10.5px;font-weight:600;">Выключено</span>';
      const toggleBtn = missing
        ? ''
        : `<button type="button" data-ext-action="${active ? 'disable' : 'enable'}" data-ext-name="${entry.name.replace(/"/g, '&quot;')}" style="background:${active ? '#282f3d' : '#238636'};color:${active ? '#c9d1d9' : '#fff'};border:1px solid ${active ? '#3b4557' : '#2ea043'};border-radius:6px;padding:4px 10px;cursor:pointer;font-size:11.5px;font-weight:600;">${active ? 'Выключить' : 'Включить'}</button>`;
      const removeBtn = `<button type="button" data-ext-action="remove" data-ext-name="${entry.name.replace(/"/g, '&quot;')}" title="Удалить из списка" style="display:flex;align-items:center;gap:4px;background:#282f3d;color:#f88;border:1px solid #3b4557;border-radius:6px;padding:4px 8px;cursor:pointer;font-size:11.5px;">
          <svg viewBox="0 0 24 24" width="12" height="12" stroke="currentColor" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"></path><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"></path><path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>
          Удалить</button>`;
      return `
        <div style="display:flex;align-items:center;gap:8px;background:#13161d;border:1px solid #2d3544;border-radius:8px;padding:9px 12px;">
          <div style="flex:1;min-width:0;">
            <div style="display:flex;align-items:center;gap:7px;font-size:13px;font-weight:600;color:#f0f6fc;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">${entry.name} ${badge}</div>
            <div style="font-size:10.5px;color:#6e7d94;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;font-family:Consolas,monospace;" title="${entry.path.replace(/"/g, '&quot;')}">${entry.path}</div>
          </div>
          ${toggleBtn}
          ${removeBtn}
        </div>`;
    }).join('');

    listDiv.querySelectorAll('button[data-ext-action]').forEach((button) => {
      button.addEventListener('click', () => {
        const action = button.dataset.extAction;
        const name = button.dataset.extName;
        if (action === 'builtin') {
          post({ type: 'extensions-set-active', name: '__builtin__', enabled: true });
        } else if (action === 'enable' || action === 'disable') {
          post({ type: 'extensions-set-active', name, enabled: action === 'enable' });
        } else if (action === 'remove') {
          post({ type: 'extensions-remove', name });
        }
      });
    });
  }

  function renderInGameExtensionsPage(container) {
    container.innerHTML = `
      <div id="logic-extensions-studio" style="display:flex;flex-direction:column;width:100%;height:100%;background:#161a22;color:#f0f6fc;font-family:var(--font,Roboto,-apple-system,sans-serif);box-sizing:border-box;">
        <div style="display:flex;align-items:center;gap:8px;padding:8px 14px;background:#1f242e;border-bottom:1px solid #2d3544;z-index:2;">
          <svg viewBox="0 0 24 24" width="18" height="18" stroke="#58a6ff" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"><path d="M11 21.73a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73z"></path><path d="M12 22V12"></path><polyline points="3.29 7 12 12 20.71 7"></polyline><path d="m7.5 4.27 9 5.15"></path></svg>
          <span style="font-size:13px;font-weight:600;color:#f0f6fc;">Расширения</span>
        </div>

        <div style="flex:1;overflow-y:auto;padding:16px;display:flex;flex-direction:column;gap:12px;">
          <div style="background:#13161d;border:1px solid #2d3544;border-radius:8px;padding:14px;">
            <div style="display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;">
              <div style="font-size:13px;font-weight:600;color:#f0f6fc;">Мои расширения</div>
              <button type="button" id="logic-ext-add-btn" style="display:flex;align-items:center;gap:6px;background:#238636;color:#fff;border:1px solid #2ea043;border-radius:6px;padding:7px 14px;cursor:pointer;font-size:12.5px;font-weight:bold;">
                <svg viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2.2" fill="none" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14"></path><path d="M12 5v14"></path></svg>
                Добавить расширение
              </button>
            </div>
            <div style="font-size:11.5px;color:#8ea0be;line-height:1.5;margin-top:8px;">
              Расширение — папка с <code style="color:#58a6ff;">.js</code> файлами, которые запускаются в игре <b>до её кода</b> (моды, автокликеры, свои интерфейсы).
              Одновременно активно одно расширение: включение другого выключит текущее, страница игры перезагрузится.
            </div>
            <div id="logic-ext-add-status" style="font-size:11.5px;color:#8ea0be;margin-top:6px;display:none;"></div>
          </div>

          <div id="logic-ext-list" style="display:flex;flex-direction:column;gap:8px;"></div>
          <div id="logic-ext-diag" style="font-size:10.5px;color:#6e7d94;line-height:1.4;font-family:Consolas,monospace;"></div>
        </div>
      </div>
    `;

    const addBtn = document.getElementById('logic-ext-add-btn');
    if (addBtn) {
      addBtn.addEventListener('click', () => {
        const status = document.getElementById('logic-ext-add-status');
        if (status) {
          status.style.display = 'block';
          status.style.color = '#8ea0be';
          status.textContent = 'Открываю проводник… выберите папку расширения.';
        }
        post({ type: 'extensions-add' });
      });
    }

    renderExtensionsList();
    post({ type: 'extensions-list-request' });
  }

  function patchExtensionsSideBar() {
    const sideBar = document.getElementById('menu-page-side-bar');
    if (!sideBar) return;

    if (!document.getElementById(EXTENSIONS_SIDEBAR_ID)) {
      const extBtn = document.createElement('div');
      extBtn.id = EXTENSIONS_SIDEBAR_ID;
      extBtn.className = 'side-menu-element';
      extBtn.setAttribute('role', 'button');
      extBtn.setAttribute('tabindex', '0');
      extBtn.style.cursor = 'pointer';

      const title = document.createElement('div');
      title.className = 'side-menu-title';
      title.textContent = 'Расширения';

      extBtn.append(makeExtensionIcon(), title);
      // пользователь просил: вкладка в самом низу списка
      sideBar.appendChild(extBtn);

      const activateExtensions = () => {
        sideBar.querySelectorAll('.side-menu-element').forEach(el => el.classList.remove('side-menu-element-selected'));
        extBtn.classList.add('side-menu-element-selected');

        const previewWrap = document.getElementById('logic-preview-page-container');
        if (previewWrap) previewWrap.style.display = 'none';

        const content = document.getElementById('menu-page-content');
        if (content) {
          Array.from(content.children).forEach(child => {
            if (child.id !== 'logic-extensions-page-container') {
              child.style.display = 'none';
            }
          });
          let extWrap = document.getElementById('logic-extensions-page-container');
          if (!extWrap) {
            extWrap = document.createElement('div');
            extWrap.id = 'logic-extensions-page-container';
            extWrap.style.cssText = 'display:flex;width:100%;height:100%;';
            content.appendChild(extWrap);
            renderInGameExtensionsPage(extWrap);
          } else {
            extWrap.style.display = 'flex';
            renderExtensionsList();
            post({ type: 'extensions-list-request' });
          }
        }
      };

      extBtn.addEventListener('click', activateExtensions);
      extBtn.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); activateExtensions(); }
      });
    } else {
      // Самовосстановление: сторонний код мог удалить иконку/заголовок нашей вкладки.
      const btn = document.getElementById(EXTENSIONS_SIDEBAR_ID);
      const icon = btn.querySelector('.side-menu-icon');
      if (!icon || !icon.querySelector('svg')) {
        if (icon) icon.remove();
        btn.prepend(makeExtensionIcon());
      }
      if (!btn.querySelector('.side-menu-title')) {
        const title = document.createElement('div');
        title.className = 'side-menu-title';
        title.textContent = 'Расширения';
        btn.appendChild(title);
      }
    }

    // Наблюдатель: фиксируем в диагностике, если кто-то удаляет нашу иконку.
    if (!globalThis.__laExtIconWatch) {
      globalThis.__laExtIconWatch = true;
      try {
        const watchSideBar = document.getElementById('menu-page-side-bar');
        if (watchSideBar) {
          new MutationObserver(() => {
            const watchBtn = document.getElementById(EXTENSIONS_SIDEBAR_ID);
            const watchIcon = watchBtn && watchBtn.querySelector('.side-menu-icon');
            if (!watchBtn || !watchIcon || !watchIcon.querySelector('svg')) {
              globalThis.__laExtIconNote = 'иконка/кнопка удалена внешним кодом в ' + new Date().toLocaleTimeString();
            }
          }).observe(watchSideBar, { subtree: true, childList: true });
        }
      } catch { }
    }

    if (!globalThis.__laExtensionsStateHooked) {
      globalThis.__laExtensionsStateHooked = true;
      globalThis.addEventListener('la-extensions-state', (event) => {
        // Формат состояния: {version, builtInActive, entries}; массив — совместимость со старыми сборками.
        const detail = event?.detail;
        globalThis.__laExtensionsState = detail && typeof detail === 'object'
          ? (Array.isArray(detail) ? { entries: detail } : detail)
          : {};
        renderExtensionsList();
      });
    }
  }

  function syncUi() {
    installGameFocusRecovery();
    // Встроенное расширение (тёмная тема, оптимизация, превью) — только когда включено:
    // оно выключено, пока активно стороннее расширение, чтобы не конфликтовать с ним.
    if (globalThis.__laBuiltinEnabled !== false) {
      installDarkArrowCellShaderHook();
      patchDarkBackgroundFiltering();
      patchDarkScreenClear();
      patchDarkDrawOrder();
      patchDarkRenderClear();
      ensureThemeStyle();
      applyTheme();
      patchGamePerformance();
      ensureSettingsTheme();
      upgradeSettingsDropdowns();
      addLobbyImportCard();
      addExportButton();
      patchMapMenuPanel();
      patchMenuPageSideBar();
    }
    patchExtensionsSideBar();
    tryPendingLobbyImport();
  }

  let isSyncing = false;
  let syncScheduled = false;

  function scheduleSyncUi() {
    if (syncScheduled || isSyncing) return;
    syncScheduled = true;
    globalThis.requestAnimationFrame(() => {
      syncScheduled = false;
      if (isSyncing) return;
      isSyncing = true;
      try {
        syncUi();
      } finally {
        isSyncing = false;
      }
    });
  }

  function startObserver() {
    if (!document.documentElement) {
      globalThis.setTimeout(startObserver, 25);
      return;
    }
    const observer = new MutationObserver(scheduleSyncUi);
    observer.observe(document.documentElement, { childList: true, subtree: true });
    globalThis.setInterval(scheduleSyncUi, 500);
    scheduleSyncUi();
  }

  // Состояние встроенного расширения (синхронно, до любых патчей): '1' = включено.
  try {
    const __laBuiltinXhr = new XMLHttpRequest();
    __laBuiltinXhr.open('GET', '/__la_builtin_state', false);
    __laBuiltinXhr.send(null);
    if (__laBuiltinXhr.status === 200) {
      globalThis.__laBuiltinEnabled = __laBuiltinXhr.responseText.trim() === '1';
    }
  } catch (__laBuiltinError) { }

  if (globalThis.__laBuiltinEnabled !== false) {
    installDarkArrowCellShaderHook();
    patchDarkBackgroundFiltering();
    patchDarkScreenClear();
    patchDarkDrawOrder();
    patchDarkRenderClear();
  }
  startObserver();

  // Активное расширение лаунчера: синхронно запрашиваем у хоста (перехват /__la_extension)
  // и исполняем ДО скриптов игры — пользовательские моды видят весь игровой контекст.
  try {
    const __laXhr = new XMLHttpRequest();
    __laXhr.open('GET', '/__la_extension', false);
    __laXhr.send(null);
    if (__laXhr.status === 200 && __laXhr.responseText) {
      (new Function(__laXhr.responseText))();
    }
  } catch (__laExtensionError) { }
})();
""";
}
