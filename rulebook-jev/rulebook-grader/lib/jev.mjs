// jev.mjs — grade rubrics with TypeSafe's "jev" System One model.
// Each rubric becomes a `noul` (yes/no) question; jev returns a probability 0-1.
// No text feedback — that's by design; failed rubrics go to Claude afterward.

const SYSTEMONE_URL = 'https://api.typesafe.ai/v1/systemone';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/**
 * POST to jev with retry/backoff on 429/529.
 * @returns parsed JSON response
 */
async function callSystemOne(body, { apiKey, fetchImpl = fetch, maxRetries = 4 } = {}) {
  if (!apiKey) throw new Error('Missing TypeSafe API key (set TYPESAFE_API_KEY).');
  let attempt = 0;
  for (;;) {
    const res = await fetchImpl(SYSTEMONE_URL, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${apiKey}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });
    if (res.ok) return res.json();
    if ((res.status === 429 || res.status === 529) && attempt < maxRetries) {
      await sleep(2000 * 2 ** attempt); // 2s, 4s, 8s, 16s
      attempt += 1;
      continue;
    }
    const text = await res.text().catch(() => '');
    throw new Error(`jev ${res.status}: ${text || res.statusText}`);
  }
}

/**
 * Build the `questions` map: one noul question per rubric.
 * The rubric's pass condition is the instruction; criteria pins true/false meaning.
 */
function buildQuestions(rubrics) {
  const questions = {};
  for (const r of rubrics) {
    questions[r.id] = {
      type: 'noul',
      instructions: `${r.title}: ${r.passWhen}`,
      criteria: {
        true: 'The content clearly satisfies this, judged only on its written text.',
        false: 'The content does not clearly satisfy it — missing, ambiguous, or only true if you assume unstated details.',
      },
    };
  }
  return questions;
}

/**
 * Grade one content item against every rubric in a single jev call.
 * @returns { contentId, rubricResults: [{ rubricId, passed, probability }] }
 */
export async function gradeItem(item, rubrics, rulesContext, opts = {}) {
  const { model = 'jev-latest', threshold = 0.5 } = opts;
  const state = {
    rules_as_context: rulesContext, // context only — the standards the content should meet
    content: { title: item.title, text: item.text },
  };
  const data = await callSystemOne(
    { state, model, questions: buildQuestions(rubrics) },
    opts,
  );
  const answers = data.answers || {};
  const rubricResults = rubrics.map((r) => {
    const a = answers[r.id] || {};
    const probability = typeof a.noul === 'number' ? a.noul : 0;
    return { rubricId: r.id, passed: probability >= threshold, probability };
  });
  return { contentId: item.id, rubricResults, usage: data.usage };
}

/** Grade every content item. Sequential to stay gentle on rate limits. */
export async function gradeAll(content, rubrics, rulesContext, opts = {}) {
  const results = [];
  for (const item of content) {
    results.push(await gradeItem(item, rubrics, rulesContext, opts));
  }
  return results;
}
