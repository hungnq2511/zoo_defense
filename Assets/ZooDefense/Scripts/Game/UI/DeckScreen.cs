using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// Deck tab: the 5 battle animals and 4 send monsters on top, the whole collection below.
    /// Tap an animal for stats, upgrades and "use"; "use" then asks which deck slot to replace.
    /// </summary>
    public sealed class DeckScreen : ScreenBase
    {
        static float _scroll = 1f;   // keeps the collection position between rebuilds

        string _swapIn;              // unit waiting for a deck slot (replace mode)
        ScrollRect _scrollRect;

        public DeckScreen(string swapIn = null) { _swapIn = swapIn; }

        static readonly Color CardFill = Art.Hex(0x261C3C);
        readonly List<RectTransform> _upgradeBadges = new List<RectTransform>();
        float _t;

        protected override void Build()
        {
            var content = App.Content;
            var meta = App.Meta;
            var profile = App.Profile;

            Deco.ScreenBackground(Root, App, Art.Hex(0x3D8BFF));
            new CurrencyBar(Root, App, false);
            float top = Deco.Header(Root, Loc.T("nav.deck"), Art.Hex(0x3D8BFF));
            var guide = UIKit.ChunkyButton(Root, "guide", Loc.T("guide.button"), Art.Hex(0x2E7D4A), () => ElementGuide.Open(App), 26);
            guide.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14, -CurrencyBar.Height - 22), new Vector2(170, 84));
            if (!ElementGuide.Seen) UIKit.Motion(guide.GetComponent<RectTransform>()).Pulse(0.05f, 4f);

            var area = UIKit.Rect("deckArea", Root).Stretch(20, 20, top, NavBar.Height + 6);
            var list = ScrollKit.Vertical(area);
            _scrollRect = area.GetComponent<ScrollRect>();
            float y = 6;

            // Battle deck
            Deco.Section(list, Loc.T("deck.title"), y, Palette.Primary);
            y += 74;
            // 3 saved decks
            profile.decks[profile.activeDeck].units = (string[])profile.deck.Clone();
            for (int d = 0; d < 3; d++)
            {
                int slot = d;
                var tab = UIKit.ChunkyButton(list, "deckTab" + d, Loc.F("deck.preset", d + 1), d == profile.activeDeck ? Palette.Primary : Palette.Panel, () => SwitchDeck(slot), 30);
                tab.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6 + d * 346, -y), new Vector2(330, 84));
            }
            y += 100;
            for (int i = 0; i < profile.deck.Length; i++)
            {
                var def = content.Unit(profile.deck[i]);
                int slot = i;
                var card = UnitCard(list, def, new Vector2(6 + i * 206, -y), new Vector2(198, 290), () => OnDeckSlot(slot));
                if (_swapIn != null)
                {
                    var glow = UIKit.Panel(card.transform, "swap", new Color(0.3f, 1f, 0.4f, 0.35f)).rectTransform;
                    glow.Stretch();
                    _upgradeBadges.Add(glow);
                }
            }
            y += 306;

            // Send monsters
            Deco.Section(list, Loc.T("deck.sends_title"), y, Art.Hex(0xC9A5FF));
            y += 74;
            for (int i = 0; i < profile.sendDeck.Length; i++)
            {
                int slot = i;
                SendCard(list, content.Send(profile.sendDeck[i]), new Vector2(6 + i * 258, -y), () => SendPopup(slot));
            }
            y += 176;

            // Keeper spells
            Deco.Section(list, Loc.T("deck.spells_title"), y, Art.Hex(0x7FD3FF));
            y += 74;
            for (int i = 0; i < profile.spells.Length; i++)
            {
                int slot = i;
                SpellCard(list, content.Spell(profile.spells[i]), new Vector2(6 + i * 258, -y), () => SpellPopup(slot));
            }
            y += 176;

            // Collection: unlocked first (by rarity then level), locked after.
            Deco.Section(list, Loc.F("deck.collection", CountUnlocked(), content.Units.Count), y, Palette.TextDim);
            y += 74;
            var units = new List<UnitDef>(content.Units);
            units.Sort((a, b) =>
            {
                bool ua = meta.IsUnlocked(a.id), ub = meta.IsUnlocked(b.id);
                if (ua != ub) return ua ? -1 : 1;
                if (a.rarity != b.rarity) return b.rarity.CompareTo(a.rarity);
                return meta.Level(b.id).CompareTo(meta.Level(a.id));
            });
            int cols = 4, shown = 0;
            foreach (var def in units)
            {
                if (System.Array.IndexOf(profile.deck, def.id) >= 0) continue;
                var d = def;
                UnitCard(list, d, new Vector2(6 + (shown % cols) * 258, -(y + (shown / cols) * 330)), new Vector2(246, 318), () => UnitPopup(d.id));
                shown++;
            }
            y += ((shown + cols - 1) / cols) * 330 + 20;
            ScrollKit.SetHeight(list, y);
            _scrollRect.verticalNormalizedPosition = _swapIn != null ? 1f : _scroll;

            // Swap mode banner (fixed): "Tap a deck slot for X" + cancel
            if (_swapIn != null)
            {
                var banner = UIKit.Panel(Root, "swapBanner", new Color(0.18f, 0.55f, 0.28f, 0.96f)).rectTransform
                    .Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, NavBar.Height + 14), new Vector2(1040, 120));
                Icons.Unit(banner, content.Unit(_swapIn), 1, 100).Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12, 0), new Vector2(100, 100));
                UIKit.Label(banner, Loc.F("deck.choose_slot", Loc.UnitName(content.Unit(_swapIn))), 32, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(130, 230, 0, 0);
                var cancel = UIKit.ChunkyButton(banner, "cancelSwap", Loc.T("common.cancel"), Palette.Red, () => App.Show(new DeckScreen()), 28);
                cancel.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12, 0), new Vector2(200, 92));
            }

            NavBar.Build(Root, App, Tab.Deck);
        }

        public override void Tick(float dt)
        {
            _t += dt;
            float s = 1f + 0.12f * Mathf.Abs(Mathf.Sin(_t * 4f));
            foreach (var b in _upgradeBadges)
            {
                if (b == null) continue;
                if (b.name == "swap") { var img = b.GetComponent<Image>(); img.color = new Color(0.3f, 1f, 0.4f, 0.2f + 0.2f * Mathf.Abs(Mathf.Sin(_t * 4f))); }
                else b.localScale = new Vector3(s, s, 1f);
            }
        }

        public override void Close() { if (_scrollRect != null) _scroll = _scrollRect.verticalNormalizedPosition; }

        int CountUnlocked()
        {
            int n = 0;
            foreach (var c in App.Meta.State.cards) if (c.level > 0) n++;
            return n;
        }

        void Rebuild(string swapIn = null) { App.Show(new DeckScreen(swapIn)); }

        // ------------------------------------------------------------------ widgets

        /// <summary>Animal card: rarity-coloured frame, element dot, big art, level ribbon, name and shard bar.</summary>
        Button UnitCard(RectTransform parent, UnitDef def, Vector2 pos, Vector2 size, System.Action onTap)
        {
            var meta = App.Meta;
            bool unlocked = meta.IsUnlocked(def.id);
            int level = meta.Level(def.id);
            var rarity = Art.RarityColor(def.rarity);

            var btn = UIKit.Button(parent, def.id, "", new Color(0f, 0f, 0f, 0f), onTap);
            var rt = btn.GetComponent<RectTransform>();
            rt.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);
            var c = Deco.Frame(btn.transform, "frame", unlocked ? rarity : Art.Hex(0x4A4060), CardFill);

            // Element dot + rarity text strip
            var dot = UIKit.Icon(c, "element", Art.Circle, Art.ElementColor(def.element)).rectTransform;
            dot.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6, -6), new Vector2(34, 34));
            UIKit.Icon(dot, "ring", Art.Ring, new Color(0f, 0f, 0f, 0.5f)).rectTransform.Stretch(-2, -2, -2, -2);

            float art = size.x * 0.84f;
            var glow = UIKit.Icon(c, "glow", Art.Glow, new Color(rarity.r, rarity.g, rarity.b, unlocked ? 0.45f : 0.1f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(art, art));
            int look = level >= 10 ? 7 : (level >= 5 ? 4 : 1);
            var icon = Icons.Unit(c, def, look, art);
            icon.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(art, art));

            UIKit.Label(c, Loc.UnitName(def), size.x < 220 ? 22 : 24, unlocked ? Palette.Text : Palette.TextDim).rectTransform.Band(false, 34, 46, 2);

            if (!unlocked)
            {
                foreach (var g in icon.GetComponentsInChildren<Graphic>()) g.color = new Color(0.12f, 0.1f, 0.18f, 0.85f);
                Deco.Lock(c, 60).anchoredPosition = new Vector2(0, 30);
                UIKit.Label(c, Loc.T("deck.locked"), 22, Palette.TextDim).rectTransform.Band(false, 36, 6, 4);
                return btn;
            }

            // Level ribbon over the bottom of the art
            var lv = UIKit.Panel(c, "lv", Color.Lerp(rarity, Color.black, 0.25f)).rectTransform;
            lv.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(110, 38));
            UIKit.Label(lv, Loc.F("deck.level_short", level), 24, Palette.Text).rectTransform.Stretch();

            int needShards, needGold;
            bool can = meta.CanUpgrade(def.id);
            var bar = new Bar(c, can ? Palette.Green : Art.Hex(0x7F6BFF), 20);
            bar.Root.Band(false, 36, 6, 6);
            if (meta.UpgradeCost(def.id, out needShards, out needGold))
            {
                int have = meta.Card(def.id).shards;
                bar.Set(have / (float)needShards, have + "/" + needShards);
                if (can)
                {
                    var up = UIKit.Icon(btn.transform, "upgrade", Art.Circle, Palette.Green).rectTransform;
                    up.Place(new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-14, -14), new Vector2(48, 48));
                    UIKit.Label(up, "↑", 34, Palette.Text).rectTransform.Stretch();
                    _upgradeBadges.Add(up);
                }
            }
            else bar.Set(1f, Loc.T("deck.max"));
            return btn;
        }

        void SendCard(RectTransform parent, SendDef def, Vector2 pos, System.Action onTap)
        {
            var btn = UIKit.Button(parent, def.id, "", new Color(0f, 0f, 0f, 0f), onTap);
            btn.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(248, 160));
            var c = Deco.Frame(btn.transform, "frame", Art.Hex(0x9B5CFF), CardFill);
            var art = Art.Enemy(def.enemyId);
            var icon = UIKit.Icon(c, "icon", art != null ? art : Art.Diamond, art != null ? Color.white : Palette.Candy);
            icon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0, 8), new Vector2(120, 120));
            UIKit.Label(c, Loc.SendName(def), 22, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(118, 4, 8, 60);
            var pill = Deco.Pill(c, null, def.cost + (def.oncePerMatch ? " · " + Loc.T("deck.once") : ""), 24);
            pill.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-4, 6), new Vector2(120, 44));
            pill.GetComponent<Image>().color = new Color(0.36f, 0.2f, 0.6f, 0.9f);
        }

        void SpellCard(RectTransform parent, SpellDef def, Vector2 pos, System.Action onTap)
        {
            var color = SpellColor(def.kind);
            var btn = UIKit.Button(parent, def.id, "", new Color(0f, 0f, 0f, 0f), onTap);
            btn.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(248, 160));
            var c = Deco.Frame(btn.transform, "frame", color, CardFill);
            var orb = UIKit.Icon(c, "orb", Art.Glow, Color.Lerp(color, Color.white, 0.4f)).rectTransform;
            orb.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-6, 6), new Vector2(120, 120));
            var spellArt = Art.Icon("spell_" + def.id);
            if (spellArt != null) UIKit.Icon(orb, "core", spellArt, Color.white).rectTransform.Stretch(16, 16, 16, 16);
            else UIKit.Icon(orb, "core", Art.Diamond, color).rectTransform.Stretch(34, 34, 34, 34);
            UIKit.Label(c, Loc.T("spell." + def.id), 24, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(104, 4, 10, 60);
            Deco.Pill(c, "candy", def.cost.ToString(), 26).Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-4, 6), new Vector2(120, 44));
        }

        static Color SpellColor(SpellKind k)
        {
            switch (k)
            {
                case SpellKind.Freeze: return Art.Hex(0x4FB6F0);
                case SpellKind.Meteor: return Art.Hex(0xF06A3D);
                case SpellKind.Gust: return Art.Hex(0x7CC48A);
                case SpellKind.Rage: return Art.Hex(0xE5484D);
                case SpellKind.CandyRain: return Art.Hex(0xFF7AC8);
                default: return Art.Hex(0xFFD23F);
            }
        }

        // ------------------------------------------------------------------ actions

        void OnDeckSlot(int slot)
        {
            var profile = App.Profile;
            if (_swapIn == null) { UnitPopup(profile.deck[slot]); return; }
            profile.deck[slot] = _swapIn;
            App.SaveProfile();
            App.Toast(Loc.T("deck.saved"));
            Rebuild();
        }

        void UnitPopup(string unitId)
        {
            var content = App.Content;
            var meta = App.Meta;
            var def = content.Unit(unitId);
            bool unlocked = meta.IsUnlocked(unitId);
            bool inDeck = System.Array.IndexOf(App.Profile.deck, unitId) >= 0;
            int level = Mathf.Max(1, meta.Level(unitId));
            var rarity = Art.RarityColor(def.rarity);

            var box = Modal.Open(App, Loc.UnitName(def), 1240);
            // Hero art on a rarity glow with the rarity / element / role chips
            var glow = UIKit.Icon(box, "glow", Art.Glow, new Color(rarity.r, rarity.g, rarity.b, 0.7f));
            glow.preserveAspect = false;
            glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(520, 420));
            int look = level >= 10 ? 7 : (level >= 5 ? 4 : 1);
            var icon = Icons.Unit(box, def, unlocked ? look : 1, 360);
            icon.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(360, 360));
            if (!unlocked) foreach (var g in icon.GetComponentsInChildren<Graphic>()) g.color = new Color(0.15f, 0.12f, 0.22f, 0.9f);

            var chips = UIKit.Rect("chips", box).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -490), new Vector2(860, 56));
            string[] chipText = { Loc.RarityName(def.rarity), Loc.ElementName(def.element), Loc.RoleName(def.role) };
            Color[] chipColor = { rarity, Art.ElementColor(def.element), Palette.Panel };
            for (int i = 0; i < 3; i++)
            {
                var chip = UIKit.Panel(chips, "chip" + i, Color.Lerp(chipColor[i], Color.black, 0.3f)).rectTransform;
                chip.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 290, 0), new Vector2(276, 56));
                UIKit.Label(chip, chipText[i], 28, Palette.Text).rectTransform.Stretch();
                if (i == 1)
                {
                    var tap = UIKit.Button(chip, "guide", "", new Color(0f, 0f, 0f, 0f), () => ElementGuide.Open(App));
                    tap.GetComponent<RectTransform>().Stretch();
                }
            }

            // Stats
            int pct = 100 + App.Content.Balance.cardLevelDamagePercent * (level - 1);
            var stats = UIKit.Panel(box, "stats", new Color(0f, 0f, 0f, 0.35f)).rectTransform;
            stats.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -560), new Vector2(860, 300));
            var head = unlocked ? Loc.F("deck.card_level", level, App.MetaConfig.maxCardLevel) : Loc.T("deck.locked_hint");
            UIKit.Label(stats, head, 32, Palette.Primary, TextAnchor.UpperLeft).rectTransform.Stretch(24, 24, 14, 240);
            UIKit.Label(stats, Describe(def, pct, level, App.Content.Balance), 26, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(24, 24, 64, 10);

            if (!unlocked) return;

            int needShards, needGold;
            if (meta.UpgradeCost(unitId, out needShards, out needGold))
            {
                int have = meta.Card(unitId).shards;
                var bar = new Bar(box, have >= needShards ? Palette.Green : Art.Hex(0x7F6BFF), 30);
                bar.Root.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 210), new Vector2(860, 60));
                bar.Set(have / (float)needShards, Loc.F("deck.shards", have, needShards));

                bool can = meta.CanUpgrade(unitId);
                var up = UIKit.ChunkyButton(box, "upgrade", Loc.F("deck.upgrade", needGold), can ? Palette.Green : Palette.Disabled, null, 34);
                up.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40, 50), new Vector2(430, 140));
                up.onClick.AddListener(() =>
                {
                    if (!meta.Upgrade(unitId))
                    {
                        AudioService.Play(Sfx.Error);
                        App.Toast(Loc.T(have < needShards ? "deck.need_shards" : "shop.not_enough_gold"));
                        return;
                    }
                    App.SaveProfile();
                    AudioService.Play(Sfx.Upgrade);
                    App.Toast(Loc.F("deck.upgraded", Loc.UnitName(def), meta.Level(unitId)));
                    Rebuild();
                    UnitPopup(unitId);
                });
            }
            else UIKit.Label(box, Loc.T("deck.max"), 44, Palette.Primary).rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40, 50), new Vector2(430, 140));

            var use = UIKit.ChunkyButton(box, "use", Loc.T(inDeck ? "deck.in_deck" : "deck.use"), inDeck ? Palette.Disabled : Art.Hex(0xFFB020), null, 38);
            use.interactable = !inDeck;
            use.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40, 50), new Vector2(430, 140));
            use.onClick.AddListener(() => Rebuild(unitId));
        }

        void SwitchDeck(int slot)
        {
            var p = App.Profile;
            if (slot == p.activeDeck) return;
            p.decks[p.activeDeck].units = (string[])p.deck.Clone();
            p.activeDeck = slot;
            p.deck = (string[])p.decks[slot].units.Clone();
            App.SaveProfile();
            App.Show(new DeckScreen());
        }

        static string Describe(UnitDef d, int pct, int level, BalanceConfig b)
        {
            int dmg = d.damage * pct / 100;
            string s = d.damage > 0 ? Loc.F("deck.stat_damage", dmg, (d.attackIntervalTicks / 20f).ToString("0.0")) : "";
            if (d.splashRadius > 0) s += Loc.F("deck.stat_splash", (d.splashRadius / 1000f).ToString("0.0"));
            if (d.slowPercent > 0) s += Loc.F("deck.stat_slow", d.slowPercent);
            if (d.stunTicks > 0) s += Loc.T("deck.stat_stun");
            if (d.skill != UnitSkill.None) s += (s.Length > 0 ? "\n" : "") + Loc.SkillText(d);
            s += "\n<color=#B6F25A>" + ElementGuide.MatchupLine(d, b) + "</color>";
            if (level > 1) s += "\n" + Loc.F("deck.level_bonus", pct - 100);
            return s;
        }

        void SpellPopup(int slot)
        {
            var content = App.Content;
            var profile = App.Profile;
            var current = content.Spell(profile.spells[slot]);
            var box = Modal.Open(App, Loc.T("spell." + current.id), 1300);
            UIKit.Label(box, Loc.T("spell." + current.id + ".desc"), 30, Palette.Text).rectTransform.Band(true, 90, 115, 40);
            UIKit.Label(box, Loc.T("deck.spell_swap"), 28, Palette.TextDim).rectTransform.Band(true, 50, 210, 30);
            int row = 0;
            foreach (var def in content.Spells)
            {
                if (System.Array.IndexOf(profile.spells, def.id) >= 0) continue;
                var d = def;
                var btn = UIKit.Button(box, d.id, "", Palette.PanelDark, null, 28);
                btn.GetComponent<RectTransform>().Band(true, 170, 270 + row * 185, 40);
                var t = btn.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.text = Loc.T("spell." + d.id) + "  (" + Loc.F("deck.spell_cost", d.cost) + ")\n" + Loc.T("spell." + d.id + ".desc");
                t.rectTransform.Stretch(24, 24, 8, 8);
                btn.onClick.AddListener(() =>
                {
                    profile.spells[slot] = d.id;
                    App.SaveProfile();
                    App.Toast(Loc.T("deck.saved"));
                    Rebuild();
                });
                row++;
            }
        }

        void SendPopup(int slot)
        {
            var content = App.Content;
            var profile = App.Profile;
            var current = content.Send(profile.sendDeck[slot]);
            var box = Modal.Open(App, Loc.SendName(current), 1100);
            UIKit.Label(box, Loc.T("deck.send_swap"), 32, Palette.TextDim).rectTransform.Band(true, 50, 115, 30);
            int row = 0;
            foreach (var def in content.Sends)
            {
                if (System.Array.IndexOf(profile.sendDeck, def.id) >= 0) continue;
                var d = def;
                var btn = UIKit.Button(box, d.id, "", Palette.PanelDark, null);
                btn.GetComponent<RectTransform>().Band(true, 150, 190 + row * 170, 40);
                var art = Art.Enemy(d.enemyId);
                var icon = UIKit.Icon(btn.transform, "icon", art != null ? art : Art.Diamond, art != null ? Color.white : Palette.Candy);
                icon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14, 0), new Vector2(120, 120));
                UIKit.Label(btn.transform, Loc.F("deck.send_label", Loc.SendName(d), d.cost, d.oncePerMatch ? Loc.T("deck.once") : ""), 32, Palette.Text, TextAnchor.MiddleLeft)
                    .rectTransform.Stretch(150, 20, 0, 0);
                btn.onClick.AddListener(() =>
                {
                    profile.sendDeck[slot] = d.id;
                    App.SaveProfile();
                    App.Toast(Loc.T("deck.saved"));
                    Rebuild();
                });
                row++;
            }
            if (row == 0) UIKit.Label(box, Loc.T("deck.no_other_sends"), 34, Palette.TextDim).rectTransform.Band(true, 80, 200, 30);
        }
    }
}
