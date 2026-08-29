// Регресс-тест: JS-исходник моста (MapBridgeScript.Source) обязан парситься.
// v1.4.3 не грузил расширения из-за await в синхронной функции — AddScriptToExecuteOnDocumentCreatedAsync
// молча отказывается исполнять скрипт с SyntaxError, поэтому ловим это до сборки.
// use: node tools/bridge-syntax-test.js
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const cs = fs.readFileSync(path.join(__dirname, '..', 'src', 'MapBridgeScript.cs'), 'utf8');
const match = cs.match(/Source\s*=\s*"""([\s\S]*?)"""/);
if (!match) {
  console.error('FAIL: raw string Source не найден в MapBridgeScript.cs');
  process.exit(1);
}
try {
  new vm.Script(match[1], { filename: 'MapBridgeScript.Source' });
  console.log('ok MapBridgeScript.Source парсится (' + match[1].length + ' байт)');
  // доп. защита: await не имеет права встречаться вне async-функций — vm.Script уже это ловит,
  // но проверим и типичный след: await внутри function, не помеченной async.
  const lines = match[1].split('\n');
  let depth = 0;
  const stack = []; // true = функция async
  let ok = true;
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    for (let j = 0; j < line.length; j++) {
      const ch = line[j];
      if (ch === '{') { stack.push(/async\s*(\([^)]*\)|[A-Za-z_$]+)\s*=>?\s*$/.test(line.slice(0, j).trim()) || false); depth++; }
      else if (ch === '}') { stack.pop(); depth--; }
    }
    if (/\bawait\b/.test(line) && !stack.some(Boolean) && !/async/.test(line)) {
      console.error('FAIL: await вне async-контекста, строка ' + (i + 1) + ': ' + line.trim());
      ok = false;
    }
  }
  if (!ok) process.exit(1);
  console.log('ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ');
} catch (error) {
  console.error('FAIL: SyntaxError в MapBridgeScript.Source:\n' + error.stack);
  process.exit(1);
}
