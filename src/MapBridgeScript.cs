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
      if (document.visibilityState === 'hidden') return;
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
      if (document.visibilityState === 'hidden') return;
      globalThis.setTimeout(focusGameSurface, 0);
    };

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

  function patchDarkArrowCellShader(source) {
    if (!isDarkTheme() || typeof source !== 'string') return source;
    if (
      !source.includes('const vec4 signal_colors[]') ||
      !source.includes('vec3 base = color.rgb + signal_colors')
    ) return source;

    return source
      // default signal (index 0) gets no cell fill, only the glyph
      .replace('vec4(1.0, 1.0, 1.0, 1.0)', 'vec4(1.0, 1.0, 1.0, 0.0)')
      // chunk: keep colored cell but make it semi-transparent so the grid shows through
      .replace(
        'alpha = max(alpha, signal_colors[signal_index].a);',
        'alpha = max(color.a * u_alpha, signal_colors[signal_index].a * 0.5 * (1.0 - color.a));'
      )
      // arrow: same, plus preserve glyph opacity boost at distance
      .replace(
        'alpha = mix(alpha, 0.75, scale);',
        'alpha = max(color.a * mix(u_alpha, 0.75, scale), signal_colors[u_signal].a * 0.5 * (1.0 - color.a));'
      );
  }

  function patchDarkGridGeneratorShader(source) {
    if (!isDarkTheme() || typeof source !== 'string') return source;
    if (
      !source.includes('uniform float u_show_chunk_borders') ||
      !source.includes('out_color = vec4(vec3(color), 1.0);')
    ) return source;

    return source.replace(
      'out_color = vec4(vec3(color), 1.0);',
      `vec2 grid2 = fract(uv * u_scale) - 0.08;
  float _gridD = min(grid2.x, grid2.y);
  float _gridAA = min(fwidth(_gridD) * 1.5, 0.08);
  float gridLine = 1.0 - smoothstep(0.0, _gridAA, _gridD);
  vec3 bg = vec3(0.14, 0.16, 0.20);
  vec3 line = vec3(0.5, 0.53, 0.6);
  out_color = vec4(mix(bg, line, gridLine), 1.0);`,
    );
  }

  function patchDarkGridTileShader(source) {
    if (!isDarkTheme() || typeof source !== 'string') return source;
    if (
      !source.includes('uniform sampler2D u_texture') ||
      !source.includes('mix(vec3(0.98), color.rgb, scale)')
    ) return source;

    return source
      .replace('smoothstep(32.0, 64.0, scale)', 'smoothstep(8.0, 24.0, scale)')
      .replace(
        'mix(vec3(0.98), color.rgb, scale)',
        'mix(vec3(0.14, 0.16, 0.20), color.rgb, scale)',
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
      if (this.screenUpdated) e.clearRenderTextures();
      if (this.drawPastedArrows || this.selectedMap.getSelectedArrows().length) this.screenUpdated = true;
      // keep original adaptive check
      const h = globalThis.game?.PlayerSettings;
      if (h && h.framesToUpdate && h.framesToUpdate[this.updateSpeedLevel] > 1) this.screenUpdated = true;
      const t = this.scale;
      e.startArrowsRendering();
      e.setChunkArrowSize(t);
      e.setChunkArrowAlpha(1);
      e.setChunkArrowOffset(this.offset[0] / 256, this.offset[1] / 256);
      const s = Math.floor(-this.offset[0] / 256 / 16) - 1;
      const i = Math.floor(-this.offset[1] / 256 / 16) - 1;
      const a = Math.floor(-this.offset[0] / 256 / 16 + this.width / this.scale / 16);
      const n = Math.floor(-this.offset[1] / 256 / 16 + this.height / this.scale / 16);
      this.gameMap.chunks.forEach((ch, key) => {
        if (!(ch.x >= s && ch.x <= a && ch.y >= i && ch.y <= n)) return;
        const need = ch.renderDirty || !e.hasChunkMesh(key);
        if (need) {
          const m = this.buildChunkMesh(ch);
          e.updateChunkMesh(key, m.vertices, m.indices);
          ch.renderDirty = false;
        }
        if (this.screenUpdated || need) e.drawChunkMesh(key);
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

  function syncUi() {
    installGameFocusRecovery();
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

  installDarkArrowCellShaderHook();
  patchDarkBackgroundFiltering();
  patchDarkScreenClear();
  patchDarkDrawOrder();
  patchDarkRenderClear();
  startObserver();
})();
""";
}
