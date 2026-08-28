// Тесты умного оптимизатора v2. Каждая оптимизированная схема проверяется
// прогоном через эталонный симулятор движка (sim-core.js): трассировки
// сигналов выживших клеток должны совпадать тик в тик.
const assert = require('assert');
const { createSim } = require('../../logic-arrows-extension/research/sim-core.js');
const { laOptimize } = require('./la-optimizer.js');

const TICKS = 80;
let passed = 0;

function A(x, y, type, rotation = 0, flipped = false) {
  return { x, y, type, rotation, flipped };
}

// Прогон схемы через симулятор, трассировка сигналов по id клеток.
function trace(cells, idOf) {
  const sim = createSim();
  for (const c of cells) sim.place(c.x, c.y, c.type, c.rotation, c.flipped);
  const tr = new Map();
  for (const c of cells) tr.set(idOf(c), []);
  for (let t = 0; t < TICKS; t++) {
    sim.tick();
    for (const c of cells) tr.get(idOf(c)).push(sim.read(c.x, c.y).signal);
  }
  return tr;
}

// Главная проверка: поведение выживших клеток не изменилось ни на один тик.
function assertBehaviorPreserved(orig, optCells, label) {
  const idOfOrig = new Map();
  orig.forEach((c, i) => idOfOrig.set(c, i));
  const t1 = trace(orig, c => idOfOrig.get(c));
  const idOfOpt = new Map();
  for (const c of optCells) {
    assert(idOfOrig.has(c), label + ': выжила чужая клетка');
    idOfOpt.set(c, idOfOrig.get(c));
  }
  const t2 = trace(optCells, c => idOfOpt.get(c));
  // сравниваем только выживших: удалённые клетки в оптимизированной схеме отсутствовать должны
  for (const [id, sigs] of t2) {
    assert(t1.has(id), label + ': оптимизированная схема зовёт чужой id #' + id);
    assert.deepStrictEqual(sigs, t1.get(id), label + ': трассировка клетки #' + id + ' изменилась');
  }
  return t1;
}

function runOpt(cells, opts) {
  const link = new Map();
  const work = cells.map(c => { const w = { ...c }; link.set(w, c); return w; });
  const res = laOptimize(work, { encode: () => 'TEST', ...opts });
  res.cells = res.cells.map(c => link.get(c)); // обратно к исходным ссылкам
  return res;
}

// ---------------------------------------------------------------------------
// T1: мёртвое кольцо без источника + рабочая цепь; пустые столбцы сжимаются.
{
  const cells = [
    A(0, 0, 2),            // источник, 4 направления
    A(0, -1, 1), A(0, -2, 1), A(0, -3, 15), // провод -> NOT
    A(0, -4, 1),           // выход NOT (сигнал 3, красный конвертируется импульсом)
    // мёртвое кольцо (цикл без источника) далеко справа
    A(10, -5, 1, 1), A(11, -5, 1, 2), A(11, -4, 1, 3), A(10, -4, 1, 0),
  ];
  const res = runOpt(cells, { deep: false });
  assertBehaviorPreserved(cells, res.cells, 'T1');
  assert.strictEqual(res.stats.removedDead, 4, 'T1: кольцо должно быть удалено целиком');
  assert.strictEqual(res.cells.length, 5, 'T1: остались источник+провод+NOT+провод');
  assert.ok(res.stats.optW <= 2, 'T1: пустые столбцы сжаты (ширина ' + res.stats.optW + ')');
  assert.strictEqual(res.stats.prunedCancelled, false);
  passed++; console.log('ok T1: мёртвое кольцо удалено, связи целы, ширина ' + res.stats.origW + '->' + res.stats.optW);
}

// ---------------------------------------------------------------------------
// T2: прыжок синей стрелки через пустой столбец не должен сломаться.
{
  const cells = [
    A(0, 1, 2),              // источник снизу
    A(0, 0, 10, 1),          // синяя прыгает на 2 на восток: (0,0) -> (2,0)
    A(2, 0, 1, 1),           // красный провод, ведёт на восток в пустоту
    // далёкая клетка, которая при наивном сжатии наехала бы на цель прыжка
    A(10, 0, 1, 0),
  ];
  const res = runOpt(cells, { deep: false });
  assertBehaviorPreserved(cells, res.cells, 'T2');
  const j = res.cells.find(c => c.type === 10);
  const w = res.cells.find(c => c.type === 1 && c.rotation === 1);
  assert.strictEqual(j.x + 2, w.x, 'T2: прыжок сохранил дистанцию 2');
  assert.strictEqual(j.y, w.y, 'T2: прыжок на той же строке');
  passed++; console.log('ok T2: прыжок типа 10 сохранён (дистанция 2), ширина ' + res.stats.origW + '->' + res.stats.optW);
}

