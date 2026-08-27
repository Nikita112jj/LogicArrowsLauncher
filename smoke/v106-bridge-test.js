const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

class FakeElement {
  constructor(tagName) {
    this.tagName = tagName.toUpperCase();
    this.children = [];
    this.parentElement = null;
    this.style = {};
    this.dataset = {};
    this.attributes = {};
    this.listeners = {};
    this.textContent = '';
    this.value = '';
    this.hidden = false;
    this.id = '';
  }

  append(...nodes) {
    for (const node of nodes) {
      if (!node) continue;
      this.children.push(node);
      node.parentElement = this;
    }
  }

  appendChild(node) {
    this.append(node);
    return node;
  }

  insertBefore(node, before) {
    const index = this.children.indexOf(before);
    if (index < 0) this.append(node);
    else {
      this.children.splice(index, 0, node);
      node.parentElement = this;
    }
  }

  after(node) {
    if (!this.parentElement) return;
    const index = this.parentElement.children.indexOf(this);
    this.parentElement.children.splice(index + 1, 0, node);
    node.parentElement = this.parentElement;
  }

  remove() {
    if (!this.parentElement) return;
    const index = this.parentElement.children.indexOf(this);
    if (index >= 0) this.parentElement.children.splice(index, 1);
    this.parentElement = null;
  }

  setAttribute(name, value) {
    this.attributes[name] = String(value);
  }

  removeAttribute(name) {
    delete this.attributes[name];
  }

  addEventListener(type, handler) {
    this.listeners[type] = handler;
  }

  click() {
    this.listeners.click?.({ preventDefault() {} });
  }

  querySelector() {
    return null;
  }

  querySelectorAll() {
    return [];
  }

  closest() {
    return null;
  }
}

const html = new FakeElement('html');
const head = new FakeElement('head');
const body = new FakeElement('body');
const settingsPage = new FakeElement('div');
const settingsTable = new FakeElement('table');
const interfaceRow = new FakeElement('tr');
const interfaceSelect = new FakeElement('select');
interfaceSelect.closest = () => interfaceRow;
interfaceSelect.className = 'interface-mode-select';
interfaceRow.append(interfaceSelect);
settingsTable.append(interfaceRow);
settingsPage.append(settingsTable);
settingsTable.querySelector = (selector) => selector === '.interface-mode-select' ? interfaceSelect : null;
settingsPage.querySelector = (selector) => selector === '.settings-table' ? settingsTable : null;

function findById(node, id) {
  if (node.id === id) return node;
  for (const child of node.children) {
    const found = findById(child, id);
    if (found) return found;
  }
  return null;
}

let pathname = '/settings';
const document = {
  documentElement: html,
  head,
  body,
  createElement: (tagName) => new FakeElement(tagName),
  getElementById: (id) => {
    const roots = [html, head, body, settingsPage, settingsTable];
    for (const root of roots) {
      const found = findById(root, id);
      if (found) return found;
    }
    return null;
  },
  querySelector: (selector) => selector === '.settings-page' ? settingsPage : null,
  querySelectorAll: () => [],
};

const storage = new Map();
let clock = 0;
let tickCount = 0;
let originalCalls = 0;
let observedDrawLevel = null;
const clearCalls = [];
const shaderSourceCalls = [];
class FakeWebGL2RenderingContext {
  shaderSource(_shader, source) {
    shaderSourceCalls.push(source);
  }
}
const gameRender = {
  mainRenderTexture: {},
  gridRenderTexture: {},
  render: {
    setRenderTarget(target) { clearCalls.push(['target', target]); },
    clear(r, g, b, a) { clearCalls.push(['clear', r, g, b, a]); },
  },
  clearRenderTextures() { clearCalls.push(['official-clear']); },
};
const game = {
  updateSpeedLevel: 5,
  frame: 0,
  playing: true,
  updatesPerSecond: 0,
  updateTime: -10000,
  tps: 0,
  onFPSUpdate: () => {},
  updateTick() {
    tickCount += 1;
    clock += 1;
  },
  updateFrame() {
    originalCalls += 1;
  },
  draw() {
    observedDrawLevel = this.updateSpeedLevel;
  },
};

const sandbox = {
  document,
  location: { get pathname() { return pathname; } },
  localStorage: {
    getItem: (key) => storage.get(key) ?? null,
    setItem: (key, value) => storage.set(key, String(value)),
  },
  performance: { now: () => { clock += 1; return clock; } },
  Date,
  MutationObserver: class { observe() {} disconnect() {} },
  setInterval: () => 1,
  setTimeout: () => 1,
  clearInterval: () => {},
  atob: (value) => Buffer.from(value, 'base64').toString('binary'),
  gameVersion: '1_4',
  WebGL2RenderingContext: FakeWebGL2RenderingContext,
  game: { navigation: { gamePage: { game: { ...game, gameMap: {}, render: gameRender } } } },
  chrome: { webview: { postMessage: () => {} } },
};
sandbox.globalThis = sandbox;

