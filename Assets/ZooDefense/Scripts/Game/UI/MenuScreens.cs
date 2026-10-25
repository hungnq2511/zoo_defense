using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    // Lobby (home tab with chests, missions and the pass strip), Matchmaking and Result.
    // Other tabs: ShopScreen, DeckScreen, PassScreen, SettingsScreen.

    public sealed class LobbyScreen : ScreenBase
    {
        CurrencyBar _bar;
        Button _difficulty, _battle;
        ChestSlotView[] _chests;
        Text _missionsLabel, _passLabel;
        Image _missionsBadge, _passBadge, _modesBadge, _friendsBadge;
        RectTransform _inviteBanner;
        float _invitePoll = 2f;
        Showcase _showcase;
        RectTransform _battleRoot, _rays;
        float _refresh, _t;

        protected override void Build()
        {
            var meta = App.Meta;
            meta.Refresh(App.Now);
            var s = meta.State;
            var arena = MetaRules.ArenaFor(s.trophies);
            AudioService.PlayMusic("music_lobby");

            // Background: arena art (cover) with soft fades top and bottom for readable UI.
            UIKit.Panel(Root, "bg", Color.Lerp(arena.tint, Palette.Background, 0.5f), false).rectTransform.Stretch();
            var art = Art.Background(arena.index);
            if (art != null) UIKit.Cover(UIKit.Icon(Root, "arena_art", art, Color.white));
            Deco.Fade(Root, true, 700, new Color(0.10f, 0.07f, 0.18f, 0.85f));
            Deco.Fade(Root, false, 900, new Color(0.10f, 0.07f, 0.18f, 0.95f));

            _bar = new CurrencyBar(Root, App, true);

            // Logo + arena ribbon + trophy road
            Deco.Logo(Root).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -CurrencyBar.Height - 6), new Vector2(760, 170));
            var ribbon = Deco.Ribbon(Root, Loc.F("lobby.arena", arena.index) + "  ·  " + Loc.ArenaName(arena), Color.Lerp(arena.tint, Palette.Primary, 0.35f), 40);
            ribbon.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -CurrencyBar.Height - 186), new Vector2(760, 96));
            Text roadLabel;
            var road = Deco.TrophyRoad(Root, s.trophies, out roadLabel);
            road.Root.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(30, -CurrencyBar.Height - 300), new Vector2(620, 56));

            // Side buttons: missions + pass (left), practice + bot difficulty (right)
            float sideTop = -CurrencyBar.Height - 390;
            var missions = SideButton("missions", Loc.T("lobby.missions"), Palette.Blue, "xp", new Vector2(24, sideTop), true, () => MissionsPopup.Show(App, Refresh), out _missionsLabel, out _missionsBadge);
            var pass = SideButton("pass", "", Palette.Purple, "trophy", new Vector2(24, sideTop - 180), true, () => App.Show(new PassScreen()), out _passLabel, out _passBadge);
            Text modesLabel, diffLabel, lbLabel, friendsLabel;
            Image diffBadge, lbBadge;
            SideButton("leaderboard", Loc.T("lobby.leaderboard"), Art.Hex(0xE0902F), "star", new Vector2(24, sideTop - 360), true, () => App.Show(new LeaderboardScreen()), out lbLabel, out lbBadge);
            SideButton("friends", Loc.T("lobby.friends"), Art.Hex(0x2FA37A), "gems", new Vector2(-24, sideTop - 360), false, () => App.Show(new FriendsScreen()), out friendsLabel, out _friendsBadge);
            // Game modes hub (daily, endless, campaign, mayhem, tournament, raid, practice, bestiary, achievements, replays)
            SideButton("modes", Loc.T("lobby.modes"), Palette.Green, "star", new Vector2(-24, sideTop), false, () => App.Show(new ModesScreen()), out modesLabel, out _modesBadge);
            _difficulty = SideButton("difficulty", "", Palette.Panel, "candy", new Vector2(-24, sideTop - 180), false, CycleDifficulty, out diffLabel, out diffBadge);
            RefreshDifficulty();

            // Showcase: your 5 animals on a podium, between the side buttons and the battle button.
            // Fill the free band between the trophy road and the battle button (taller phones get a bigger team),
            float rootH = Root.rect.height > 100f ? Root.rect.height : 1920f;
            // Lift the whole battle cluster (chests + battle button) off the nav bar; taller phones get more air.
            float lift = Mathf.Clamp((rootH - 1920f) * 0.3f, 40f, 90f);
            float chestY = NavBar.Height + 30f + lift, battleY = chestY + 236f + 28f;
            float bandBottom = battleY + 196f + 24f, bandTop = rootH - (CurrencyBar.Height + 500f);
            float free = Mathf.Max(600f, bandTop - bandBottom);
            float showH = Mathf.Clamp(free * 0.95f, 700f, 1040f);
            float showW = Mathf.Clamp(showH * 0.8f, 640f, 820f);
            // The team's visual centre is ~42% up the showcase: put that on the band's centre.
            float showY = Mathf.Max(bandBottom, bandBottom + free * 0.5f - showH * 0.42f);
            _showcase = new Showcase(Root, App, showW, showH);
            _showcase.Root.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, showY), new Vector2(showW, showH));

            // Battle button: rays behind, big chunky yellow, gentle pulse.
            _battleRoot = UIKit.Rect("battleRoot", Root).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, battleY), new Vector2(720, 196));
            var rays = UIKit.Icon(_battleRoot, "rays", Art.Rays, new Color(1f, 0.9f, 0.5f, 0.35f));
            _rays = rays.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 900));
            _battle = UIKit.ChunkyButton(_battleRoot, "battle", Loc.T("lobby.battle"), Art.Hex(0xFFB020), StartBattle, 76, "button_battle");
            _battle.GetComponent<RectTransform>().Stretch();
            var sub = UIKit.Label(_battle.transform.Find("face"), Loc.T("lobby.ranked"), 26, new Color(1f, 1f, 1f, 0.9f));
            sub.rectTransform.Band(false, 40, 14);
            var trophy = Icons.Currency(_battle.transform.Find("face"), "trophy");
            trophy.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40, 4), new Vector2(96, 96));

            // Chest slots
            _chests = new ChestSlotView[s.chests.Length];
            for (int i = 0; i < _chests.Length; i++) _chests[i] = new ChestSlotView(this, Root, i, new Vector2((i - (_chests.Length - 1) * 0.5f) * 258, chestY));

            NavBar.Build(Root, App, Tab.Battle);
            Refresh();

            if (!App.Profile.tutorialDone) OfferTutorial();
            else if (s.pendingSeasonTier > 0) SeasonEndPopup();
            else DailyLoginPopup.ShowIfReady(App, Refresh);
            Leaderboards.SubmitProgress(App);
        }

        /// <summary>A friend invited us to their room: banner under the arena ribbon, polled every 20 seconds.</summary>
        void PollInvites()
        {
            if (!SocialHub.Available || App.Profile.friends.Count == 0) return;
            SocialHub.Service.LoadProfiles(new List<string>(App.Profile.friends), list =>
            {
                if (_friendsBadge == null || list == null) return;
                FriendInfo inviter = null;
                foreach (var f in list) if (f.InvitesMe(SocialHub.Service.PlayerId, App.Now)) { inviter = f; break; }
                _friendsBadge.gameObject.SetActive(inviter != null);
                if (_inviteBanner != null) { Object.Destroy(_inviteBanner.gameObject); _inviteBanner = null; }
                if (inviter == null) return;
                var banner = Deco.Frame(Root, "invite", Palette.Primary, Art.Hex(0x2B1F3F), 6f);
                _inviteBanner = (RectTransform)banner.parent;
                _inviteBanner.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -CurrencyBar.Height - 6), new Vector2(960, 150));
                UIKit.Label(banner, Loc.F("friends.invite_banner", inviter.profile.name), 30, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(30, 300, 0, 0);
                string code = inviter.profile.room;
                var go = UIKit.ChunkyButton(banner, "go", Loc.T("friends.accept"), Palette.Green, () => App.Show(new RoomScreen(code, null)), 32);
                go.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16, 0), new Vector2(260, 110));
                UIKit.Motion(_inviteBanner).PopIn(0f, 0.25f);
                AudioService.Play(Sfx.Click);
            });
        }

        /// <summary>Round-cornered square button with an icon, a caption and a red badge (hidden by default).</summary>
        Button SideButton(string name, string caption, Color color, string icon, Vector2 pos, bool left, System.Action onClick, out Text label, out Image badge)
        {
            var b = UIKit.ChunkyButton(Root, name, "", color, onClick, 26);
            b.GetComponent<RectTransform>().Place(new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? 0f : 1f, 1f), pos, new Vector2(170, 170));
            var face = b.transform.Find("face");
            Icons.Currency(face, icon).rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(84, 84));
            label = face.GetComponentInChildren<Text>();
            label.text = caption;
            label.fontSize = 24;
            label.rectTransform.Band(false, 56, 6, 4);
            badge = UIKit.Icon(b.transform, "badge", Art.Circle, Palette.Red);
            badge.rectTransform.Place(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-14, -14), new Vector2(46, 46));
            UIKit.Label(badge.transform, "!", 30, Palette.Text).rectTransform.Stretch();
            badge.gameObject.SetActive(false);
            return b;
        }

        void StartBattle()
        {
            if (!App.Profile.tutorialDone) { OfferTutorial(); return; }
            App.Show(new MatchmakingScreen(true));
        }

        /// <summary>A ranked season ended: show the reward for the best arena reached.</summary>
        void SeasonEndPopup()
        {
            var st = App.Meta.State;
            var box = Modal.Open(App, Loc.T("season.ended"), 700);
            UIKit.Label(box, Loc.F("season.ended_text", st.pendingSeasonTier, MetaService.SeasonResetFloor), 34, Palette.Text).rectTransform.Stretch(50, 50, 130, 300);
            var claim = UIKit.ChunkyButton(box, "claim", Loc.T("season.claim"), Palette.Green, () =>
            {
                var r = App.Meta.ClaimSeasonReward();
                App.SaveProfile();
                Modal.CloseAll();
                if (r != null) Modal.Rewards(App, Loc.T("season.reward_title"), r, null, true, "magic");
                Refresh();
            }, 46);
            claim.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(560, 130));
        }

        void OfferTutorial()
        {
            var box = Modal.Open(App, Loc.T("tutorial.welcome_title"), 640);
            UIKit.Label(box, Loc.T("tutorial.welcome_text"), 40, Palette.Text).rectTransform.Stretch(50, 50, 130, 300);
            var play = UIKit.ChunkyButton(box, "play", Loc.T("tutorial.play"), Palette.Green, () => App.Show(BattleScreen.Tutorial()), 46);
            play.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 160), new Vector2(560, 120));
            var skip = UIKit.ChunkyButton(box, "skip", Loc.T("tutorial.skip"), Palette.PanelDark, null, 32);
            skip.onClick.AddListener(() =>
            {
                App.Profile.tutorialDone = true;
                App.SaveProfile();
                Track.TutorialComplete(true);
                Modal.CloseFrom(skip.transform);
            });
            skip.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(400, 90));
        }

        void CycleDifficulty()
        {
            App.Profile.botDifficulty = (App.Profile.botDifficulty + 1) % 3;
            App.SaveProfile();
            RefreshDifficulty();
        }

        void RefreshDifficulty() { UIKit.SetButtonText(_difficulty, Loc.F("lobby.bot", Loc.T("difficulty." + App.Profile.botDifficulty))); }

        public void Refresh()
        {
            var meta = App.Meta;
            _bar.Refresh();
            int claimable = meta.ClaimableMissions();
            var st = meta.State;
            _modesBadge.gameObject.SetActive(st.dailyBest == 0 || st.pendingSeasonTier > 0 || meta.ClaimableAchievements() > 0 || meta.ClaimableBestiary() > 0);
            _missionsBadge.gameObject.SetActive(claimable > 0);
            int tier = meta.PassTier, max = App.MetaConfig.passTiers.Count;
            _passLabel.text = Loc.F("lobby.pass_short", tier, max);
            _passBadge.gameObject.SetActive(meta.ClaimablePass() > 0);
            long now = App.Now;
            foreach (var c in _chests) c.Refresh(now);
        }

        public override void Tick(float dt)
        {
            _t += dt;
            _showcase.Tick(dt);
            _rays.localRotation = Quaternion.Euler(0f, 0f, -_t * 12f);
            float pulse = 1f + 0.025f * Mathf.Sin(_t * 3.2f);
            _battleRoot.localScale = new Vector3(pulse, pulse, 1f);
            _invitePoll -= dt;
            if (_invitePoll <= 0f) { _invitePoll = 20f; PollInvites(); }
            _refresh -= dt;
            if (_refresh > 0f) return;
            _refresh = 1f;
            long now = App.Now;
            foreach (var c in _chests) c.Refresh(now);
        }

        /// <summary>
        /// Chest slot: wooden frame, chest on a coloured glow and a status pill.
        /// Empty = faint chest silhouette; Locked = "tap to unlock"; Unlocking = blue, timer + gem price;
        /// Ready = green, rays behind a bobbing chest and a pulsing OPEN pill.
        /// </summary>
        sealed class ChestSlotView
        {
            readonly LobbyScreen _screen;
            readonly int _slot;
            readonly Button _button;
            readonly Image _frameBase, _frameRim, _glow, _pillBg;
            readonly RectTransform _iconHolder, _rays, _pill;
            readonly Text _name, _status;
            string _shownChest = "?";
            ChestStatus _shownStatus = (ChestStatus)(-1);

            public ChestSlotView(LobbyScreen screen, RectTransform root, int slot, Vector2 pos)
            {
                _screen = screen;
                _slot = slot;
                _button = UIKit.Button(root, "chest" + slot, "", new Color(0f, 0f, 0f, 0f), OnTap);
                var rt = _button.GetComponent<RectTransform>();
                rt.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos, new Vector2(242, 236));
                var c = Deco.Frame(rt, "frame", Art.Hex(0xC08A4E), Art.Hex(0x2B1F3F), 7f);
                _frameBase = c.parent.GetComponent<Image>();
                _frameRim = c.parent.Find("rim").GetComponent<Image>();

                _rays = UIKit.Icon(c, "rays", Art.Rays, new Color(1f, 0.95f, 0.6f, 0.5f)).rectTransform;
                _rays.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(300, 300));
                UIKit.Motion(_rays).Spin(30f);
                _glow = UIKit.Icon(c, "glow", Art.Glow, Color.white);
                _glow.preserveAspect = false;
                _glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(230, 170));
                _iconHolder = UIKit.Rect("icon", c).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(170, 128));

                _name = UIKit.Label(c, "", 22, Palette.Text);
                _name.rectTransform.Band(false, 30, 58, 4);
                _pillBg = UIKit.Panel(c, "pill", Palette.PanelDark);
                _pill = _pillBg.rectTransform.Band(false, 48, 6, 8);
                _status = UIKit.Label(_pill, "", 24, Palette.Text);
                _status.rectTransform.Stretch(4, 4, 0, 0);
                UIKit.Outlined(_status, 1.5f);
            }

            static GameApp App { get { return GameApp.Instance; } }

            void SetFrame(Color c)
            {
                _frameBase.color = Color.Lerp(c, Color.black, 0.35f);
                _frameRim.color = c;
            }

            public void Refresh(long now)
            {
                var meta = App.Meta;
                var slot = meta.State.chests[_slot];
                var status = meta.Status(_slot, now);
                if (slot.chestId != _shownChest || status != _shownStatus)
                {
                    _shownChest = slot.chestId;
                    _shownStatus = status;
                    UIKit.Clear(_iconHolder);
                    var chest = Icons.Chest(_iconHolder, status == ChestStatus.Empty ? "wood" : slot.chestId);
                    chest.Stretch();
                    if (status == ChestStatus.Empty)
                        foreach (var g in chest.GetComponentsInChildren<Graphic>()) g.color = new Color(0f, 0f, 0f, 0.35f);
                    var motion = UIKit.Motion(_iconHolder);
                    motion.bobAmp = status == ChestStatus.Ready ? 12f : 0f;
                    var pillMotion = UIKit.Motion(_pill);
                    pillMotion.pulseAmp = status == ChestStatus.Ready ? 0.07f : 0f;
                    _rays.gameObject.SetActive(status == ChestStatus.Ready);
                    var chestColor = status == ChestStatus.Empty ? Color.clear : Icons.ChestColor(slot.chestId);
                    _glow.color = new Color(chestColor.r, chestColor.g, chestColor.b, status == ChestStatus.Empty ? 0f : 0.55f);
                }
                switch (status)
                {
                    case ChestStatus.Empty:
                        _name.text = "";
                        _status.text = Loc.T("chest.empty");
                        _status.color = Palette.TextDim;
                        _pillBg.color = new Color(0f, 0f, 0f, 0.25f);
                        SetFrame(Art.Hex(0x6B5A80));
                        break;
                    case ChestStatus.Locked:
                        _name.text = Loc.T("chest." + slot.chestId);
                        _status.text = meta.IsAnyUnlocking(now) ? Fmt.Duration(meta.SecondsLeft(_slot, now)) : Loc.T("chest.tap_unlock");
                        _status.color = Palette.Text;
                        _pillBg.color = Art.Hex(0x4A3A66);
                        SetFrame(Art.Hex(0xC08A4E));
                        break;
                    case ChestStatus.Unlocking:
                        _name.text = Fmt.Duration(meta.SecondsLeft(_slot, now));
                        _status.text = Loc.F("chest.open_now", meta.SkipCost(_slot, now));
                        _status.color = Palette.Text;
                        _pillBg.color = Palette.Blue;
                        SetFrame(Art.Hex(0x5BB6FF));
                        break;
                    case ChestStatus.Ready:
                        _name.text = Loc.T("chest." + slot.chestId);
                        _status.text = Loc.T("chest.open");
                        _status.color = Palette.Text;
                        _pillBg.color = Palette.Green;
                        SetFrame(Art.Hex(0x7CE08A));
                        break;
                }
            }

            void OnTap()
            {
                var app = App;
                var meta = app.Meta;
                long now = app.Now;
                switch (meta.Status(_slot, now))
                {
                    case ChestStatus.Empty:
                        app.Toast(Loc.T("chest.how_to_get"));
                        break;
                    case ChestStatus.Locked:
                        if (meta.StartUnlock(_slot, now)) { app.SaveProfile(); app.Toast(Loc.T("chest.unlock_started")); _screen.Refresh(); }
                        else OfferSkip(now);
                        break;
                    case ChestStatus.Unlocking:
                        OfferSkip(now);
                        break;
                    case ChestStatus.Ready:
                        Open(false);
                        break;
                }
            }

            void OfferSkip(long now)
            {
                var app = App;
                int cost = app.Meta.SkipCost(_slot, now);
                Modal.Confirm(app, Loc.T("chest." + app.Meta.State.chests[_slot].chestId), Loc.F("chest.skip_question", cost), Loc.F("chest.open_now", cost), () => Open(true));
            }

            void Open(bool payGems)
            {
                var app = App;
                var locked = Modal.LockedUnits(app);
                string chest = app.Meta.State.chests[_slot].chestId;
                var rewards = app.Meta.OpenChest(_slot, app.Now, payGems);
                if (rewards == null) { app.Toast(Loc.T("shop.not_enough_gems")); AudioService.Play(Sfx.Error); return; }
                app.SaveProfile();
                Track.ChestOpen(chest, payGems);
                Reminders.AskOnce(app);
                AudioService.Play(Sfx.Chest);
                Modal.Rewards(app, Loc.T("chest." + chest), rewards, locked, true, chest);
                _screen.Refresh();
            }
        }
    }

    public static class MissionsPopup
    {
        public static void Show(GameApp app, System.Action onChanged)
        {
            var meta = app.Meta;
            meta.Refresh(app.Now);
            var box = Modal.Open(app, Loc.T("missions.title"), 1020, onChanged);
            long untilReset = MetaService.SecondsPerDay - app.Now % MetaService.SecondsPerDay;
            UIKit.Label(box, Loc.F("missions.reset", Fmt.Duration(untilReset)), 30, Palette.TextDim).rectTransform.Band(true, 50, 115);
            for (int i = 0; i < meta.State.missions.Count; i++)
            {
                var ms = meta.State.missions[i];
                var def = app.MetaConfig.Mission(ms.id);
                if (def == null) continue;
                bool rowDone = ms.progress >= def.target && !ms.claimed;
                var row = Deco.Frame(box, "mission" + i, rowDone ? Palette.Green : Art.Hex(0x6B5A99), Art.Hex(0x221836), 6f);
                ((RectTransform)row.parent).Band(true, 240, 170 + i * 260, 30);
                UIKit.Label(row, Fmt.Mission(def), 36, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(30, 290, 24, 150);
                UIKit.Label(row, Loc.F("missions.reward", def.gold, def.passXp), 28, Palette.Primary, TextAnchor.UpperLeft).rectTransform.Stretch(30, 290, 92, 100);
                var bar = new Bar(row, Palette.Green, 26);
                bar.Root.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30, 26), new Vector2(560, 50));
                bar.Set(ms.progress / (float)def.target, ms.progress + "/" + def.target);
                int index = i;
                bool done = ms.progress >= def.target;
                var claim = UIKit.ChunkyButton(row, "claim", ms.claimed ? Loc.T("missions.claimed") : Loc.T("missions.claim"),
                    done && !ms.claimed ? Palette.Green : Palette.Disabled, null, 34);
                claim.interactable = done && !ms.claimed;
                claim.onClick.AddListener(() =>
                {
                    var rewards = meta.ClaimMission(index);
                    if (rewards == null) return;
                    app.SaveProfile();
                    Modal.CloseFrom(claim.transform);
                    if (onChanged != null) onChanged();
                    Modal.Rewards(app, Loc.T("missions.title"), rewards);
                });
                claim.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24, 0), new Vector2(240, 120));
            }
        }
    }

    /// <summary>
    /// End of match. Victory: warm light, spinning rays, the hero animal jumps for joy and rewards pop in one by one.
    /// Defeat: cool dim light, the hero sits sadly, a tip on how to improve. Optional art: ribbon / ribbon_defeat / rays.
    /// </summary>
    public sealed class ResultScreen : ScreenBase
    {
        readonly MatchRecord _record;
        readonly bool _ranked, _tutorial;
        readonly MatchSummary _summary;
        MatchRewards _rewards;
        Button _doubleGold;
        bool _valid = true, _won, _lost;
        float _t;

        RectTransform _ribbon, _rays, _hero, _heroShadow;
        readonly List<RectTransform> _pops = new List<RectTransform>();
        readonly List<float> _popAt = new List<float>();
        readonly List<Text> _counters = new List<Text>();
        readonly List<int> _counterTarget = new List<int>();
        readonly List<string> _counterPrefix = new List<string>();
        readonly List<float> _counterAt = new List<float>();
        Bar _road;
        float _roadFrom, _roadTo;
        Text _roadLabel;
        int _roadTrophiesFrom, _roadTrophiesTo;

        public ResultScreen(MatchRecord record, bool ranked, MatchSummary summary, bool tutorial = false)
        {
            _record = record;
            _ranked = ranked;
            _summary = summary;
            _tutorial = tutorial;
        }

        /// <summary>Friendly match with a friend: no trophies, rating or rewards.</summary>
        public bool Friendly;

        protected override void Build()
        {
            var p = App.Profile;
            var meta = App.Meta;
            _won = _summary.winner == 0;
            _lost = _summary.winner == 1;
            int trophiesBefore = meta.State.trophies;
            // Rewards only for a match that replays to the same result (Cloud Code will do this server-side).
            Services.Backend.SubmitMatch(_record, ok => _valid = ok);

            List<Reward> tutorialRewards = null;
            HashSet<string> locked = null;
            if (_tutorial)
            {
                p.tutorialDone = true;
                locked = Modal.LockedUnits(App);
                tutorialRewards = new List<Reward>();
                meta.Grant(new Reward(RewardType.Chest, 1, "silver"), tutorialRewards);
                tutorialRewards = MetaService.Merge(tutorialRewards);
            }
            else if (Friendly) { p.matchesPlayed++; Track.Social("friendly_" + (_won ? "win" : "loss")); }
            else if (_valid)
            {
                _rewards = meta.ApplyMatch(_summary, App.Now);
                p.matchesPlayed++;
                if (_won) p.wins++;
            }
            App.SaveProfile();
            if (_tutorial) Track.TutorialComplete(false);
            else if (!Friendly)
            {
                if (meta.ArenaIndex() > MetaRules.ArenaFor(trophiesBefore).index) Track.ArenaUp(meta.ArenaIndex());
                Leaderboards.SubmitProgress(App);
                // Ask for notification permission once things are going well (a win), not at launch.
                if (_won) Reminders.AskOnce(App);
            }
            AudioService.PlayMusic("music_lobby");
            AudioService.Play(_won ? Sfx.Win : Sfx.Lose);

            var arena = MetaRules.ArenaFor(meta.State.trophies);
            var mood = _won ? Art.Hex(0xFFB547) : (_lost ? Art.Hex(0x3A4F86) : Art.Hex(0x6B6290));

            // Background: arena art tinted by the mood, dark fades.
            UIKit.Panel(Root, "bg", Color.Lerp(mood, Color.black, 0.55f), false).rectTransform.Stretch();
            var art = Art.Background(arena.index);
            if (art != null) UIKit.Cover(UIKit.Icon(Root, "arena_art", art, _won ? new Color(1f, 0.92f, 0.8f, 0.55f) : new Color(0.55f, 0.6f, 0.8f, 0.4f)));
            Deco.Fade(Root, true, 600, new Color(0.08f, 0.05f, 0.14f, 0.85f));
            Deco.Fade(Root, false, 1100, new Color(0.08f, 0.05f, 0.14f, 0.97f));

            // Rays + glow behind the hero (victory and draw only)
            var glow = UIKit.Icon(Root, "glow", Art.Glow, _won ? new Color(1f, 0.85f, 0.4f, 0.75f) : new Color(0.5f, 0.6f, 1f, 0.35f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -620), new Vector2(900, 900));
            if (!_lost)
            {
                _rays = UIKit.Icon(Root, "rays", Art.Rays, _won ? new Color(1f, 0.92f, 0.55f, 0.6f) : new Color(1f, 1f, 1f, 0.25f)).rectTransform;
                _rays.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -620), new Vector2(1300, 1300));
            }

            // Hero animal (first deck slot) on a little mound
            var lead = App.Content.Unit(_tutorial ? "fire_fox" : p.deck[0]);
            int lv = Mathf.Max(1, meta.Level(lead.id));
            _heroShadow = Deco.GroundShadow(Root, Vector2.zero, new Vector2(300, 64)).rectTransform;
            _heroShadow.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -850), new Vector2(300, 64));
            _hero = UIKit.Rect("hero", Root).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.1f), new Vector2(0, -850), new Vector2(400, 400));
            Icons.Unit(_hero, lead, lv >= 10 ? 7 : (lv >= 5 ? 4 : 1), 400).Stretch();
            if (_lost) foreach (var g in _hero.GetComponentsInChildren<Graphic>()) g.color = new Color(0.72f, 0.76f, 0.9f, 1f);

            // Title ribbon
            string title = Loc.T(_won ? "result.victory" : (_lost ? "result.defeat" : "result.draw"));
            var ribbonColor = _won ? Art.Hex(0xF2A31B) : (_lost ? Art.Hex(0x5468A8) : Art.Hex(0x7D6FA8));
            _ribbon = Deco.Ribbon(Root, title, ribbonColor, 92, _lost);
            _ribbon.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -230), new Vector2(860, 170));
            AddPop(_ribbon, 0.05f);

            // Stats chips: wave · kills · merges · sends
            var stats = UIKit.Rect("stats", Root).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -900), new Vector2(1000, 110));
            string[] statKeys = { "result.stat_wave", "result.stat_kills", "result.stat_merges", "result.stat_sends" };
            int[] statValues = { _summary.wave, _summary.kills, _summary.merges, _summary.sends };
            for (int i = 0; i < 4; i++)
            {
                var chip = UIKit.Panel(stats, "stat" + i, new Color(0f, 0f, 0f, 0.45f)).rectTransform;
                chip.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 252 + 4, 0), new Vector2(240, 104));
                UIKit.Label(chip, Loc.T(statKeys[i]), 22, Palette.TextDim).rectTransform.Band(true, 34, 8);
                var v = UIKit.Label(chip, "0", 40, Palette.Text);
                v.rectTransform.Band(false, 56, 8);
                AddCounter(v, statValues[i], "", 0.5f + i * 0.08f);
                AddPop(chip, 0.45f + i * 0.08f);
            }

            // Match stats: damage per animal and MVP.
            var statsOpen = UIKit.ChunkyButton(Root, "statsOpen", Loc.T("stats.button"), Palette.Blue, () => MatchStats.Show(App, _record, _summary.localPlayer), 30);
            statsOpen.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24, -48), new Vector2(250, 96));
            statsOpen.gameObject.SetActive(!_tutorial);
            AddPop(statsOpen.GetComponent<RectTransform>(), 1.0f);

            // Reward card
            var card = UIKit.Panel(Root, "rewards", new Color(0.16f, 0.11f, 0.26f, 0.94f)).rectTransform;
            card.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -1030), new Vector2(1000, 420));
            var head = UIKit.Label(card, Loc.T(_tutorial ? "result.tutorial_done" : (Friendly ? "result.friendly" : (_valid ? "result.rewards" : "result.invalid"))), 34, Palette.Primary);
            head.rectTransform.Band(true, 56, 10);
            var items = new List<KeyValuePair<string, int>>();   // icon -> amount
            string chestId = null;
            if (!_tutorial && _valid && !Friendly)
            {
                if (_ranked) items.Add(new KeyValuePair<string, int>("trophy", _rewards.trophies));
                items.Add(new KeyValuePair<string, int>("gold", _rewards.gold));
                items.Add(new KeyValuePair<string, int>("xp", _rewards.passXp));
                chestId = _rewards.chestId;
            }
            int slots = items.Count + (chestId != null ? 1 : 0);
            float slotW = slots > 0 ? Mathf.Min(230f, 960f / slots) : 230f;
            float x0 = -(slots - 1) * slotW * 0.5f;
            for (int i = 0; i < items.Count; i++)
            {
                var cell = UIKit.Rect("item" + i, card).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x0 + i * slotW, -70), new Vector2(slotW - 16, 200));
                Icons.Currency(cell, items[i].Key).rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(110, 110));
                var amount = UIKit.Label(cell, "", 44, items[i].Key == "trophy" && items[i].Value < 0 ? Palette.Red : Palette.Text);
                amount.rectTransform.Band(false, 64, 10);
                AddCounter(amount, items[i].Value, items[i].Value >= 0 ? "+" : "", 0.9f + i * 0.25f);
                AddPop(cell, 0.85f + i * 0.25f);
            }
            if (chestId != null)
            {
                var cell = UIKit.Rect("chest", card).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x0 + items.Count * slotW, -70), new Vector2(slotW - 16, 200));
                Icons.Chest(cell, chestId).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(130, 110));
                UIKit.Label(cell, Loc.T("chest." + chestId), 26, Palette.Text).rectTransform.Band(false, 64, 10);
                AddPop(cell, 0.85f + items.Count * 0.25f);
            }
            if (!_tutorial && !Friendly && _valid && _rewards.bonusGold > 0)
            {
                var streak = UIKit.Panel(card, "streak", Art.Hex(0xE0582F)).rectTransform
                    .Place(new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-16, -4), new Vector2(330, 48));
                string text = _summary.revenge ? Loc.F("result.revenge_won", _rewards.bonusGold) : Loc.F("result.streak", _rewards.winStreak, _rewards.bonusGold);
                UIKit.Outlined(UIKit.Label(streak, text, 24, Palette.Text), 1.5f).rectTransform.Stretch(6, 6, 0, 0);
                AddPop(streak, 1.3f);
            }
            if (!_tutorial && !Friendly && _valid && _rewards.chestSlotsFull)
                UIKit.Label(card, Loc.T("result.chest_full"), 26, Palette.TextDim).rectTransform.Band(false, 40, 120);

            // Trophy road: animates from the old to the new trophy count.
            Text roadLabel;
            _road = Deco.TrophyRoad(card, meta.State.trophies, out roadLabel);
            _road.Root.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(30, 30), new Vector2(760, 54));
            _roadLabel = roadLabel;
            _roadTrophiesFrom = trophiesBefore;
            _roadTrophiesTo = meta.State.trophies;
            _road.Root.gameObject.SetActive(_ranked && !_tutorial && _valid);

            // Defeat tip
            if (_lost && !_tutorial)
            {
                var tip = UIKit.Label(Root, Loc.T("result.tip." + (_summary.merges < 10 ? "merge" : (_summary.sends < 3 ? "send" : "upgrade"))), 28, Palette.TextDim);
                tip.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(940, 80));
            }

            // Buttons: [x2 gold (ad)] [play again] + big CONTINUE (paw button art)
            _doubleGold = UIKit.ChunkyButton(Root, "double", "", Palette.Green, DoubleGold, 34);
            var dgRt = _doubleGold.GetComponent<RectTransform>();
            dgRt.Place(new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-10, 300), new Vector2(480, 140));
            var dgFace = _doubleGold.transform.Find("face");
            var goldIcon = Icons.Currency(dgFace, "gold").rectTransform;
            goldIcon.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14, 2), new Vector2(96, 96));
            var x2 = UIKit.Panel(goldIcon, "x2", Palette.Red).rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(64, 40));
            UIKit.Label(x2, "x2", 26, Palette.Text).rectTransform.Stretch();
            _doubleTitle = UIKit.Outlined(UIKit.Label(dgFace, Loc.T("result.x2_title"), 36, Palette.Text, TextAnchor.LowerLeft), 2.5f);
            _doubleTitle.rectTransform.Stretch(124, 10, 10, 56);
            var adRow = UIKit.Rect("ad", dgFace).Stretch(124, 10, 64, 14);
            AdIcon(adRow).Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(40, 40));
            UIKit.Label(adRow, Loc.T("result.watch_ad"), 22, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleLeft).rectTransform.Stretch(48, 0, 0, 0);
            AddShine(dgFace);
            _doubleGold.gameObject.SetActive(!_tutorial && _valid && _rewards.gold > 0 && Services.Ads.IsRewardedReady);

            // Rematch the same opponent; after a loss it is a Revenge match (bonus gold if you win).
            int oppIndex = 1 - Mathf.Clamp(_summary.localPlayer, 0, 1);
            var oppSetup = _record.setup != null && _record.setup.players != null && _record.setup.players.Length > 1 ? _record.setup.players[oppIndex] : null;
            // Online: a real player has gone, so "play again" starts a new search instead of a bot copy of them.
            if (_summary.online) oppSetup = null;
            bool revenge = _lost && oppSetup != null;
            System.Action rematch = () =>
            {
                if (oppSetup != null) App.Show(new MatchmakingScreen(_ranked, oppSetup.name, oppSetup.deck, revenge));
                else App.Show(new MatchmakingScreen(_ranked));
            };
            var again = UIKit.ChunkyButton(Root, "again", "", revenge ? Art.Hex(0xE0582F) : Palette.Blue, rematch, 34);
            var agRt = again.GetComponent<RectTransform>();
            agRt.Place(new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(10, 300), new Vector2(480, 140));
            var agFace = again.transform.Find("face");
            ReplayIcon(agFace).Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18, 2), new Vector2(88, 88));
            UIKit.Outlined(UIKit.Label(agFace, Loc.T(revenge ? "result.revenge" : "result.rematch"), 38, Palette.Text, TextAnchor.LowerLeft), 2.5f).rectTransform.Stretch(122, 10, 10, 50);
            UIKit.Label(agFace, revenge ? Loc.F("result.revenge_bonus", App.MetaConfig.revengeBonusPercent) : (oppSetup != null ? oppSetup.name : ""), 22, new Color(1f, 1f, 1f, 0.9f), TextAnchor.UpperLeft).rectTransform.Stretch(122, 10, 82, 8);
            again.gameObject.SetActive(!_tutorial && !Friendly);
            if (!_doubleGold.gameObject.activeSelf) agRt.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 300), new Vector2(560, 140));

            var cont = UIKit.ChunkyButton(Root, "continue", Loc.T("result.continue"), Art.Hex(0xFFB020), () => AdPolicy.AfterMatch(App, () => App.Show(new LobbyScreen())), 60, "button_battle");
            UIKit.Outlined(cont.transform.Find("face").GetComponentInChildren<Text>(), 4f);
            cont.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(780, 176));
            AddPop(cont.GetComponent<RectTransform>(), 1.6f);
            AddPop(agRt, 1.75f);
            AddPop(dgRt, 1.85f);

            if (tutorialRewards != null) Modal.Rewards(App, Loc.T("chest.silver"), tutorialRewards, locked, true, "silver");
        }

        Text _doubleTitle;
        RectTransform _shine;

        /// <summary>Ad badge: white rounded square with a play triangle (art ZooArt/UI/icon_ad.png if present).</summary>
        static RectTransform AdIcon(RectTransform parent)
        {
            var art = Art.Icon("icon_ad");
            if (art != null) return UIKit.Icon(parent, "adIcon", art, Color.white).rectTransform;
            var bg = UIKit.Panel(parent, "adIcon", Color.white).rectTransform;
            var tri = UIKit.Icon(bg, "play", Art.Diamond, Palette.Green).rectTransform;
            tri.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(3, 0), new Vector2(22, 22));
            return bg;
        }

        /// <summary>Replay badge (art ZooArt/UI/icon_replay.png if present, else a trophy).</summary>
        static RectTransform ReplayIcon(Transform parent)
        {
            var art = Art.Icon("icon_replay");
            if (art != null) return UIKit.Icon(parent, "replay", art, Color.white).rectTransform;
            return Icons.Currency(parent, "trophy").rectTransform;
        }

        /// <summary>Diagonal light streak that sweeps across a button face (animated in Tick).</summary>
        void AddShine(Transform face)
        {
            var mask = UIKit.Rect("shineMask", face).Stretch(6, 6, 6, 6);
            mask.gameObject.AddComponent<RectMask2D>();
            _shine = UIKit.Panel(mask, "shine", new Color(1f, 1f, 1f, 0.35f), false).rectTransform;
            _shine.Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-100, 0), new Vector2(46, 220));
            _shine.localRotation = Quaternion.Euler(0f, 0f, -20f);
        }

        void AddPop(RectTransform rt, float at)
        {
            rt.localScale = Vector3.zero;
            _pops.Add(rt);
            _popAt.Add(at);
        }

        void AddCounter(Text t, int target, string prefix, float at)
        {
            t.text = prefix + "0";
            _counters.Add(t);
            _counterTarget.Add(target);
            _counterPrefix.Add(prefix);
            _counterAt.Add(at);
        }

        public override void Tick(float dt)
        {
            float prev = _t;
            _t += dt;
            for (int i = 0; i < _pops.Count; i++)
            {
                float k = (_t - _popAt[i]) / 0.35f;
                float s = k <= 0f ? 0f : Deco.EaseOutBack(k);
                _pops[i].localScale = new Vector3(s, s, 1f);
                if (prev < _popAt[i] && _t >= _popAt[i] && i > 0) AudioService.Play(Sfx.Reward, 0.4f);
            }
            for (int i = 0; i < _counters.Count; i++)
            {
                float k = Mathf.Clamp01((_t - _counterAt[i]) / 0.6f);
                int v = Mathf.RoundToInt(_counterTarget[i] * (1f - (1f - k) * (1f - k)));
                _counters[i].text = (_counterTarget[i] < 0 ? "" : _counterPrefix[i]) + v;
            }
            if (_rays != null) _rays.localRotation = Quaternion.Euler(0f, 0f, -_t * 14f);
            if (_shine != null && _doubleGold != null && _doubleGold.interactable)
            {
                float cycle = (_t % 2.4f) / 0.9f;   // sweep for 0.9 s every 2.4 s
                _shine.anchoredPosition = new Vector2(Mathf.Lerp(-100f, 600f, Mathf.Clamp01(cycle)), 0f);
            }

            // Hero: hops for joy (victory), sways sadly (defeat), breathes (draw).
            if (_won)
            {
                float hop = Mathf.Abs(Mathf.Sin(_t * 4.2f));
                _hero.anchoredPosition = new Vector2(0f, -850f + hop * 70f);
                float sq = 1f + 0.06f * (1f - hop);
                _hero.localScale = new Vector3(sq, 2f - sq, 1f);
                _heroShadow.localScale = new Vector3(1f - hop * 0.3f, 1f - hop * 0.3f, 1f);
            }
            else if (_lost)
            {
                _hero.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_t * 1.3f) * 4f);
                _hero.localScale = new Vector3(1.02f, 0.94f, 1f);
            }
            else
            {
                float b = 1f + 0.02f * Mathf.Sin(_t * 2f);
                _hero.localScale = new Vector3(b, b, 1f);
            }

            // Trophy road fills after the rewards arrive.
            if (_road != null && _road.Root.gameObject.activeSelf)
            {
                float k = Mathf.Clamp01((_t - 1.4f) / 0.8f);
                int shown = Mathf.RoundToInt(Mathf.Lerp(_roadTrophiesFrom, _roadTrophiesTo, k));
                var cur = MetaRules.ArenaFor(shown);
                int next = -1;
                foreach (var a in MetaRules.Arenas) if (a.minTrophies > shown) { next = a.minTrophies; break; }
                _road.Set(next < 0 ? 1f : (shown - cur.minTrophies) / (float)Mathf.Max(1, next - cur.minTrophies), next < 0 ? Loc.T("lobby.top_arena") : shown + " / " + next);
            }
        }

        void DoubleGold()
        {
            _doubleGold.interactable = false;
            Services.Ads.ShowRewarded("result_double_gold", granted =>
            {
                if (!granted) return;
                App.Meta.State.gold += _rewards.gold;
                App.SaveProfile();
                _doubleTitle.text = Loc.T("result.gold_doubled");
            });
        }
    }
}