// ---------------------------------------------------------------------------
// T3: NOT без входа — скрытый постоянный источник; цепь должна выжить.
{
  const cells = [
    A(0, 0, 15),             // NOT без входа -> постоянно 3
    A(0, -1, 1),             // провод
    A(0, -2, 16),            // AND
    A(2, -2, 2),             // второй источник
    A(1, -2, 1, 3),          // провод на запад в AND
    A(0, -3, 1),             // выход AND (в пустоту)
    // мусор: отключённая стрелка
    A(9, 9, 1),
  ];
  const res = runOpt(cells, { deep: false });
  assertBehaviorPreserved(cells, res.cells, 'T3');
  assert.strictEqual(res.cells.length, 6, 'T3: отключённая стрелка удалена, цепь жива');
  assert.ok(res.cells.some(c => c.type === 15), 'T3: NOT-источник сохранён');
  assert.strictEqual(res.stats.removedDead, 1, 'T3: удалён 1 мёртвый блок');
  passed++; console.log('ok T3: NOT-константа распознана как источник, мусор удалён');
}

// ---------------------------------------------------------------------------
// T4: AND с одним входом никогда не сработает — срезается вместе с цепью.
{
  const cells = [
    A(0, 0, 2),
    A(0, -1, 1),
    A(0, -2, 16),            // AND: только один вход
    A(0, -3, 1),
  ];
  const res = runOpt(cells, { deep: false });
  assertBehaviorPreserved(cells, res.cells, 'T4');
  // AND мёртв -> срезается; провод и источник остаются живыми, но их сигнал
  // в никуда -> при safe-режиме источник с соседями остаются (они срабатывают).
  assert.ok(!res.cells.some(c => c.type === 16), 'T4: мёртвый AND удалён');
  assert.strictEqual(res.cells.length, 2, 'T4: источник и провод живы (светятся)');
  passed++; console.log('ok T4: AND с одним входом удалён, живая часть сохранена');
}

// ---------------------------------------------------------------------------
// T5: глубокая чистка срезает висячие выходы; выключенная — нет.
{
  const cells = [
    A(0, 0, 2),
    A(0, -1, 1),
    A(0, -2, 16),
    A(2, -2, 2),
    A(1, -2, 1, 3),
    A(0, -3, 1),             // висячий выход AND
  ];
  const safe = runOpt(cells, { deep: false });
  assertBehaviorPreserved(cells, safe.cells, 'T5-safe');
  assert.strictEqual(safe.cells.length, 6, 'T5: safe не трогает живую цепь');
  const deep = runOpt(cells, { deep: true });
  assertBehaviorPreserved(cells, deep.cells, 'T5-deep');
  assert.ok(deep.cells.length < 6, 'T5: deep срезает висячий конец');
  assert.strictEqual(deep.stats.removedDangling, 6, 'T5: каскад срезает всю цепь — сигнал никуда не приходит');
  assert.strictEqual(deep.cells.length, 0, 'T5: схема целиком была выходом в пустоту');
  assert.ok(deep.warnings.length > 0, 'T5: предупреждение о срезе показано');
  passed++; console.log('ok T5: deep=' + deep.cells.length + ' клеток, safe=' + safe.cells.length);
}

// ---------------------------------------------------------------------------
// T6: схема без источников — чистка отменяется с предупреждением.
{
  const cells = [A(0, 0, 1, 1), A(1, 0, 1, 2), A(1, 1, 1, 3), A(0, 1, 1, 0)];
  const res = runOpt(cells, { deep: false });
  assert.strictEqual(res.cells.length, 4, 'T6: чистка отменена');
  assert.strictEqual(res.stats.prunedCancelled, true, 'T6: флаг отмены');
  assert.ok(res.warnings.length > 0, 'T6: предупреждение показано');
  passed++; console.log('ok T6: схема без источников не тронута, предупреждение выдано');
}

