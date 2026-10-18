using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>Icons with code-drawn fallbacks so every screen works before art exists.</summary>
    public static class Icons
    {
        public static Image Currency(Transform parent, string kind)
        {
            var art = Art.Icon(kind);
            if (art != null) return UIKit.Icon(parent, kind, art, Color.white);
            switch (kind)
            {
                case "gems": return UIKit.Icon(parent, kind, Art.Diamond, Art.Hex(0x5BD6FF));
                case "trophy": return UIKit.Icon(parent, kind, Art.Diamond, Art.Hex(0xFFB020));
                case "candy": return UIKit.Icon(parent, kind, Art.Circle, Palette.Candy);
                case "heart": return UIKit.Icon(parent, kind, Art.Circle, Art.Hex(0xFF4D6D));
                case "xp": return UIKit.Icon(parent, kind, Art.Diamond, Art.Hex(0x9B5CFF));
                default: return UIKit.Icon(parent, kind, Art.Circle, Art.Hex(0xFFD23F));   // gold
            }
        }

        public static Color ChestColor(string id)
        {
            switch (id)
            {
                case "silver": return Art.Hex(0xB8C4D6);
                case "gold": return Art.Hex(0xFFC93C);
                case "magic": return Art.Hex(0xB46BFF);
                default: return Art.Hex(0xB07A4A);
            }
        }

        /// <summary>Chest art if present (ZooArt/UI/chest_{id}), otherwise a simple drawn chest.</summary>
        public static RectTransform Chest(Transform parent, string id)
        {
            var art = Art.Icon("chest_" + id);
            if (art != null) return UIKit.Icon(parent, "chest", art, Color.white).rectTransform;
            var body = UIKit.Panel(parent, "chest", ChestColor(id));
            var lid = UIKit.Panel(body.transform, "lid", Color.Lerp(ChestColor(id), Color.black, 0.25f));
            lid.rectTransform.anchorMin = new Vector2(0f, 0.55f); lid.rectTransform.anchorMax = new Vector2(1f, 1f);
            lid.rectTransform.offsetMin = Vector2.zero; lid.rectTransform.offsetMax = Vector2.zero;
            var lockImg = UIKit.Icon(body.transform, "lock", Art.Circle, Art.Hex(0x3B2412));
            lockImg.rectTransform.anchorMin = lockImg.rectTransform.anchorMax = new Vector2(0.5f, 0.55f);
            lockImg.rectTransform.sizeDelta = new Vector2(26, 26);
            return body.rectTransform;
        }

        /// <summary>Unit portrait: real art if present, else an element-coloured disc with initials.</summary>
        public static RectTransform Unit(Transform parent, UnitDef def, int level, float size)
        {
            var art = Art.Unit(def.id, Math.Max(1, level));
            if (art != null) return UIKit.Icon(parent, "icon", art, Color.white).rectTransform;
            var disc = UIKit.Icon(parent, "icon", Art.Circle, Art.ElementColor(def.element));
            var ring = UIKit.Icon(disc.transform, "ring", Art.Ring, Art.RarityColor(def.rarity));
            ring.rectTransform.Stretch(-4, -4, -4, -4);
            UIKit.Label(disc.transform, Art.Initials(def.displayName), (int)(size * 0.36f), Art.Hex(0x3B2412)).rectTransform.Stretch();
            return disc.rectTransform;
        }

        /// <summary>Icon for a reward of any type.</summary>
        public static RectTransform Reward(Transform parent, Reward r, ContentDatabase content, float size)
        {
            switch (r.type)
            {
                case RewardType.Gems: return Currency(parent, "gems").rectTransform;
                case RewardType.PassXp: return Currency(parent, "xp").rectTransform;
                case RewardType.Chest: return Chest(parent, r.id);
                case RewardType.Shards:
                    var def = content.Unit(r.id);
                    if (def != null) return Unit(parent, def, 1, size);
                    break;
            }
            return Currency(parent, "gold").rectTransform;
        }
    }

    public static class Fmt
    {
        public static string Duration(long seconds)
        {
            if (seconds < 0) seconds = 0;
            long d = seconds / 86400, h = seconds % 86400 / 3600, m = seconds % 3600 / 60, s = seconds % 60;
            if (d > 0) return Loc.F("time.dh", d, h);
            if (h > 0) return Loc.F("time.hm", h, m);
            return m + ":" + s.ToString("00");
        }

        public static string Reward(Reward r, ContentDatabase content)
        {
            switch (r.type)
            {
                case RewardType.Gold: return Loc.F("reward.gold", r.amount);
                case RewardType.Gems: return Loc.F("reward.gems", r.amount);
                case RewardType.PassXp: return Loc.F("reward.xp", r.amount);
                case RewardType.Chest: return Loc.T("chest." + r.id);
                case RewardType.Shards:
                    var def = content.Unit(r.id);
                    return Loc.F("reward.shards", r.amount, def != null ? Loc.UnitName(def) : r.id);
            }
            return "";
        }

        public static string Mission(MissionDef d)
        {
            return Loc.F("mission." + d.type.ToString().ToLowerInvariant(), d.target);
        }
    }

    /// <summary>Top bar with trophies, gold and gems. Call Refresh after spending or earning.</summary>
    public sealed class CurrencyBar
    {
        public const float Height = 130f;
        readonly GameApp _app;
        readonly Text _trophies, _gold, _gems;

        public CurrencyBar(RectTransform root, GameApp app, bool showName)
        {
            _app = app;
            var bar = UIKit.Panel(root, "currencyBar", new Color(0f, 0f, 0f, 0.35f), false).rectTransform.Band(true, Height);
            if (showName)
            {
                var title = app.Meta != null ? app.Meta.State.title : "";
                var name = UIKit.Label(bar, app.Profile.playerName, 38, Palette.Text, TextAnchor.MiddleLeft);
                name.rectTransform.Stretch(30, 760, 0, string.IsNullOrEmpty(title) ? 0 : 40);
                if (!string.IsNullOrEmpty(title))
                    UIKit.Label(bar, Loc.T("title." + title), 24, Art.Hex(0xFFD23F), TextAnchor.MiddleLeft).rectTransform.Stretch(30, 760, 78, 8);
            }
            _gems = Pair(bar, "gems", 24);
            _gold = Pair(bar, "gold", 24 + 250);
            _trophies = Pair(bar, "trophy", 24 + 500);
            Refresh();
        }

        static Text Pair(RectTransform bar, string icon, float rightOffset)
        {
            var bg = UIKit.Panel(bar, icon + "_bg", new Color(0f, 0f, 0f, 0.35f));
            bg.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-rightOffset, 0), new Vector2(226, 76));
            var img = Icons.Currency(bg.transform, icon);
            img.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(8, 0), new Vector2(76, 76));
            var t = UIKit.Label(bg.transform, "", 36, Palette.Text, TextAnchor.MiddleRight);
            t.rectTransform.Stretch(50, 16, 0, 0);
            return t;
        }

        public void Refresh()
        {
            var s = _app.Profile.meta;
            _trophies.text = s.trophies.ToString();
            _gold.text = s.gold.ToString();
            _gems.text = s.gems.ToString();
        }
    }

    public enum Tab { Shop = 0, Deck = 1, Battle = 2, Pass = 3, Settings = 4 }

    /// <summary>
    /// Bottom navigation shared by the main screens: dark wood bar with a gold rim, one icon + label per tab.
    /// The open tab is a raised gold tab with a bigger, bobbing icon. Red "!" badges mark things to collect.
    /// Optional icon art: ZooArt/UI/nav_{shop|deck|battle|pass|settings}.png.
    /// </summary>
    public static class NavBar
    {
        public const float Height = 170f;
        static readonly string[] Keys = { "nav.shop", "nav.deck", "nav.battle", "nav.pass", "nav.settings" };
        static readonly string[] IconNames = { "nav_shop", "nav_deck", "nav_battle", "nav_pass", "nav_settings" };

        public static void Build(RectTransform root, GameApp app, Tab active)
        {
            var bar = UIKit.Panel(root, "navBar", Art.Hex(0x22183A), false).rectTransform.Band(false, Height);
            bar.GetComponent<Image>().raycastTarget = true;
            var sheen = UIKit.Icon(bar, "sheen", Art.GradientV, new Color(0.55f, 0.4f, 0.85f, 0.35f));
            sheen.preserveAspect = false;
            sheen.rectTransform.Stretch();
            sheen.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            UIKit.Panel(bar, "rimShadow", new Color(0f, 0f, 0f, 0.45f), false).rectTransform.Band(true, 10, -4);
            UIKit.Panel(bar, "rim", Art.Hex(0xFFC93C), false).rectTransform.Band(true, 6, -6);

            long now = app.Now;
            for (int i = 0; i < Keys.Length; i++)
            {
                var tab = (Tab)i;
                bool on = tab == active;
                var cell = UIKit.Rect("tab_" + i, bar);
                cell.anchorMin = new Vector2(i / 5f, 0f);
                cell.anchorMax = new Vector2((i + 1) / 5f, 1f);
                cell.offsetMin = new Vector2(5, 8);
                cell.offsetMax = new Vector2(-5, on ? 30 : -12);
                if (on)
                {
                    var face = Deco.Frame(cell, "face", Art.Hex(0xFFD45A), Art.Hex(0xF29A1B), 6f);
                    var glow = UIKit.Icon(face, "glow", Art.Glow, new Color(1f, 0.95f, 0.6f, 0.6f));
                    glow.preserveAspect = false;
                    glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 10), new Vector2(190, 150));
                }
                else
                {
                    UIKit.Panel(cell, "face", new Color(1f, 1f, 1f, 0.06f)).rectTransform.Stretch();
                }
                var iconHolder = UIKit.Rect("icon", cell).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, on ? -4 : -10), on ? new Vector2(108, 108) : new Vector2(80, 80));
                Icon(iconHolder, app, i);
                if (on) UIKit.Motion(iconHolder).Bob(6f, 3f);
                else foreach (var g in iconHolder.GetComponentsInChildren<Graphic>()) g.color = new Color(g.color.r, g.color.g, g.color.b, g.color.a * 0.8f);
                var label = UIKit.Label(cell, Loc.T(Keys[i]), on ? 28 : 23, on ? Palette.Text : Palette.TextDim);
                label.rectTransform.Band(false, 36, on ? 10 : 6, 2);
                if (on) UIKit.Outlined(label, 2f);

                var btn = UIKit.Button(cell, "hit", "", new Color(0f, 0f, 0f, 0f), () => Open(app, tab));
                btn.GetComponent<RectTransform>().Stretch();
                if (HasBadge(app, tab, now))
                {
                    var dot = UIKit.Icon(cell, "badge", Art.Circle, Palette.Red);
                    dot.rectTransform.Place(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-20, -10), new Vector2(40, 40));
                    UIKit.Icon(dot.transform, "ring", Art.Ring, Color.white).rectTransform.Stretch(-2, -2, -2, -2);
                    UIKit.Label(dot.transform, "!", 26, Palette.Text).rectTransform.Stretch();
                    UIKit.Motion(dot.rectTransform).Pulse(0.12f, 6f);
                }
            }
        }

        /// <summary>Tab icon: art nav_* if present, else the closest existing art, else a drawn gear.</summary>
        static void Icon(RectTransform holder, GameApp app, int tab)
        {
            var art = Art.Icon(IconNames[tab]);
            if (art == null)
            {
                switch ((Tab)tab)
                {
                    case Tab.Shop: art = Art.Icon("chest_gold"); break;
                    case Tab.Deck:
                        var deck = app.Profile.deck;
                        art = deck.Length > 0 ? Art.Unit(deck[0], 1) : null;
                        break;
                    case Tab.Battle: art = Art.Icon("trophy"); break;
                    case Tab.Pass: art = Art.Icon("xp"); break;
                }
            }
            if (art != null) { UIKit.Icon(holder, "art", art, Color.white).rectTransform.Stretch(); return; }
            Gear(holder);
        }

        /// <summary>Simple cog drawn from shapes (settings tab without art).</summary>
        public static void Gear(RectTransform holder)
        {
            var col = Art.Hex(0xC9D3E6);
            for (int k = 0; k < 8; k++)
            {
                var tooth = UIKit.Panel(holder, "tooth", col).rectTransform;
                tooth.anchorMin = tooth.anchorMax = new Vector2(0.5f, 0.5f);
                tooth.sizeDelta = new Vector2(22, 70);
                tooth.anchoredPosition = Vector2.zero;
                tooth.localRotation = Quaternion.Euler(0f, 0f, k * 22.5f);
            }
            var body = UIKit.Icon(holder, "body", Art.Circle, col).rectTransform;
            body.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            UIKit.Icon(body, "hole", Art.Circle, Art.Hex(0x3A2D57)).rectTransform.Stretch(18, 18, 18, 18);
        }

        static bool HasBadge(GameApp app, Tab tab, long now)
        {
            var m = app.Meta;
            switch (tab)
            {
                case Tab.Shop: return m.CanClaimFreeGold(now);
                case Tab.Deck:
                    foreach (var id in app.Profile.deck) if (m.CanUpgrade(id)) return true;
                    return false;
                case Tab.Battle:
                    for (int i = 0; i < m.State.chests.Length; i++) if (m.Status(i, now) == ChestStatus.Ready) return true;
                    return m.ClaimableMissions() > 0;
                case Tab.Pass: return m.ClaimablePass() > 0;
            }
            return false;
        }

        public static void Open(GameApp app, Tab tab)
        {
            switch (tab)
            {
                case Tab.Shop: app.Show(new ShopScreen()); break;
                case Tab.Deck: app.Show(new DeckScreen()); break;
                case Tab.Pass: app.Show(new PassScreen()); break;
                case Tab.Settings: app.Show(new SettingsScreen()); break;
                default: app.Show(new LobbyScreen()); break;
            }
        }
    }

    /// <summary>
    /// Modal dialogs on the overlay layer: dimmed backdrop, soft rays, a framed panel that pops in with a ribbon
    /// title across its top edge and a round close button on the corner.
    /// </summary>
    public static class Modal
    {
        static readonly List<GameObject> _open = new List<GameObject>();

        /// <summary>Opens a dialog box and returns its content rect (960 wide); content starts ~120 px below the top.</summary>
        public static RectTransform Open(GameApp app, string title, float height, Action onClose = null)
        {
            var dim = UIKit.Panel(app.OverlayLayer, "modal", new Color(0.04f, 0.02f, 0.09f, 0.78f), false);
            dim.rectTransform.Stretch();
            dim.raycastTarget = true;
            _open.Add(dim.gameObject);

            var rays = UIKit.Icon(dim.transform, "rays", Art.Rays, new Color(1f, 0.85f, 0.5f, 0.12f)).rectTransform;
            rays.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 1500));
            UIKit.Motion(rays).Spin(8f);

            var holder = UIKit.Rect("holder", dim.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, height));
            UIKit.Motion(holder).PopIn(0f, 0.28f);
            var box = Deco.Frame(holder, "box", Art.Hex(0xFFC93C), Art.Hex(0x2E2150), 8f);
            ((RectTransform)box.parent).GetComponent<Image>().raycastTarget = true;
            // Soft inner light at the top
            var light = UIKit.Icon(box, "light", Art.Glow, new Color(0.75f, 0.55f, 1f, 0.25f));
            light.preserveAspect = false;
            light.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 360));

            Deco.Ribbon(holder, title, Art.Hex(0x8A4FE0), 44)
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(Mathf.Min(780f, 300f + title.Length * 26f), 110));

            var close = UIKit.ChunkyButton(holder, "close", "×", Palette.Red, () => { Close(dim.gameObject); if (onClose != null) onClose(); }, 52);
            close.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-18, -18), new Vector2(92, 98));
            AudioService.Play(Sfx.Click, 0.6f);
            return box;
        }

        /// <summary>Registers an overlay built elsewhere (e.g. the reward reveal) so CloseAll/CloseFrom work on it.</summary>
        public static void Register(GameObject overlay) { _open.Add(overlay); }

        public static void Close(GameObject modal)
        {
            _open.Remove(modal);
            if (modal != null) UnityEngine.Object.Destroy(modal);
        }

        /// <summary>Closes the modal that contains the given element.</summary>
        public static void CloseFrom(Transform child)
        {
            var t = child;
            while (t != null && !_open.Contains(t.gameObject)) t = t.parent;
            if (t != null) Close(t.gameObject);
        }

        public static void CloseAll()
        {
            foreach (var m in _open) if (m != null) UnityEngine.Object.Destroy(m);
            _open.Clear();
        }

        /// <summary>Question with one big confirm button (the price / action) under it.</summary>
        public static void Confirm(GameApp app, string title, string message, string ok, Action onOk)
        {
            var box = Open(app, title, 560);
            var msg = UIKit.Label(box, message, 40, Palette.Text);
            msg.rectTransform.Stretch(60, 60, 120, 200);
            var yes = UIKit.ChunkyButton(box, "ok", ok, Palette.Green, null, 44);
            UIKit.Outlined(yes.GetComponentInChildren<Text>(), 2.5f);
            yes.onClick.AddListener(() => { CloseFrom(yes.transform); onOk(); });
            var rt = yes.GetComponent<RectTransform>();
            rt.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(560, 130));
            UIKit.Motion(rt).Pulse(0.025f, 4f);
        }

        /// <summary>
        /// Shows what the player received with the animated reveal (cards flip one by one).
        /// dramatic: gacha / chest opening intro (shake, flash, burst). newlyUnlocked: unit ids that were locked before.
        /// </summary>
        public static void Rewards(GameApp app, string title, List<Reward> rewards, ICollection<string> newlyUnlocked = null, bool dramatic = false, string introChest = null)
        {
            if (rewards == null || rewards.Count == 0) return;
            RewardReveal.Show(app, title, rewards, newlyUnlocked, dramatic, introChest);
        }

        /// <summary>Locked unit ids right now (call before an action, pass to Rewards to tag new unlocks).</summary>
        public static HashSet<string> LockedUnits(GameApp app)
        {
            var set = new HashSet<string>();
            foreach (var c in app.Profile.meta.cards) if (c.level == 0) set.Add(c.id);
            return set;
        }
    }

    public static class ScrollKit
    {
        /// <summary>Vertical scroll area filling `area`. Set the returned content's height with SetHeight.</summary>
        public static RectTransform Vertical(RectTransform area)
        {
            var view = UIKit.Rect("viewport", area).Stretch();
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            view.gameObject.AddComponent<RectMask2D>();
            var content = UIKit.Rect("content", view);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 100f);
            var sr = area.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 40f;
            return content;
        }

        public static void SetHeight(RectTransform content, float height) { content.sizeDelta = new Vector2(0f, height); }
    }

    /// <summary>A horizontal progress bar.</summary>
    public sealed class Bar
    {
        readonly RectTransform _fill;
        readonly Text _label;

        public Bar(Transform parent, Color color, int fontSize = 28)
        {
            var bg = UIKit.Panel(parent, "bar", new Color(0f, 0f, 0f, 0.45f));
            Root = bg.rectTransform;
            var fill = UIKit.Panel(bg.transform, "fill", color);
            _fill = fill.rectTransform;
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.offsetMin = new Vector2(4, 4);
            _fill.offsetMax = new Vector2(-4, -4);
            _label = UIKit.Label(bg.transform, "", fontSize, Palette.Text);
            _label.rectTransform.Stretch();
        }

        public RectTransform Root { get; private set; }

        public void Set(float fraction, string text)
        {
            _fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            _label.text = text;
        }
    }
}
