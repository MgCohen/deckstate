// pipeline.mjs — the loop, end to end.
//   1. jev grades every rubric for every content item (pass/fail).
//   2. every FAILED rubric is thrown at `analyze` to get written feedback.
//   3. scores are computed deterministically from the booleans (same as the artifact).

import { gradeAll } from './jev.mjs';

/** number the rules the way the artifact does: 1, 1.1, 1.1.1 ... */
export function numberRules(rules, prefix = '') {
  const out = [];
  rules.forEach((r, i) => {
    const number = prefix ? `${prefix}.${i + 1}` : String(i + 1);
    out.push({ number, title: r.title, text: r.text });
    if (r.children && r.children.length) out.push(...numberRules(r.children, number));
  });
  return out;
}

/**
 * @param payload { rules, rubrics:[{id,title,passWhen}], content:[{id,title,text}] }
 * @param opts { jev, analyze, concurrency }
 *   jev     — options forwarded to the grader ({ apiKey, model, threshold })
 *   analyze — async (failure) => feedbackString  (the Claude step)
 * @returns { overallScore, contentResults:[{contentId, passed, total, score,
 *            rubricResults:[{rubricId, passed, probability, feedback}]}] }
 */
export async function runPipeline(payload, { jev = {}, analyze } = {}) {
  const rulesContext = numberRules(payload.rules || []);
  const graded = await gradeAll(payload.content, payload.rubrics, rulesContext, jev);

  // Collect every failed (content, rubric) pair, then analyze them.
  const rubricById = Object.fromEntries(payload.rubrics.map((r) => [r.id, r]));
  const contentById = Object.fromEntries(payload.content.map((c) => [c.id, c]));

  const contentResults = [];
  for (const g of graded) {
    const content = contentById[g.contentId];
    const rubricResults = [];
    for (const rr of g.rubricResults) {
      let feedback = '';
      if (!rr.passed && analyze) {
        feedback = await analyze({
          content: { title: content.title, text: content.text },
          rubric: rubricById[rr.rubricId],
          rulesContext,
          probability: rr.probability,
        });
      }
      rubricResults.push({ ...rr, feedback });
    }
    const passed = rubricResults.filter((r) => r.passed).length;
    const total = rubricResults.length;
    contentResults.push({
      contentId: g.contentId,
      passed,
      total,
      score: total ? Math.round((passed / total) * 100) : 0,
      rubricResults,
    });
  }

  const overallScore = contentResults.length
    ? Math.round(contentResults.reduce((s, c) => s + c.score, 0) / contentResults.length)
    : 0;
  return { overallScore, contentResults };
}
