#!/usr/bin/env node
// run.mjs — CLI for the jev → Claude grading loop.
//
//   TYPESAFE_API_KEY=... ANTHROPIC_API_KEY=... node run.mjs [payload.json]
//
// Flags:
//   --grade-only     run jev grading only, skip the Claude feedback step
//   --threshold=0.5  probability at/above which a rubric passes (default 0.5)
//   --json           print the full result object as JSON
//
// Reads ./sample.json when no payload path is given.

import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';
import { runPipeline } from './lib/pipeline.mjs';
import { makeAnthropicAnalyze } from './lib/analyze.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const flag = (name) => args.includes(`--${name}`);
const opt = (name, def) => {
  const hit = args.find((a) => a.startsWith(`--${name}=`));
  return hit ? hit.split('=')[1] : def;
};
const payloadPath = args.find((a) => !a.startsWith('--')) || resolve(here, 'sample.json');

const tone = (n) => (n === 100 ? '\\x1b[32m' : n >= 50 ? '\\x1b[33m' : '\\x1b[31m');
const R = '\\x1b[0m';

async function main() {
  const payload = JSON.parse(await readFile(payloadPath, 'utf8'));
  const analyze = flag('grade-only')
    ? undefined
    : makeAnthropicAnalyze(); // needs ANTHROPIC_API_KEY

  const result = await runPipeline(payload, {
    jev: {
      apiKey: process.env.TYPESAFE_API_KEY,
      threshold: Number(opt('threshold', '0.5')),
    },
    analyze,
  });

  if (flag('json')) {
    console.log(JSON.stringify(result, null, 2));
    return;
  }

  const rubricTitle = Object.fromEntries(payload.rubrics.map((r) => [r.id, r.title]));
  const contentTitle = Object.fromEntries(payload.content.map((c) => [c.id, c.title]));

  console.log(`\\n  Overall  ${tone(result.overallScore)}${result.overallScore}%${R}\\n`);
  for (const c of result.contentResults) {
    console.log(`  ${contentTitle[c.contentId]}  ${tone(c.score)}${c.score}%${R}  (${c.passed}/${c.total})`);
    for (const rr of c.rubricResults) {
      const mark = rr.passed ? '\\x1b[32m✓\\x1b[0m' : '\\x1b[31m✗\\x1b[0m';
      const p = `p=${rr.probability.toFixed(2)}`;
      console.log(`    ${mark} ${rubricTitle[rr.rubricId].padEnd(20)} ${p}`);
      if (!rr.passed && rr.feedback) console.log(`        ↳ ${rr.feedback}`);
    }
    console.log('');
  }
}

main().catch((e) => {
  console.error('\\x1b[31m' + (e.stack || e.message) + '\\x1b[0m');
  process.exit(1);
});
