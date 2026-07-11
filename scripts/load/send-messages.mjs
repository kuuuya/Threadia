// 負荷試験: 同一 Conversation への並行メッセージ送信。
// 使い方: node scripts/load/send-messages.mjs [並行数=10] [総メッセージ数=200] [BASE=http://localhost:5100]
// 確認事項:
//   - レイテンシ(p50/p95/p99)
//   - Sequence が 1..N で重複・欠番なし(順序保証の検証)
const CONCURRENCY = Number(process.argv[2] ?? 10);
const TOTAL = Number(process.argv[3] ?? 200);
const BASE = process.argv[4] ?? "http://localhost:5100";

async function api(method, path, body, token) {
  const headers = { "Content-Type": "application/json" };
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(BASE + path, { method, headers, body: body ? JSON.stringify(body) : undefined });
  if (!res.ok) throw new Error(`${method} ${path} -> ${res.status}: ${await res.text()}`);
  return res.json();
}

function percentile(sorted, p) {
  return sorted[Math.min(sorted.length - 1, Math.floor((sorted.length * p) / 100))];
}

const run = async () => {
  const suffix = Math.random().toString(36).slice(2, 10);
  const alice = await api("POST", "/api/auth/register", {
    email: `load-a-${suffix}@example.com`, displayName: "LoadA", password: "password-123",
  });
  const bob = await api("POST", "/api/auth/register", {
    email: `load-b-${suffix}@example.com`, displayName: "LoadB", password: "password-123",
  });
  const ws = await api("POST", "/api/workspaces", { name: `load-${suffix}` }, alice.token);
  await api("POST", `/api/workspaces/${ws.id}/members`, { email: bob.email }, alice.token);
  const conv = await api("POST", `/api/workspaces/${ws.id}/conversations/direct`,
    { otherUserId: bob.userId }, alice.token);

  console.log(`並行数=${CONCURRENCY} 総数=${TOTAL} conversation=${conv.id}`);

  const latencies = [];
  const sequences = [];
  let sent = 0;
  let failed = 0;
  const startedAt = performance.now();

  async function worker(workerIndex) {
    const token = workerIndex % 2 === 0 ? alice.token : bob.token;
    while (true) {
      const index = sent++;
      if (index >= TOTAL) return;
      const begin = performance.now();
      try {
        const message = await api("POST", `/api/conversations/${conv.id}/messages`, {
          content: `負荷試験メッセージ ${index}`,
          clientMessageId: `load-${suffix}-${index}`,
        }, token);
        latencies.push(performance.now() - begin);
        sequences.push(message.sequence);
      } catch (e) {
        failed++;
        console.error(`送信失敗 index=${index}: ${e.message}`);
      }
    }
  }

  await Promise.all(Array.from({ length: CONCURRENCY }, (_, i) => worker(i)));
  const elapsedSec = (performance.now() - startedAt) / 1000;

  latencies.sort((a, b) => a - b);
  console.log(`完了: ${latencies.length} 件成功 / ${failed} 件失敗 / ${elapsedSec.toFixed(1)} 秒 (${(latencies.length / elapsedSec).toFixed(1)} msg/s)`);
  console.log(`レイテンシ p50=${percentile(latencies, 50).toFixed(0)}ms p95=${percentile(latencies, 95).toFixed(0)}ms p99=${percentile(latencies, 99).toFixed(0)}ms max=${latencies.at(-1).toFixed(0)}ms`);

  // Sequence の重複・欠番検証
  const unique = new Set(sequences);
  const min = Math.min(...sequences);
  const max = Math.max(...sequences);
  const noDuplicates = unique.size === sequences.length;
  const noGaps = max - min + 1 === sequences.length;
  console.log(`Sequence 検証: 範囲=${min}..${max} 重複なし=${noDuplicates} 欠番なし=${noGaps}`);
  if (!noDuplicates || !noGaps || failed > 0) {
    process.exit(1);
  }
};

run().catch((e) => {
  console.error("負荷試験の実行に失敗:", e.message);
  process.exit(1);
});
