/**
 * Zoo Defense — leaderboard submission (Unity Cloud Code script "submit_score").
 *
 * Leaderboards deny client writes (access control rule in the dashboard), so every score goes through here
 * and gets a plausibility check first.
 *
 * Params: board ("trophies" | "endless_weekly" | "daily_challenge"), score, n (name), t (title), a (arena)
 * Returns: { ok, rank?, error? }
 */

const LIMITS = {
  trophies: 20000,          // far above the last arena
  endless_weekly: 1000,     // waves
  daily_challenge: 1000,    // waves
};

module.exports = async ({ params, context, logger }) => {
  const board = String(params.board || "");
  if (!(board in LIMITS)) return { ok: false, error: "unknown board" };
  const score = Math.floor(Number(params.score));
  if (!Number.isFinite(score) || score < 0 || score > LIMITS[board]) {
    logger.warning("score rejected", { board, score: params.score });
    return { ok: false, error: "invalid score" };
  }
  const name = String(params.n || "").slice(0, 24);
  const title = String(params.t || "").slice(0, 32);
  const arena = Math.max(1, Math.min(50, Math.floor(Number(params.a) || 1)));

  const { LeaderboardsApi } = require("@unity-services/leaderboards-1.1");
  const api = new LeaderboardsApi({ accessToken: context.serviceToken });   // server rights: client writes are denied
  const res = await api.addLeaderboardPlayerScore(context.projectId, board, context.playerId, {
    score,
    metadata: { n: name, t: title, a: arena },
  });
  const rank = res.data && typeof res.data.rank === "number" ? res.data.rank + 1 : undefined;
  return { ok: true, rank };
};
