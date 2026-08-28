// ============================================================================
// Logic Arrows — умный оптимизатор схем v2.
// Основан на точных правилах движка игры (bundle.js v1.4, module ChunkUpdates;
// эталонная реплика: research/sim-core.js). Понимает механику всех типов
// стрелок: офсеты передачи, прыжки сплиттеров/синих стрелок на 2 клетки,
// детектор (читает клетку сзади), блокер (гасит стрелку перед собой),
// NOT-гейт (без входа постоянно выдаёт сигнал — скрытый источник),
// защёлку и AND (нужны 2 одновременных входа), генераторы/кнопки.
//
// Этапы:
//   1. Дедупликация координат.
//   2. Безопасная чистка: удаляет только клетки, которые НЕ МОГУТ никогда
//      сработать (нет пути от источника сигнала). Работающая схема не страдает.
//   3. Глубокая чистка (по флагу): срезает «висячие выходы» — стрелки, чей
//      сигнал ни к кому не приходит. ВНИМАНИЕ: срезает и осмысленные
//      индикаторные концы цепочек, поэтому выключена по умолчанию.
//   4. Сжатие пустот с сохранением ВСЕХ связей: удаляет пустые ряды/столбцы
//      только если ни одна стрелка не потеряет и не приобретёт соединение
//      (учитываются прыжки на 2 клетки и тайминги — расстояния не меняются).
// ============================================================================

const LA_OFF = {
  1: [[-1, 0]], 2: [[-1, 0], [0, 1], [1, 0], [0, -1]], 3: [], 4: [[-1, 0]], 5: [[-1, 0]],
  6: [[-1, 0], [1, 0]], 7: [[-1, 0], [0, 1]], 8: [[-1, 0], [0, 1], [0, -1]],
  9: [[-1, 0], [0, 1], [1, 0], [0, -1]], 10: [[-2, 0]], 11: [[-1, 1]], 12: [[-1, 0], [-2, 0]],
  13: [[-2, 0], [0, 1]], 14: [[-1, 0], [-1, 1]], 15: [[-1, 0]], 16: [[-1, 0]], 17: [[-1, 0]],
  18: [[-1, 0]], 19: [[-1, 0]], 20: [[-1, 0]], 21: [[-1, 0], [0, 1], [1, 0], [0, -1]],
  22: [[-1, 0]], 24: [[-1, 0]]
};

// Типы-источники: сигнал появляется без внешнего входа.
// 2 — источник, 9 — генератор импульсов (взводится сам), 21/24 — кнопки (клик).
const LA_SOURCES = new Set([2, 9, 21, 24]);
// AND (16) и защёлка (18) требуют два одновременных входных импульса.
const LA_MIN_INPUTS = { 16: 2, 18: 2 };

// Защищённые типы: 23 — цель уровня, 25 — декоративная стрелка («Does nothing»
// в бандле, голубая — из неё рисуют пиксель-арт). Любой НЕИЗВЕСТНЫЙ тип тоже
// считаем декором: лучше сохранить лишнее, чем удалить работающую схему.
const LA_KNOWN_TYPES = new Set([...Object.keys(LA_OFF).map(Number), 25]);
function laIsDecor(type) {
  return type === 25 || type === 23 || !LA_KNOWN_TYPES.has(type);
}

// Цель смещения в глобальных координатах (реплика h() из бандла).
function laRelTarget(cell, dx, dy) {
  const c = cell.flipped ? -dy : dy;
  const r = cell.rotation & 3;
  if (r === 0) return [cell.x + c, cell.y + dx];
  if (r === 1) return [cell.x - dx, cell.y + c];
  if (r === 2) return [cell.x - c, cell.y - dx];
  return [cell.x + dx, cell.y - c];
}

// Все смещения, влияющие на механику: передача сигнала + особые правила.
function laMechOffsets(cell) {
  const list = (LA_OFF[cell.type] || []).slice();
  if (cell.type === 5) list.push([1, 0]);   // детектор читает клетку сзади
  if (cell.type === 3) list.push([-1, 0]);  // блокер гасит стрелку перед собой
  return list;
}

