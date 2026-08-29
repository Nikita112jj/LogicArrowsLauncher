// Диагностический тест моста: запускает настоящий MapBridgeScript.Source в фейковом DOM
// (vm + Proxy-элементы) и прогоняет сценарии вкладки «Расширения» до релиза.
// Мир 1 — встроенное активно; Мир 2 — стороннее расширение (встроенное выключено):
// регрессия пользователя «при чужом расширении вкладки не переключаются».
// use: node tools/bridge-dom-test.js
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const cs = fs.readFileSync(path.join(__dirname, '..', 'src', 'MapBridgeScript.cs'), 'utf8');
const sourceMatch = cs.match(/Source\s*=\s*"""([\s\S]*?)"""/);
if (!sourceMatch) {
  console.error('FAIL: MapBridgeScript.Source не найден');
  process.exit(1);
}
const source = sourceMatch[1];

let pass = 0;
let total = 0;
function check(name, cond) {
  total++;
  console.log((cond ? 'ok ' : 'FAIL ') + name);
  if (cond) pass++;
}

function makeWorld(builtinState, extensionCode) {
  const registry = new Map();
  const allElements = [];
  const posted = [];
  const windowListeners = {};

  function makeElement(tag = 'div') {
    const el = {
      tagName: tag.toUpperCase(),
      children: [],
      listeners: {},
      dataset: {},
      style: new Proxy({}, { get: (t, k) => (k in t ? t[k] : ''), set: (t, k, v) => { t[k] = v; return true; } }),
      classList: { _set: new Set(), add(c) { this._set.add(c); }, remove(c) { this._set.delete(c); }, contains(c) { return this._set.has(c); }, toggle(c) { this._set.has(c) ? this._set.delete(c) : this._set.add(c); } },
      className: '',
      _id: '',
      _innerHTML: '',
      textContent: '',
      parentElement: null,
      addEventListener(type, fn) { (el.listeners[type] = el.listeners[type] || []).push(fn); },
      removeEventListener() {},
      setAttribute(name, value) {
        if (name === 'id') el.id = value;
        else if (name.startsWith('data-')) el.dataset[name.slice(5)] = value;
        else el['attr_' + name] = value;
      },
      getAttribute(name) { return el['attr_' + name] ?? null; },
      append(...nodes) { for (const n of nodes) { n.parentElement = el; el.children.push(n); allElements.push(n); } },
      appendChild(n) { n.parentElement = el; el.children.push(n); allElements.push(n); return n; },
      insertBefore(n, ref) { n.parentElement = el; const i = ref ? el.children.indexOf(ref) : -1; if (i >= 0) el.children.splice(i, 0, n); else el.children.push(n); allElements.push(n); return n; },
      prepend(n) { n.parentElement = el; el.children.unshift(n); allElements.push(n); return n; },
      remove() { el.parentElement = null; },
      contains() { return false; },
      getBoundingClientRect() { return { width: 800, height: 600, top: 0, left: 0 }; },
      getContext() { return new Proxy({}, { get: (t, k) => (k === 'canvas' ? el : () => {}), set: () => true }); },
      querySelector(sel) {
        const cls = sel.startsWith('.') ? sel.slice(1) : null;
        if (cls) return el.children.find((c) => String(c.className).split(' ').includes(cls)) ?? null;
        return null;
      },
      querySelectorAll(sel) {
        if (sel.includes('button[data-ext-action]')) {
          return allElements.filter((x) => x.tagName === 'BUTTON' && x.dataset.extAction);
        }
        if (sel.startsWith('.')) {
          const cls = sel.slice(1);
          return allElements.filter((x) => String(x.className).split(' ').includes(cls));
        }
        return [];
      },
      click() { (el.listeners.click || []).forEach((fn) => fn({ key: '', preventDefault() {}, stopPropagation() {} })); },
      removeAttribute() {}, setAttributeNS() {}, getRootNode() { return el; },
      closest() { return null; }, matches() { return false; }, focus() {}, blur() {},
    };
    Object.defineProperty(el, 'id', {
      get: () => el._id,
      set(value) { el._id = value; if (value) registry.set(value, el); },
    });
    Object.defineProperty(el, 'innerHTML', {
      get: () => el._innerHTML,
      set(value) {
        el._innerHTML = String(value);
        for (const m of String(value).matchAll(/id="([^"]+)"/g)) registry.set(m[1], el);
        for (const m of String(value).matchAll(/<button[^>]*data-ext-action="([^"]*)"[^>]*>/g)) {
          const button = makeElement('button');
          button.dataset.extAction = m[1];
          const nameMatch = m[0].match(/data-ext-name="([^"]*)"/);
          button.dataset.extName = nameMatch ? nameMatch[1] : '';
          button._ownerList = el;
          allElements.push(button);
        }
      },
    });
    return el;
  }

  const sideBar = makeElement('div'); sideBar.id = 'menu-page-side-bar'; registry.set(sideBar.id, sideBar);
  const content = makeElement('div'); content.id = 'menu-page-content'; registry.set(content.id, content);
  function nativeTab(title) {
    const el = makeElement('div');
    el.className = 'side-menu-element';
    const titleEl = makeElement('div'); titleEl.className = 'side-menu-title'; titleEl.textContent = title;
    el.appendChild(titleEl);
    allElements.push(el);
    return el;
  }
  const mapsTab = nativeTab('Карты');
  sideBar.appendChild(mapsTab);
  sideBar.appendChild(nativeTab('Настройки'));
  // нативная страница игры внутри menu-page-content — индикатор переключаемости вкладок
  const nativePage = makeElement('div');
  nativePage._name = 'native-maps-page';
  content.appendChild(nativePage);

  const xhrResponses = {
    '/__la_builtin_state': builtinState,
    '/__la_extension': extensionCode,
  };
  class FakeXHR {
    open(method, url) { this.url = url; }
    send() {
      this.status = Object.prototype.hasOwnProperty.call(xhrResponses, this.url) ? 200 : 404;
      this.responseText = xhrResponses[this.url] ?? '';
    }
  }
  class FakeMutationObserver { observe() {} disconnect() {} }
  class FakeCustomEvent {
    constructor(type, options) { this.type = type; this.detail = options?.detail; }
  }

  const sandbox = {
    console,
    document: {
      getElementById: (id) => registry.get(id) ?? null,
      createElement: (tag) => makeElement(tag),
      querySelector: () => null,
      querySelectorAll: () => [],
      addEventListener() {},
      documentElement: makeElement('html'),
      body: makeElement('body'),
      title: '',
      visibilityState: 'visible',
    },
    location: { pathname: '/', href: 'https://logic-arrows.io/', reload() {} },
    history: { pushState() {}, replaceState() {} },
    XMLHttpRequest: FakeXHR,
    MutationObserver: FakeMutationObserver,
    CustomEvent: FakeCustomEvent,
    PopStateEvent: FakeCustomEvent,
    MessageEvent: FakeCustomEvent,
    requestAnimationFrame: (fn) => { fn(); return 0; },
    setTimeout: () => 0,
    clearTimeout() {},
    setInterval: () => 0,
    clearInterval() {},
    performance: { now: () => Date.now() },
    addEventListener(type, fn) { (windowListeners[type] = windowListeners[type] || []).push(fn); },
    removeEventListener() {},
    dispatchEvent(event) { (windowListeners[event.type] || []).forEach((fn) => fn(event)); return true; },
    fetch: () => new Promise(() => {}),
    chrome: { webview: { postMessage: (msg) => posted.push(msg) } },
    Image: class { addEventListener() {} set src(v) {} },
    devicePixelRatio: 1,
    navigator: { userAgent: 'test' },
  };
  sandbox.window = sandbox;
  sandbox.globalThis = sandbox;
  sandbox.self = sandbox;
  vm.createContext(sandbox);
  vm.runInContext(source, sandbox, { filename: 'MapBridgeScript.Source' });

  return { sandbox, registry, allElements, posted, windowListeners, sideBar, content, mapsTab, nativePage, byId: (id) => registry.get(id) ?? null };
}

