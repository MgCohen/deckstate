#!/usr/bin/env node
// jev-mcp — a tiny MCP server that exposes TypeSafe's "jev" grader as one tool.
//
// Transport: stdio, newline-delimited JSON-RPC 2.0 (no dependencies, so no
// `npm install` needed). Register it locally in the Claude desktop app and the
// artifact reaches it as `host:jev`. The same grading core deploys to Cloudflare
// later as a remote connector — only the transport wrapper changes.
//
// Env: TYPESAFE_API_KEY (required)   TYPESAFE_MODEL (default 'jev-latest')
//
// Tool: grade_rubrics(input) -> { contentResults: [...] }  (pass/fail + probability)

import process from 'node:process';

const API_URL = 'https://api.typesafe.ai/v1/systemone';
const MODEL = process.env.TYPESAFE_MODEL || 'jev-latest';
const log = (...a) => process.stderr.write(a.join(' ') + '\n'); // never stdout

// ---- jev grading core -----------------------------------------------------

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function systemOne(body, { apiKey, maxRetries = 4 } = {}) {
  if (!apiKey) throw new Error('Missing TYPESAFE_API_KEY');
  for (let attempt = 0; ; attempt++) {
    const res = await fetch(API_URL, {
      method: 'POST',
      headers: { Authorization: `Bearer ${apiKey}`, 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    if (res.ok) return res.json();
    if ((res.status === 429 || res.status === 529) && attempt < maxRetries) {
      await sleep(2000 * 2 ** attempt);
      continue;
    }
    throw new Error(`jev ${res.status}: ${(await res.text().catch(() => '')) || res.statusText}`);
  }
}

function questionsFor(rubrics) {
  const q = {};
  for (const r of rubrics) {
    q[r.id] = {
      type: 'noul',
      instructions: `${r.title ? r.title + ': ' : ''}${r.passWhen}`,
      criteria: {
        true: 'The content clearly satisfies this, judged only on its written text.',
        false: 'The content does not clearly satisfy it — missing, ambiguous, or only true if you assume unstated details.',
      },
    };
  }
  return q;
}

async function gradeRubrics({ rulesContext = [], rubrics, content, threshold = 0.5 }, apiKey) {
  if (!Array.isArray(rubrics) || !rubrics.length) throw new Error('rubrics[] required');
  if (!Array.isArray(content) || !content.length) throw new Error('content[] required');
  const contentResults = [];
  for (const item of content) {
    const data = await systemOne(
      {
        model: MODEL,
        state: { rules_as_context: rulesContext, content: { title: item.title, text: item.text } },
        questions: questionsFor(rubrics),
      },
      { apiKey },
    );
    const answers = data.answers || {};
    const rubricResults = rubrics.map((r) => {
      const p = typeof answers[r.id]?.noul === 'number' ? answers[r.id].noul : 0;
      return { rubricId: r.id, passed: p >= threshold, probability: p };
    });
    contentResults.push({ contentId: item.id, rubricResults });
  }
  return { contentResults };
}

// ---- MCP tool definition --------------------------------------------------

const TOOL = {
  name: 'grade_rubrics',
  description:
    'Grade content items against true/false rubrics using the jev classifier. ' +
    'Returns pass/fail plus a 0-1 probability per rubric per content item. No text feedback.',
  annotations: { readOnlyHint: true, title: 'Grade rubrics with jev' },
  inputSchema: {
    type: 'object',
    properties: {
      rulesContext: { type: 'array', items: { type: 'object' }, description: 'Numbered rules used as context (no grading).' },
      rubrics: {
        type: 'array',
        items: {
          type: 'object',
          properties: { id: { type: 'string' }, title: { type: 'string' }, passWhen: { type: 'string' } },
          required: ['id', 'passWhen'],
        },
      },
      content: {
        type: 'array',
        items: {
          type: 'object',
          properties: { id: { type: 'string' }, title: { type: 'string' }, text: { type: 'string' } },
          required: ['id', 'text'],
        },
      },
      threshold: { type: 'number', description: 'Probability at/above which a rubric passes (default 0.5).' },
    },
    required: ['rubrics', 'content'],
  },
};

// ---- stdio JSON-RPC loop --------------------------------------------------

const PROTOCOL = '2025-06-18';
const send = (msg) => process.stdout.write(JSON.stringify(msg) + '\n');
const ok = (id, result) => send({ jsonrpc: '2.0', id, result });
const err = (id, code, message) => send({ jsonrpc: '2.0', id, error: { code, message } });

async function handle(msg) {
  const { id, method, params } = msg;
  if (method === 'initialize') {
    return ok(id, {
      protocolVersion: params?.protocolVersion || PROTOCOL,
      capabilities: { tools: {} },
      serverInfo: { name: 'jev', version: '0.1.0' },
    });
  }
  if (method === 'ping') return ok(id, {});
  if (method === 'tools/list') return ok(id, { tools: [TOOL] });
  if (method === 'tools/call') {
    if (params?.name !== TOOL.name) return err(id, -32602, `Unknown tool: ${params?.name}`);
    try {
      const payload = await gradeRubrics(params.arguments || {}, process.env.TYPESAFE_API_KEY);
      return ok(id, { content: [{ type: 'text', text: JSON.stringify(payload) }], structuredContent: payload });
    } catch (e) {
      return ok(id, { content: [{ type: 'text', text: `Error: ${e.message}` }], isError: true });
    }
  }
  if (id !== undefined) return err(id, -32601, `Method not found: ${method}`);
  // notifications (no id): ignore
}

let buf = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', (chunk) => {
  buf += chunk;
  let nl;
  while ((nl = buf.indexOf('\n')) >= 0) {
    const line = buf.slice(0, nl).trim();
    buf = buf.slice(nl + 1);
    if (!line) continue;
    let msg;
    try {
      msg = JSON.parse(line);
    } catch {
      log('bad json line');
      continue;
    }
    Promise.resolve(handle(msg)).catch((e) => {
      log('handler error', e.message);
      if (msg?.id !== undefined) err(msg.id, -32603, e.message);
    });
  }
});
process.stdin.on('end', () => process.exit(0));
log(`jev-mcp ready (model ${MODEL}, key ${process.env.TYPESAFE_API_KEY ? 'set' : 'MISSING'})`);