// Смещения-«выходы» (что клетка делает с миром): передача + гашение блокера.
function laOutOffsets(cell) {
  const list = (LA_OFF[cell.type] || []).slice();
  if (cell.type === 3) list.push([-1, 0]);
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

// Расчёт множества клеток, которые могут хоть когда-нибудь сработать
// (получить сигнал, равный REQ их типа, и передать/действовать).
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

// Этап 2: удалить клетки, которые никогда не сработают (декор не трогаем).
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

// Этап 3 (опция): срезать клетки, чей сигнал ни к кому не приходит (декор не трогаем).
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

// Этап 4: сжатие пустот, сохраняющее все соединения и расстояния-прыжки.
// Удаляем пустые столбцы/ряды, затем точно проверяем два инварианта:
//   a) каждая существующая связь сохраняет точное смещение;
//   b) «пустые» цели смещений остаются пустыми (не возникает новых связей).
// Нарушения чиним возвратом отдельных столбцов/рядов.
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

  // Ограничения по исходным координатам.
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

  const less = (S, arr, v) => { let n2 = 0; for (const s of arr) if (s < v) n2++; return n2; };
  let arrX = [...Sx].sort((p, q) => p - q);
  let arrY = [...Sy].sort((p, q) => p - q);
  const Mx = v => v - less(Sx, arrX, v);
  const My = v => v - less(Sy, arrY, v);

  const repairBetween = (a, b) => {
    const loX = Math.min(a.x, b.x), hiX = Math.max(a.x, b.x);
    for (let g = loX; g < hiX; g++) if (Sx.has(g)) { Sx.delete(g); arrX = [...Sx].sort((p, q) => p - q); return true; }
    const loY = Math.min(a.y, b.y), hiY = Math.max(a.y, b.y);
    for (let g = loY; g < hiY; g++) if (Sy.has(g)) { Sy.delete(g); arrY = [...Sy].sort((p, q) => p - q); return true; }
    return false;
  };

  let fail = null;
  for (let iter = 0; iter < 200000; iter++) {
    fail = null;
    for (const { a, b, ox, oy } of consOcc) {
      if (Mx(b.x) - Mx(a.x) !== ox || My(b.y) - My(a.y) !== oy) { fail = { pair: [a, b] }; break; }
    }
    if (!fail) {
      const img = new Map(); // key -> cell
      for (const k of cells) img.set(Mx(k.x) + ',' + My(k.y), k);
      for (const { a, ox, oy } of consVoid) {
        const hit = img.get((Mx(a.x) + ox) + ',' + (My(a.y) + oy));
        if (hit) { fail = { pair: [a, hit] }; break; }
      }
    }
    if (!fail) break;
    if (!repairBetween(fail.pair[0], fail.pair[1])) return { ok: false };
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

// Подсчёт «дальних связей» (прыжки на 2 клетки) для статистики.
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

// Главный вход. cells: [{x, y, type, rotation, flipped}] — мутируются координаты
// выживших клеток. Возвращает { cells, base64, stats } (base64 заполняет колбэк).
function laOptimize(cells, options) {
  const opts = options || {};
  const encode = opts.encode || (list => '');
  const warnings = [];
  const stats = {
    origCells: cells.length,
    removedDead: 0,
    removedDangling: 0,
    removedSources: 0,
    prunedCancelled: false,
    deletedCols: 0,
    deletedRows: 0,
    longLinks: 0,
    origW: 0, origH: 0, optW: 0, optH: 0,
    reduction: 0
  };

  // 0. Дедупликация (последняя по списку побеждает).
  const byCoord = new Map();
  for (const c of cells) byCoord.set(laKey(c.x, c.y), c);
  let work = [...byCoord.values()];
  stats.duplicateCells = cells.length - work.length;

  const bbox0 = laBBox(work);
  stats.origW = bbox0.w; stats.origH = bbox0.h;
  const origArea = Math.max(1, bbox0.w * bbox0.h);

  // 1. Безопасная чистка (никогда не срабатывающие).
  let result = laSafePrune(work);
  stats.removedDead = result.removed.length;

  // Защита: если чистка удалила ВСЁ — в схеме нет ни одного источника
  // сигнала. Скорее всего это не мусор, а особенность схемы — отменяем.
  if (result.kept.length === 0 && work.length > 0) {
    warnings.push('В схеме не найдено ни одного работающего источника сигнала ' +
      '(источник, генератор, кнопка или NOT без входа). Чистка отменена — удалён 0 блоков.');
    stats.prunedCancelled = true;
    result = { kept: work.slice(), removed: [] };
    stats.removedDead = 0;
  }

  // 2. Глубокая чистка (по флагу): висячие выходы.
  if (opts.deep && result.kept.length > 0) {
    const deep = laDeepTrim(result.kept);
    stats.removedDangling = deep.removed.length;
    result = { kept: deep.kept, removed: result.removed };
    if (deep.removed.length > 0) {
      warnings.push('Глубокая чистка срезала ' + deep.removed.length +
        ' блок(ов) с висячими выходами (сигнал ни к кому не приходил).');
    }
  }

  work = result.kept;

  // 3. Сжатие пустот с сохранением связей.
  if (work.length > 0) {
    const comp = laCompact(work);
    if (comp.ok) {
      stats.deletedCols = comp.deletedCols;
      stats.deletedRows = comp.deletedRows;
      laNormalize(work);
    } else {
      warnings.push('Сжатие пропущено: схема слишком плотно связана.');
      laNormalize(work);
    }
  }

  const bbox1 = laBBox(work);
  stats.optW = bbox1.w; stats.optH = bbox1.h;
  const optArea = Math.max(1, Math.max(1, bbox1.w) * Math.max(1, bbox1.h));
  stats.reduction = Math.max(0, Math.round((1 - optArea / origArea) * 1000) / 10);
  stats.optCells = work.length;
  stats.longLinks = laCountLongLinks(work);

  return { cells: work, base64: encode(work), stats, warnings };
}

if (typeof module !== 'undefined' && module.exports) {
  module.exports = { laOptimize, laRelTarget, laMechOffsets, laSafePrune, laDeepTrim, laCompact, laIsDecor, laDecorProtection, LA_OFF };
}