// ================= Мир 1: встроенное расширение активно =================
const w1 = makeWorld('1', 'globalThis.__laExtensionRan = 42;');

check('мост исполняется без ошибок', true);
check('вкладка «Расширения» создана в сайдбаре', !!w1.byId('side-menu-extensions-btn'));
check('вкладка «Превью» создана в сайдбаре', !!w1.byId('side-menu-preview-btn'));
check('вкладка «Расширения» в самом низу сайдбара', w1.sideBar.children[w1.sideBar.children.length - 1]?.id === 'side-menu-extensions-btn');
const iconEl = w1.byId('side-menu-extensions-btn').children.find((c) => c.className === 'side-menu-icon');
check('иконка вкладки — inline SVG с явным stroke', iconEl && iconEl.tagName === 'DIV' && String(iconEl?._innerHTML).includes('stroke="#e8edf7"') && !String(iconEl?._innerHTML).includes('currentColor'));
check('у вкладки «Расширения» ровно один клик-обработчик', (w1.byId('side-menu-extensions-btn').listeners.click || []).length === 1);

w1.byId('side-menu-extensions-btn').click();
check('страница расширений открылась', !!w1.byId('logic-extensions-page-container'));
check('запрошен список расширений у лаунчера', w1.posted.some((m) => m.type === 'extensions-list-request'));
check('в странице есть строка диагностики', !!w1.byId('logic-ext-diag'));

