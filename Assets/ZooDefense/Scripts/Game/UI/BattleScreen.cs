using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// In-match HUD ("Meadow" layout, concepts A + C):
    ///   top bar        : hearts, wave + progress, candy, chat, pause (cream pills on wood)
    ///   opponent strip : live preview of the opponent's board (tap = watch big), name / hearts / upgrades,
    ///                    send points and the 4 send-monster buttons
    ///   incoming chips : monsters the opponent queued against you, with Reflect
    ///   your board     : big, the enemy path wraps it in a U (top-left gate -> bottom-right zoo gate).
    ///                    Drag animals to merge, tap a glowing animal for its skill, tap any other animal for
    ///                    its info sheet, tap balloons, tap the path while a spell is armed.
    ///   bottom panel   : deck bar (5 animals, tap = power up that animal) and the action row
    ///                    (2 spells | big SUMMON | 2 spells)
    /// Overlays: unit info sheet over the bottom panel, perk choice every 5 waves, emote picker, banners, pause.
    /// </summary>
    public sealed partial class BattleScreen : ScreenBase
    {
        const float TopBarHeight = 112f;
        const float StripTop = 142f;
        const float StripHeight = 196f;
        const float BoardTop = StripTop + StripHeight + 10f;
        const float BottomHeight = 440f;

        static readonly Color Wood = Art.Hex(0x8A5A32);
        static readonly Color WoodLight = Art.Hex(0xC08A4E);
        static readonly Color WoodDark = Art.Hex(0x5B3A22);
        static readonly Color Cream = Art.Hex(0xFFF1D6);
        static readonly Color CardCream = Art.Hex(0xF6DFB0);
        static readonly Color Ink = Art.Hex(0x4A2E18);
        static readonly Color Gold = Art.Hex(0xFFC21A);

        readonly int _seed;
        readonly string _opponentName;
        readonly int _difficulty;
        readonly bool _ranked;
        readonly bool _tutorial;
        static readonly string[] TutorialDeck = { "fire_fox", "bubble_frog" };

        MatchRunner _runner;
        BattleWorld _world;
        Match _match;
        Board _me, _opp;

        RectTransform _topBar, _thumb, _sendsRoot, _boardArea, _incomingList, _bottom, _deckRow, _actionRow, _summonRt;
        RectTransform _perkPanel, _emotePicker, _pausePanel, _focusHint;
        Text _myHearts, _wave, _timer, _candy, _points, _oppHearts, _oppInfo, _banner, _myBubble, _oppBubble, _zoomLabel, _perkTimer;
        Text _summonPrice, _units, _focusText;
        RectTransform _nextHolder, _perkCardsRoot, _perkRays;
        Image _perkTimerFill;
        int _shownNext = -1;
        float _perkShownAt;
        readonly List<RectTransform> _perkCards = new List<RectTransform>();
        readonly List<RectTransform> _flyers = new List<RectTransform>();
        readonly List<float> _flyerT = new List<float>();
        readonly List<Vector3> _flyerFrom = new List<Vector3>(), _flyerTo = new List<Vector3>();
        Bar _waveBar;
        Button _summon;
        Image _bannerBg;
        float _bannerTime, _myBubbleTime, _oppBubbleTime, _focusTime, _emoteCooldown, _t;
        DeckCard[] _deckCards;
        SendButton[] _sends;
        SpellButton[] _spells;
        readonly List<IncomingRow> _incoming = new List<IncomingRow>();
        UnitSheet _sheet;
        TutorialDirector _tutor;
        int _sendsMade;
        float _endTimer = -1f;
        RectTransform _tugMine, _duelPanel, _waveChip;
        Element _shownWaveElement = (Element)(-1), _shownNextElement = (Element)(-1);
        Image _thumbFlash;
        Bar _duelMine, _duelOpp;
        float _tugShare = 0.5f;
        int _shownCandy = -1, _shownPoints = -1, _shownMyHearts = -1, _shownOppHearts = -1, _shownSecond = -1, _shownWave = -1;
        string _shownPerkOffer = "", _shownIncoming = "";

        readonly string[] _botDeckOverride;
        readonly bool _revenge;

        /// <summary>Game mode set-up (rules, decks, levels, replay). Null = normal ranked / practice match.</summary>
        readonly BattleConfig _cfg;

        public BattleScreen(BattleConfig cfg) : this(cfg.seed, cfg.opponentName, cfg.difficulty, cfg.ranked, false, cfg.botDeck, cfg.revenge)
        {
            _cfg = cfg;
            _opponentMmr = cfg.opponentMmr;
        }

        int _opponentMmr, _botLevelOffset;

        /// <summary>Ranked bot match: the bot's rating (for the hidden rating) and its card level offset.</summary>
        public BattleScreen WithRating(int opponentMmr, int botLevelOffset)
        {
            _opponentMmr = opponentMmr;
            _botLevelOffset = botLevelOffset;
            return this;
        }

        bool IsFriendly { get { return _cfg != null && _cfg.friendly; } }

        public BattleScreen(int seed, string opponentName, int difficulty, bool ranked, bool tutorial = false, string[] botDeck = null, bool revenge = false)
        {
            _botDeckOverride = botDeck;
            _revenge = revenge;
            _seed = seed;
            _opponentName = opponentName;
            _difficulty = difficulty;
            _ranked = ranked;
            _tutorial = tutorial;
        }

        /// <summary>Guided first match: 2-animal deck (merges come fast), easy bot, no trophies.</summary>
        public static BattleScreen Tutorial() { return new BattleScreen(424242, Loc.T("tutorial.coach"), 0, false, true); }

        public BattleWorld World { get { return _world; } }
        public MatchRunner Runner { get { return _runner; } }

        protected override void Build()
        {
            var content = App.Content;
            var profile = App.Profile;
            // Out-of-match card levels: the player's upgrades, the bot near the player's average.
            var deck = _tutorial ? (string[])TutorialDeck.Clone() : (_cfg != null && _cfg.playerDeck != null ? _cfg.playerDeck : profile.deck);
            var levels = _tutorial ? null : (_cfg != null && _cfg.playerLevels != null ? _cfg.playerLevels : App.Meta.DeckLevels(deck));
            var botDeck = _tutorial ? (string[])TutorialDeck.Clone() : (_botDeckOverride != null ? (string[])_botDeckOverride.Clone() : BotBrain.RandomDeck(content, _seed ^ 0x5bd1e995));
            var botLevels = _tutorial ? null : (_cfg != null && _cfg.botLevels != null ? _cfg.botLevels : MetaRules.BotCardLevels(levels, botDeck.Length, _seed ^ 0x2c1b3c6d));
            if (botLevels != null && _botLevelOffset != 0 && (_cfg == null || _cfg.botLevels == null))
                for (int i = 0; i < botLevels.Length; i++) botLevels[i] = Mathf.Clamp(botLevels[i] + _botLevelOffset, 1, 15);
            var setup = IsOnline ? _cfg.online.setup : _cfg != null && _cfg.replay != null ? _cfg.replay.setup : new MatchSetup
            {
                seed = _seed,
                players = new[]
                {
                    new PlayerSetup { name = profile.playerName, deck = deck, sendDeck = profile.sendDeck, cardLevels = levels, spells = profile.spells },
                    new PlayerSetup
                    {
                        name = _opponentName,
                        deck = botDeck,
                        sendDeck = _tutorial ? new[] { "rat_rush", "rat_rush", "rat_rush", "rat_rush" } : new[] { "rat_rush", "sneaky_ghost", "candy_crow", "brick_golem" },
                        cardLevels = botLevels,
                        spells = MetaRules.BotSpells(content, _seed),
                    },
                },
            };
            if (IsOnline) { }   // the host already chose the rules (live event included)
            else if (_cfg != null && _cfg.replay == null) setup.rules = _cfg.rules;
            else if (_cfg == null && !_tutorial)
            {
                // Live-ops week (element week, boss week, candy festival) applies to ranked and practice for both players.
                var rules = new MatchRules { mode = MatchMode.Versus };
                LiveOps.ApplyTo(LiveOps.Current(App.Now), rules);
                setup.rules = rules;
            }
            _match = new Match(content, setup);
            IMatchTransport transport;
            if (IsOnline) transport = _online = new OnlineLockstepTransport(_cfg.online.link) { Timeout = Tuning.Current.reconnectSeconds };
            else if (_cfg != null && _cfg.replay != null) transport = new ReplayTransport(_cfg.replay);
            else if (_match.IsSolo) transport = new SoloTransport();
            else transport = new LocalBotTransport(new BotBrain(1, _difficulty, _seed + 17));
            _runner = new MatchRunner(_match, IsOnline ? _cfg.online.localPlayer : 0, transport);
            _runner.OnEvent += OnSimEvent;
            _me = _runner.LocalBoard;
            _opp = _runner.RemoteBoard;

            var arena = MetaRules.ArenaFor(App.Meta.State.trophies);
            if (_cfg != null && _cfg.arena > 0) arena = MetaRules.Arenas[Mathf.Clamp(_cfg.arena - 1, 0, MetaRules.Arenas.Length - 1)];
            _world = BattleWorld.Create(_runner, App.Cam, arena);
            _world.HideRemote = _match.IsSolo;
            _world.OnUnitTapped = OnUnitTapped;
            _world.OnSpellTarget = (slot, distance) => { _runner.Submit(CommandType.CastSpell, slot, distance); ArmSpell(-1); };
            _world.OnRemoteTapped = ToggleFocus;

            // Invisible rect: where the big board goes (between the strip and the bottom panel).
            _boardArea = UIKit.Rect("boardArea", Root).Stretch(8, 8, BoardTop, BottomHeight + 6);

            BuildTopBar();
            BuildOpponentStrip();
            BuildIncoming();
            BuildTugBar();
            BuildBossDuel();
            BuildFocusHint();
            BuildBottomPanel();
            _sheet = new UnitSheet(this);
            BuildBubbles();
            BuildPerkPanel();
            BuildEmotePicker();
            BuildBanner();
            BuildPausePanel();
            BuildModeHud();

            Track.MatchStart(_match.Mode, _ranked, IsOnline, IsFriendly);
            ErrorReporter.Step("match " + _match.Mode + (IsOnline ? " online" : ""));
            AudioService.PlayMusic("music_battle");
            if (_tutorial) _tutor = new TutorialDirector(this);
            else ShowBanner(ModeIntro(), Palette.Text, 1.8f);
        }

        public override void Close()
        {
            if (_runner != null) _runner.OnEvent -= OnSimEvent;
            LeaveOnline();
            if (_world != null) Object.Destroy(_world.gameObject);
            App.Cam.orthographicSize = 7f;
        }

        // ------------------------------------------------------------------ small builders

        /// <summary>Cream pill with a wood rim (concept style). Returns the inner rect.</summary>
        static RectTransform CreamPill(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rim = UIKit.Panel(parent, name, Wood).rectTransform.Place(anchor, pivot, pos, size);
            var inner = UIKit.Panel(rim, "inner", Cream).rectTransform;
            inner.Stretch(5, 5, 5, 8);
            return inner;
        }

        /// <summary>Dark-brown text for cream backgrounds (no drop shadow).</summary>
        static Text InkLabel(Transform parent, string text, int size, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = UIKit.Label(parent, text, size, Ink, align);
            var shadow = t.GetComponent<Shadow>();
            if (shadow != null) shadow.enabled = false;
            return t;
        }

        /// <summary>Frame of 4 bars around a rect, leaving the middle see-through (for world content behind).</summary>
        static void FrameBars(RectTransform rt, Color color, float t)
        {
            for (int side = 0; side < 4; side++)
            {
                var bar = UIKit.Panel(rt, "edge" + side, color, false).rectTransform;
                bool horizontal = side < 2;
                bar.anchorMin = horizontal ? new Vector2(0f, side == 0 ? 0f : 1f) : new Vector2(side == 2 ? 0f : 1f, 0f);
                bar.anchorMax = horizontal ? new Vector2(1f, side == 0 ? 0f : 1f) : new Vector2(side == 2 ? 0f : 1f, 1f);
                bar.pivot = new Vector2(horizontal ? 0.5f : (side == 2 ? 0f : 1f), horizontal ? (side == 0 ? 0f : 1f) : 0.5f);
                bar.anchoredPosition = Vector2.zero;
                bar.sizeDelta = horizontal ? new Vector2(0f, t) : new Vector2(t, 0f);
            }
        }

        static Rect ScreenRect(RectTransform rt, float insetPx = 0f)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x + insetPx, c[0].y + insetPx, c[2].x - insetPx, c[2].y - insetPx);
        }

        /// <summary>Title + candy price on the face of a ChunkyButton. Returns the price label.</summary>
        static Text PriceFace(Button b, string title, int titleSize, int priceSize, Color text)
        {
            var face = b.transform.Find("face");
            var titleLabel = UIKit.Label(face, title, titleSize, text);
            titleLabel.rectTransform.Stretch(8, 8, 6, 0).anchorMin = new Vector2(0f, 0.5f);
            AddOutline(titleLabel);
            var row = UIKit.Rect("price", face);
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 0.5f);
            row.offsetMin = new Vector2(0f, 6f);
            row.offsetMax = Vector2.zero;
            Icons.Currency(row, "candy").rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4, 0), new Vector2(priceSize + 14, priceSize + 14));
            var price = UIKit.Label(row, "", priceSize, text, TextAnchor.MiddleLeft);
            price.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4, 0), new Vector2(200, priceSize + 16));
            AddOutline(price);
            return price;
        }

        /// <summary>Dark outline so white text stays readable on bright button art.</summary>
        static void AddOutline(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.23f, 0.14f, 0.07f, 0.85f);
            o.effectDistance = new Vector2(2.5f, -2.5f);
        }

        // ------------------------------------------------------------------ build

        void BuildTopBar()
        {
            _topBar = UIKit.Rect("top", Root).Band(true, TopBarHeight - 8, 8, 12);
            var left = new Vector2(0f, 0.5f);

            var heart = CreamPill(_topBar, "hearts", left, left, new Vector2(0, 0), new Vector2(200, 96));
            Icons.Currency(heart, "heart").rectTransform.Place(left, left, new Vector2(6, 0), new Vector2(70, 70));
            _myHearts = InkLabel(heart, "", 44, TextAnchor.MiddleLeft);
            _myHearts.rectTransform.Stretch(86, 6, 0, 0);

            var wave = CreamPill(_topBar, "wave", left, left, new Vector2(212, 0), new Vector2(400, 96));
            var waveTap = UIKit.Button(wave, "guide", "", new Color(0f, 0f, 0f, 0f), () => ElementGuide.Open(App));
            waveTap.GetComponent<RectTransform>().Stretch();
            _wave = InkLabel(wave, "", 32, TextAnchor.UpperLeft);
            _wave.rectTransform.Stretch(18, 110, 4, 40);
            _wave.raycastTarget = false;
            // This wave's element -> next wave's element (tap the box for the element guide).
            _waveChip = UIKit.Rect("elements", wave).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150, -4), new Vector2(110, 40));
            if (!ElementGuide.Seen) UIKit.Motion(_waveChip).Pulse(0.08f, 5f);
            _timer = InkLabel(wave, "", 28, TextAnchor.UpperRight);
            _timer.raycastTarget = false;
            _timer.rectTransform.Stretch(18, 16, 6, 40);
            _waveBar = new Bar(wave, Art.Hex(0x6CC24A), 1);
            _waveBar.Root.Stretch(16, 16, 46, 10);

            var candy = CreamPill(_topBar, "candy", left, left, new Vector2(624, 0), new Vector2(230, 96));
            Icons.Currency(candy, "candy").rectTransform.Place(left, left, new Vector2(6, 0), new Vector2(70, 70));
            _candy = InkLabel(candy, "", 44, TextAnchor.MiddleLeft);
            _candy.rectTransform.Stretch(84, 6, 0, 0);

            var chat = UIKit.ChunkyButton(_topBar, "chat", Loc.T("battle.chat"), Palette.Purple, ToggleEmotePicker, 24);
            chat.GetComponent<RectTransform>().Place(left, left, new Vector2(866, 0), new Vector2(92, 96));
            var pause = UIKit.ChunkyButton(_topBar, "pause", "II", Wood, () => SetPaused(true), 40);
            pause.GetComponent<RectTransform>().Place(left, left, new Vector2(964, 0), new Vector2(92, 96));
        }

        void BuildOpponentStrip()
        {
            var strip = UIKit.Rect("strip", Root).Band(true, StripHeight, StripTop, 12);
            var left = new Vector2(0f, 0.5f);

            // Live preview of the opponent's board: the board itself is drawn by the world camera behind this
            // see-through frame. Tapping it swaps the two boards.
            _thumb = UIKit.Rect("thumb", strip).Place(left, left, Vector2.zero, new Vector2(250, StripHeight));
            var tap = UIKit.Button(_thumb, "tap", "", new Color(0f, 0f, 0f, 0f), ToggleFocus);
            tap.GetComponent<RectTransform>().Stretch();
            _thumbFlash = UIKit.Panel(_thumb, "flash", new Color(1f, 0.2f, 0.2f, 0f), false);
            _thumbFlash.rectTransform.Stretch();
            _thumbFlash.raycastTarget = false;
            FrameBars(_thumb, Wood, 6f);
            var view = UIKit.Panel(_thumb, "view", new Color(0f, 0f, 0f, 0.6f)).rectTransform
                .Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(170, 40));
            _zoomLabel = UIKit.Label(view, Loc.T("battle.view"), 22, Palette.Text);
            _zoomLabel.rectTransform.Stretch();

            var info = UIKit.Panel(strip, "oppInfo", new Color(0.42f, 0.12f, 0.2f, 0.92f)).rectTransform;
            info.Stretch(258, 0, 0, 0);
            UIKit.Label(info, _opp.PlayerName, 26, Palette.Text, TextAnchor.MiddleLeft).rectTransform
                .Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14, -8), new Vector2(214, 40));
            var hearts = UIKit.Rect("hearts", info).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10, -50), new Vector2(214, 70));
            Icons.Currency(hearts, "heart").rectTransform.Place(left, left, new Vector2(2, 0), new Vector2(62, 62));
            _oppHearts = UIKit.Label(hearts, "", 44, Palette.Text, TextAnchor.MiddleLeft);
            _oppHearts.rectTransform.Stretch(74, 0, 0, 0);
            _oppInfo = UIKit.Label(info, "", 22, Palette.TextDim, TextAnchor.UpperLeft);
            _oppInfo.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14, -124), new Vector2(214, 64));
            _oppInfo.supportRichText = true;

            // Income from sends (candy every wave) + the 4 send buttons (paid with candy)
            var pts = UIKit.Panel(info, "income", Art.Hex(0x2E7D4A)).rectTransform.Place(left, left, new Vector2(236, 0), new Vector2(96, StripHeight - 20));
            UIKit.Label(pts, Loc.T("battle.income"), 18, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperCenter).rectTransform.Stretch(2, 2, 10, 0);
            Icons.Currency(pts, "candy").rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -38), new Vector2(46, 46));
            _points = UIKit.Outlined(UIKit.Label(pts, "+0", 34, Palette.Text), 2f);
            _points.rectTransform.Stretch(0, 0, 80, 30);
            UIKit.Label(pts, Loc.T("battle.per_wave"), 18, new Color(1f, 1f, 1f, 0.8f), TextAnchor.LowerCenter).rectTransform.Stretch(0, 0, 0, 10);

            _sendsRoot = UIKit.Rect("sends", info).Place(left, left, new Vector2(340, 0), new Vector2(450, StripHeight - 20));
            _sends = new SendButton[_me.SendDeck.Length];
            float w = 450f / Mathf.Max(4, _sends.Length);
            for (int i = 0; i < _sends.Length; i++)
            {
                int slot = i;
                _sends[i] = new SendButton(_sendsRoot, App.Content, _me.SendDeck[i], _me.SendIncome(i), new Vector2(i * w, 0), new Vector2(w - 6, StripHeight - 20),
                    () => _runner.Submit(CommandType.SendMonster, slot));
            }
        }

        /// <summary>Tug-of-war bar under the top bar: your hearts (blue) against the opponent's (red).</summary>
        void BuildTugBar()
        {
            var bar = UIKit.Panel(Root, "tug", new Color(0f, 0f, 0f, 0.55f)).rectTransform.Band(true, 22, TopBarHeight + 4, 16);
            var red = UIKit.Panel(bar, "opp", Palette.Red).rectTransform;
            red.Stretch(3, 3, 3, 3);
            _tugMine = UIKit.Panel(bar, "mine", Palette.Blue).rectTransform;
            _tugMine.anchorMin = Vector2.zero;
            _tugMine.anchorMax = new Vector2(0.5f, 1f);
            _tugMine.offsetMin = new Vector2(3, 3);
            _tugMine.offsetMax = new Vector2(0, -3);
            var knot = UIKit.Icon(_tugMine, "knot", Art.Circle, Color.white).rectTransform;
            knot.Place(new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            UIKit.Icon(knot, "ring", Art.Ring, Art.Hex(0x3B2412)).rectTransform.Stretch(-2, -2, -2, -2);
        }

        /// <summary>Boss duel: who is killing the shared boss faster (shown while the duel is open).</summary>
        void BuildBossDuel()
        {
            _duelPanel = UIKit.Panel(Root, "bossDuel", new Color(0.18f, 0.06f, 0.2f, 0.92f)).rectTransform
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -(BoardTop + 80f)), new Vector2(760, 112));
            UIKit.Outlined(UIKit.Label(_duelPanel, Loc.T("battle.boss_duel"), 28, Palette.Primary), 2f).rectTransform.Band(true, 34, 4);
            var row1 = UIKit.Rect("me", _duelPanel).Band(true, 30, 40, 14);
            UIKit.Label(row1, Loc.T("battle.you"), 22, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(0, 600, 0, 0);
            _duelMine = new Bar(row1, Palette.Blue, 18);
            _duelMine.Root.Stretch(130, 0, 0, 0);
            var row2 = UIKit.Rect("opp", _duelPanel).Band(true, 30, 76, 14);
            UIKit.Label(row2, _opp.PlayerName, 22, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(0, 600, 0, 0);
            _duelOpp = new Bar(row2, Palette.Red, 18);
            _duelOpp.Root.Stretch(130, 0, 0, 0);
            _duelPanel.gameObject.SetActive(false);
        }

        /// <summary>Remaining HP share of the shared wave boss on a board (1 = not spawned yet / untouched).</summary>
        float BossHp(Board board)
        {
            foreach (var e in board.Enemies)
                if (e.alive && e.sender < 0 && App.Content.Enemies[e.def].kind == EnemyKind.Boss) return e.hp / (float)Mathf.Max(1, e.maxHp);
            return 1f;
        }

        void BuildIncoming()
        {
            _incomingList = UIKit.Rect("incoming", Root).Band(true, 72, BoardTop, 12);
        }

        void BuildFocusHint()
        {
            _focusHint = UIKit.Panel(Root, "focusHint", new Color(0.42f, 0.12f, 0.2f, 0.92f)).rectTransform
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -(BoardTop + 200f)), new Vector2(760, 64));
            _focusText = UIKit.Label(_focusHint, "", 28, Palette.Text);
            _focusText.rectTransform.Stretch(10, 10, 0, 0);
            _focusHint.gameObject.SetActive(false);
        }

        void BuildBottomPanel()
        {
            _bottom = UIKit.Rect("bottom", Root).Band(false, BottomHeight);
            var hit = _bottom.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;   // taps on the panel never reach the board behind it
            var frame = Deco.Frame(_bottom, "frame", WoodLight, WoodDark, 8f);

            // Deck bar: one card per deck animal, tap = power up that animal (+damage for the rest of the match)
            _deckRow = UIKit.Rect("deck", frame).Band(true, 220, 8, 8);
            int n = _me.Deck.Length;
            float cell = 1f / Mathf.Max(5, n);
            float start = (1f - cell * n) * 0.5f;
            _deckCards = new DeckCard[n];
            for (int i = 0; i < n; i++) _deckCards[i] = new DeckCard(this, _deckRow, i, start + i * cell, start + (i + 1) * cell);

            // Action row: spells left and right of the big SUMMON button
            _actionRow = UIKit.Rect("actions", frame).Band(false, 176, 8, 10);
            _summon = UIKit.ChunkyButton(_actionRow, "summon", "", Gold, () => _runner.Submit(CommandType.Summon, -1), 30, "button_summon");
            _summonRt = _summon.GetComponent<RectTransform>();
            _summonRt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 168));
            _summonPrice = PriceFace(_summon, Loc.T("battle.summon_random"), 38, 42, Palette.Text);
            // Next animal preview on the left of the button: you always know what Summon brings.
            var face = _summon.transform.Find("face");
            for (int i = 0; i < face.childCount; i++)
            {
                var child = face.GetChild(i) as RectTransform;
                if (child != null && child.name != "shine") child.offsetMin = new Vector2(child.offsetMin.x + 118f, child.offsetMin.y);
            }
            var nextBg = UIKit.Icon(face, "nextBg", Art.Circle, Art.Hex(0xFFF1D6)).rectTransform;
            nextBg.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14, 4), new Vector2(124, 124));
            UIKit.Icon(nextBg, "ring", Art.Ring, Art.Hex(0x8A5A32)).rectTransform.Stretch(-3, -3, -3, -3);
            _nextHolder = UIKit.Rect("next", nextBg).Stretch(6, 6, 4, 8);
            var nextTag = UIKit.Panel(nextBg, "nextTag", WoodDark).rectTransform
                .Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(116, 32));
            UIKit.Label(nextTag, Loc.T("battle.next"), 20, Palette.Text).rectTransform.Stretch();
            var unitsTag = UIKit.Panel(_summon.transform.Find("face"), "units", new Color(0f, 0f, 0f, 0.3f)).rectTransform
                .Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12, 10), new Vector2(96, 40));
            _units = UIKit.Label(unitsTag, "", 26, Palette.Text);
            _units.rectTransform.Stretch();

            _spells = new SpellButton[_me.Spells.Length];
            for (int i = 0; i < _spells.Length; i++)
            {
                int slot = i;
                bool rightSide = i >= 2;
                float x = rightSide ? -((i - 2) == 0 ? 0f : 140f) : (i == 0 ? 0f : 140f);
                // Outer slots first: 0 = far left, 1 = inner left, 2 = far right, 3 = inner right.
                _spells[i] = new SpellButton(_actionRow, App.Content.Spells[_me.Spells[i]], rightSide, x, () => OnSpellPressed(slot));
            }
        }

        void BuildBubbles()
        {
            _myBubble = Bubble(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16, BottomHeight + 16), Palette.Blue);
            _oppBubble = Bubble(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16, -(StripTop + StripHeight + 8)), Palette.Red);
        }

        Text Bubble(Vector2 anchor, Vector2 pivot, Vector2 pos, Color color)
        {
            var bg = UIKit.Panel(Root, "bubble", color).rectTransform.Place(anchor, pivot, pos, new Vector2(300, 84));
            var t = UIKit.Label(bg, "", 34, Palette.Text);
            t.rectTransform.Stretch(8, 8, 0, 0);
            bg.gameObject.SetActive(false);
            return t;
        }

        void BuildPerkPanel()
        {
            // Full-screen overlay: dimmed backdrop, rotating rays, ribbon title, timer bar, 3 perk cards.
            _perkPanel = UIKit.Panel(Root, "perks", new Color(0.05f, 0.03f, 0.1f, 0.78f), false).rectTransform.Stretch();
            _perkPanel.GetComponent<Image>().raycastTarget = true;
            var rays = UIKit.Icon(_perkPanel, "rays", Art.Rays, new Color(1f, 0.85f, 0.4f, 0.22f));
            _perkRays = rays.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1500, 1500));
            Deco.Ribbon(_perkPanel, Loc.T("battle.perk_title"), Art.Hex(0x8A4FE0), 46)
                .Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 420), new Vector2(760, 110));
            var timer = UIKit.Panel(_perkPanel, "timer", new Color(0f, 0f, 0f, 0.55f)).rectTransform
                .Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 335), new Vector2(520, 44));
            _perkTimerFill = UIKit.Panel(timer, "fill", Gold);
            _perkTimerFill.rectTransform.anchorMin = Vector2.zero;
            _perkTimerFill.rectTransform.anchorMax = Vector2.one;
            _perkTimerFill.rectTransform.offsetMin = new Vector2(4, 4);
            _perkTimerFill.rectTransform.offsetMax = new Vector2(-4, -4);
            _perkTimer = UIKit.Label(timer, "", 24, Palette.Text);
            _perkTimer.rectTransform.Stretch();
            _perkCardsRoot = UIKit.Rect("cards", _perkPanel).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1050, 600));
            _perkPanel.gameObject.SetActive(false);
        }

        void BuildEmotePicker()
        {
            _emotePicker = UIKit.Panel(Root, "emotes", new Color(0.12f, 0.08f, 0.2f, 0.95f)).rectTransform
                .Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12, -(TopBarHeight + 8)), new Vector2(560, 290));
            _emotePicker.GetComponent<Image>().raycastTarget = true;
            var emotes = new List<int>();
            for (int i = 0; i < MetaContent.BaseEmotes + MetaContent.ExtraEmotes; i++) if (App.Meta.HasEmote(i)) emotes.Add(i);
            ((RectTransform)_emotePicker).sizeDelta = new Vector2(560, 20 + 90 * ((emotes.Count + 1) / 2));
            for (int k = 0; k < emotes.Count; k++)
            {
                int i = k, id = emotes[k];
                var b = UIKit.Button(_emotePicker, "emote" + i, Loc.T("emote." + id), id >= MetaContent.BaseEmotes ? Art.Hex(0x6B3FA0) : Palette.Panel, () => SendEmote(id), 30);
                b.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14 + (i % 2) * 272, -14 - (i / 2) * 90), new Vector2(260, 80));
            }
            _emotePicker.gameObject.SetActive(false);
        }

        void BuildBanner()
        {
            _bannerBg = UIKit.Panel(Root, "banner", new Color(0.85f, 0.2f, 0.35f, 0.9f));
            // Just under the opponent strip, so it never hides the middle of the board.
            _bannerBg.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -(BoardTop + 4f)), new Vector2(900, 130));
            _banner = UIKit.Label(_bannerBg.transform, "", 52, Palette.Text);
            _banner.rectTransform.Stretch(20, 20, 0, 0);
            _bannerBg.raycastTarget = false;
            _bannerBg.gameObject.SetActive(false);
        }

        void BuildPausePanel()
        {
            _pausePanel = UIKit.Panel(Root, "pausePanel", new Color(0f, 0f, 0f, 0.7f), false).rectTransform.Stretch();
            _pausePanel.GetComponent<Image>().raycastTarget = true;
            var box = Deco.Frame(_pausePanel, "box", WoodLight, Art.Hex(0x3A2D57), 8f);
            ((RectTransform)box.parent).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 520));
            UIKit.Label(box, Loc.T("battle.paused"), 70, Palette.Text).rectTransform.Band(true, 120, 24);
            var resume = UIKit.ChunkyButton(box, "resume", Loc.T("battle.resume"), Palette.Green, () => SetPaused(false), 48);
            resume.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -170), new Vector2(520, 130));
            var surrender = UIKit.ChunkyButton(box, "surrender", Loc.T("battle.surrender"), Palette.Red, () => { SetPaused(false); _runner.Submit(CommandType.Surrender); }, 48);
            surrender.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(520, 130));
            _pausePanel.gameObject.SetActive(false);
        }

        void SetPaused(bool paused)
        {
            // Bot matches can pause. Online PvP: show the menu but keep the match running.
            _runner.Paused = paused && !IsOnline;
            _world.InputEnabled = !paused;
            _pausePanel.gameObject.SetActive(paused);
            _pausePanel.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ actions

        void OnSpellPressed(int slot)
        {
            var def = App.Content.Spells[_me.Spells[slot]];
            if (_match.Tick < _me.SpellReadyTick[slot]) { App.Toast(Loc.RejectText(RejectReason.Cooldown)); return; }
            if (_me.Candy < def.cost) { App.Toast(Loc.RejectText(RejectReason.NotEnoughCandy)); return; }
            if (!def.NeedsTarget) { _runner.Submit(CommandType.CastSpell, slot, 0); ArmSpell(-1); return; }
            ArmSpell(_world.ArmedSpell == slot ? -1 : slot);
            if (_world.ArmedSpell >= 0) ShowBanner(Loc.F("battle.target_hint", Loc.T("spell." + def.id)), Palette.Text, 1.6f, Palette.Blue);
        }

        /// <summary>The summoned animal's portrait flies from the Summon button to its cell.</summary>
        void FlyToCell(int unitDef, int cell)
        {
            if (_world.FocusRemote || cell < 0) return;
            var c = new Vector3[4];
            _summonRt.GetWorldCorners(c);
            var fly = UIKit.Rect("flyer", Root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
            Icons.Unit(fly, App.Content.Units[unitDef], 1, 150).Stretch();
            foreach (var g in fly.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            var from = (c[0] + c[2]) * 0.5f;
            fly.position = from;
            _flyers.Add(fly); _flyerT.Add(0f); _flyerFrom.Add(from); _flyerTo.Add(_world.CellScreen(cell));
        }

        void TickFlyers(float dt)
        {
            for (int i = _flyers.Count - 1; i >= 0; i--)
            {
                _flyerT[i] += dt / 0.38f;
                float k = Mathf.Clamp01(_flyerT[i]);
                var p = Vector3.Lerp(_flyerFrom[i], _flyerTo[i], k);
                p.y += Mathf.Sin(k * Mathf.PI) * 160f * _flyers[i].lossyScale.y;   // little hop
                _flyers[i].position = p;
                float s = Mathf.Lerp(1f, 0.6f, k);
                _flyers[i].localScale = new Vector3(s, s, 1f);
                if (k >= 1f)
                {
                    Object.Destroy(_flyers[i].gameObject);
                    _flyers.RemoveAt(i); _flyerT.RemoveAt(i); _flyerFrom.RemoveAt(i); _flyerTo.RemoveAt(i);
                }
            }
        }

        void ArmSpell(int slot)
        {
            if (_world.FocusRemote && slot >= 0) SetFocus(false);
            _world.ArmedSpell = slot;
            _world.Local.PathGlow = slot >= 0;
            for (int i = 0; i < _spells.Length; i++) _spells[i].SetArmed(i == slot);
        }

        void ToggleFocus() { SetFocus(!_world.FocusRemote); }

        void SetFocus(bool on)
        {
            _world.FocusRemote = on;
            _focusTime = on ? 6f : 0f;
            _zoomLabel.text = Loc.T(on ? "battle.zoom_back" : "battle.view");
            _focusText.text = Loc.F("battle.watching", _opp.PlayerName);
            _focusHint.gameObject.SetActive(on);
            if (on) { ArmSpell(-1); _sheet.Hide(); }
        }

        void ToggleEmotePicker()
        {
            _emotePicker.gameObject.SetActive(!_emotePicker.gameObject.activeSelf);
            _emotePicker.SetAsLastSibling();
        }

        void SendEmote(int id)
        {
            _emotePicker.gameObject.SetActive(false);
            if (_emoteCooldown > 0f) return;
            _emoteCooldown = 3f;
            _runner.Submit(CommandType.Emote, id);
        }

        void ChoosePerk(int index)
        {
            _runner.Submit(CommandType.ChoosePerk, index);
            _perkPanel.gameObject.SetActive(false);
        }

        void OnUnitTapped(int cell)
        {
            var u = _me.Cells[cell];
            if (u == null) return;
            if (_me.IsSkillReady(u)) { _runner.Submit(CommandType.UseSkill, cell); return; }
            if (_sheet.Cell == cell) _sheet.Hide();
            else _sheet.Show(cell);
        }

        // ------------------------------------------------------------------ frame

        public override void Tick(float dt)
        {
            _t += dt;
            _runner.Update(dt);
            OnlineTick(dt);
            _world.Layout(ScreenRect(_boardArea), ScreenRect(_thumb, 6f * Mathf.Max(0.01f, _thumb.lossyScale.x)));
            RefreshHud(dt);
            RefreshModeHud(dt);
            _sheet.Refresh();
            if (_tutor != null) _tutor.Tick(dt);

            if (_bannerTime > 0f) { _bannerTime -= dt; if (_bannerTime <= 0f) _bannerBg.gameObject.SetActive(false); }
            if (_myBubbleTime > 0f) { _myBubbleTime -= dt; if (_myBubbleTime <= 0f) _myBubble.transform.parent.gameObject.SetActive(false); }
            if (_oppBubbleTime > 0f) { _oppBubbleTime -= dt; if (_oppBubbleTime <= 0f) _oppBubble.transform.parent.gameObject.SetActive(false); }
            if (_emoteCooldown > 0f) _emoteCooldown -= dt;
            if (_focusTime > 0f) { _focusTime -= dt; if (_focusTime <= 0f) SetFocus(false); }

            if (_match.IsOver)
            {
                if (_endTimer < 0f)
                {
                    _endTimer = 2.2f;
                    _world.InputEnabled = false;
                    SetFocus(false);
                    _sheet.Hide();
                    _perkPanel.gameObject.SetActive(false);
                    bool won = _match.Winner == _runner.LocalPlayer;
                    string text = Loc.T(won ? "result.victory" : (_match.Winner < 0 ? "result.draw" : "result.defeat"));
                    ShowBanner(text, Palette.Text, 3f, won ? Palette.Green : Palette.Red);
                    LeaveOnline();
                }
                _endTimer -= dt;
                if (_endTimer <= 0f)
                {
                    if (IsReplay) { App.Show(new ReplaysScreen()); return; }
                    var record = MatchRecord.From(_match);
                    if (!_tutorial) ReplayStore.Add(record, _opponentName, _match.Winner == _runner.LocalPlayer ? 0 : (_match.Winner < 0 ? -1 : 1), App.Now);
                    var summary = new MatchSummary
                    {
                        winner = _match.Winner == _runner.LocalPlayer ? 0 : (_match.Winner < 0 ? -1 : 1),
                        ranked = _ranked,
                        kills = _me.Kills,
                        merges = _me.Merges,
                        sends = _sendsMade,
                        wave = _match.Wave,
                        cardsBought = _me.Purchases,
                        revenge = _revenge,
                        mode = _match.Mode,
                        effectiveHits = _me.EffectiveHits,
                        bossKills = _me.BossKills,
                        heartsLeft = _me.Hearts,
                        level = _cfg != null ? _cfg.campaignLevel : -1,
                        online = IsOnline,
                        localPlayer = _runner.LocalPlayer,
                        opponentMmr = _ranked ? _opponentMmr : 0,
                        enemiesSeen = new List<string>(_seenEnemies),
                        unitsUsed = new List<string>(_me.Deck.Length),
                    };
                    foreach (var d in _me.Deck) summary.unitsUsed.Add(App.Content.Units[d].id);
                    Track.MatchEnd(_match.Mode, _ranked, IsOnline, IsFriendly, summary.winner, _match.Wave, Mathf.RoundToInt(_t));
                    if (_match.Mode == MatchMode.Versus) App.Show(new ResultScreen(record, _ranked, summary, _tutorial) { Friendly = IsFriendly });
                    else App.Show(new ModeResultScreen(_cfg, record, summary));
                }
            }
        }

        void RefreshHud(float dt)
        {
            var b = App.Content.Balance;
            if (_me.Hearts != _shownMyHearts) { _shownMyHearts = _me.Hearts; _myHearts.text = _me.Hearts.ToString(); }
            if (_opp.Hearts != _shownOppHearts) { _shownOppHearts = _opp.Hearts; _oppHearts.text = _opp.Hearts.ToString(); }
            if (_me.Candy != _shownCandy) { _shownCandy = _me.Candy; _candy.text = _me.Candy.ToString(); }
            if (_me.Income != _shownPoints) { _shownPoints = _me.Income; _points.text = "+" + _me.Income; }

            int ticksLeft = _match.TicksUntilNextWave;
            int seconds = Mathf.CeilToInt(ticksLeft / (float)b.tickRate);
            if (seconds != _shownSecond || _match.Wave != _shownWave)
            {
                _shownSecond = seconds;
                _shownWave = _match.Wave;
                _wave.text = _match.Wave == 0 ? Loc.T("battle.ready") : Loc.F("battle.wave", _match.Wave);
                _wave.color = _match.IsSuddenDeath ? Palette.Red : Ink;
                _timer.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");
            }
            var waveEl = _match.Wave == 0 ? Element.None : _match.WaveElement;
            if (waveEl != _shownWaveElement || _match.NextWaveElement != _shownNextElement)
            {
                _shownWaveElement = waveEl;
                _shownNextElement = _match.NextWaveElement;
                WaveChip.Draw(_waveChip, waveEl, _match.NextWaveElement);
            }
            int span = _match.Wave == 0 ? b.firstWaveDelayTicks : b.waveDurationTicks;
            _waveBar.Set(1f - ticksLeft / (float)Mathf.Max(1, span), "");

            // Summon
            int free = _me.CountFreeCells();
            _summonPrice.text = _me.CardPrice.ToString();
            _summon.interactable = _me.Candy >= _me.CardPrice && free > 0;
            _units.text = (_me.Cells.Length - free) + "/" + _me.Cells.Length;
            float pulse = _summon.interactable ? 1f + 0.03f * Mathf.Sin(_t * 5f) : 1f;
            _summonRt.localScale = new Vector3(pulse, pulse, 1f);

            if (_me.NextSummon != _shownNext)
            {
                _shownNext = _me.NextSummon;
                UIKit.Clear(_nextHolder);
                Icons.Unit(_nextHolder, App.Content.Units[_shownNext], 1, 110).Stretch();
                _nextHolder.localScale = new Vector3(0.4f, 0.4f, 1f);   // pops back in below
            }
            if (_nextHolder.localScale.x < 1f)
            {
                float k = Mathf.Min(1f, _nextHolder.localScale.x + dt * 4f);
                _nextHolder.localScale = new Vector3(k, k, 1f);
            }

            // Deck bar (per-animal power-ups: only for animals on the board)
            for (int i = 0; i < _deckCards.Length; i++)
            {
                int lv = _me.DeckLevel[i];
                bool max = lv >= b.unitUpgradeMax;
                int cost = _me.UnitUpgradeCost(i);
                int count = _me.CountOnBoard(_me.Deck[i]);
                int price = _me.SummonPrice(i);
                _deckCards[i].Set(price, _me.Candy >= price && free > 0, lv, cost, count > 0 && !max && _me.Candy >= cost, max, count, _me.Deck[i] == _me.NextSummon, _t);
            }
            TickFlyers(dt);

            bool oppFull = _opp.IncomingSends.Count >= b.maxIncomingSendsPerWave;
            for (int i = 0; i < _sends.Length; i++)
            {
                var def = App.Content.Sends[_me.SendDeck[i]];
                bool used = def.oncePerMatch && _me.SendUsed[i];
                int cost = _me.SendCost(i);
                _sends[i].Set(cost, !used && !oppFull && _me.Candy >= cost, used, _me.HasComebackDiscount);
            }

            for (int i = 0; i < _spells.Length; i++)
            {
                var def = App.Content.Spells[_me.Spells[i]];
                int left = _me.SpellReadyTick[i] - _match.Tick;
                _spells[i].Set(left > 0 ? left / (float)_me.SpellCooldown(i) : 0f, Mathf.CeilToInt(left / (float)b.tickRate), _me.Candy >= def.cost);
            }

            // Tug-of-war and the thumbnail hit flash
            float share = (_me.Hearts + 0.01f) / (_me.Hearts + _opp.Hearts + 0.02f);
            _tugShare = Mathf.Lerp(_tugShare, share, dt * 4f);
            _tugMine.anchorMax = new Vector2(Mathf.Clamp(_tugShare, 0.04f, 0.96f), 1f);
            if (_thumbFlash.color.a > 0f) _thumbFlash.color = new Color(1f, 0.2f, 0.2f, Mathf.Max(0f, _thumbFlash.color.a - dt * 1.5f));

            // Boss duel race
            bool duel = _match.IsBossDuelOpen;
            if (_duelPanel.gameObject.activeSelf != duel) _duelPanel.gameObject.SetActive(duel);
            if (duel)
            {
                float hm = BossHp(_me), ho = BossHp(_opp);
                _duelMine.Set(hm, Mathf.RoundToInt(hm * 100f) + "%");
                _duelOpp.Set(ho, Mathf.RoundToInt(ho * 100f) + "%");
            }

            RefreshOpponentInfo();
            RefreshIncoming();
            RefreshPerks();
        }

        void RefreshOpponentInfo()
        {
            int ups = 0;
            for (int i = 0; i < _opp.DeckLevel.Length; i++) ups += _opp.DeckLevel[i];
            var sb = new System.Text.StringBuilder();
            if (ups > 0) sb.Append("<color=#7CFF8A>↑").Append(ups).Append("</color>   ");
            if (_opp.Perks.Count > 0) sb.Append(Loc.F("battle.opp_perks", _opp.Perks.Count));
            _oppInfo.text = sb.Length > 0 ? sb.ToString() : Loc.T("battle.opp_no_upgrades");
        }

        /// <summary>Monsters the opponent queued against you, each with a Reflect button.</summary>
        void RefreshIncoming()
        {
            var sig = new System.Text.StringBuilder();
            for (int i = 0; i < _me.IncomingSends.Count; i++) sig.Append(_me.IncomingSends[i]).Append(_me.IncomingReflected[i] ? 'r' : 'n').Append(',');
            string s = sig.ToString();
            if (s != _shownIncoming)
            {
                _shownIncoming = s;
                UIKit.Clear(_incomingList);
                _incoming.Clear();
                for (int i = 0; i < _me.IncomingSends.Count; i++) _incoming.Add(new IncomingRow(this, _incomingList, i, App.Content.Sends[_me.IncomingSends[i]], _me.IncomingReflected[i]));
            }
            for (int i = 0; i < _incoming.Count; i++)
                if (i < _me.IncomingSends.Count)
                    _incoming[i].Set(_me.ReflectCost(i), _me.Candy, Mathf.CeilToInt(Mathf.Max(0, _me.IncomingArriveTick[i] - _match.Tick) / (float)App.Content.Balance.tickRate));
        }

        void RefreshPerks()
        {
            if (_me.PerkOffer.Count == 0)
            {
                if (_perkPanel.gameObject.activeSelf) _perkPanel.gameObject.SetActive(false);
                _shownPerkOffer = "";
                return;
            }
            string sig = string.Join(",", _me.PerkOffer.ConvertAll(p => ((int)p).ToString()).ToArray());
            if (sig != _shownPerkOffer)
            {
                _shownPerkOffer = sig;
                UIKit.Clear(_perkCardsRoot);
                _perkCards.Clear();
                int n = _me.PerkOffer.Count;
                float w = 1050f / Mathf.Max(1, n);
                for (int i = 0; i < n; i++) _perkCards.Add(PerkCard(_perkCardsRoot, _me.PerkOffer[i], i, (i - (n - 1) * 0.5f) * w, w - 26));
                _perkPanel.gameObject.SetActive(true);
                _perkPanel.SetAsLastSibling();
                _perkShownAt = _t;
                _sheet.Hide();
                AudioService.Play(Sfx.Reward);
            }
            var bal = App.Content.Balance;
            float leftTicks = Mathf.Max(0, _me.PerkDeadlineTick - _match.Tick);
            int left = Mathf.CeilToInt(leftTicks / bal.tickRate);
            _perkTimer.text = Loc.F("battle.perk_auto", left);
            _perkTimerFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(leftTicks / Mathf.Max(1, bal.perkAutoPickTicks)), 1f);
            _perkRays.localRotation = Quaternion.Euler(0f, 0f, -_t * 12f);
            // Cards pop in one after another.
            for (int i = 0; i < _perkCards.Count; i++)
            {
                float k = Deco.EaseOutBack(Mathf.Clamp01((_t - _perkShownAt - i * 0.1f) / 0.35f));
                _perkCards[i].localScale = new Vector3(k, k, 1f);
            }
        }

        /// <summary>One perk choice: coloured frame, big icon on a glow, category chip, name, description, Choose.</summary>
        RectTransform PerkCard(RectTransform parent, PerkType perk, int index, float x, float width)
        {
            var color = PerkColor(perk);
            string key = "perk." + perk.ToString().ToLowerInvariant();
            var holder = UIKit.Rect("perk" + index, parent).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(width, 600));
            var c = Deco.Frame(holder, "frame", Color.Lerp(color, Color.white, 0.15f), Art.Hex(0x241A38), 9f);
            var top = new Vector2(0.5f, 1f);

            var glow = UIKit.Icon(c, "glow", Art.Glow, new Color(color.r, color.g, color.b, 0.75f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(top, new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(300, 260));
            var disc = UIKit.Icon(c, "disc", Art.Circle, Color.Lerp(color, Color.black, 0.35f)).rectTransform;
            disc.Place(top, new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(190, 190));
            UIKit.Icon(disc, "ring", Art.Ring, Color.Lerp(color, Color.white, 0.4f)).rectTransform.Stretch(-5, -5, -5, -5);
            var icon = PerkIcon(perk);
            if (icon != null) UIKit.Icon(disc, "icon", icon, Color.white).rectTransform.Stretch(26, 26, 26, 26);

            var chip = UIKit.Panel(c, "category", color).rectTransform.Place(top, new Vector2(0.5f, 0.5f), new Vector2(0, -260), new Vector2(200, 40));
            UIKit.Label(chip, Loc.T("perk.cat." + PerkCategory(perk)), 22, Palette.Text).rectTransform.Stretch();

            var name = UIKit.Label(c, Loc.T(key), 34, Palette.Text);
            name.rectTransform.Place(top, top, new Vector2(0, -286), new Vector2(width - 30, 56));
            var o = name.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.6f);
            o.effectDistance = new Vector2(2f, -2f);
            UIKit.Label(c, Loc.T(key + ".desc"), 25, Palette.TextDim).rectTransform.Place(top, top, new Vector2(0, -344), new Vector2(width - 40, 120));

            var choose = UIKit.ChunkyButton(c, "choose", Loc.T("battle.choose"), Palette.Green, () => ChoosePerk(index), 34);
            choose.GetComponent<RectTransform>().Band(false, 96, 16, 18);
            holder.localScale = Vector3.zero;
            return holder;
        }

        Sprite PerkIcon(PerkType p)
        {
            switch (p)
            {
                case PerkType.FirePower: return Art.Projectile("-", Element.Fire);
                case PerkType.WaterPower: return Art.Projectile("-", Element.Water);
                case PerkType.GrassPower: return Art.Projectile("-", Element.Grass);
                case PerkType.LightPower: return Art.Projectile("-", Element.Light);
                case PerkType.MergeCandy: case PerkType.CheapCards: return Art.Icon("candy");
                case PerkType.Interest: return Art.Icon("gold");
                case PerkType.ExtraHearts: return Art.Icon("heart");
                case PerkType.SpellHaste: return Art.Icon("spell_lightning");
                case PerkType.LuckyCrit: return Art.Icon("star");
                case PerkType.CheapSends: case PerkType.SendPoints:
                    return App.Content.Sends.Count > 0 ? Art.Enemy(App.Content.Sends[0].enemyId) : null;
                default: return Art.Icon("star");
            }
        }

        static string PerkCategory(PerkType p)
        {
            switch (p)
            {
                case PerkType.FirePower: case PerkType.WaterPower: case PerkType.GrassPower: case PerkType.LightPower: case PerkType.LuckyCrit: return "power";
                case PerkType.MergeCandy: case PerkType.CheapCards: case PerkType.Interest: return "economy";
                case PerkType.ExtraHearts: return "defense";
                case PerkType.SpellHaste: return "spell";
                default: return "attack";
            }
        }

        static Color PerkColor(PerkType p)
        {
            switch (p)
            {
                case PerkType.FirePower: return Color.Lerp(Art.ElementColor(Element.Fire), Color.black, 0.25f);
                case PerkType.WaterPower: return Color.Lerp(Art.ElementColor(Element.Water), Color.black, 0.25f);
                case PerkType.GrassPower: return Color.Lerp(Art.ElementColor(Element.Grass), Color.black, 0.25f);
                case PerkType.LightPower: return Color.Lerp(Art.ElementColor(Element.Light), Color.black, 0.35f);
                case PerkType.CheapSends: case PerkType.SendPoints: return Palette.Purple;
                case PerkType.ExtraHearts: return Palette.Red;
                case PerkType.MergeCandy: case PerkType.CheapCards: return Art.Hex(0xD9559E);
                case PerkType.Interest: return Art.Hex(0xD99A1E);
                case PerkType.LuckyCrit: return Art.Hex(0xE0A400);
                default: return Palette.Blue;
            }
        }

        static Color SpellColor(SpellKind k)
        {
            switch (k)
            {
                case SpellKind.Freeze: return Art.Hex(0x3D9BE0);
                case SpellKind.Meteor: return Art.Hex(0xE0582F);
                case SpellKind.Gust: return Art.Hex(0x58B36A);
                case SpellKind.Rage: return Art.Hex(0xD43A44);
                case SpellKind.CandyRain: return Art.Hex(0xE05AAE);
                default: return Art.Hex(0xE8C21E);
            }
        }

        // ------------------------------------------------------------------ events

        void OnSimEvent(SimEvent e)
        {
            var c = App.Content;
            bool mine = e.board == _runner.LocalPlayer;
            if (mine && _tutor != null) _tutor.OnEvent(e);
            OnModeEvent(e, mine);
            switch (e.type)
            {
                case SimEventType.UnitPlaced:
                    if (mine) { AudioService.Play(Sfx.Buy); FlyToCell(e.c, e.b); }
                    break;
                case SimEventType.UnitMerged:
                    if (mine) AudioService.Play(Sfx.Merge);
                    break;
                case SimEventType.UnitAttacked:
                    if (mine)
                    {
                        var shooter = _me.FindUnit(e.a);
                        AudioService.PlayAttack(shooter != null ? c.Units[shooter.def].element : Element.None, 0.35f);
                    }
                    break;
                case SimEventType.EnemyKilled:
                    if (mine) AudioService.Play(Sfx.Kill, 0.5f);
                    break;
                case SimEventType.EnemyLeaked:
                    if (mine) AudioService.Play(Sfx.Leak);
                    // PvP feedback: whose monster got through, and how much it hurt.
                    if (!mine && e.c == _runner.LocalPlayer)
                    {
                        ShowBanner(Loc.F("battle.your_leak", e.b), Palette.Text, 1.4f, Palette.Green);
                        _thumbFlash.color = new Color(1f, 0.2f, 0.2f, 0.65f);
                        AudioService.Play(Sfx.Reward, 0.7f);
                    }
                    else if (mine && e.c >= 0 && e.c != _runner.LocalPlayer)
                        ShowBanner(Loc.F("battle.opp_leak", e.b), Palette.Text, 1.4f, Palette.Red);
                    break;
                case SimEventType.BossDuelWon:
                    if (mine)
                    {
                        bool won = e.c == _runner.LocalPlayer;
                        ShowBanner(won ? Loc.F("battle.boss_duel_win", e.a) : Loc.T("battle.boss_duel_lose"), Palette.Text, 2.2f, won ? Palette.Green : Palette.Red);
                        AudioService.Play(won ? Sfx.Win : Sfx.Boss, 0.8f);
                    }
                    break;
                case SimEventType.WaveStarted:
                    if (mine) AudioService.Play(Sfx.Wave);
                    if (mine)
                    {
                        string title = Loc.F(e.b == 1 ? "battle.sudden_death_wave" : "battle.wave", e.a);
                        var el = (Element)e.c;
                        if (el != Element.None) title = Loc.F("battle.wave_element", title, Loc.ElementName(el));
                        ShowBanner(title, Palette.Text, 1.3f, e.b == 1 ? Palette.Red : (el != Element.None ? Color.Lerp(Art.ElementColor(el), Color.black, 0.25f) : Palette.Green));
                    }
                    break;
                case SimEventType.BossIncoming:
                    if (mine) AudioService.Play(Sfx.Boss);
                    if (mine) ShowBanner(Loc.T("battle.boss"), Palette.Text, 2f, Palette.Red);
                    break;
                case SimEventType.SendIncoming:
                    if (mine) ShowBanner(Loc.F("battle.incoming", Loc.SendName(c.Sends[e.a])), Palette.Text, 1.8f, Palette.Purple);
                    break;
                case SimEventType.SendQueued:
                    if (mine) { _sendsMade++; AudioService.Play(Sfx.Send); App.Toast(Loc.F("battle.sent", Loc.SendName(c.Sends[e.a]))); }
                    else { AudioService.Play(Sfx.Boss, 0.4f); App.Toast(Loc.F("battle.opp_sending", _opp.PlayerName, Loc.SendName(c.Sends[e.a]))); }
                    break;
                case SimEventType.SendReflected:
                    AudioService.Play(Sfx.Send);
                    ShowBanner(Loc.F(mine ? "battle.reflected" : "battle.opp_reflected", Loc.SendName(c.Sends[e.a])), Palette.Text, 1.6f, mine ? Palette.Green : Palette.Red);
                    break;
                case SimEventType.CandyStolen:
                    if (mine) App.Toast(Loc.F(e.b < 0 ? "battle.raccoon" : "battle.crow", e.a));
                    break;
                case SimEventType.CellLocked:
                    if (mine) App.Toast(Loc.T("battle.golem"));
                    break;
                case SimEventType.SpellCast:
                    if (mine) AudioService.Play(Sfx.Upgrade, 0.8f);
                    else App.Toast(Loc.F("battle.opp_spell", _opp.PlayerName, Loc.T("spell." + c.Spells[e.a].id)));
                    break;
                case SimEventType.SkillUsed:
                    if (mine) { AudioService.Play(Sfx.Chest, 0.7f); ShowBanner(Loc.T("skill.role." + ((Role)e.c).ToString().ToLowerInvariant()), Palette.Text, 0.9f, Palette.Primary); }
                    break;
                case SimEventType.UnitUpgraded:
                    if (mine)
                    {
                        AudioService.Play(Sfx.Upgrade);
                        if (e.a >= 0 && e.a < _deckCards.Length) _deckCards[e.a].Flash();
                        App.Toast(Loc.F("battle.unit_upgraded", Loc.UnitName(c.Units[_me.Deck[e.a]]), e.b + 1, e.b * c.Balance.unitUpgradePercent));
                    }
                    break;
                case SimEventType.ElementUpgraded:
                    if (mine) AudioService.Play(Sfx.Upgrade);
                    break;
                case SimEventType.BalloonPopped:
                    if (mine) AudioService.Play(Sfx.Reward);
                    break;
                case SimEventType.PerkChosen:
                    if (mine) { AudioService.Play(Sfx.Upgrade); App.Toast(Loc.F("battle.perk_chosen", Loc.T("perk." + ((PerkType)e.a).ToString().ToLowerInvariant()))); }
                    break;
                case SimEventType.StarCellsMoved:
                    if (mine) App.Toast(Loc.T("battle.star_moved"));
                    break;
                case SimEventType.Emote:
                    ShowEmote(mine, e.a);
                    break;
                case SimEventType.BossTelegraph:
                    if (mine && e.c == (int)BossSkill.LockCell) { AudioService.Play(Sfx.Boss, 0.5f); App.Toast(Loc.T("battle.lock_warning")); }
                    break;
                case SimEventType.GuardBlocked:
                    if (mine) AudioService.Play(Sfx.Upgrade, 0.6f);
                    break;
                case SimEventType.ShieldBroken:
                    if (mine) AudioService.Play(Sfx.Kill, 0.8f);
                    break;
                case SimEventType.CommandRejected:
                    if (mine && (CommandType)e.a != CommandType.PopBalloon) { App.Toast(Loc.RejectText(e.reason)); AudioService.Play(Sfx.Error); }
                    break;
            }
        }

        void ShowEmote(bool mine, int id)
        {
            var t = mine ? _myBubble : _oppBubble;
            t.text = Loc.T("emote." + Mathf.Clamp(id, 0, 5));
            t.transform.parent.gameObject.SetActive(true);
            t.transform.parent.SetAsLastSibling();
            if (mine) _myBubbleTime = 2.5f; else _oppBubbleTime = 2.5f;
            AudioService.Play(Sfx.Click);
        }

        void ShowBanner(string text, Color textColor, float seconds, Color? bg = null)
        {
            _banner.text = text;
            _banner.color = textColor;
            _bannerBg.color = bg.HasValue ? bg.Value : new Color(0f, 0f, 0f, 0.6f);
            _bannerBg.gameObject.SetActive(true);
            _bannerBg.transform.SetAsLastSibling();
            _bannerTime = seconds;
        }

        // ------------------------------------------------------------------ unit info sheet (concept C)

        /// <summary>
        /// Bottom sheet for the tapped animal: big portrait, level / element, attack / speed / bonus, skill charge,
        /// and buttons to power up that animal or summon another one. Covers the bottom panel while open.
        /// </summary>
        sealed class UnitSheet
        {
            readonly BattleScreen _s;
            readonly RectTransform _root, _portrait;
            readonly Image _glow, _elementDot;
            readonly Text _name, _level, _element, _atk, _spd, _bonus, _skill, _upgradePrice, _summonPrice, _upgradeTitle, _matchup;
            readonly Button _upgrade, _summon;
            int _uid = -1, _shownDef = -1, _shownLevel = -1;

            public int Cell { get; private set; }

            public UnitSheet(BattleScreen s)
            {
                _s = s;
                Cell = -1;
                _root = UIKit.Rect("unitSheet", s.Root).Band(false, BottomHeight);
                var hit = _root.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                var c = Deco.Frame(_root, "frame", WoodLight, Cream, 8f);
                var top = new Vector2(0f, 1f);

                _glow = UIKit.Icon(c, "glow", Art.Glow, Color.white);
                _glow.preserveAspect = false;
                _glow.rectTransform.Place(top, top, new Vector2(-10, 0), new Vector2(300, 280));
                _portrait = UIKit.Rect("portrait", c).Place(top, top, new Vector2(20, -14), new Vector2(256, 256));

                _name = InkLabel(c, "", 44, TextAnchor.MiddleLeft);
                _name.rectTransform.Place(top, top, new Vector2(300, -10), new Vector2(620, 60));

                var lvChip = UIKit.Panel(c, "lv", WoodDark).rectTransform.Place(top, top, new Vector2(300, -78), new Vector2(150, 50));
                _level = UIKit.Label(lvChip, "", 28, Palette.Text);
                _level.rectTransform.Stretch();
                var elChip = UIKit.Panel(c, "element", new Color(0f, 0f, 0f, 0.1f)).rectTransform.Place(top, top, new Vector2(462, -78), new Vector2(230, 50));
                _elementDot = UIKit.Icon(elChip, "dot", Art.Circle, Color.white);
                _elementDot.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8, 0), new Vector2(36, 36));
                _element = InkLabel(elChip, "", 26, TextAnchor.MiddleLeft);
                _element.rectTransform.Stretch(52, 6, 0, 0);
                // Element matchup at a glance; tap for the full guide.
                var mu = UIKit.Button(c, "matchup", "", new Color(0.18f, 0.49f, 0.29f, 0.9f), () => ElementGuide.Open(s.App), 22);
                mu.GetComponent<RectTransform>().Place(top, top, new Vector2(704, -78), new Vector2(250, 50));
                _matchup = mu.GetComponentInChildren<Text>();

                _atk = StatBox(c, 0, Loc.T("battle.atk"));
                _spd = StatBox(c, 1, Loc.T("battle.spd"));
                _bonus = StatBox(c, 2, Loc.T("battle.bonus"));

                _skill = InkLabel(c, "", 24, TextAnchor.MiddleLeft);
                _skill.rectTransform.Place(top, top, new Vector2(300, -236), new Vector2(740, 36));

                _upgrade = UIKit.ChunkyButton(c, "upgrade", "", Palette.Green, Upgrade, 30);
                _upgrade.GetComponent<RectTransform>().Place(Vector2.zero, Vector2.zero, new Vector2(16, 10), new Vector2(510, 124));
                _upgradePrice = PriceFace(_upgrade, "", 34, 38, Palette.Text);
                _upgradeTitle = _upgrade.transform.Find("face").GetComponentsInChildren<Text>()[1];
                _summon = UIKit.ChunkyButton(c, "summon", "", Gold, SummonSame, 30);
                _summon.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16, 10), new Vector2(510, 124));
                _summonPrice = PriceFace(_summon, Loc.T("battle.summon"), 34, 38, Palette.Text);

                var close = UIKit.ChunkyButton(c, "close", "×", Wood, Hide, 44);
                close.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10, -8), new Vector2(84, 84));
                _root.gameObject.SetActive(false);
            }

            static Text StatBox(RectTransform parent, int index, string title)
            {
                var top = new Vector2(0f, 1f);
                var box = UIKit.Panel(parent, "stat" + index, new Color(0f, 0f, 0f, 0.08f)).rectTransform
                    .Place(top, top, new Vector2(300 + index * 246, -138), new Vector2(236, 92));
                InkLabel(box, title, 22, TextAnchor.UpperLeft).rectTransform.Stretch(14, 8, 6, 50);
                var value = InkLabel(box, "", 36, TextAnchor.LowerLeft);
                value.rectTransform.Stretch(14, 8, 34, 6);
                return value;
            }

            /// <summary>Summon one more of the shown animal (merge partner), into a random free cell.</summary>
            void SummonSame()
            {
                var u = Cell >= 0 ? _s._me.Cells[Cell] : null;
                if (u == null) return;
                int slot = _s._me.DeckSlotOf(u.def);
                if (slot >= 0) _s._runner.Submit(CommandType.SummonUnit, slot, -1);
            }

            void Upgrade()
            {
                var u = Cell >= 0 ? _s._me.Cells[Cell] : null;
                if (u == null) return;
                int slot = _s._me.DeckSlotOf(u.def);
                if (slot >= 0) _s._runner.Submit(CommandType.UpgradeUnit, slot);
            }

            public void Show(int cell)
            {
                var u = _s._me.Cells[cell];
                if (u == null) return;
                Cell = cell;
                _uid = u.uid;
                _shownDef = -1;
                _root.gameObject.SetActive(true);
                _root.SetAsLastSibling();
                _s._world.Local.SetSelected(cell);
                Refresh();
            }

            public void Hide()
            {
                if (Cell < 0) return;
                Cell = -1;
                _uid = -1;
                _root.gameObject.SetActive(false);
                _s._world.Local.SetSelected(-1);
            }

            public void Refresh()
            {
                if (Cell < 0) return;
                var me = _s._me;
                var u = me.Cells[Cell];
                if (u == null || u.uid != _uid)
                {
                    // Moved (dragged) or merged away: follow the same animal if it is still on the board.
                    var moved = me.FindUnit(_uid);
                    if (moved == null) { Hide(); return; }
                    Cell = moved.cell;
                    u = moved;
                    _s._world.Local.SetSelected(Cell);
                }
                var content = _s.App.Content;
                var b = content.Balance;
                var def = content.Units[u.def];
                if (u.def != _shownDef || u.level != _shownLevel)
                {
                    _shownDef = u.def;
                    _shownLevel = u.level;
                    UIKit.Clear(_portrait);
                    Icons.Unit(_portrait, def, u.level >= 5 ? 7 : (u.level >= 3 ? 4 : 1), 256).Stretch();
                    var rc = Art.RarityColor(def.rarity);
                    _glow.color = new Color(rc.r, rc.g, rc.b, 0.75f);
                    _name.text = Loc.UnitName(def);
                    _level.text = Loc.F("battle.lv", u.level);
                    var emblem = ElementGuide.Emblem(def.element);
                    _elementDot.sprite = emblem != null ? emblem : Art.Circle;
                    _elementDot.color = emblem != null ? Color.white : Art.ElementColor(def.element);
                    _element.text = Loc.ElementName(def.element) + " · " + Loc.RoleName(def.role);
                    _matchup.text = ElementGuide.ShortLine(def);
                    _upgradeTitle.text = Loc.F("battle.upgrade", Loc.UnitName(def));
                }
                _atk.text = me.HitDamage(u).ToString();
                float perSecond = b.tickRate / (float)Mathf.Max(1, me.AttackInterval(u));
                _spd.text = Loc.F("battle.per_sec", perSecond.ToString("0.0"));
                _bonus.text = "+" + (me.PositionPercent(u) - 100) + "%";
                int charge = me.SkillCharge(u);
                _skill.text = charge > 0
                    ? Loc.F("battle.skill_charging", Loc.T("skill.role." + def.role.ToString().ToLowerInvariant()), u.energy * 100 / charge)
                    : Loc.F("battle.skill_need_level", b.skillMinLevel);

                int slot = me.DeckSlotOf(u.def);
                bool max = slot < 0 || me.DeckLevel[slot] >= b.unitUpgradeMax;
                int cost = slot >= 0 ? me.UnitUpgradeCost(slot) : 0;
                _upgradePrice.text = max ? Loc.T("deck.max") : cost.ToString();
                _upgrade.interactable = !max && me.Candy >= cost;
                int price = slot >= 0 ? me.SummonPrice(slot) : me.CardPrice;
                _summonPrice.text = price.ToString();
                _summon.interactable = slot >= 0 && me.Candy >= price && me.CountFreeCells() > 0;
            }
        }

        // ------------------------------------------------------------------ tutorial

        /// <summary>Step-by-step hints for the first match: summon, power up, merge, spell, send, win.</summary>
        sealed class TutorialDirector
        {
            enum Step { Buy, BuyMore, Merge, Spell, Send, Finish, Done }

            readonly BattleScreen _s;
            readonly RectTransform _panel;
            readonly Text _text;
            Step _step = Step.Buy;
            RectTransform _target;
            Image _glow;
            float _t, _finishTime;

            public TutorialDirector(BattleScreen s)
            {
                _s = s;
                _panel = UIKit.Panel(s.Root, "tutorial", new Color(0.1f, 0.08f, 0.16f, 0.94f)).rectTransform.Band(false, 190, BottomHeight + 16, 24);
                var coachArt = Art.Icon("coach_panda");
                var coach = coachArt != null ? UIKit.Icon(_panel, "coach", coachArt, Color.white) : Icons.Currency(_panel, "trophy");
                coach.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(6, 4), new Vector2(150, 150));
                _text = UIKit.Label(_panel, "", 32, Palette.Text, TextAnchor.MiddleLeft);
                _text.rectTransform.Stretch(165, 20, 10, 10);
                _glow = UIKit.Panel(s.Root, "tutorialGlow", new Color(1f, 0.85f, 0.2f, 0.25f));
                _glow.raycastTarget = false;
                Show(Step.Buy);
            }

            void Show(Step step)
            {
                _step = step;
                _t = 0f;
                _panel.gameObject.SetActive(step != Step.Done);
                switch (step)
                {
                    case Step.Buy: _text.text = Loc.T("tutorial.buy"); _target = _s._deckRow; break;
                    case Step.BuyMore: _text.text = Loc.T("tutorial.buy_more"); _target = _s._deckRow; break;
                    case Step.Merge: _text.text = Loc.T("tutorial.merge"); _target = null; break;
                    case Step.Spell: _text.text = Loc.T("tutorial.spell"); _target = _s._actionRow; break;
                    case Step.Send: _text.text = Loc.T("tutorial.send"); _target = _s._sendsRoot; break;
                    case Step.Finish: _text.text = Loc.T("tutorial.finish"); _target = null; _finishTime = 7f; break;
                }
                _glow.gameObject.SetActive(_target != null);
                _glow.rectTransform.SetAsLastSibling();
                _panel.SetAsLastSibling();
                if (step != Step.Buy && step != Step.Done) AudioService.Play(Sfx.Reward, 0.6f);
            }

            public void Tick(float dt)
            {
                _t += dt;
                if (_target != null)
                {
                    // Follow the target in screen space (it can live under any parent).
                    var c = new Vector3[4];
                    _target.GetWorldCorners(c);
                    var rt = _glow.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.position = (c[0] + c[2]) * 0.5f;
                    rt.sizeDelta = _target.rect.size + new Vector2(20, 20);
                    _glow.color = new Color(1f, 0.85f, 0.2f, 0.15f + 0.2f * (0.5f + 0.5f * Mathf.Sin(_t * 6f)));
                }
                float pulse = 1f + 0.03f * Mathf.Sin(_t * 5f);
                _panel.localScale = new Vector3(pulse, pulse, 1f);

                if (_step == Step.BuyMore && (_s._me.Purchases >= 4 || _t > 20f)) Show(Step.Merge);
                if (_step == Step.Spell && _t > 25f) Show(Step.Send);
                if (_step == Step.Send && _t > 25f) Show(Step.Finish);   // no points yet: move on
                if (_step == Step.Finish) { _finishTime -= dt; if (_finishTime <= 0f) Show(Step.Done); }
            }

            public void OnEvent(SimEvent e)
            {
                switch (e.type)
                {
                    case SimEventType.UnitPlaced: if (_step == Step.Buy) Show(Step.BuyMore); break;
                    case SimEventType.UnitUpgraded: if (_step == Step.BuyMore) Show(Step.Merge); break;
                    case SimEventType.UnitMerged: if (_step <= Step.Merge) Show(Step.Spell); break;
                    case SimEventType.SpellCast: if (_step == Step.Spell) Show(Step.Send); break;
                    case SimEventType.SendQueued: if (_step == Step.Send) Show(Step.Finish); break;
                }
            }
        }

        // ------------------------------------------------------------------ widgets

        /// <summary>
        /// Deck bar card = the summon card of one animal.
        ///   tap the card      : summon THIS animal into a random free cell (price on the bottom pill)
        ///   drag it to a cell : summon it exactly there
        ///   tap the ↑ badge   : power up this animal (needs at least one on the board)
        /// </summary>
        sealed class DeckCard
        {
            readonly RectTransform _root, _costPill, _nextTag, _portrait, _badge, _upTag;
            readonly Image _costBg, _countBg, _badgeBg, _upTagBg;
            readonly Text _level, _cost, _count, _upCost;
            float _flash;
            int _shownCount = -1;
            bool _shownCan = true;

            public DeckCard(BattleScreen s, RectTransform row, int slot, float x0, float x1)
            {
                var def = s.App.Content.Units[s._me.Deck[slot]];
                _root = UIKit.Rect("card" + slot, row);
                _root.anchorMin = new Vector2(x0, 0f);
                _root.anchorMax = new Vector2(x1, 1f);
                _root.offsetMin = new Vector2(5f, 0f);
                _root.offsetMax = new Vector2(-5f, 0f);
                var hit = _root.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                _root.gameObject.AddComponent<DeckCardDrag>().Init(s, slot, def);
                var rarity = Art.RarityColor(def.rarity);
                var c = Deco.Frame(_root, "frame", rarity, CardCream, 6f);

                var glow = UIKit.Icon(c, "glow", Art.Glow, new Color(rarity.r, rarity.g, rarity.b, 0.45f));
                glow.preserveAspect = false;
                glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 4), new Vector2(180, 160));
                _portrait = Icons.Unit(c, def, 1, 146);
                _portrait.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(146, 146));

                // How many of this animal stand on the board.
                _countBg = UIKit.Panel(c, "count", WoodDark);
                _countBg.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(6, 60), new Vector2(64, 36));
                _count = UIKit.Label(_countBg.transform, "", 24, Palette.Text);
                _count.rectTransform.Stretch();

                // "NEXT" tag: this animal comes with the next random Summon.
                _nextTag = UIKit.Panel(_root, "nextTag", Gold).rectTransform
                    .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(130, 34));
                var nt = UIKit.Label(_nextTag, Loc.T("battle.next"), 20, Ink);
                nt.rectTransform.Stretch();
                var ntShadow = nt.GetComponent<Shadow>();
                if (ntShadow != null) ntShadow.enabled = false;
                _nextTag.gameObject.SetActive(false);

                var dot = UIKit.Icon(c, "element", Art.Circle, Art.ElementColor(def.element)).rectTransform;
                dot.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6, -6), new Vector2(30, 30));
                UIKit.Icon(dot, "ring", Art.Ring, new Color(0f, 0f, 0f, 0.45f)).rectTransform.Stretch(-2, -2, -2, -2);

                // Power-up badge (its own button): power level, turns green with ↑ when it can be bought.
                var badgeBtn = UIKit.Button(_root, "powerUp", "", new Color(0f, 0f, 0f, 0f), () => s._runner.Submit(CommandType.UpgradeUnit, slot));
                _badge = badgeBtn.GetComponent<RectTransform>();
                _badge.Place(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-30, -30), new Vector2(64, 64));
                _badgeBg = UIKit.Icon(_badge, "bg", Art.Circle, WoodDark);
                _badgeBg.rectTransform.Stretch();
                UIKit.Icon(_badge, "ring", Art.Ring, Cream).rectTransform.Stretch(-2, -2, -2, -2);
                _level = UIKit.Label(_badge, "1", 30, Palette.Text);
                _level.rectTransform.Stretch();
                _upTagBg = UIKit.Panel(_badge, "upCost", Palette.Green);
                _upTag = _upTagBg.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, 6), new Vector2(76, 28));
                _upCost = UIKit.Label(_upTag, "", 20, Palette.Text);
                _upCost.rectTransform.Stretch();

                // Summon price
                _costBg = UIKit.Panel(c, "cost", Palette.Green);
                _costPill = _costBg.rectTransform.Band(false, 50, 4, 6);
                Icons.Currency(_costPill, "candy").rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6, 0), new Vector2(44, 44));
                _cost = UIKit.Outlined(UIKit.Label(_costPill, "", 32, Palette.Text), 1.5f);
                _cost.rectTransform.Stretch(44, 4, 0, 0);
            }

            public void Set(int summonPrice, bool canSummon, int powerLevel, int upCost, bool canUpgrade, bool max, int count, bool isNext, float t)
            {
                _cost.text = summonPrice.ToString();
                _costBg.color = canSummon ? Palette.Green : WoodDark;
                if (canSummon != _shownCan)
                {
                    _shownCan = canSummon;
                    foreach (var g in _portrait.GetComponentsInChildren<Graphic>())
                        g.color = canSummon ? Color.white : new Color(0.62f, 0.6f, 0.66f, 0.85f);
                }
                if (count != _shownCount)
                {
                    _shownCount = count;
                    _count.text = "x" + count;
                    _countBg.gameObject.SetActive(count > 0);
                }

                // Power-up badge: level number; green + ↑ + price when it can be bought.
                _level.text = canUpgrade ? "↑" : (powerLevel + 1).ToString();
                _badgeBg.color = canUpgrade ? Palette.Green : WoodDark;
                _upTag.gameObject.SetActive(count > 0 && !max);
                _upCost.text = "↑" + upCost;
                _upTagBg.color = canUpgrade ? Palette.Green : new Color(0f, 0f, 0f, 0.6f);
                float bs = canUpgrade ? 1f + 0.1f * Mathf.Abs(Mathf.Sin(t * 5f)) : 1f;
                _badge.localScale = new Vector3(bs, bs, 1f);

                _nextTag.gameObject.SetActive(isNext);
                if (isNext) { float p = 1f + 0.06f * Mathf.Sin(t * 6f); _nextTag.localScale = new Vector3(p, p, 1f); }
                if (_flash > 0f)
                {
                    _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime);
                    float k = 1f + 0.12f * _flash / 0.35f;
                    _root.localScale = new Vector3(k, k, 1f);
                }
            }

            public void Flash() { _flash = 0.35f; }
        }

        /// <summary>Tap = summon this animal (random free cell); drag onto a free cell = summon it there.</summary>
        sealed class DeckCardDrag : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler, UnityEngine.EventSystems.IBeginDragHandler,
            UnityEngine.EventSystems.IDragHandler, UnityEngine.EventSystems.IEndDragHandler
        {
            BattleScreen _screen;
            int _slot;
            UnitDef _def;
            RectTransform _ghost;
            int _cell = -1;

            public void Init(BattleScreen screen, int slot, UnitDef def) { _screen = screen; _slot = slot; _def = def; }

            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
            {
                if (e.dragging) return;
                AudioService.Play(Sfx.Click);
                _screen._runner.Submit(CommandType.SummonUnit, _slot, -1);
            }

            public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData e)
            {
                _ghost = UIKit.Rect("ghost", _screen.Root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 170));
                Icons.Unit(_ghost, _def, 1, 170).Stretch();
                foreach (var g in _ghost.GetComponentsInChildren<Graphic>()) { g.raycastTarget = false; g.color = new Color(1f, 1f, 1f, 0.85f); }
                _ghost.SetAsLastSibling();
                OnDrag(e);
            }

            public void OnDrag(UnityEngine.EventSystems.PointerEventData e)
            {
                if (_ghost == null) return;
                _ghost.position = new Vector3(e.position.x, e.position.y + 70f * _ghost.lossyScale.y, 0f);
                _cell = _screen._world.PlaceCellAt(e.position);
                _screen._world.ShowPlaceTarget(_cell);
            }

            public void OnEndDrag(UnityEngine.EventSystems.PointerEventData e)
            {
                if (_ghost == null) return;
                Destroy(_ghost.gameObject);
                _ghost = null;
                _screen._world.ShowPlaceTarget(-1);
                if (_cell >= 0) _screen._runner.Submit(CommandType.SummonUnit, _slot, _cell);
                _cell = -1;
            }
        }

        /// <summary>Round spell button with its name and candy cost; a dark radial sweep shows the cooldown.</summary>
        sealed class SpellButton
        {
            readonly Button _button;
            readonly Image _face, _cooldown;
            readonly Text _seconds;
            readonly Color _color;
            bool _armed;

            public SpellButton(Transform parent, SpellDef def, bool rightSide, float x, System.Action onClick)
            {
                _color = SpellColor(def.kind);
                var anchor = new Vector2(rightSide ? 1f : 0f, 0.5f);
                var root = UIKit.Rect("spell_" + def.id, parent).Place(anchor, anchor, new Vector2(x, 0), new Vector2(132, 172));

                var ring = UIKit.Icon(root, "ring", Art.Circle, Color.Lerp(_color, Color.black, 0.45f)).rectTransform;
                ring.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(124, 124));
                _face = UIKit.Icon(ring, "face", Art.Circle, _color);
                _face.rectTransform.Stretch(6, 6, 6, 10);
                _face.raycastTarget = true;
                _button = _face.gameObject.AddComponent<Button>();
                _button.targetGraphic = _face;
                _button.onClick.AddListener(() => AudioService.Play(Sfx.Click));
                _button.onClick.AddListener(() => onClick());
                UIKit.Icon(_face.transform, "shine", Art.Circle, new Color(1f, 1f, 1f, 0.25f)).rectTransform
                    .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(70, 34));
                // Optional art ZooArt/UI/spell_{id}.png, else a coloured gem.
                var spellArt = Art.Icon("spell_" + def.id);
                var gem = UIKit.Icon(_face.transform, "gem", spellArt != null ? spellArt : Art.Diamond, spellArt != null ? Color.white : Color.Lerp(_color, Color.white, 0.55f)).rectTransform;
                gem.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, spellArt != null ? 10 : 4), spellArt != null ? new Vector2(78, 78) : new Vector2(46, 46));
                UIKit.Label(_face.transform, Loc.T("spell." + def.id), 20, Palette.Text).rectTransform.Band(false, 30, 10, 2);

                _cooldown = UIKit.Icon(ring, "cooldown", Art.Circle, new Color(0f, 0f, 0f, 0.6f));
                _cooldown.rectTransform.Stretch(6, 6, 6, 10);
                _cooldown.type = Image.Type.Filled;
                _cooldown.fillMethod = Image.FillMethod.Radial360;
                _cooldown.fillOrigin = 2;   // top
                _cooldown.raycastTarget = false;
                _seconds = UIKit.Label(_cooldown.transform, "", 40, Palette.Text);
                _seconds.rectTransform.Stretch();

                var price = UIKit.Panel(root, "price", WoodDark).rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(104, 42));
                Icons.Currency(price, "candy").rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(2, 0), new Vector2(38, 38));
                UIKit.Label(price, def.cost.ToString(), 26, Palette.Text).rectTransform.Stretch(36, 4, 0, 0);
            }

            public void SetArmed(bool armed) { _armed = armed; }

            public void Set(float cooldownFraction, int secondsLeft, bool affordable)
            {
                bool cooling = cooldownFraction > 0f;
                _cooldown.gameObject.SetActive(cooling);
                if (cooling)
                {
                    _cooldown.fillAmount = Mathf.Clamp01(cooldownFraction);
                    _seconds.text = secondsLeft.ToString();
                }
                _button.interactable = !cooling && affordable;
                _face.color = _armed ? Color.Lerp(_color, Color.white, 0.25f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f))
                    : (affordable || cooling ? _color : Color.Lerp(_color, Palette.Disabled, 0.6f));
            }
        }

        /// <summary>Send-monster button: monster art, element dot, "+income" tag and the candy price.</summary>
        sealed class SendButton
        {
            readonly Button _button;
            readonly Text _cost;
            readonly Image _frame;

            public SendButton(Transform parent, ContentDatabase content, int sendIndex, int income, Vector2 pos, Vector2 size, System.Action onClick)
            {
                var def = content.Sends[sendIndex];
                _button = UIKit.Button(parent, "send_" + def.id, "", Palette.Panel, onClick);
                _frame = (Image)_button.targetGraphic;
                _button.GetComponent<RectTransform>().Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), pos, size);
                var art = Art.Enemy(def.enemyId);
                var icon = UIKit.Icon(_button.transform, "icon", art != null ? art : Art.Diamond, art != null ? Color.white : Palette.Purple);
                icon.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(size.x - 18, size.x - 18));
                var dot = UIKit.Icon(_button.transform, "element", Art.Circle, Art.ElementColor(def.element)).rectTransform;
                dot.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6, -6), new Vector2(24, 24));
                UIKit.Icon(dot, "ring", Art.Ring, new Color(0f, 0f, 0f, 0.5f)).rectTransform.Stretch(-2, -2, -2, -2);
                if (income > 0)
                {
                    var inc = UIKit.Panel(_button.transform, "income", Art.Hex(0x2E7D4A)).rectTransform
                        .Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4, -4), new Vector2(52, 28));
                    UIKit.Label(inc, "+" + income, 20, Palette.Text).rectTransform.Stretch();
                }
                var name = UIKit.Label(_button.transform, Loc.SendName(def), 17, Palette.TextDim);
                name.rectTransform.Band(false, 26, 44, 2);
                var price = UIKit.Rect("price", _button.transform).Band(false, 40, 4, 2);
                Icons.Currency(price, "candy").rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4, 0), new Vector2(34, 34));
                _cost = UIKit.Label(price, "", 28, Palette.Primary);
                _cost.rectTransform.Stretch(30, 0, 0, 0);
            }

            public void Set(int cost, bool enabled, bool used, bool discount)
            {
                _cost.text = used ? Loc.T("battle.send_used") : (discount ? cost + "*" : cost.ToString());
                _button.interactable = enabled;
                _frame.color = enabled ? Palette.Purple : Palette.Panel;
            }
        }

        /// <summary>Monster the opponent sent you (travelling): element, arrival countdown and the Reflect button.</summary>
        sealed class IncomingRow
        {
            readonly Button _reflect;
            readonly Text _label, _eta;
            readonly bool _reflected;

            public IncomingRow(BattleScreen screen, RectTransform list, int index, SendDef def, bool reflected)
            {
                _reflected = reflected;
                var row = UIKit.Panel(list, "in" + index, new Color(0.25f, 0.08f, 0.3f, 0.92f)).rectTransform
                    .Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(index * 290f, 0f), new Vector2(282f, 68f));
                var art = Art.Enemy(def.enemyId);
                var icon = UIKit.Icon(row, "icon", art != null ? art : Art.Diamond, art != null ? Color.white : Palette.Purple).rectTransform;
                icon.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4, 0), new Vector2(60, 60));
                var dot = UIKit.Icon(icon, "element", Art.Circle, Art.ElementColor(def.element)).rectTransform;
                dot.Place(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
                _eta = UIKit.Outlined(UIKit.Label(row, "", 26, Palette.Primary), 2f);
                _eta.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64, 0), new Vector2(40, 60));
                _reflect = UIKit.Button(row, "reflect", "", Palette.Green, () => screen._runner.Submit(CommandType.Reflect, index), 22);
                _reflect.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4, 0), new Vector2(170, 58));
                _label = _reflect.GetComponentInChildren<Text>();
                UIKit.Motion(row).PopIn(0f, 0.25f);
            }

            public void Set(int cost, int candy, int secondsLeft)
            {
                _eta.text = secondsLeft + "s";
                if (_reflected)
                {
                    _label.text = Loc.T("battle.reflected_short");
                    _reflect.interactable = false;
                    return;
                }
                _label.text = Loc.F("battle.reflect", cost);
                _reflect.interactable = candy >= cost;
            }
        }
    }
}
