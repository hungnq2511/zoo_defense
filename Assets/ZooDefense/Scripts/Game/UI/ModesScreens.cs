using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>Starts every game mode with the right rules, decks and card levels.</summary>
    public static class ModeLauncher
    {
        static int NewSeed() { return UnityEngine.Random.Range(1, int.MaxValue); }

        public static void Daily(GameApp app)
        {
            int day = GameCalendar.DayIndex(app.Now);
            var deck = DailyChallenge.Deck(app.Content, day);
            var rules = MatchRules.Daily();
            rules.waveElements = DailyChallenge.WaveElements(day);
            app.Show(new BattleScreen(new BattleConfig
            {
                seed = DailyChallenge.Seed(day), rules = rules, playerDeck = deck, playerLevels = DailyChallenge.Levels(deck.Length),
                botDeck = deck, opponentName = "-", arena = 1 + day % 5,
            }));
        }

        public static void Endless(GameApp app)
        {
            app.Show(new BattleScreen(new BattleConfig { seed = NewSeed(), rules = MatchRules.Endless(), opponentName = "-" }));
        }

        public static void Campaign(GameApp app, int index)
        {
            var lv = Core.Campaign.Get(index);
            string[] deck = app.Profile.deck;
            if (lv.onlyElement != Element.None)
            {
                // Only that element: the player's strongest unlocked animals of it.
                var list = new List<string>();
                foreach (var id in app.Profile.deck) { var d = app.Content.Unit(id); if (d != null && d.element == lv.onlyElement) list.Add(id); }
                foreach (var u in app.Content.Units)
                    if (list.Count < 5 && u.element == lv.onlyElement && app.Meta.IsUnlocked(u.id) && !list.Contains(u.id)) list.Add(u.id);
                if (list.Count == 0) { app.Toast(Loc.F("campaign.need_element", Loc.ElementName(lv.onlyElement))); return; }
                deck = list.ToArray();
            }
            app.Show(new BattleScreen(new BattleConfig
            {
                seed = NewSeed(), rules = Core.Campaign.Rules(lv), playerDeck = deck, campaignLevel = index, opponentName = "-", arena = lv.region + 1,
            }));
        }

        public static void Mayhem(GameApp app)
        {
            var rule = GameCalendar.MayhemOfWeek(GameCalendar.WeekIndex(app.Now));
            int seed = NewSeed();
            var cfg = new BattleConfig { seed = seed, rules = MatchRules.MayhemOf(rule), difficulty = 1, opponentName = MetaRules.RandomBotName(seed) };
            if (rule == MayhemRule.Mirror)
            {
                // Same random deck and card levels for both: pure skill.
                cfg.playerDeck = BotBrain.RandomDeck(app.Content, seed ^ 0x77);
                cfg.botDeck = cfg.playerDeck;
                cfg.playerLevels = TournamentRules.Levels(cfg.playerDeck.Length);
                cfg.botLevels = cfg.playerLevels;
            }
            app.Show(new BattleScreen(cfg));
        }

        public static void Raid(GameApp app)
        {
            int seed = NewSeed();
            var levels = app.Meta.DeckLevels(app.Profile.deck);
            var partnerDeck = BotBrain.RandomDeck(app.Content, seed ^ 0x3c);
            app.Show(new BattleScreen(new BattleConfig
            {
                seed = seed, rules = MatchRules.Raid(), difficulty = 2, opponentName = Loc.F("raid.partner", MetaRules.RandomBotName(seed)),
                botDeck = partnerDeck, botLevels = MetaRules.BotCardLevels(levels, partnerDeck.Length, seed),
            }));
        }

        public static void TournamentRound(GameApp app)
        {
            int round = app.Meta.State.tournamentRound;
            int seed = NewSeed();
            var deck = app.Profile.deck;
            app.Show(new BattleScreen(new BattleConfig
            {
                seed = seed, rules = MatchRules.Tournament(), difficulty = TournamentRules.BotDifficulty(round),
                opponentName = TournamentScreen.OpponentName(app, round), playerDeck = deck, playerLevels = TournamentRules.Levels(deck.Length),
                botDeck = BotBrain.RandomDeck(app.Content, app.Meta.State.tournamentWeek * 31 + round), botLevels = TournamentRules.Levels(5),
            }));
        }

        public static void Replay(GameApp app, ReplayStore.Entry entry)
        {
            app.Show(new BattleScreen(new BattleConfig { seed = entry.record.setup.seed, replay = entry.record, opponentName = entry.opponent }));
        }

        public static void Again(GameApp app, BattleConfig cfg)
        {
            if (cfg == null) { app.Show(new ModesScreen()); return; }
            switch (cfg.Mode)
            {
                case MatchMode.Daily: Daily(app); break;
                case MatchMode.Endless: Endless(app); break;
                case MatchMode.Campaign: Campaign(app, cfg.campaignLevel); break;
                case MatchMode.Mayhem: Mayhem(app); break;
                case MatchMode.Raid: Raid(app); break;
                case MatchMode.Tournament: app.Show(new TournamentScreen()); break;
                default: app.Show(new ModesScreen()); break;
            }
        }
    }

    /// <summary>Shared look of the mode screens: background, currency bar, ribbon title, back button, scroll list.</summary>
    public abstract class ListScreen : ScreenBase
    {
        protected RectTransform List;
        protected float Y;
        protected CurrencyBar Bar;

        protected RectTransform Frame(string title, Color color, Action back)
        {
            Deco.ScreenBackground(Root, App, color);
            Bar = new CurrencyBar(Root, App, false);
            float top = Deco.Header(Root, title, color);
            if (back != null)
            {
                var b = UIKit.ChunkyButton(Root, "back", "‹", Art.Hex(0x5B3A22), () => back(), 60);
                b.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18, -CurrencyBar.Height - 20), new Vector2(110, 96));
            }
            var area = UIKit.Rect("list", Root).Stretch(20, 20, top, back == null ? NavBar.Height + 6 : 24);
            List = ScrollKit.Vertical(area);
            Y = 8;
            return List;
        }

        protected RectTransform Card(float height, Color border)
        {
            var c = Deco.Frame(List, "card", border, Art.Hex(0x261C3C), 7f);
            ((RectTransform)c.parent).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -Y), new Vector2(1020, height));
            Y += height + 18;
            return c;
        }

        protected void Section(string text)
        {
            Deco.Section(List, text, Y, Palette.Primary);
            Y += 74;
        }

        protected void Done() { ScrollKit.SetHeight(List, Y + 40); }

        protected static Text Label(RectTransform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var t = UIKit.Label(parent, text, size, color, align);
            t.supportRichText = true;
            return t;
        }

        protected static Button Btn(RectTransform parent, string text, Color color, Action click, Vector2 pos, Vector2 size, int font = 34)
        {
            var b = UIKit.ChunkyButton(parent, "btn", text, color, () => click(), font);
            b.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), pos, size);
            return b;
        }

        protected static void Badge(RectTransform parent)
        {
            var badge = UIKit.Icon(parent, "badge", Art.Circle, Palette.Red);
            badge.rectTransform.Place(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-12, -12), new Vector2(46, 46));
            UIKit.Label(badge.transform, "!", 30, Palette.Text).rectTransform.Stretch();
            UIKit.Motion(badge.rectTransform).Pulse(0.12f, 5f);
        }
    }

    /// <summary>
    /// "Game modes" hub: this week's live event, the ranked season, every mode card, and shortcuts to the bestiary,
    /// achievements and replays.
    /// </summary>
    public sealed class ModesScreen : ListScreen
    {
        protected override void Build()
        {
            var meta = App.Meta;
            long now = App.Now;
            meta.Refresh(now);
            var s = meta.State;
            Frame(Loc.T("modes.title"), Art.Hex(0x3D8BFF), () => App.Show(new LobbyScreen()));

            // ---- Shortcuts: bestiary, achievements, replays
            var row = UIKit.Rect("shortcuts", List).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -Y), new Vector2(1020, 150));
            Shortcut(row, 0, Loc.T("bestiary.title"), "xp", Art.Hex(0x4CC46B), meta.ClaimableBestiary() > 0, () => App.Show(new BestiaryScreen()));
            Shortcut(row, 1, Loc.T("achievements.title"), "trophy", Art.Hex(0xFFB020), meta.ClaimableAchievements() > 0, () => App.Show(new AchievementsScreen()));
            Shortcut(row, 2, Loc.T("replay.title"), "star", Art.Hex(0x9B5CFF), false, () => App.Show(new ReplaysScreen()));
            Y += 168;

            // ---- Live event of the week
            var ev = LiveOps.Current(now);
            var evCard = Card(170, Art.Hex(0xFF7AC8));
            Label(evCard, Loc.T("event.title"), 26, Palette.Primary, TextAnchor.UpperLeft).rectTransform.Stretch(24, 24, 12, 110);
            Label(evCard, EventName(ev), 38, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(24, 260, 46, 50);
            Label(evCard, EventDesc(ev), 24, Palette.TextDim, TextAnchor.UpperLeft).rectTransform.Stretch(24, 260, 100, 6);
            var ends = Deco.Pill(evCard, null, Loc.F("event.ends", Fmt.Duration(GameCalendar.WeekEnd(now) - now)), 22);
            ends.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16, -16), new Vector2(240, 50));
            if (ev.kind == LiveOps.EventKind.ElementWeek)
            {
                var dot = UIKit.Rect("el", evCard).Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-90, 14), new Vector2(84, 84));
                UIKit.Icon(dot, "c", Art.Circle, Art.ElementColor(ev.element)).rectTransform.Stretch();
                var em = ElementGuide.Emblem(ev.element);
                if (em != null) UIKit.Icon(dot, "e", em, Color.white).rectTransform.Stretch(8, 8, 8, 8);
            }

            // ---- Ranked season
            var season = Card(190, Art.Hex(0x5BD6FF));
            Label(season, Loc.F("season.title", meta.Season(now) + 1), 34, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(24, 24, 14, 120);
            Label(season, Loc.F("season.best", s.seasonBestTrophies, MetaService.SeasonResetFloor), 24, Palette.TextDim, TextAnchor.UpperLeft).rectTransform.Stretch(24, 300, 62, 10);
            var sEnds = Deco.Pill(season, null, Loc.F("season.ends", Fmt.Duration(meta.SeasonSecondsLeft(now))), 22);
            sEnds.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16, -16), new Vector2(260, 50));
            if (s.pendingSeasonTier > 0)
            {
                Btn(season, Loc.T("season.claim"), Palette.Green, ClaimSeason, new Vector2(-16, 14), new Vector2(260, 96), 30);
                Badge((RectTransform)season.parent);
            }

            // ---- Modes
            Section(Loc.T("modes.solo"));
            int day = GameCalendar.DayIndex(now);
            ModeCard(MatchMode.Daily, Loc.T("mode.daily"), Loc.F("mode.daily_desc", DailyChallenge.CardLevel),
                s.dailyBest > 0 ? Loc.F("mode.best_today", s.dailyBest) : Loc.F("mode.resets", Fmt.Duration(GameCalendar.DayEnd(now) - now)),
                Loc.T("mode.play"), true, () => ModeLauncher.Daily(App), s.dailyBest == 0);
            ModeCard(MatchMode.Endless, Loc.T("mode.endless"), Loc.T("mode.endless_desc"),
                Loc.F("mode.best_week", s.endlessBest, s.endlessRecord), Loc.T("mode.play"), true, () => ModeLauncher.Endless(App), false);
            ModeCard(MatchMode.Campaign, Loc.T("mode.campaign"), Loc.T("mode.campaign_desc"),
                Loc.F("mode.stars", meta.TotalStars(), Campaign.Count * 3), Loc.T("mode.open"), true, () => App.Show(new CampaignScreen()), false);

            Section(Loc.T("modes.versus"));
            bool weekend = GameCalendar.IsWeekend(now);
            var rule = GameCalendar.MayhemOfWeek(GameCalendar.WeekIndex(now));
            ModeCard(MatchMode.Mayhem, Loc.T("mode.mayhem") + " · " + Loc.T("mayhem." + (int)rule), Loc.T("mayhem_desc." + (int)rule),
                weekend ? Loc.F("mode.mayhem_wins", s.mayhemWins) : Loc.F("mode.opens_in", Fmt.Duration(OpensIn(now))),
                Loc.T("mode.play"), weekend, () => ModeLauncher.Mayhem(App), false);
            ModeCard(MatchMode.Tournament, Loc.T("mode.tournament"), Loc.F("mode.tournament_desc", TournamentRules.CardLevel),
                meta.TournamentRunActive ? Loc.F("mode.tournament_round", s.tournamentRound + 1, TournamentRules.Rounds) : Loc.T("mode.tournament_idle"),
                Loc.T("mode.open"), true, () => App.Show(new TournamentScreen()), false);
            ModeCard(MatchMode.Versus, Loc.T("lobby.practice_short"), Loc.T("mode.practice_desc"), "", Loc.T("mode.play"), true, () => App.Show(new MatchmakingScreen(false)), false);

            Section(Loc.T("modes.coop"));
            ModeCard(MatchMode.Raid, Loc.T("mode.raid"), Loc.T("mode.raid_desc"),
                Loc.F("mode.raid_chests", Math.Max(0, MetaService.RaidChestsPerDay - s.raidRewarded), MetaService.RaidChestsPerDay), Loc.T("mode.play"), true, () => ModeLauncher.Raid(App), false);
            Label(List, Loc.T("modes.online_note"), 22, Palette.TextDim, TextAnchor.UpperCenter).rectTransform
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -Y), new Vector2(980, 70));
            Y += 80;
            Done();
            NavBar.Build(Root, App, Tab.Battle);
        }

        static long OpensIn(long now)
        {
            long weekStart = GameCalendar.WeekEnd(now) - 7 * GameCalendar.Day;
            long friday = weekStart + 4 * GameCalendar.Day;
            return Math.Max(0, friday - now);
        }

        void ClaimSeason()
        {
            var r = App.Meta.ClaimSeasonReward();
            App.SaveProfile();
            if (r != null) Modal.Rewards(App, Loc.T("season.reward_title"), r, null, true, "magic");
            App.Show(new ModesScreen());
        }

        public static string EventName(LiveOps.WeekEvent ev)
        {
            switch (ev.kind)
            {
                case LiveOps.EventKind.ElementWeek: return Loc.F("event.element", Loc.ElementName(ev.element));
                case LiveOps.EventKind.BossWeek: return Loc.F("event.boss", Loc.EnemyName(GameApp.Instance.Content.Enemy(ev.bossId)));
                default: return Loc.T("event.candy");
            }
        }

        public static string EventDesc(LiveOps.WeekEvent ev)
        {
            switch (ev.kind)
            {
                case LiveOps.EventKind.ElementWeek: return Loc.F("event.element_desc", Loc.ElementName(ev.element), Loc.ElementName(ElementRules.WeakTo(ev.element)));
                case LiveOps.EventKind.BossWeek: return Loc.T("event.boss_desc");
                default: return Loc.T("event.candy_desc");
            }
        }

        void Shortcut(RectTransform row, int i, string text, string icon, Color color, bool badge, Action click)
        {
            var b = UIKit.ChunkyButton(row, "sc" + i, "", color, () => click(), 26);
            b.GetComponent<RectTransform>().Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 346, 0), new Vector2(330, 140));
            var face = b.transform.Find("face");
            Icons.Currency(face, icon).rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14, 4), new Vector2(84, 84));
            var t = face.GetComponentInChildren<Text>();
            t.text = text;
            t.fontSize = 28;
            t.rectTransform.Stretch(100, 8, 0, 0);
            if (badge) Badge(b.GetComponent<RectTransform>());
        }

        void ModeCard(MatchMode mode, string title, string desc, string status, string button, bool enabled, Action click, bool badge)
        {
            var color = BattleScreen.ModeColor(mode);
            var c = Card(230, color);
            var icon = UIKit.Rect("icon", c).Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16, 6), new Vector2(150, 150));
            BattleScreen.ModeIcon(icon, mode);
            UIKit.Outlined(Label(c, title, 36, Palette.Text, TextAnchor.UpperLeft), 2f).rectTransform.Stretch(186, 24, 14, 160);
            Label(c, desc, 24, Palette.TextDim, TextAnchor.UpperLeft).rectTransform.Stretch(186, 280, 62, 50);
            if (!string.IsNullOrEmpty(status))
                Label(c, status, 24, Art.Hex(0xFFE7A8), TextAnchor.LowerLeft).rectTransform.Stretch(186, 280, 150, 12);
            var b = Btn(c, button, enabled ? Palette.Green : Palette.Disabled, click, new Vector2(-16, 16), new Vector2(240, 110), 34);
            b.interactable = enabled;
            if (badge) Badge((RectTransform)c.parent);
        }
    }

    /// <summary>Campaign map: 5 regions (one per arena), 10 levels each, 1-3 stars.</summary>
    public sealed class CampaignScreen : ListScreen
    {
        static int _region = -1;

        protected override void Build()
        {
            var meta = App.Meta;
            meta.Refresh(App.Now);
            if (_region < 0)
            {
                _region = 0;
                for (int i = 0; i < Campaign.Count; i++) if (meta.IsCampaignOpen(i)) _region = i / Campaign.LevelsPerRegion;
            }
            Frame(Loc.T("mode.campaign"), Art.Hex(0x4CC46B), () => App.Show(new ModesScreen()));

            // Region tabs
            var tabs = UIKit.Rect("regions", List).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -Y), new Vector2(1020, 110));
            for (int r = 0; r < Campaign.Regions; r++)
            {
                int region = r;
                bool open = meta.IsCampaignOpen(r * Campaign.LevelsPerRegion);
                var t = UIKit.ChunkyButton(tabs, "r" + r, (r + 1).ToString(), r == _region ? Palette.Primary : (open ? Palette.Panel : Palette.Disabled), () => { _region = region; App.Show(new CampaignScreen()); }, 40);
                t.GetComponent<RectTransform>().Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(r * 206, 0), new Vector2(194, 100));
                t.interactable = open;
            }
            Y += 126;

            var arena = MetaRules.Arenas[_region];
            var head = Card(150, arena.tint);
            Label(head, Loc.F("campaign.region", _region + 1, Loc.ArenaName(arena)), 38, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(24, 24, 14, 70);
            int regionStars = 0;
            for (int i = 0; i < Campaign.LevelsPerRegion; i++) regionStars += meta.State.campaignStars[_region * Campaign.LevelsPerRegion + i];
            Label(head, Loc.F("campaign.region_stars", regionStars, Campaign.LevelsPerRegion * 3), 26, Palette.TextDim, TextAnchor.LowerLeft).rectTransform.Stretch(24, 24, 70, 14);

            // Level grid: 2 columns
            for (int k = 0; k < Campaign.LevelsPerRegion; k++)
            {
                int index = _region * Campaign.LevelsPerRegion + k;
                var lv = Campaign.Get(index);
                bool open = meta.IsCampaignOpen(index);
                int stars = meta.State.campaignStars[index];
                int col = k % 2;
                var tile = Deco.Frame(List, "lv" + k, open ? (lv.number == 10 ? Palette.Red : arena.tint) : Palette.Disabled, Art.Hex(0x261C3C), 6f);
                ((RectTransform)tile.parent).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(col == 0 ? -258 : 258, -Y), new Vector2(500, 210));
                Label(tile, Loc.F("campaign.level", _region + 1, lv.number), 34, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(20, 20, 12, 150);
                Label(tile, open ? BattleScreen.CampaignRuleText(lv) : Loc.T("campaign.locked"), 22, Palette.TextDim, TextAnchor.UpperLeft).rectTransform.Stretch(20, 20, 58, 60);
                Label(tile, Loc.F("campaign.goal_short", lv.winWave), 22, Art.Hex(0xFFE7A8), TextAnchor.LowerLeft).rectTransform.Stretch(20, 200, 140, 14);
                for (int st = 0; st < 3; st++)
                {
                    var star = Art.Icon("star");
                    var img = UIKit.Icon(tile, "star" + st, star != null ? star : Art.Diamond, st < stars ? Color.white : new Color(0.3f, 0.26f, 0.4f, 1f));
                    img.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16 - (2 - st) * 52, 12), new Vector2(48, 48));
                }
                if (open)
                {
                    var tap = UIKit.Button(tile, "tap", "", new Color(0f, 0f, 0f, 0f), () => LevelPopup(index));
                    tap.GetComponent<RectTransform>().Stretch();
                }
                if (col == 1 || k == Campaign.LevelsPerRegion - 1) Y += 226;
            }
            Done();
        }

        void LevelPopup(int index)
        {
            var lv = Campaign.Get(index);
            var box = Modal.Open(App, Loc.F("campaign.level", lv.region + 1, lv.number), 900);
            var rows = new[]
            {
                Loc.F("campaign.goal_long", lv.winWave),
                BattleScreen.CampaignRuleText(lv),
                Loc.F("campaign.hearts", lv.hearts),
                Loc.F("campaign.boss", Loc.EnemyName(App.Content.Enemy(lv.bossId)), lv.bossEvery),
                Loc.T("campaign.stars_rule"),
            };
            for (int i = 0; i < rows.Length; i++)
                UIKit.Label(box, rows[i], 30, i == 1 ? Palette.Primary : Palette.Text, TextAnchor.MiddleLeft).rectTransform
                    .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -110 - i * 90), new Vector2(840, 84));
            int stars = App.Meta.State.campaignStars[index];
            string reward = stars == 0 ? Loc.F("campaign.first_reward", lv.gold, lv.gems) : Loc.F("campaign.best_stars", stars);
            UIKit.Label(box, reward, 28, Art.Hex(0xFFE7A8), TextAnchor.MiddleCenter).rectTransform
                .Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 180), new Vector2(840, 60));
            var play = UIKit.ChunkyButton(box, "play", Loc.T("mode.play"), Palette.Green, () => ModeLauncher.Campaign(App, index), 46);
            play.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(520, 124));
        }
    }

    /// <summary>Weekend tournament: 3 knockout rounds, card levels set to 11, free entry once a week.</summary>
    public sealed class TournamentScreen : ListScreen
    {
        static readonly string[] Names = { "PawsOfFury", "KingKiwi", "LadyLynx", "Thornado", "SnowPrince", "MossBoss", "RubyRoar", "Quackers" };

        public static string OpponentName(GameApp app, int round)
        {
            int week = app.Meta.State.tournamentWeek;
            return Names[Mathf.Abs(week * 3 + round * 5) % Names.Length];
        }

        protected override void Build()
        {
            var meta = App.Meta;
            meta.Refresh(App.Now);
            var s = meta.State;
            Frame(Loc.T("mode.tournament"), Art.Hex(0x3D8BFF), () => App.Show(new ModesScreen()));

            var info = Card(170, Art.Hex(0x3D8BFF));
            Label(info, Loc.F("tournament.rules", TournamentRules.CardLevel), 26, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(24, 24, 14, 14);

            // Bracket: one row per round, the player's path through it.
            string[] roundKeys = { "tournament.quarter", "tournament.semi", "tournament.final" };
            for (int r = 0; r < TournamentRules.Rounds; r++)
            {
                bool done = r < s.tournamentRound;
                bool current = meta.TournamentRunActive && r == s.tournamentRound;
                bool lost = s.tournamentOut && r == s.tournamentRound && r < TournamentRules.Rounds && s.tournamentEntries > 0;
                var color = done ? Palette.Green : (current ? Palette.Primary : (lost ? Palette.Red : Palette.Disabled));
                var c = Card(170, color);
                Label(c, Loc.T(roundKeys[r]), 34, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(24, 24, 14, 90);
                Label(c, Loc.F("tournament.vs", OpponentName(App, r)), 28, Palette.TextDim, TextAnchor.LowerLeft).rectTransform.Stretch(24, 300, 80, 18);
                string state = done ? Loc.T("tournament.won") : (lost ? Loc.T("tournament.lost") : (current ? "" : "—"));
                if (current) Btn(c, Loc.T("mode.play"), Palette.Green, () => ModeLauncher.TournamentRound(App), new Vector2(-16, 16), new Vector2(240, 110), 34);
                else Label(c, state, 30, color, TextAnchor.MiddleRight).rectTransform.Stretch(24, 30, 0, 0);
            }

            var prize = Card(150, Art.Hex(0xFFB020));
            Label(prize, Loc.T("tournament.prize"), 28, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(24, 24, 0, 0);

            if (!meta.TournamentRunActive)
            {
                bool free = s.tournamentEntries == 0;
                var c = Card(170, Palette.Green);
                Label(c, free ? Loc.T("tournament.free_entry") : Loc.F("tournament.paid_entry", MetaService.TournamentReentryGems), 28, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(24, 300, 0, 0);
                Btn(c, Loc.T("tournament.enter"), Palette.Green, Enter, new Vector2(-16, 26), new Vector2(260, 110), 34);
            }
            Done();
        }

        void Enter()
        {
            if (!App.Meta.EnterTournament()) { App.Toast(Loc.T("reject.gems")); return; }
            App.SaveProfile();
            App.Show(new TournamentScreen());
        }
    }

    /// <summary>Result of a non-ranked mode: score / stars, record, rewards, play again.</summary>
    public sealed class ModeResultScreen : ScreenBase
    {
        readonly BattleConfig _cfg;
        readonly MatchRecord _record;
        readonly MatchSummary _summary;

        public ModeResultScreen(BattleConfig cfg, MatchRecord record, MatchSummary summary)
        {
            _cfg = cfg;
            _record = record;
            _summary = summary;
        }

        protected override void Build()
        {
            var meta = App.Meta;
            var r = meta.ApplyMode(_summary, App.Now);
            App.Profile.matchesPlayed++;
            if (_summary.winner == 0) App.Profile.wins++;
            App.SaveProfile();
            Leaderboards.SubmitProgress(App);
            Services.Analytics.LogEvent("mode_end", new Dictionary<string, object> { { "mode", _summary.mode.ToString() }, { "wave", _summary.wave }, { "winner", _summary.winner } });
            AudioService.PlayMusic("music_lobby");
            bool won = _summary.winner == 0;
            bool score = _summary.mode == MatchMode.Daily || _summary.mode == MatchMode.Endless;
            AudioService.Play(won || (score && r.newRecord) ? Sfx.Win : Sfx.Lose);

            var color = BattleScreen.ModeColor(_summary.mode);
            UIKit.Panel(Root, "bg", Color.Lerp(color, Color.black, 0.65f), false).rectTransform.Stretch();
            var glow = UIKit.Icon(Root, "glow", Art.Glow, new Color(color.r, color.g, color.b, 0.6f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -560), new Vector2(1000, 1000));
            var rays = UIKit.Icon(Root, "rays", Art.Rays, new Color(1f, 0.9f, 0.6f, won || r.newRecord ? 0.45f : 0.12f)).rectTransform;
            rays.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -560), new Vector2(1300, 1300));
            UIKit.Motion(rays).Spin(10f);

            string title;
            if (score) title = r.newRecord ? Loc.T("result.new_record") : Loc.T("result.run_over");
            else if (_summary.mode == MatchMode.Tournament && r.tournamentWon) title = Loc.T("tournament.champion");
            else title = Loc.T(won ? "result.victory" : "result.defeat");
            Deco.Ribbon(Root, title, won || r.newRecord ? Art.Hex(0xE58A1F) : Art.Hex(0x5A5080), 56, !(won || r.newRecord))
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -260), new Vector2(900, 170));

            var icon = UIKit.Rect("icon", Root).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -520), new Vector2(260, 260));
            BattleScreen.ModeIcon(icon, _summary.mode);
            UIKit.Motion(icon).PopIn(0.1f, 0.4f);

            // Big number: wave reached, or stars
            string big, small;
            switch (_summary.mode)
            {
                case MatchMode.Daily: big = Loc.F("result.wave", _summary.wave); small = Loc.F("mode.best_today", r.best); break;
                case MatchMode.Endless: big = Loc.F("result.wave", _summary.wave); small = Loc.F("mode.best_week", r.best, meta.State.endlessRecord); break;
                case MatchMode.Campaign: big = ""; small = Loc.F("campaign.level", _summary.level / Campaign.LevelsPerRegion + 1, _summary.level % Campaign.LevelsPerRegion + 1); break;
                case MatchMode.Tournament: big = ""; small = r.tournamentWon ? Loc.T("tournament.prize") : Loc.F("mode.tournament_round", Mathf.Min(r.tournamentRound + 1, TournamentRules.Rounds), TournamentRules.Rounds); break;
                case MatchMode.Mayhem: big = ""; small = Loc.F("mode.mayhem_wins", meta.State.mayhemWins); break;
                default: big = ""; small = Loc.F("result.wave", _summary.wave); break;
            }
            if (!string.IsNullOrEmpty(big)) UIKit.Outlined(UIKit.Label(Root, big, 84, Palette.Primary), 3f).rectTransform
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -720), new Vector2(900, 110));
            UIKit.Label(Root, small, 34, Palette.Text).rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -800), new Vector2(980, 60));

            if (_summary.mode == MatchMode.Campaign)
            {
                for (int st = 0; st < 3; st++)
                {
                    var sp = Art.Icon("star");
                    var img = UIKit.Icon(Root, "star" + st, sp != null ? sp : Art.Diamond, st < r.stars ? Color.white : new Color(0.25f, 0.22f, 0.35f, 1f));
                    img.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2((st - 1) * 170, st == 1 ? -700 : -720), new Vector2(st == 1 ? 170 : 140, st == 1 ? 170 : 140));
                    if (st < r.stars) UIKit.Motion(img.rectTransform).PopIn(0.3f + st * 0.25f, 0.35f);
                }
            }

            // Rewards row
            var row = UIKit.Rect("rewards", Root).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -880), new Vector2(1000, 260));
            int n = r.rewards.Count;
            if (n == 0) UIKit.Label(row, Loc.T("result.no_rewards"), 30, Palette.TextDim).rectTransform.Stretch();
            for (int i = 0; i < n; i++)
            {
                var cell = Deco.Frame(row, "r" + i, Art.Hex(0xFFC93C), Art.Hex(0x2E2150), 6f);
                float w = Mathf.Min(220f, 1000f / n - 16f);
                ((RectTransform)cell.parent).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i - (n - 1) * 0.5f) * (w + 16), 0), new Vector2(w, 240));
                Icons.Reward(cell, r.rewards[i], App.Content, 110).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(110, 110));
                UIKit.Label(cell, Fmt.Reward(r.rewards[i], App.Content), 22, Palette.Text).rectTransform.Stretch(8, 8, 130, 8);
                UIKit.Motion((RectTransform)cell.parent).PopIn(0.5f + i * 0.12f, 0.3f);
            }

            var again = UIKit.ChunkyButton(Root, "again", Loc.T(_summary.mode == MatchMode.Tournament ? "tournament.bracket" : "mode.again"), Palette.Green, () => ModeLauncher.Again(App, _cfg), 44);
            again.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-260, 120), new Vector2(480, 150));
            var next = UIKit.ChunkyButton(Root, "next", Loc.T("result.continue"), Art.Hex(0xFFB020), Continue, 44, "button_battle");
            next.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(260, 120), new Vector2(480, 150));
            var watch = UIKit.ChunkyButton(Root, "watch", Loc.T("replay.watch"), Palette.Purple, () => { var list = ReplayStore.Load(); if (list.Count > 0) ModeLauncher.Replay(App, list[0]); }, 30);
            watch.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 300), new Vector2(360, 96));
        }

        void Continue()
        {
            switch (_summary.mode)
            {
                case MatchMode.Campaign: App.Show(new CampaignScreen()); break;
                case MatchMode.Tournament: App.Show(new TournamentScreen()); break;
                default: App.Show(new ModesScreen()); break;
            }
        }
    }

    /// <summary>Last matches kept on the device for replay viewing (the command log replays the match exactly).</summary>
    public static class ReplayStore
    {
        const string Key = "zoo_replays_v1";
        public const int Max = 5;

        [Serializable]
        public class Entry
        {
            public MatchRecord record;
            public string opponent;
            public int result;     // 0 win, 1 loss, -1 draw
            public long time;
            public int mode;
        }

        [Serializable] class Box { public List<Entry> items = new List<Entry>(); }

        public static List<Entry> Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(Key, "");
                if (string.IsNullOrEmpty(json)) return new List<Entry>();
                var box = JsonUtility.FromJson<Box>(json);
                return box != null && box.items != null ? box.items : new List<Entry>();
            }
            catch { return new List<Entry>(); }
        }

        public static void Add(MatchRecord record, string opponent, int result, long time)
        {
            try
            {
                var list = Load();
                int mode = record.setup != null && record.setup.rules != null ? (int)record.setup.rules.mode : 0;
                list.Insert(0, new Entry { record = record, opponent = opponent, result = result, time = time, mode = mode });
                while (list.Count > Max) list.RemoveAt(list.Count - 1);
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(new Box { items = list }));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("[ZooDefense] replay not saved: " + e.Message); }
        }
    }
}