w1.sandbox.dispatchEvent(new w1.sandbox.CustomEvent('la-extensions-state', {
  detail: { version: '1.4.9', builtInActive: true, entries: [] },
}));
const listHtml1 = w1.byId('logic-ext-list')?._innerHTML ?? '';
check('встроенное расширение показано как «Активно»', listHtml1.includes('Активно'));
check('у активного встроенного нет кнопки «Включить»', !listHtml1.includes('data-ext-action="builtin"'));

w1.sandbox.dispatchEvent(new w1.sandbox.CustomEvent('la-extensions-state', {
  detail: { version: '1.4.9', builtInActive: false, entries: [{ name: 'MyMod', path: '/mods/MyMod', enabled: true, missing: false }] },
}));
const listHtml2 = w1.byId('logic-ext-list')?._innerHTML ?? '';
check('встроенное показано выключенным с кнопкой «Включить»', listHtml2.includes('data-ext-action="builtin"'));
check('стороннее расширение отображается в списке', listHtml2.includes('MyMod'));

const builtinEnable = w1.allElements.find((x) => x.tagName === 'BUTTON' && x.dataset.extAction === 'builtin');
builtinEnable.click();
check('«Включить» встроенного шлёт extensions-set-active __builtin__', w1.posted.some((m) => m.type === 'extensions-set-active' && m.name === '__builtin__' && m.enabled === true));

const disableBtn = w1.allElements.find((x) => x.tagName === 'BUTTON' && x.dataset.extAction === 'disable' && x.dataset.extName === 'MyMod');
disableBtn.click();
check('«Выключить» стороннего шлёт extensions-set-active enabled=false', w1.posted.some((m) => m.type === 'extensions-set-active' && m.name === 'MyMod' && m.enabled === false));

check('флаг встроенного получен от хоста', w1.sandbox.__laBuiltinEnabled === true);
check('код пользовательского расширения исполнен до скриптов игры', w1.sandbox.__laExtensionRan === 42);

// ================= Мир 2: стороннее расширение активно (встроенное выключено) =================
const w2 = makeWorld('0', 'globalThis.__laExtensionRan = 42;');
check('мир 2: флаг встроенного = false', w2.sandbox.__laBuiltinEnabled === false);
check('мир 2: код стороннего расширения исполнен', w2.sandbox.__laExtensionRan === 42);
check('мир 2: вкладка «Расширения» создана', !!w2.byId('side-menu-extensions-btn'));
check('мир 2: вкладка «Превью» не создаётся (встроенное выключено)', !w2.byId('side-menu-preview-btn'));

// пользователь открывает вкладку «Расширения»
w2.byId('side-menu-extensions-btn').click();
const extPage2 = w2.byId('logic-extensions-page-container');
check('мир 2: страница расширений открылась', !!extPage2);
check('мир 2: нативная страница игры скрыта нашей', w2.nativePage.style.display === 'none');

// КЛЮЧЕВАЯ РЕГРЕССИЯ: клик по нативной вкладке должен вернуть игру (переключение работает)
w2.mapsTab.click();
check('мир 2: клик по нативной вкладке возвращает её страницу (display снят)', w2.nativePage.style.display !== 'none');
check('мир 2: страница расширений при этом скрыта', (extPage2?.style?.display ?? '') === 'none' || !extPage2);
check('мир 2: вкладки переключаются туда и обратно', (() => {
  w2.byId('side-menu-extensions-btn').click();
  const hiddenAgain = w2.nativePage.style.display === 'none';
  w2.mapsTab.click();
  return hiddenAgain && w2.nativePage.style.display !== 'none';
})());

console.log(`\nИТОГ: ${pass} из ${total}`);
process.exit(pass === total ? 0 : 1);
