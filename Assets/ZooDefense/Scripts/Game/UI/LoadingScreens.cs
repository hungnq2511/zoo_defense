using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// App start ("màn chờ"): loading artwork, logo, a chunky progress bar with a paw riding the fill and rotating
    /// tips. Really preloads the sprites the next screens need (deck animals, UI icons, arena art) while it shows.
    /// </summary>
    public sealed class SplashScreen : ScreenBase
    {
        /// <summary>The loading screen shows at least this long (art, tips), even when everything is already loaded.</summary>
        const float MinTime = 5f;
        readonly List<System.Action> _jobs = new List<System.Action>();
        int _done;
        float _t, _shown, _tipTime;
        int _tip;
        RectTransform _fill, _paw;
        Text _percent, _tipText;

        public const int TipCount = 6;

        protected override void Build()
        {
            UIKit.Panel(Root, "bg", Art.Hex(0x7EC8F0), false).rectTransform.Stretch();
            var art = Art.Loading();
            if (art != null) UIKit.Cover(UIKit.Icon(Root, "art", art, Color.white));
            Deco.Fade(Root, false, 760, new Color(0.08f, 0.05f, 0.14f, 0.92f));

            var logo = Deco.Logo(Root);
            logo.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(940, 230));
            UIKit.Motion(logo).PopIn(0.1f, 0.45f);

            // Tip pill
            var tip = UIKit.Panel(Root, "tip", new Color(0.1f, 0.06f, 0.18f, 0.78f)).rectTransform
                .Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 250), new Vector2(960, 110));
            _tipText = UIKit.Label(tip, "", 30, Palette.Text);
            _tipText.rectTransform.Stretch(30, 30, 6, 6);
            _tip = Random.Range(0, TipCount);
            _tipText.text = Loc.T("tip." + _tip);

            // Progress bar: wood rim, dark track, gold fill with a shine, paw at the fill's tip
            var bar = Deco.Frame(Root, "bar", Art.Hex(0xC08A4E), Art.Hex(0x2A1D3A), 7f);
            ((RectTransform)bar.parent).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 130), new Vector2(860, 84));
            _fill = UIKit.Panel(bar, "fill", Art.Hex(0xFFC21A)).rectTransform;
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.offsetMin = new Vector2(4, 4);
            _fill.offsetMax = new Vector2(-4, -4);
            UIKit.Panel(_fill, "shine", new Color(1f, 1f, 1f, 0.3f)).rectTransform.Stretch(6, 6, 4, 0).anchorMin = new Vector2(0f, 0.55f);
            _percent = UIKit.Outlined(UIKit.Label(bar, "0%", 32, Palette.Text), 2.5f);
            _percent.rectTransform.Stretch();
            _paw = UIKit.Rect("paw", bar).Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 96));
            var star = Art.Icon("star");
            UIKit.Icon(_paw, "icon", star != null ? star : Art.Circle, star != null ? Color.white : Palette.Primary).rectTransform.Stretch();
            UIKit.Label(Root, Loc.T("loading.title"), 26, Palette.TextDim).rectTransform
                .Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(600, 44));

            // Real work: warm the sprite cache so the lobby and the first match open without hitches.
            var content = App.Content;
            foreach (var id in App.Profile.deck) { var d = id; _jobs.Add(() => { Art.Unit(d, 1); Art.Unit(d, 4); Art.Projectile(d, Element.None); Art.UnitCast(d, 1); }); }
            string[] ui = { "logo", "ribbon", "ribbon_defeat", "podium", "button_battle", "button_summon", "rays", "star", "candy", "heart", "gold", "gems", "trophy", "xp",
                            "chest_wood", "chest_silver", "chest_gold", "chest_magic", "board_portal", "board_gate", "balloon" };
            foreach (var n in ui) { var name = n; _jobs.Add(() => Art.Icon(name)); }
            for (int i = 1; i <= 5; i++) { int a = i; _jobs.Add(() => Art.Background(a)); }
            foreach (var s in content.Spells) { var id = s.id; _jobs.Add(() => Art.Icon("spell_" + id)); }
            foreach (var e in content.Enemies) { var id = e.id; _jobs.Add(() => Art.Enemy(id)); }
        }

        public override void Tick(float dt)
        {
            _t += dt;
            // A couple of loads per frame keeps the animation smooth.
            for (int k = 0; k < 2 && _done < _jobs.Count; k++) { _jobs[_done](); _done++; }
            float real = _jobs.Count == 0 ? 1f : _done / (float)_jobs.Count;
            // Eased: fast start, slows down near the end, like a real download.
            float p = Mathf.Clamp01(_t / MinTime);
            float target = Mathf.Min(real, 1f - (1f - p) * (1f - p));
            _shown = Mathf.MoveTowards(_shown, target, dt * 0.8f);
            _fill.anchorMax = new Vector2(Mathf.Max(0.02f, _shown), 1f);
            _percent.text = Mathf.RoundToInt(_shown * 100f) + "%";
            var parentW = ((RectTransform)_fill.parent).rect.width;
            _paw.anchoredPosition = new Vector2(_shown * parentW, 10f + Mathf.Abs(Mathf.Sin(_t * 9f)) * 14f);
            _paw.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_t * 9f) * 10f);

            _tipTime += dt;
            if (_tipTime > 2.5f) { _tipTime = 0f; _tip = (_tip + 1) % TipCount; _tipText.text = Loc.T("tip." + _tip); }

            if (_shown >= 0.999f && _done >= _jobs.Count)
            {
                App.Show(new LobbyScreen());
                Privacy.AskIfNeeded(App);   // first launch: personalised ads yes / no
            }
        }
    }

    /// <summary>
    /// Matchmaking in two acts.
    ///   Searching: radar rings pulse out from your lead animal, a sweep turns, timer, trophies, tips, Cancel.
    ///   Found: VS screen - your card slides in from the left (blue), the opponent's from the right (red), each
    ///   with the lead animal, name, trophies and the 5-animal team; a big VS bursts in the middle, then 3-2-1.
    /// </summary>
    public sealed class MatchmakingScreen : ScreenBase
    {
        readonly bool _ranked;
        float _t, _searchTime;
        bool _found;
        int _seed, _count = -1;
        string _opponent;
        string[] _oppDeck;
        int _oppTrophies;

        RectTransform _search, _vs, _sweep, _cancel, _hero, _myCard, _oppCard, _vsBadge, _countBg;
        readonly List<RectTransform> _rings = new List<RectTransform>();
        Text _status, _timer, _countText;

        readonly string _fixedOpponent;
        readonly string[] _fixedDeck;
        readonly bool _revenge;
        // Online: looking for a real player of the same arena first; a bot if nobody is found in time.
        bool _online, _handedOff;
        /// <summary>Ranked: look for a real player this long (seconds) before playing a bot (Remote Config "bot_wait_seconds").</summary>
        float OnlineWait { get { return Tuning.Current.botWaitSeconds; } }
        float _onlineTime, _retryAt = -1f;
        OnlineMatchInfo _onlineMatch;
        string _oppTitle;

        public MatchmakingScreen(bool ranked) { _ranked = ranked; }

        /// <summary>Rematch the same opponent (same name and team). revenge: you lost to them (bonus if you win).</summary>
        public MatchmakingScreen(bool ranked, string opponent, string[] deck, bool revenge)
        {
            _ranked = ranked; _fixedOpponent = opponent; _fixedDeck = deck; _revenge = revenge;
        }

        protected override void Build()
        {
            _seed = Random.Range(1, int.MaxValue);
            _opponent = _fixedOpponent ?? MetaRules.RandomBotName(_seed);
            _oppDeck = _fixedDeck ?? BotBrain.RandomDeck(App.Content, _seed ^ 0x5bd1e995);
            int mine = App.Meta.State.trophies;
            _oppTrophies = Mathf.Max(0, mine + (_seed % 61) - 30);
            _searchTime = _fixedOpponent != null ? 0.6f : 1.6f + (_seed % 100) / 100f;
            var center = new Vector2(0.5f, 0.5f);

            UIKit.Panel(Root, "bg", Art.Hex(0x1B1230), false).rectTransform.Stretch();
            var art = Art.Loading();
            if (art != null) UIKit.Cover(UIKit.Icon(Root, "art", art, new Color(0.55f, 0.5f, 0.75f, 0.55f)));
            Deco.Fade(Root, true, 600, new Color(0.08f, 0.05f, 0.14f, 0.9f));
            Deco.Fade(Root, false, 800, new Color(0.08f, 0.05f, 0.14f, 0.95f));
            var rays = UIKit.Icon(Root, "rays", Art.Rays, new Color(1f, 0.85f, 0.5f, 0.12f)).rectTransform;
            rays.Place(center, center, new Vector2(0, 80), new Vector2(1800, 1800));
            UIKit.Motion(rays).Spin(6f);

            var arena = MetaRules.ArenaFor(mine);
            Deco.Ribbon(Root, _ranked ? Loc.F("lobby.arena", arena.index) + "  ·  " + Loc.ArenaName(arena) : Loc.T("mm.practice"), Art.Hex(0x8A4FE0), 40)
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(820, 120));

            BuildSearch();
            BuildVs();

            if (_ranked && _fixedOpponent == null && Online.Pvp != null && App.Profile.onlinePvp)
            {
                _online = true;
                Online.Pvp.StartSearch(Online.MyHello(App), OnlineWait);
            }

            var cancel = UIKit.ChunkyButton(Root, "cancel", Loc.T("common.cancel"), Palette.Red, () => App.Show(new LobbyScreen()), 42);
            UIKit.Outlined(cancel.GetComponentInChildren<Text>(), 2.5f);
            _cancel = cancel.GetComponent<RectTransform>();
            _cancel.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(460, 136));
            AudioService.PlayMusic("music_lobby");
        }

        void BuildSearch()
        {
            var center = new Vector2(0.5f, 0.5f);
            _search = UIKit.Rect("search", Root).Place(center, center, new Vector2(0, 120), new Vector2(900, 900));

            for (int i = 0; i < 3; i++)
            {
                var ring = UIKit.Icon(_search, "ring" + i, Art.Ring, new Color(0.5f, 0.85f, 1f, 0.6f)).rectTransform;
                ring.Place(center, center, Vector2.zero, new Vector2(420, 420));
                _rings.Add(ring);
            }
            var disc = UIKit.Icon(_search, "disc", Art.Circle, new Color(0.12f, 0.2f, 0.38f, 0.85f)).rectTransform;
            disc.Place(center, center, Vector2.zero, new Vector2(560, 560));
            UIKit.Icon(disc, "edge", Art.Ring, Art.Hex(0x5BB6FF)).rectTransform.Stretch(-6, -6, -6, -6);
            UIKit.Icon(disc, "mid", Art.Ring, new Color(0.36f, 0.71f, 1f, 0.35f)).rectTransform.Stretch(90, 90, 90, 90);
            // Sweep: a glowing wedge-ish bar turning around the centre
            _sweep = UIKit.Rect("sweep", disc).Place(center, center, Vector2.zero, new Vector2(10, 10));
            for (int k = 0; k < 6; k++)
            {
                var bar = UIKit.Panel(_sweep, "beam" + k, new Color(0.5f, 0.9f, 1f, 0.5f - k * 0.075f), false).rectTransform;
                bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
                bar.pivot = new Vector2(0.5f, 0f);
                bar.sizeDelta = new Vector2(10, 270);
                bar.anchoredPosition = Vector2.zero;
                bar.localRotation = Quaternion.Euler(0f, 0f, k * 5f);
            }
            // A few "blips" (other players) on the radar
            for (int k = 0; k < 5; k++)
            {
                float a = (k * 73 + _seed % 50) * Mathf.Deg2Rad, r = 120 + (k * 37) % 120;
                var blip = UIKit.Icon(disc, "blip", Art.Circle, new Color(1f, 0.85f, 0.4f, 0.9f)).rectTransform;
                blip.Place(center, center, new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r), new Vector2(18, 18));
                UIKit.Motion(blip).Pulse(0.35f, 3f + k);
            }

            // Your lead animal in the centre
            var lead = App.Content.Unit(App.Profile.deck[0]);
            var heroBg = UIKit.Icon(_search, "heroBg", Art.Circle, Art.Hex(0xFFF1D6)).rectTransform;
            heroBg.Place(center, center, Vector2.zero, new Vector2(250, 250));
            UIKit.Icon(heroBg, "ring", Art.Ring, Art.Hex(0xFFC93C)).rectTransform.Stretch(-8, -8, -8, -8);
            _hero = UIKit.Rect("hero", heroBg).Stretch(14, 14, 10, 18);
            Icons.Unit(_hero, lead, 1, 220).Stretch();

            _status = UIKit.Outlined(UIKit.Label(_search, Loc.T("mm.finding"), 54, Palette.Text), 3f);
            _status.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -420), new Vector2(1000, 80));
            _timer = UIKit.Label(_search, "0:00", 34, Palette.TextDim);
            _timer.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -490), new Vector2(400, 50));

            var trophies = UIKit.Panel(_search, "trophies", new Color(0f, 0f, 0f, 0.45f)).rectTransform
                .Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -560), new Vector2(260, 64));
            Icons.Currency(trophies, "trophy").rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8, 0), new Vector2(56, 56));
            UIKit.Label(trophies, App.Meta.State.trophies.ToString(), 32, Palette.Text).rectTransform.Stretch(60, 10, 0, 0);
        }

        void BuildVs()
        {
            var center = new Vector2(0.5f, 0.5f);
            _vs = UIKit.Rect("vs", Root).Stretch();
            int mine = App.Meta.State.trophies;
            var p = App.Profile;
            _myCard = PlayerCard(_vs, p.playerName, mine, p.deck, Art.Hex(0x3D8BFF), true);
            _myCard.Place(center, center, new Vector2(0, 360), new Vector2(980, 420));
            _oppCard = PlayerCard(_vs, _opponent, _oppTrophies, _oppDeck, Art.Hex(0xE5484D), false);
            _oppCard.Place(center, center, new Vector2(0, -300), new Vector2(980, 420));

            _vsBadge = UIKit.Rect("vsBadge", _vs).Place(center, center, new Vector2(0, 30), new Vector2(300, 300));
            var burst = UIKit.Icon(_vsBadge, "rays", Art.Rays, new Color(1f, 0.85f, 0.3f, 0.5f)).rectTransform;
            burst.Place(center, center, Vector2.zero, new Vector2(620, 620));
            UIKit.Motion(burst).Spin(30f);
            var vsArt = Art.Icon("vs_badge");
            if (vsArt != null) UIKit.Icon(_vsBadge, "art", vsArt, Color.white).rectTransform.Stretch();
            else
            {
                var disc = UIKit.Icon(_vsBadge, "disc", Art.Circle, Art.Hex(0xFFC21A)).rectTransform;
                disc.Stretch(40, 40, 40, 40);
                UIKit.Icon(disc, "ring", Art.Ring, Art.Hex(0x7A3A10)).rectTransform.Stretch(-6, -6, -6, -6);
                Deco.Title(disc, "VS", 120, Color.white).rectTransform.Stretch(0, 0, 0, 10);
            }

            _countBg = UIKit.Icon(_vs, "count", Art.Circle, new Color(0f, 0f, 0f, 0.6f)).rectTransform;
            _countBg.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 130), new Vector2(170, 170));
            _countText = Deco.Title(_countBg, "", 110, Palette.Primary);
            _countText.rectTransform.Stretch();
            _countBg.gameObject.SetActive(false);
            _vs.gameObject.SetActive(false);
        }

        /// <summary>Player card for the VS screen: coloured frame, lead animal, name, trophies, 5 team portraits.</summary>
        RectTransform PlayerCard(RectTransform parent, string name, int trophies, string[] deck, Color color, bool left)
        {
            var holder = UIKit.Rect(left ? "me" : "opp", parent);
            var c = Deco.Frame(holder, "frame", color, Color.Lerp(color, Art.Hex(0x1B1230), 0.78f), 9f);
            var lead = App.Content.Unit(deck[0]);
            var anchor = new Vector2(left ? 0f : 1f, 0.5f);
            var glow = UIKit.Icon(c, "glow", Art.Glow, new Color(color.r, color.g, color.b, 0.7f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(anchor, new Vector2(0.5f, 0.5f), new Vector2(left ? 190 : -190, 30), new Vector2(380, 360));
            var hero = UIKit.Rect("hero", c).Place(anchor, new Vector2(0.5f, 0.5f), new Vector2(left ? 190 : -190, 40), new Vector2(330, 330));
            var heroArt = Icons.Unit(hero, lead, 1, 330).Stretch();
            if (!left) heroArt.localScale = new Vector3(-1f, 1f, 1f);   // face each other
            UIKit.Motion(hero).Bob(10f, 3.2f + (left ? 0f : 0.4f));

            var textAnchor = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            var nameLabel = UIKit.Outlined(UIKit.Label(c, name, 46, Palette.Text, textAnchor), 3f);
            nameLabel.rectTransform.Place(new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? 380 : -380, -30), new Vector2(560, 70));
            var tro = UIKit.Panel(c, "trophies", new Color(0f, 0f, 0f, 0.45f)).rectTransform
                .Place(new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? 380 : -380, -110), new Vector2(230, 60));
            Icons.Currency(tro, "trophy").rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6, 0), new Vector2(54, 54));
            UIKit.Label(tro, trophies.ToString(), 30, Palette.Text).rectTransform.Stretch(58, 8, 0, 0);
            // Title (achievements / seasons) next to the trophies on your own card.
            var title = left ? App.Meta.State.title : _oppTitle;
            if (!string.IsNullOrEmpty(title))
                UIKit.Label(c, Loc.T("title." + title), 26, Art.Hex(0xFFD23F), textAnchor).rectTransform
                    .Place(new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? 620 : -620, -110), new Vector2(330, 60));

            // Team row
            for (int i = 0; i < deck.Length && i < 5; i++)
            {
                var def = App.Content.Unit(deck[i]);
                if (def == null) continue;
                float x = left ? 380 + i * 112 : -380 - (deck.Length - 1 - i) * 112;
                var slot = UIKit.Icon(c, "team" + i, Art.Circle, new Color(0f, 0f, 0f, 0.4f)).rectTransform;
                slot.Place(new Vector2(left ? 0f : 1f, 0f), new Vector2(left ? 0f : 1f, 0f), new Vector2(x, 40), new Vector2(102, 102));
                UIKit.Icon(slot, "ring", Art.Ring, Art.RarityColor(def.rarity)).rectTransform.Stretch(-3, -3, -3, -3);
                Icons.Unit(slot, def, 1, 92).Stretch(6, 6, 4, 8);
            }
            return holder;
        }

        public override void Tick(float dt)
        {
            _t += dt;
            if (!_found)
            {
                int dots = 1 + (int)(_t * 3f) % 3;
                _status.text = (_fixedOpponent != null ? Loc.F("mm.rematch", _fixedOpponent) : Loc.T("mm.finding")) + new string('.', dots);
                int s = (int)_t;
                _timer.text = (s / 60) + ":" + (s % 60).ToString("00");
                _sweep.localRotation = Quaternion.Euler(0f, 0f, -_t * 200f);
                for (int i = 0; i < _rings.Count; i++)
                {
                    float k = ((_t * 0.6f) + i / 3f) % 1f;
                    float sc = 0.6f + k * 1.6f;
                    _rings[i].localScale = new Vector3(sc, sc, 1f);
                    _rings[i].GetComponent<Image>().color = new Color(0.5f, 0.85f, 1f, 0.6f * (1f - k));
                }
                float b = Mathf.Abs(Mathf.Sin(_t * 4f));
                _hero.anchoredPosition = new Vector2(0f, b * 12f);
                if (_online)
                {
                    var pvp = Online.Pvp;
                    pvp.Update(dt);
                    _onlineTime += dt;
                    if (pvp.State == OnlineState.Ready && pvp.Match != null) FoundOnline(pvp.Match);
                    // A half-finished handshake gets 2 s more at most, then the bot takes over.
                    else if (_onlineTime >= OnlineWait && (pvp.State != OnlineState.Handshake || _onlineTime >= OnlineWait + 2f))
                    {
                        // Nobody of this arena showed up in time: play a bot instead.
                        Debug.Log("[Online] no opponent after " + OnlineWait + " s: bot match");
                        pvp.Leave();
                        _online = false;
                        _searchTime = _t + 0.4f;
                    }
                    else if (pvp.State == OnlineState.Failed || pvp.State == OnlineState.Idle)
                    {
                        // A failed attempt (network hiccup, service error) does not end the search early:
                        // try again after a short pause until the full wait is over.
                        if (_retryAt < 0f) _retryAt = _onlineTime + 1f;
                        else if (_onlineTime >= _retryAt && _onlineTime < OnlineWait - 1.5f)
                        {
                            _retryAt = -1f;
                            pvp.StartSearch(Online.MyHello(App), OnlineWait - _onlineTime);
                        }
                    }
                    else if (pvp.State == OnlineState.Handshake) _status.text = Loc.T("online.connecting");
                    return;
                }
                if (_t > _searchTime) Found();
                return;
            }
            if (_online && Online.Pvp != null) Online.Pvp.Update(dt);   // keep the connection alive during the countdown

            // VS act: cards slide in, VS bursts, then 3-2-1.
            float slide = Deco.EaseOutBack(Mathf.Clamp01(_t / 0.45f));
            _myCard.anchoredPosition = new Vector2(Mathf.Lerp(-1200f, 0f, slide), 360f);
            _oppCard.anchoredPosition = new Vector2(Mathf.Lerp(1200f, 0f, Deco.EaseOutBack(Mathf.Clamp01((_t - 0.12f) / 0.45f))), -300f);
            float vs = _t < 0.45f ? 0f : Deco.EaseOutBack(Mathf.Clamp01((_t - 0.45f) / 0.3f));
            float punch = 1f + 0.08f * Mathf.Sin(Mathf.Max(0f, _t - 0.75f) * 8f) * Mathf.Exp(-Mathf.Max(0f, _t - 0.75f) * 2f);
            _vsBadge.localScale = new Vector3(vs * punch, vs * punch, 1f);

            int count = 3 - Mathf.FloorToInt((_t - 1.0f) / 0.6f);
            if (_t >= 1.0f && count != _count && count > 0)
            {
                _count = count;
                _countBg.gameObject.SetActive(true);
                _countText.text = count.ToString();
                UIKit.Motion(_countBg).PopIn(0f, 0.25f);
                AudioService.Play(Sfx.Click);
            }
            if (_t >= 1.0f + 3 * 0.6f)
            {
                if (_onlineMatch != null)
                {
                    _handedOff = true;
                    App.Show(new BattleScreen(new BattleConfig { seed = _onlineMatch.setup.seed, opponentName = _opponent, ranked = !_onlineMatch.friendly, friendly = _onlineMatch.friendly, opponentMmr = _onlineMatch.opponentMmr, online = _onlineMatch }));
                }
                else if (_ranked)
                {
                    // Ranked bot: strength and card levels follow the hidden rating, so new players meet beatable bots.
                    int mmr = App.Meta.State.mmr;
                    App.Show(new BattleScreen(_seed, _opponent, MatchmakingRules.BotDifficulty(mmr), true, false, _oppDeck, _revenge)
                        .WithRating(MatchmakingRules.BotMmr(mmr, _seed), MatchmakingRules.BotLevelOffset(mmr)));
                }
                else App.Show(new BattleScreen(_seed, _opponent, App.Profile.botDifficulty, _ranked, false, _oppDeck, _revenge));
            }
        }

        void FoundOnline(OnlineMatchInfo info)
        {
            _onlineMatch = info;
            _opponent = info.opponentName;
            _oppDeck = info.opponentDeck;
            _oppTrophies = info.opponentTrophies;
            _oppTitle = info.opponentTitle;
            // Rebuild the VS cards with the real opponent.
            Object.Destroy(_vs.gameObject);
            BuildVs();
            Found();
        }

        public override void Close()
        {
            // Left the screen without starting the online match (cancel, back): stop searching.
            if (_online && !_handedOff && Online.Pvp != null) Online.Pvp.Leave();
        }

        void Found()
        {
            _found = true;
            _t = 0f;
            _search.gameObject.SetActive(false);
            _cancel.gameObject.SetActive(false);
            _vs.gameObject.SetActive(true);
            _vsBadge.localScale = Vector3.zero;
            AudioService.Play(Sfx.Wave);
        }
    }
}