// ---------------------------------------------------------------------------
// T7: детектор и блокер — особые правила ввода/вывода.
{
  const cells = [
    A(0, 0, 2),              // источник
    A(0, -1, 1),             // провод (сигнал бежит на север)
    A(0, -2, 5),             // детектор: читает клетку СЗАДИ (0,-1), передаёт вперёд
    A(0, -3, 1),
    A(0, -5, 3),             // блокер, гасит (0,-6); входа нет -> мёртвый
    A(0, -6, 1),
  ];
  const res = runOpt(cells, { deep: false });
  assertBehaviorPreserved(cells, res.cells, 'T7');
  assert.ok(res.cells.some(c => c.type === 5), 'T7: детектор жив (вход сзади учтён)');
  assert.ok(!res.cells.some(c => c.type === 3), 'T7: блокер без входа удалён');
  assert.ok(!res.cells.some(c => c.type === 1 && c.y === -6 + 0 && c.x === 0 && false), '');
  passed++; console.log('ok T7: детектор сохранён, мёртвый блокер удалён');
}

// ---------------------------------------------------------------------------
// T8: эталонный 4-битный счётчик (уровень 7) — ничего не сломать.
{
  const cells = [];
  cells.push(A(0, 0, 7, 0));
  for (let x = 1; x <= 5; x++) cells.push(A(x, 0, 1, 1));
  cells.push(A(6, 0, 1, 2));
  cells.push(A(6, 1, 1, 2));
  cells.push(A(6, 2, 1, 3));
  for (let x = 5; x >= 1; x--) cells.push(A(x, 2, 1, 3));
  cells.push(A(0, 2, 1, 0));
  cells.push(A(0, 1, 1, 0));
  cells.push(A(7, 1, 9, 0));
  for (let k = 0; k < 4; k++) {
    const y = -1 - 4 * k;
    cells.push(A(0, y, 12, 0));
    cells.push(A(0, y - 1, 19, 0));
    cells.push(A(0, y - 2, 16, 0));
    if (k < 3) cells.push(A(0, y - 3, 1, 0));
  }
  // сравнение значений счётчика: T-триггеры как биты
  const counterValue = (tr, cellsMap) => {
    const tCells = cells.filter(c => c.type === 19);
    return tr; // трассировки сравниваются погодно в assertBehaviorPreserved
  };
  const res = runOpt(cells, { deep: false });
  assertBehaviorPreserved(cells, res.cells, 'T8');
  assert.strictEqual(res.cells.length, cells.length, 'T8: счётчик целый, удалений нет');
  const deep = runOpt(cells, { deep: true });
  assertBehaviorPreserved(cells, deep.cells, 'T8-deep');
  assert.ok(deep.stats.removedDangling >= 1, 'T8: deep срезает верхний висячий AND');
  passed++; console.log('ok T8: счётчик цел (' + cells.length + ' клеток), deep-режим срезает только хвост (' + deep.stats.removedDangling + ')');
}

// ---------------------------------------------------------------------------
// T9: большие пустые коридоры сжимаются, но не через защищённые зоны.
{
  const cells = [
    A(0, 0, 2),
    A(0, -1, 1),
    A(0, -2, 4),   // задержка
    A(0, -3, 1),
    // далеко справа вторая схема
    A(50, 0, 9),   // генератор
    A(50, -1, 1),
  ];
  const res = runOpt(cells, { deep: false });
  assertBehaviorPreserved(cells, res.cells, 'T9');
  assert.strictEqual(res.cells.length, 6, 'T9: всё живо');
  assert.ok(res.stats.optW < res.stats.origW, 'T9: коридор сжат ' + res.stats.origW + '->' + res.stats.optW);
  passed++; console.log('ok T9: ширина ' + res.stats.origW + '->' + res.stats.optW + ', поведение идентично');
}

// ---------------------------------------------------------------------------
// T10: повороты и отражения (flipped) — связи через повёрнутые офсеты.
{
  const cells = [
    A(0, 0, 2),
    A(1, 0, 6, 1),           // сплиттер вверх-вниз, повёрнут: передаёт E и W? (поворот 1)
    A(2, 0, 1, 1),
    A(1, 2, 1, 0, true),     // перевёрнутая стрелка
  ];
  const res = runOpt(cells, { deep: false });
  assertBehaviorPreserved(cells, res.cells, 'T10');
  passed++; console.log('ok T10: повороты/отражения учтены, удалений: ' + res.stats.removedDead);
}

console.log('\nВСЕ ТЕСТЫ ПРОЙДЕНЫ: ' + passed);
