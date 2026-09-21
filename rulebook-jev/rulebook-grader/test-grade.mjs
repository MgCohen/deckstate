import { readFile } from 'node:fs/promises';
import { gradeAll } from './lib/jev.mjs';
import { numberRules } from './lib/pipeline.mjs';

const p = JSON.parse(await readFile(new URL('./sample.json', import.meta.url)));
const rc = numberRules(p.rules);
const res = await gradeAll(p.content, p.rubrics, rc, { apiKey: process.env.TYPESAFE_API_KEY });

const rubricTitle = Object.fromEntries(p.rubrics.map((r) => [r.id, r.title]));
const contentTitle = Object.fromEntries(p.content.map((c) => [c.id, c.title]));
for (const g of res) {
  console.log(`\n${contentTitle[g.contentId]}`);
  for (const rr of g.rubricResults) {
    console.log(`  ${rr.passed ? 'PASS' : 'FAIL'}  p=${rr.probability.toFixed(3)}  ${rubricTitle[rr.rubricId]}`);
  }
}
