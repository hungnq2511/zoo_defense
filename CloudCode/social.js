/**
 * Zoo Defense — friends and invites (Unity Cloud Code script "social").
 *
 * Params: action = "code" | "resolve" | "profiles" | "redeem" | "count"
 *   code      -> { code }                 our 6-character friend / invite code (created once, never changes)
 *   resolve   -> { playerId | null }      params.code: the player behind a code
 *   profiles  -> { profiles: {id: json} } params.ids (max 50): friends' public profiles (Cloud Save public "profile")
 *   redeem    -> { ok, error? }           params.code: a new player enters a friend's code (once per account)
 *   count     -> { count }                players who joined with our code
 *
 * Storage (Cloud Save):
 *   custom "friend_codes"      key = code -> { playerId }
 *   player protected data      friend_code, invited_by, invite_count (only this script writes them)
 */

const CODES_ID = "friend_codes";
const ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // no O/0, I/1: easy to read and type
const MAX_PROFILES = 50;

module.exports = async ({ params, context, logger }) => {
  const { DataApi } = require("@unity-services/cloud-save-1.4");
  const api = new DataApi({ accessToken: context.serviceToken });
  const projectId = context.projectId;
  const me = context.playerId;
  const action = String(params.action || "");

  async function protectedValue(playerId, key) {
    const res = await api.getProtectedItems(projectId, playerId, [key]);
    const r = res.data && res.data.results && res.data.results[0];
    return r ? r.value : null;
  }

  async function codeOwner(code) {
    const res = await api.getCustomItems(projectId, CODES_ID, [code]);
    const r = res.data && res.data.results && res.data.results[0];
    return r && r.value ? r.value.playerId : null;
  }

  function clean(code) {
    return String(code || "").toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 6);
  }

  switch (action) {
    case "code": {
      let code = await protectedValue(me, "friend_code");
      if (code) return { code };
      for (let attempt = 0; attempt < 8; attempt++) {
        let c = "";
        for (let i = 0; i < 6; i++) c += ALPHABET[Math.floor(Math.random() * ALPHABET.length)];
        if (await codeOwner(c)) continue;
        await api.setCustomItem(projectId, CODES_ID, { key: c, value: { playerId: me } });
        await api.setProtectedItem(projectId, me, { key: "friend_code", value: c });
        return { code: c };
      }
      throw new Error("could not create a code");
    }

    case "resolve": {
      const code = clean(params.code);
      if (code.length !== 6) return { playerId: null };
      return { playerId: await codeOwner(code) };
    }

    case "profiles": {
      const ids = (Array.isArray(params.ids) ? params.ids : []).filter(x => typeof x === "string").slice(0, MAX_PROFILES);
      const profiles = {};
      await Promise.all(ids.map(async id => {
        try {
          const res = await api.getPublicItems(projectId, id, ["profile"]);
          const r = res.data && res.data.results && res.data.results[0];
          if (r) profiles[id] = r.value;
        } catch (e) {
          // A player without a public profile yet: skip.
        }
      }));
      return { profiles };
    }

    case "redeem": {
      const code = clean(params.code);
      if (code.length !== 6) return { ok: false, error: "friends.bad_code" };
      if (await protectedValue(me, "invited_by")) return { ok: false, error: "invite.used" };
      const inviter = await codeOwner(code);
      if (!inviter) return { ok: false, error: "friends.not_found" };
      if (inviter === me) return { ok: false, error: "friends.own_code" };
      await api.setProtectedItem(projectId, me, { key: "invited_by", value: inviter });
      const count = Number(await protectedValue(inviter, "invite_count")) || 0;
      await api.setProtectedItem(projectId, inviter, { key: "invite_count", value: count + 1 });
      logger.info("invite redeemed", { inviter, player: me });
      return { ok: true };
    }

    case "count":
      return { count: Number(await protectedValue(me, "invite_count")) || 0 };

    default:
      return { error: "unknown action" };
  }
};
