using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;
using ZooDefense.Core;
using ZooDefense.Game;
#if ZOO_CLOUDSAVE
using Unity.Services.CloudSave;
#endif

namespace ZooDefense.Online
{
    /// <summary>
    /// Friends and invites:
    ///   - our public profile (name, trophies, last seen, room invite) in Cloud Save public player data "profile",
    ///   - friend codes, invite redemption and invite counts in Cloud Code "social" (CloudCode/social.js),
    ///     which also reads friends' public profiles in one call.
    /// </summary>
    public sealed class UgsSocial : ISocialService
    {
        const string CodeKey = "zoo_friend_code";
        public const string ProfileKey = "profile";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { SocialHub.Service = new UgsSocial(); }

        bool _publishing;

        public bool Available { get { return Application.internetReachability != NetworkReachability.NotReachable; } }

        public string PlayerId { get { return UgsCore.SignedIn ? AuthenticationService.Instance.PlayerId : null; } }

        static Task<object> Call(string action, Dictionary<string, object> p = null)
        {
            p = p ?? new Dictionary<string, object>();
            p["action"] = action;
            return UgsRest.CloudCode("social", p);
        }

        public void MyCode(Action<string> done)
        {
            string cached = PlayerPrefs.GetString(CodeKey + "_" + (PlayerId ?? ""), "");
            if (!string.IsNullOrEmpty(cached) && PlayerId != null) { done(cached); return; }
            UgsRest.Run(async () =>
            {
                var r = await Call("code");
                string code = MiniJson.Str(r, "code", null);
                if (!string.IsNullOrEmpty(code)) { PlayerPrefs.SetString(CodeKey + "_" + PlayerId, code); PlayerPrefs.Save(); }
                return code;
            }, done, null, "Social");
        }

        public void Resolve(string code, Action<string, string> done)
        {
            UgsRest.Run(async () =>
            {
                var r = await Call("resolve", new Dictionary<string, object> { { "code", code } });
                return MiniJson.Str(r, "playerId", null);
            }, id => done(id, id == null ? "friends.not_found" : null), null, "Social");
        }

        public void LoadProfiles(List<string> playerIds, Action<List<FriendInfo>> done)
        {
            if (playerIds == null || playerIds.Count == 0) { done(new List<FriendInfo>()); return; }
            UgsRest.Run(async () =>
            {
                var r = await Call("profiles", new Dictionary<string, object> { { "ids", playerIds } });
                var map = MiniJson.Obj(r, "profiles");
                var list = new List<FriendInfo>();
                foreach (var id in playerIds)
                {
                    var info = new FriendInfo { playerId = id };
                    object v;
                    if (map != null && map.TryGetValue(id, out v) && v != null)
                    {
                        string json = v as string ?? MiniJson.Serialize(v);
                        try { info.profile = JsonUtility.FromJson<PublicProfile>(json); } catch (Exception) { }
                    }
                    list.Add(info);
                }
                return list;
            }, done, null, "Social");
        }

        public async void Publish(PublicProfile me)
        {
#if ZOO_CLOUDSAVE
            if (_publishing || me == null) return;
            _publishing = true;
            try
            {
                await UgsCore.EnsureSignedInAsync();
                await CloudSaveService.Instance.Data.Player.SaveAsync(
                    new Dictionary<string, object> { { ProfileKey, JsonUtility.ToJson(me) } },
                    new Unity.Services.CloudSave.Models.Data.Player.SaveOptions(new Unity.Services.CloudSave.Models.Data.Player.PublicWriteAccessClassOptions()));
            }
            catch (Exception e) { Debug.LogWarning("[Social] publish: " + e.Message); }
            finally { _publishing = false; }
#else
            await Task.CompletedTask;
#endif
        }

        public void RedeemInvite(string code, Action<bool, string> done)
        {
            UgsRest.Run(async () =>
            {
                var r = await Call("redeem", new Dictionary<string, object> { { "code", code } });
                if (MiniJson.Bool(r, "ok")) return "";
                return MiniJson.Str(r, "error", "invite.failed");
            }, err => done(err == "", string.IsNullOrEmpty(err) ? null : err), "invite.failed", "Social");
        }

        public void InviteCount(Action<int> done)
        {
            UgsRest.Run(async () =>
            {
                var r = await Call("count");
                return MiniJson.Int(r, "count");
            }, done, 0, "Social");
        }
    }
}
