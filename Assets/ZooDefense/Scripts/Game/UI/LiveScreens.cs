using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    // Retention and social screens: 7-day login calendar, leaderboards, friends (codes, invites) and friendly rooms.

    /// <summary>A one-line text box (friend codes, room codes). Upper-case letters and digits only when `code` is set.</summary>
    public static class TextEntry
    {
        public static InputField Create(Transform parent, string placeholder, int limit, bool code)
        {
            var frame = Deco.Frame(parent, "entry", Art.Hex(0x6B5A99), Art.Hex(0x1A1330), 4f);
            var text = UIKit.Label(frame, "", 40, Palette.Text, TextAnchor.MiddleCenter);
            text.supportRichText = false;
            text.rectTransform.Stretch(20, 20, 4, 4);
            var hint = UIKit.Label(frame, placeholder, 32, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleCenter, FontStyle.Normal);
            hint.rectTransform.Stretch(20, 20, 4, 4);
            var input = ((RectTransform)frame.parent).gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = limit;
            input.lineType = InputField.LineType.SingleLine;
            if (code)
            {
                input.contentType = InputField.ContentType.Alphanumeric;
                input.onValueChanged.AddListener(v =>
                {
                    string up = Clean(v);
                    if (up != v) input.text = up;
                });
            }
            return input;
        }

        /// <summary>Codes are shown and typed in upper case; similar-looking letters are mapped (O -> 0 is not done: codes avoid them).</summary>
        public static string Clean(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            var sb = new System.Text.StringBuilder(v.Length);
            foreach (char c in v.ToUpperInvariant()) if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) sb.Append(c);
            return sb.ToString();
        }
    }

    // ===================================================================== 7-day login calendar

    /// <summary>
    /// Shown once a day on the lobby (after the privacy choice and the tutorial). Seven boxes, today's glows; one
    /// reward a day, a missed day just waits (no streak lost). Closing the box still gives today's reward.
    /// </summary>
    public static class DailyLoginPopup
    {
        static int _shownDay = -1;

        public static bool ShowIfReady(GameApp app, Action onChanged)
        {
            var meta = app.Meta;
            if (!app.Profile.tutorialDone || !Privacy.Decided) return false;
            if (!meta.CanClaimLogin(app.Now)) return false;
            int day = MetaService.Day(app.Now);
            if (_shownDay == day) return false;
            _shownDay = day;
            Show(app, onChanged);
            return true;
        }

        public static void Show(GameApp app, Action onChanged)
        {
            var meta = app.Meta;
            long now = app.Now;
            bool claimed = false;
            Action claim = () =>
            {
                if (claimed) return;
                claimed = true;
                int index = meta.LoginTodayIndex(now);
                var locked = Modal.LockedUnits(app);
                var r = meta.ClaimLogin(now);
                if (r == null) return;
                app.SaveProfile();
                Track.LoginReward(index + 1);
                if (onChanged != null) onChanged();
                Modal.Rewards(app, Loc.F("login.day", index + 1), r, locked, index == meta.LoginDays - 1, index == meta.LoginDays - 1 ? "magic" : null);
            };
            var box = Modal.Open(app, Loc.T("login.title"), 1240, claim);
            UIKit.Label(box, Loc.T("login.hint"), 28, Palette.TextDim).rectTransform.Band(true, 50, 112);

            int today = meta.LoginTodayIndex(now);
            for (int i = 0; i < meta.LoginDays; i++)
            {
                bool last = i == meta.LoginDays - 1;
                // Days 1-4 on the first row, 5-6 on the second, day 7 as a wide box next to them.
                int row = i < 4 ? 0 : 1;
                float w = 200f, h = 290f;
                float x = row == 0 ? -330f + i * 220f : -330f + (i - 4) * 220f;
                if (last) { w = 420f; x = 220f; }
                var state = meta.LoginDay(i, now);
                Color border = state == LoginDayState.Today ? Palette.Primary : (state == LoginDayState.Claimed ? Palette.Green : Art.Hex(0x6B5A99));
                var tile = Deco.Frame(box, "day" + i, border, state == LoginDayState.Claimed ? Art.Hex(0x1C2A22) : Art.Hex(0x221836), state == LoginDayState.Today ? 8f : 5f);
                var holder = (RectTransform)tile.parent;
                holder.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -180f - row * 320f), new Vector2(w, h));
                UIKit.Label(tile, Loc.F("login.day", i + 1), 28, state == LoginDayState.Today ? Palette.Primary : Palette.TextDim).rectTransform.Band(true, 44, 10);
                var reward = app.MetaConfig.loginRewards[i];
                var icon = Icons.Reward(tile, reward, app.Content, last ? 170f : 120f);
                icon.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), last ? new Vector2(170, 170) : new Vector2(110, 110));
                UIKit.Outlined(UIKit.Label(tile, Fmt.Reward(reward, app.Content), 24, Palette.Text), 2f).rectTransform.Band(false, 56, 8, 6);
                if (state == LoginDayState.Claimed)
                {
                    var dim = UIKit.Panel(tile, "done", new Color(0f, 0f, 0f, 0.45f));
                    dim.rectTransform.Stretch();
                    var tick = UIKit.Outlined(UIKit.Label(dim.transform, Loc.T("login.claimed"), 30, Palette.Green), 2.5f);
                    tick.rectTransform.Stretch();
                }
                else if (state == LoginDayState.Today) UIKit.Motion(holder).Pulse(0.04f, 3f);
            }

            bool can = meta.CanClaimLogin(now);
            var btn = UIKit.ChunkyButton(box, "claim", can ? Loc.F("login.claim", today + 1) : Loc.T("login.come_back"), can ? Palette.Green : Palette.Disabled, null, 44);
            btn.interactable = can;
            btn.onClick.AddListener(() => { Modal.CloseFrom(btn.transform); claim(); });
            btn.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(620, 140));
        }
    }

    // ===================================================================== leaderboards

    /// <summary>World rankings: trophies, this week's Endless, today's Daily challenge. World / around me / friends.</summary>
    public sealed class LeaderboardScreen : ListScreen
    {
        public enum View { World = 0, AroundMe = 1, Friends = 2 }

        LeaderboardKind _kind;
        View _view;
        RectTransform _rows;
        float _rowsTop;
        int _request;

        public LeaderboardScreen(LeaderboardKind kind = LeaderboardKind.Trophies, View view = View.World) { _kind = kind; _view = view; }

        protected override void Build()
        {
            Frame(Loc.T("lb.title"), Art.Hex(0xFFB020), () => App.Show(new LobbyScreen()));
            Leaderboards.SubmitProgress(App);

            // Board tabs
            var tabs = UIKit.Rect("tabs", List).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -Y), new Vector2(1020, 120));
            string[] names = { Loc.T("lb.trophies"), Loc.T("lb.endless"), Loc.T("lb.daily") };
            for (int i = 0; i < 3; i++)
            {
                var k = (LeaderboardKind)i;
                var b = UIKit.ChunkyButton(tabs, "tab" + i, names[i], k == _kind ? Palette.Primary : Palette.Panel, () => App.Show(new LeaderboardScreen(k, _view)), 30);
                b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 340f, 0), new Vector2(325, 110));
            }
            Y += 132;
            // View switch
            var views = UIKit.Rect("views", List).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -Y), new Vector2(1020, 96));
            string[] viewNames = { Loc.T("lb.world"), Loc.T("lb.around_me"), Loc.T("lb.friends") };
            for (int i = 0; i < 3; i++)
            {
                var v = (View)i;
                var b = UIKit.ChunkyButton(views, "view" + i, viewNames[i], v == _view ? Palette.Blue : Art.Hex(0x3A2D57), () => App.Show(new LeaderboardScreen(_kind, v)), 26);
                b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 340f, 0), new Vector2(325, 90));
            }
            Y += 106;

            // Reset time + my best
            var st = App.Meta.State;
            long now = App.Now;
            string info;
            switch (_kind)
            {
                case LeaderboardKind.Endless: info = Loc.F("lb.resets", Fmt.Duration(GameCalendar.WeekEnd(now) - now)) + "   ·   " + Loc.F("lb.my_best_wave", st.endlessBest); break;
                case LeaderboardKind.Daily: info = Loc.F("lb.resets", Fmt.Duration(GameCalendar.DayEnd(now) - now)) + "   ·   " + Loc.F("lb.my_best_wave", st.dailyBest); break;
                default: info = Loc.F("lb.my_trophies", st.trophies); break;
            }
            Label(UIKit.Rect("info", List).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -Y), new Vector2(1000, 50)), info, 26, Palette.TextDim, TextAnchor.MiddleCenter).rectTransform.Stretch();
            Y += 60;

            if (GameCenter.Supported)
            {
                var gc = UIKit.ChunkyButton(List, "gamecenter", Loc.T("lb.game_center"), Art.Hex(0x4A3D66), GameCenter.ShowLeaderboards, 28);
                gc.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -Y), new Vector2(560, 90));
                Y += 106;
            }

            _rowsTop = Y;
            _rows = UIKit.Rect("rows", List).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -Y), new Vector2(1020, 10));
            Message(Loc.T("lb.loading"));
            Load();
        }

        void Load()
        {
            var service = Leaderboards.Service;
            if (service == null || !service.Available) { Message(Loc.T("lb.offline")); return; }
            int request = ++_request;
            Action<List<LeaderboardEntry>> done = list => { if (request == _request && _rows != null) Show(list); };
            switch (_view)
            {
                case View.AroundMe: service.AroundMe(_kind, 10, done); break;
                case View.Friends:
                {
                    var ids = new List<string>(App.Profile.friends);
                    if (SocialHub.Available && !string.IsNullOrEmpty(SocialHub.Service.PlayerId)) ids.Add(SocialHub.Service.PlayerId);
                    if (App.Profile.friends.Count == 0) { Message(Loc.T("lb.no_friends")); return; }
                    service.Players(_kind, ids, done);
                    break;
                }
                default: service.Top(_kind, 50, done); break;
            }
        }

        void Message(string text)
        {
            UIKit.Clear(_rows);
            Label(UIKit.Rect("msg", _rows).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(940, 200)), text, 32, Palette.TextDim, TextAnchor.MiddleCenter).rectTransform.Stretch();
            Y = _rowsTop + 240;
            Done();
        }

        void Show(List<LeaderboardEntry> list)
        {
            if (list == null) { Message(Loc.T("lb.offline")); return; }
            if (list.Count == 0) { Message(Loc.T("lb.empty")); return; }
            UIKit.Clear(_rows);
            const float RowH = 112f;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                Color border = e.me ? Palette.Primary : (e.rank <= 3 ? Art.Hex(0xFFD23F) : Art.Hex(0x4A3D66));
                var row = Deco.Frame(_rows, "row" + i, border, e.me ? Art.Hex(0x3A2A14) : Art.Hex(0x221836), e.me ? 6f : 4f);
                ((RectTransform)row.parent).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -i * (RowH + 10f)), new Vector2(1000, RowH));
                string rank = e.rank <= 3 ? "<color=#FFD23F>#" + e.rank + "</color>" : "#" + e.rank;
                Label(row, rank, 36, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(24, 820, 0, 0);
                string name = string.IsNullOrEmpty(e.name) ? Loc.T("lb.player") : e.name;
                if (!string.IsNullOrEmpty(e.title)) name += "  <size=22><color=#C9BEE6>" + Loc.T("title." + e.title) + "</color></size>";
                Label(row, name, 32, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(170, 250, 0, 0);
                string score = _kind == LeaderboardKind.Trophies ? e.score.ToString() : Loc.F("lb.wave", e.score);
                var right = Label(row, score, 34, Palette.Primary, TextAnchor.MiddleRight);
                right.rectTransform.Stretch(700, 90, 0, 0);
                var icon = Icons.Currency(row, _kind == LeaderboardKind.Trophies ? "trophy" : "star");
                icon.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18, 0), new Vector2(64, 64));
            }
            Y = _rowsTop + list.Count * (RowH + 10f) + 20f;
            Done();
        }
    }

    // ===================================================================== friends

    /// <summary>
    /// My friend code (copy / share), add a friend by code, friendly rooms (create, join by code, invite a friend),
    /// invite rewards, and the friends list with who is online.
    /// </summary>
    public sealed class FriendsScreen : ListScreen
    {
        public const int MaxFriends = 50;
        string _code;
        Text _codeText, _invitedText;
        Button _claimInvites;
        RectTransform _listRoot;
        float _listTop, _poll;
        int _joined = -1;
        List<FriendInfo> _friends;

        protected override void Build()
        {
            Frame(Loc.T("friends.title"), Art.Hex(0x4CC46B), () => App.Show(new LobbyScreen()));
            if (!SocialHub.Available)
            {
                var off = Card(260, Art.Hex(0x6B5A99));
                Label(off, Loc.T("friends.offline"), 32, Palette.TextDim, TextAnchor.MiddleCenter).rectTransform.Stretch(30, 30, 20, 20);
                Done();
                return;
            }
            var meta = App.Meta;

            // ---- My code
            var me = Card(250, Palette.Primary);
            Label(me, Loc.T("friends.my_code"), 28, Palette.TextDim, TextAnchor.UpperLeft).rectTransform.Stretch(26, 26, 16, 180);
            _codeText = Label(me, "…", 64, Palette.Primary, TextAnchor.MiddleLeft);
            _codeText.rectTransform.Stretch(26, 420, 60, 20);
            Btn(me, Loc.T("friends.copy"), Palette.Blue, Copy, new Vector2(-220, 70), new Vector2(190, 110), 30);
            Btn(me, Loc.T("friends.share"), Palette.Green, Share, new Vector2(-16, 70), new Vector2(190, 110), 30);
            SocialHub.Service.MyCode(c => { _code = c; if (_codeText != null) _codeText.text = string.IsNullOrEmpty(c) ? "—" : c; });

            // ---- Add friend
            Section(Loc.T("friends.add"));
            var add = Card(170, Art.Hex(0x6B5A99));
            var addField = TextEntry.Create(add, Loc.T("friends.code_hint"), 6, true);
            ((RectTransform)addField.transform).Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24, 0), new Vector2(640, 110));
            Btn(add, Loc.T("friends.add_btn"), Palette.Green, () => AddFriend(addField.text), new Vector2(-16, 30), new Vector2(280, 110), 32);

            // ---- Friendly room
            Section(Loc.T("room.title"));
            var room = Card(330, Art.Hex(0x3D8BFF));
            Label(room, Loc.T("room.hint"), 26, Palette.TextDim, TextAnchor.UpperLeft).rectTransform.Stretch(26, 26, 14, 250);
            var create = UIKit.ChunkyButton(room, "create", Loc.T("room.create"), Palette.Blue, () => App.Show(new RoomScreen(null, null)), 34);
            create.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(940, 100));
            var joinField = TextEntry.Create(room, Loc.T("room.code_hint"), 6, true);
            ((RectTransform)joinField.transform).Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24, 24), new Vector2(640, 110));
            Btn(room, Loc.T("room.join"), Palette.Green, () => JoinRoom(joinField.text), new Vector2(-16, 24), new Vector2(280, 110), 32);

            // ---- Invites: enter a friend's code (new players), rewards for friends who joined with ours
            Section(Loc.T("invite.title"));
            var inv = Card(meta.State.inviteRedeemed ? 170 : 330, Art.Hex(0xFF7AC8));
            _invitedText = Label(inv, Loc.F("invite.joined", "…", App.MetaConfig.inviteGems), 28, Palette.Text, TextAnchor.UpperLeft);
            _invitedText.rectTransform.Stretch(26, 330, 18, meta.State.inviteRedeemed ? 18 : 180);
            _claimInvites = Btn(inv, Loc.T("shop.claim"), Palette.Disabled, ClaimInvites, new Vector2(-16, meta.State.inviteRedeemed ? 30 : 190), new Vector2(280, 110), 32);
            _claimInvites.interactable = false;
            if (!meta.State.inviteRedeemed)
            {
                var redeemField = TextEntry.Create(inv, Loc.F("invite.enter", App.MetaConfig.inviteGems), 6, true);
                ((RectTransform)redeemField.transform).Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24, 24), new Vector2(640, 110));
                Btn(inv, Loc.T("invite.redeem"), Palette.Candy, () => Redeem(redeemField.text), new Vector2(-16, 24), new Vector2(280, 110), 32);
            }
            SocialHub.Service.InviteCount(n =>
            {
                _joined = n;
                if (_invitedText == null) return;
                _invitedText.text = Loc.F("invite.joined", n, App.MetaConfig.inviteGems);
                bool can = n > App.Meta.State.inviteRewardsClaimed;
                _claimInvites.interactable = can;
                _claimInvites.GetComponent<Image>().color = can ? Palette.Green : Palette.Disabled;
                if (can) Badge((RectTransform)_claimInvites.transform);
            });

            // ---- Friends list
            Section(Loc.F("friends.list", App.Profile.friends.Count, MaxFriends));
            _listTop = Y;
            _listRoot = UIKit.Rect("friends", List).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -Y), new Vector2(1020, 10));
            ShowFriends(null);
            Reload();
        }

        public override void Tick(float dt)
        {
            _poll += dt;
            if (_poll > 20f) { _poll = 0f; Reload(); }   // online status and room invites
        }

        void Reload()
        {
            var ids = App.Profile.friends;
            if (ids.Count == 0 || !SocialHub.Available) return;
            SocialHub.Service.LoadProfiles(new List<string>(ids), list => { _friends = list; if (_listRoot != null) ShowFriends(list); });
        }

        void ShowFriends(List<FriendInfo> infos)
        {
            UIKit.Clear(_listRoot);
            var ids = App.Profile.friends;
            const float RowH = 150f;
            if (ids.Count == 0)
            {
                Label(UIKit.Rect("empty", _listRoot).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(940, 160)), Loc.T("friends.empty"), 30, Palette.TextDim, TextAnchor.MiddleCenter).rectTransform.Stretch();
                Y = _listTop + 190;
                Done();
                return;
            }
            long now = App.Now;
            string myId = SocialHub.Service.PlayerId;
            var order = new List<FriendInfo>();
            foreach (var id in ids)
            {
                FriendInfo f = null;
                if (infos != null) foreach (var x in infos) if (x.playerId == id) { f = x; break; }
                order.Add(f ?? new FriendInfo { playerId = id });
            }
            // Invites first, then online, then the rest by trophies.
            order.Sort((a, b) =>
            {
                int sa = a.InvitesMe(myId, now) ? 2 : (a.Online(now) ? 1 : 0), sb = b.InvitesMe(myId, now) ? 2 : (b.Online(now) ? 1 : 0);
                if (sa != sb) return sb.CompareTo(sa);
                return (b.profile != null ? b.profile.trophies : 0).CompareTo(a.profile != null ? a.profile.trophies : 0);
            });
            for (int i = 0; i < order.Count; i++)
            {
                var f = order[i];
                bool invites = f.InvitesMe(myId, now), online = f.Online(now);
                var row = Deco.Frame(_listRoot, "friend" + i, invites ? Palette.Primary : (online ? Palette.Green : Art.Hex(0x4A3D66)), Art.Hex(0x221836), 5f);
                ((RectTransform)row.parent).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -i * (RowH + 12f)), new Vector2(1000, RowH));
                var dot = UIKit.Icon(row, "dot", Art.Circle, online ? Palette.Green : Palette.Disabled);
                dot.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40, 0), new Vector2(30, 30));
                var p = f.profile;
                string name = p != null && !string.IsNullOrEmpty(p.name) ? p.name : Loc.T("lb.player");
                Label(row, name, 34, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(76, 380, 18, 70);
                string sub = p == null ? "…" : (invites ? Loc.T("friends.invites_you") : (online ? Loc.T("friends.online") : Loc.F("friends.seen", Fmt.Duration(Math.Max(0, now - p.seen)))));
                if (p != null) sub = Loc.F("friends.trophies", p.trophies) + "   ·   " + sub;
                Label(row, sub, 24, invites ? Palette.Primary : Palette.TextDim, TextAnchor.LowerLeft).rectTransform.Stretch(76, 380, 70, 18);
                var friend = f;
                if (invites)
                {
                    string code = p.room;
                    Btn(row, Loc.T("friends.accept"), Palette.Green, () => App.Show(new RoomScreen(code, null)), new Vector2(-110, 20), new Vector2(240, 110), 30);
                    Badge((RectTransform)row.parent);
                }
                else Btn(row, Loc.T("friends.invite"), online ? Palette.Blue : Art.Hex(0x4A3D66), () => App.Show(new RoomScreen(null, friend.playerId)), new Vector2(-110, 20), new Vector2(240, 110), 30);
                Btn(row, "×", Art.Hex(0x5B3A3A), () => Remove(friend.playerId, name), new Vector2(-12, 20), new Vector2(90, 110), 40);
            }
            Y = _listTop + order.Count * (RowH + 12f) + 20f;
            Done();
        }

        void Copy()
        {
            if (string.IsNullOrEmpty(_code)) return;
            GUIUtility.systemCopyBuffer = ShareText();
            App.Toast(Loc.T("friends.copied"));
            Track.Social("copy_code");
        }

        string ShareText() { return Loc.F("friends.share_text", _code, SocialHub.ShareLink); }

        /// <summary>Share via the chat apps players use most (no native share sheet without a plugin): WhatsApp, LINE, Messenger, or copy.</summary>
        void Share()
        {
            if (string.IsNullOrEmpty(_code)) return;
            string text = Uri.EscapeDataString(ShareText());
            var box = Modal.Open(App, Loc.T("friends.share"), 700);
            string[] names = { "WhatsApp", "LINE", "Messenger", Loc.T("friends.copy") };
            Color[] colors = { Art.Hex(0x25D366), Art.Hex(0x06C755), Art.Hex(0x0084FF), Palette.Panel };
            string[] urls =
            {
                "whatsapp://send?text=" + text,
                "https://line.me/R/share?text=" + text,
                "fb-messenger://share?link=" + Uri.EscapeDataString(SocialHub.ShareLink),
                null,
            };
            for (int i = 0; i < names.Length; i++)
            {
                string url = urls[i], app = names[i];
                Button b = null;
                b = UIKit.ChunkyButton(box, "share" + i, names[i], colors[i], () =>
                {
                    Modal.CloseFrom(b.transform);
                    if (url == null) { Copy(); return; }
                    GUIUtility.systemCopyBuffer = ShareText();   // in case the app is not installed
                    Application.OpenURL(url);
                    Track.Social("share_" + app.ToLowerInvariant());
                }, 38);
                b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -140 - i * 130), new Vector2(720, 112));
            }
        }

        void AddFriend(string raw)
        {
            string code = TextEntry.Clean(raw);
            if (code.Length != 6) { App.Toast(Loc.T("friends.bad_code")); return; }
            if (code == _code) { App.Toast(Loc.T("friends.own_code")); return; }
            if (App.Profile.friends.Count >= MaxFriends) { App.Toast(Loc.F("friends.full", MaxFriends)); return; }
            SocialHub.Service.Resolve(code, (playerId, error) =>
            {
                if (string.IsNullOrEmpty(playerId)) { App.Toast(Loc.T(error ?? "friends.not_found")); AudioService.Play(Sfx.Error); return; }
                if (playerId == SocialHub.Service.PlayerId) { App.Toast(Loc.T("friends.own_code")); return; }
                if (App.Profile.friends.Contains(playerId)) { App.Toast(Loc.T("friends.already")); return; }
                App.Profile.friends.Add(playerId);
                App.SaveProfile();
                Track.Social("add_friend");
                App.Toast(Loc.T("friends.added"));
                App.Show(new FriendsScreen());
            });
        }

        void Remove(string playerId, string name)
        {
            Modal.Confirm(App, Loc.T("friends.remove"), Loc.F("friends.remove_q", name), Loc.T("friends.remove"), () =>
            {
                App.Profile.friends.Remove(playerId);
                App.SaveProfile();
                App.Show(new FriendsScreen());
            });
        }

        void JoinRoom(string raw)
        {
            string code = TextEntry.Clean(raw);
            if (code.Length != 6) { App.Toast(Loc.T("friends.bad_code")); return; }
            App.Show(new RoomScreen(code, null));
        }

        void Redeem(string raw)
        {
            string code = TextEntry.Clean(raw);
            if (code.Length != 6) { App.Toast(Loc.T("friends.bad_code")); return; }
            if (code == _code) { App.Toast(Loc.T("friends.own_code")); return; }
            SocialHub.Service.RedeemInvite(code, (ok, error) =>
            {
                if (!ok) { App.Toast(Loc.T(error ?? "invite.failed")); AudioService.Play(Sfx.Error); return; }
                var r = App.Meta.RedeemInvite();
                // The inviter becomes a friend too.
                SocialHub.Service.Resolve(code, (id, err) =>
                {
                    if (!string.IsNullOrEmpty(id) && !App.Profile.friends.Contains(id) && App.Profile.friends.Count < MaxFriends) App.Profile.friends.Add(id);
                    App.SaveProfile();
                    App.Show(new FriendsScreen());
                    if (r != null) Modal.Rewards(App, Loc.T("invite.title"), r);
                });
                App.SaveProfile();
                Track.Social("redeem_invite");
            });
        }

        void ClaimInvites()
        {
            if (_joined < 0) return;
            var r = App.Meta.ClaimInviteRewards(_joined);
            if (r == null) return;
            App.SaveProfile();
            Track.Social("invite_rewards");
            App.Show(new FriendsScreen());
            Modal.Rewards(App, Loc.T("invite.title"), r);
        }
    }

    // ===================================================================== friendly room

    /// <summary>
    /// Friendly 1v1 with a friend, no trophies or rating. Host: creates a private room and shows its code (and, when
    /// inviting a friend from the list, puts the code in our public profile so their game shows the invite).
    /// Guest: joins by code. Starts the online battle as soon as both are connected.
    /// </summary>
    public sealed class RoomScreen : ScreenBase
    {
        readonly string _joinCode, _inviteFriend;
        Text _status, _codeText;
        bool _started, _published;
        float _t;

        /// <param name="joinCode">Join this room (guest), or null to create one (host).</param>
        /// <param name="inviteFriend">Host: friend's player id to invite.</param>
        public RoomScreen(string joinCode, string inviteFriend) { _joinCode = joinCode; _inviteFriend = inviteFriend; }

        bool Host { get { return string.IsNullOrEmpty(_joinCode); } }

        protected override void Build()
        {
            Deco.ScreenBackground(Root, App, Art.Hex(0x3D8BFF));
            var center = new Vector2(0.5f, 0.5f);
            Deco.Ribbon(Root, Loc.T("room.title"), Art.Hex(0x3D8BFF), 44).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -260), new Vector2(760, 120));
            var card = Deco.Frame(Root, "card", Palette.Primary, Art.Hex(0x221836), 8f);
            ((RectTransform)card.parent).Place(center, center, new Vector2(0, 120), new Vector2(900, 620));
            UIKit.Label(card, Loc.T(Host ? "room.share_code" : "room.joining"), 32, Palette.TextDim).rectTransform.Band(true, 60, 40);
            _codeText = UIKit.Outlined(UIKit.Label(card, Host ? "…" : _joinCode, 120, Palette.Primary), 4f);
            _codeText.rectTransform.Band(true, 180, 130);
            _status = UIKit.Label(card, Loc.T("online.connecting"), 34, Palette.Text);
            _status.rectTransform.Band(false, 160, 40, 30);
            if (Host)
            {
                var copy = UIKit.ChunkyButton(Root, "copy", Loc.T("friends.copy"), Palette.Blue, () =>
                {
                    var code = Online.Pvp != null ? Online.Pvp.RoomCode : null;
                    if (string.IsNullOrEmpty(code)) return;
                    GUIUtility.systemCopyBuffer = code;
                    App.Toast(Loc.T("friends.copied"));
                }, 36);
                copy.GetComponent<RectTransform>().Place(center, center, new Vector2(0, -290), new Vector2(460, 120));
            }
            var cancel = UIKit.ChunkyButton(Root, "cancel", Loc.T("common.cancel"), Palette.Red, Cancel, 40);
            cancel.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(460, 136));

            if (Online.Pvp == null || !App.Profile.onlinePvp) { _status.text = Loc.T("room.offline"); return; }
            var hello = Online.MyHello(App);
            hello.friendly = true;
            if (Host) Online.Pvp.CreateRoom(hello);
            else Online.Pvp.JoinRoom(hello, _joinCode);
            Track.Social(Host ? "room_create" : "room_join");
            AudioService.PlayMusic("music_lobby");
        }

        public override void Tick(float dt)
        {
            var pvp = Online.Pvp;
            if (pvp == null || _started || !App.Profile.onlinePvp) return;
            _t += dt;
            pvp.Update(dt);
            if (Host && !string.IsNullOrEmpty(pvp.RoomCode))
            {
                _codeText.text = pvp.RoomCode;
                if (!_published && !string.IsNullOrEmpty(_inviteFriend) && SocialHub.Available)
                {
                    _published = true;
                    SocialHub.Service.Publish(SocialHub.Me(App, pvp.RoomCode, _inviteFriend));
                }
            }
            int dots = 1 + (int)(_t * 3f) % 3;
            switch (pvp.State)
            {
                case OnlineState.Ready:
                    if (pvp.Match == null) break;
                    _started = true;
                    var m = pvp.Match;
                    App.Show(new BattleScreen(new BattleConfig { seed = m.setup.seed, opponentName = m.opponentName, ranked = false, friendly = true, online = m }));
                    return;
                case OnlineState.Failed:
                    _started = true;
                    App.Toast(Loc.T(pvp.Error == "room_not_found" ? "room.not_found" : "room.failed"));
                    AudioService.Play(Sfx.Error);
                    App.Show(new FriendsScreen());
                    return;
                case OnlineState.Handshake: _status.text = Loc.T("online.connecting") + new string('.', dots); break;
                default:
                    _status.text = (Host ? Loc.T("room.waiting") : Loc.T("online.connecting")) + new string('.', dots);
                    break;
            }
        }

        void Cancel()
        {
            App.Show(new FriendsScreen());
        }

        public override void Close()
        {
            if (!_started && Online.Pvp != null) Online.Pvp.Leave();
            // Withdraw the invite.
            if (_published && SocialHub.Available) SocialHub.Service.Publish(SocialHub.Me(App));
        }
    }
}