const patchedGame = sandbox.game.navigation.gamePage.game;
const source = fs.readFileSync('src/MapBridgeScript.cs', 'utf8');
const launcherSource = fs.readFileSync('src/LauncherForm.cs', 'utf8');
assert.match(launcherSource, /Activated \+= LauncherForm_Activated;/, 'window activation restores game focus');
assert.match(launcherSource, /globalThis\.focus\?\.\(\); document\.querySelector\('canvas'\)/, 'page focus ping restores canvas input');
assert.match(launcherSource, /protected override void WndProc\(ref Message m\)/, 'host handles native focus messages');
assert.match(launcherSource, /WM_ACTIVATEAPP[\s\S]*RequestNativeFocusRecovery/, 'app activation queues native focus recovery');
assert.match(launcherSource, /WM_SETFOCUS[\s\S]*RequestNativeFocusRecovery/, 'set-focus queues native focus recovery');
assert.match(launcherSource, /!appIsActive[\s\S]*nativeFocusRecoveryQueued/, 'native recovery never steals focus while app is inactive');
assert.match(launcherSource, /!isGameFullscreen \|\| !appIsActive \|\| requestId != focusRequestId/, 'delayed retries cancel after deactivation');
const match = source.match(/public const string Source = """\n([\s\S]*?)\n""";/);
assert.ok(match, 'bridge raw string found');
assert.match(match[1], /installGameFocusRecovery/, 'page focus recovery is installed');
assert.match(match[1], /visibilitychange/, 'page recovers input after visibility returns');
assert.match(match[1], /__logicArrowsLauncherRecoverInput/, 'host can trigger page input recovery');
assert.match(match[1], /new KeyboardEvent\('keyup'/, 'page can clear official keyboard state');
assert.match(match[1], /code: 'ControlLeft'/, 'keyboard reset uses official clear branch');
assert.match(match[1], /addEventListener\?\.\('blur', clearOfficialKeyboardState/, 'blur clears stale keys');
assert.match(match[1], /addEventListener\?\.\('pagehide', clearOfficialKeyboardState/, 'pagehide clears stale keys');
vm.runInNewContext(match[1], sandbox, { filename: 'MapBridgeScript.Source' });

patchedGame.updateFrame();
assert.equal(tickCount < 100, true, 'max TPS is budgeted instead of blocking the frame');
assert.equal(tickCount > 0, true, 'high TPS still advances simulation');
assert.equal(originalCalls, 0, 'high TPS uses adaptive path');
patchedGame.draw();
assert.equal(observedDrawLevel, 0, 'high TPS draw avoids forced full refresh');
assert.equal(patchedGame.updateSpeedLevel, 5, 'draw restores selected speed level');

const highLevelTicks = tickCount;
patchedGame.updateSpeedLevel = 2;
patchedGame.frame = 0;
patchedGame.updateFrame();
assert.equal(originalCalls, 1, 'low TPS keeps official updateFrame path');
assert.equal(tickCount, highLevelTicks, 'low TPS does not use governor ticks');

const themeSelect = document.getElementById('logic-arrows-launcher-theme-select');
assert.ok(themeSelect, 'theme select injected into settings');
assert.deepEqual(themeSelect.children.map((option) => [option.value, option.textContent]), [
  ['system', 'Системная'],
  ['dark', 'Тёмная'],
  ['light', 'Светлая'],
]);
themeSelect.value = 'dark';
themeSelect.listeners.change();
assert.equal(storage.get('logic-arrows-theme'), 'dark', 'theme preference persisted');
assert.equal(html.attributes['data-logic-arrows-theme'], 'dark', 'dark theme applied immediately');
assert.equal(html.attributes['data-logic-arrows-dark-ui'], '1', 'dark UI scope applied only for dark theme');
assert.match(match[1], /html\[data-logic-arrows-dark-ui='1'\] \.ui-toolbar/, 'game overlay overrides are scoped to dark UI');
assert.match(match[1], /html\[data-logic-arrows-dark-ui='1'\] \.level-side-panel/, 'levels panel dark style is scoped to dark UI');
assert.match(match[1], /html\[data-logic-arrows-dark-ui='1'\] \.ui-toolbar-item-tooltip/, 'real toolbar tooltip dark style is scoped to dark UI');
assert.match(match[1], /\.ui-toolbar-item-tooltip \*/, 'toolbar tooltip descendants inherit readable dark text');
const darkTooltipBlock = match[1].match(/html\[data-logic-arrows-dark-ui='1'\] \.ui-toolbar-item-tooltip \{([\s\S]*?)\n\s+\}/)?.[1] || '';
assert.doesNotMatch(darkTooltipBlock, /opacity:\s*1\s*!important;/, 'toolbar tooltip keeps official hover opacity behavior');
assert.match(match[1], /html\[data-logic-arrows-dark-ui='1'\] \.ui-arrow-info/, 'legacy arrow info dark style remains scoped');
assert.match(match[1], /\.ui-arrow-info h1[\s\S]*opacity: 1 !important;/, 'arrow tooltip text stays opaque');
assert.match(match[1], /background-color: transparent !important;\n\s+color: inherit !important;/, 'level panel text does not get separate tile backgrounds');
assert.doesNotMatch(match[1], /\n    \.ui-toolbar-item,\n/, 'no global toolbar item override remains');

patchedGame.render.clearRenderTextures();
assert.deepEqual(clearCalls, [
  ['target', gameRender.mainRenderTexture],
  ['clear', 0.055, 0.075, 0.11, 1],
  ['target', gameRender.gridRenderTexture],
  ['clear', 0, 0, 0, 0],
  ['target', null],
], 'dark theme clears only the arrow background dark and keeps grid layer transparent');

clearCalls.length = 0;
storage.set('logic-arrows-theme', 'light');
patchedGame.render.clearRenderTextures();
assert.deepEqual(clearCalls, [['official-clear']], 'light theme keeps official clear path');

storage.set('logic-arrows-theme', 'dark');
const fakeContext = new sandbox.WebGL2RenderingContext();
const selectionArrowShader = `const vec4 signal_colors[] = vec4[] (vec4(1.0, 1.0, 1.0, 0.0), vec4(1.0, 0.0, 0.0, 1.0), vec4(0.3, 0.5, 1.0, 1.0)); vec4 color = texture(u_texture, uv * u_sprite_size + u_sprite_position); vec3 base = color.rgb + signal_colors[u_signal].rgb * (1.0 - color.a); float scale = smoothstep(16.0, 2.0, u_size.x); float alpha = color.a * u_alpha; alpha = mix(alpha, 0.75, scale);`;
const chunkArrowShader = `in float v_signal; const vec4 signal_colors[] = vec4[] (vec4(1.0, 1.0, 1.0, 1.0), vec4(1.0, 0.0, 0.0, 1.0), vec4(0.3, 0.5, 1.0, 1.0)); vec4 color = texture(u_texture, v_texcoord); vec3 base = color.rgb + signal_colors[signal_index].rgb * (1.0 - color.a);`;
fakeContext.shaderSource({}, selectionArrowShader);
fakeContext.shaderSource({}, chunkArrowShader);
assert.equal(shaderSourceCalls.length, 2, 'arrow shaders remain callable');
assert.match(shaderSourceCalls[0], /vec4\(1\.0, 1\.0, 1\.0, 0\.0\)/, 'selection preview keeps official transparent signal color');
assert.doesNotMatch(shaderSourceCalls[0], /vec4\(0\.055, 0\.075, 0\.11, 0\.0\)/, 'selection preview is not dark-background patched');
assert.match(shaderSourceCalls[1], /vec4\(0\.055, 0\.075, 0\.11, 1\.0\)/, 'dark theme replaces opaque chunk-cell background');
assert.doesNotMatch(shaderSourceCalls[0], /alpha = mix\(alpha, 0\.75, scale\)/, 'selection preview does not force opaque alpha');
assert.match(shaderSourceCalls[0], /float alpha = color\.a \* u_alpha;/, 'selection preview keeps texture alpha');
assert.match(shaderSourceCalls[1], /vec3 base = color\.rgb \+ signal_colors\[signal_index\]\.rgb \* \(1\.0 - color\.a\)/, 'chunk shader keeps official base composition');
assert.match(shaderSourceCalls[1], /texture\(u_texture, v_texcoord\)/, 'chunk shader keeps normal texture sampling');
assert.match(shaderSourceCalls[0], /vec4\(1\.0, 0\.0, 0\.0, 1\.0\)/, 'red signal color remains original');
assert.match(shaderSourceCalls[0], /vec4\(0\.3, 0\.5, 1\.0, 1\.0\)/, 'blue signal color remains original');

storage.set('logic-arrows-theme', 'light');
fakeContext.shaderSource({}, selectionArrowShader);
assert.match(shaderSourceCalls[2], /vec4\(1\.0, 1\.0, 1\.0, 0\.0\)/, 'light theme keeps official selection-cell background');

storage.set('logic-arrows-theme', 'dark');
const gridGeneratorShader = `uniform float u_show_chunk_borders; out vec4 out_color; void main() { vec2 grid = fract(vec2(1.0)); float color = 1.0; out_color = vec4(vec3(color), 1.0); }`;
fakeContext.shaderSource({}, gridGeneratorShader);
assert.match(shaderSourceCalls[3], /vec3\(0\.44\), gridLine/, 'dark grid uses gray lines with transparent empty cells');

console.log(`adaptive_ticks=${highLevelTicks}`);
console.log('low_tps_official_path=True');
console.log('theme_dropdown=True');
