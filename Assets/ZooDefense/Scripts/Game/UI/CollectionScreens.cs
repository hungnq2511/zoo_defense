using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// Bestiary ("Sổ tay"): every animal and monster. Met ones show their stats, skill and element matchup;
    /// the first look at a new page gives a few gems.
    /// </summary>
    public sealed class BestiaryScreen : ListScreen
    {
        static bool _monsters;

        protected override void Build()
        {
            var meta = App.Meta;
            Frame(Loc.T("bestiary.title"), Art.Hex(0x4CC46B), () => App.Show(new ModesScreen()));
            var tabs = UIKit.Rect("tabs", List).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -Y), new Vector2(1020, 100));
            var a = UIKit.ChunkyButton(tabs, "animals", Loc.T("bestiary.animals"), !_monsters ? Palette.Primary : Palette.Panel, () => { _monsters = false; App.Show(new BestiaryScreen()); }, 34);
            a.GetComponent<RectTransform>().Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(500, 96));
            var m = UIKit.ChunkyButton(tabs, "monsters", Loc.T("bestiary.monsters"), _monsters ? Palette.Primary : Palette.Panel, () => { _monsters = true; App.Show(new BestiaryScreen()); }, 34);
            m.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(500, 96));
            Y += 116;
            Label(List, Loc.F("bestiary.progress", meta.Stat(StatKind.Discovered), App.Content.Units.Count + App.Content.Enemies.Count, MetaContent.BestiaryGems), 26, Palette.TextDim, TextAnchor.MiddleCenter)
                .rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -Y), new Vector2(1000, 50));
            Y += 64;

            var ids = new List<string>();
            if (_monsters) foreach (var e in App.Content.Enemies) ids.Add(e.id);
            else foreach (var u in App.Content.Units) ids.Add(u.id);
            const int cols = 4;
            float w = 1020f / cols;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                bool seen = meta.IsSeen(id);
                var cell = UIKit.Rect("cell", List).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2((i % cols) * w + 4, -Y - (i / cols) * 270), new Vector2(w - 12, 256));
                var frame = Deco.Frame(cell, "f", seen ? FrameColor(id) : Palette.Disabled, Art.Hex(0x261C3C), 6f);
                var pic = UIKit.Rect("pic", frame).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(170, 170));
                Picture(pic, id, seen);
                UIKit.Label(frame, seen ? Name(id) : "???", 22, Palette.Text).rectTransform.Stretch(6, 6, 178, 6);
                if (seen)
                {
                    var tap = UIKit.Button(frame, "tap", "", new Color(0f, 0f, 0f, 0f), () => Detail(id));
                    tap.GetComponent<RectTransform>().Stretch();
                    if (meta.CanClaimBestiary(id)) Badge(cell);
                }
            }
            Y += ((ids.Count + cols - 1) / cols) * 270 + 10;
            Done();
        }

        Color FrameColor(string id)
        {
            var u = App.Content.Unit(id);
            if (u != null) return Art.RarityColor(u.rarity);
            var e = App.Content.Enemy(id);
            return e != null && e.kind == EnemyKind.Boss ? Palette.Red : (e != null && e.kind == EnemyKind.Elite ? Palette.Purple : Art.Hex(0x8A7FA6));
        }

        string Name(string id)
        {
            var u = App.Content.Unit(id);
            if (u != null) return Loc.UnitName(u);
            return Loc.EnemyName(App.Content.Enemy(id));
        }

        void Picture(RectTransform holder, string id, bool seen)
        {
            var u = App.Content.Unit(id);
            RectTransform img;
            if (u != null) img = Icons.Unit(holder, u, 1, 170);
            else
            {
                var art = Art.Enemy(id);
                img = art != null ? UIKit.Icon(holder, "icon", art, Color.white).rectTransform : UIKit.Icon(holder, "icon", Art.Circle, Art.Hex(0x9C8AC4)).rectTransform;
            }
            img.Stretch();
            if (!seen) foreach (var g in img.GetComponentsInChildren<Graphic>()) g.color = new Color(0.08f, 0.06f, 0.14f, 0.95f);
        }

        void Detail(string id)
        {
            var meta = App.Meta;
            var box = Modal.Open(App, Name(id), 1180);
            var pic = UIKit.Rect("pic", box).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(330, 330));
            Picture(pic, id, true);
            UIKit.Motion(pic).Bob(6f, 2f);
            string text;
            var u = App.Content.Unit(id);
            var b = App.Content.Balance;
            if (u != null)
            {
                text = Loc.ElementName(u.element) + " · " + Loc.RoleName(u.role) + " · " + Loc.RarityName(u.rarity) + "\n";
                if (u.damage > 0) text += Loc.F("deck.stat_damage", u.damage, (u.attackIntervalTicks / 20f).ToString("0.0")) + "\n";
                if (u.skill != UnitSkill.None) text += Loc.SkillText(u) + "\n";
                text += "<color=#B6F25A>" + ElementGuide.MatchupLine(u, b) + "</color>";
            }
            else
            {
                var e = App.Content.Enemy(id);
                text = Loc.T("enemy_kind." + e.kind.ToString().ToLowerInvariant()) + " · " + Loc.F("bestiary.hp", e.hp) + (e.armorPercent > 0 ? " · " + Loc.F("bestiary.armor", e.armorPercent) : "") + "\n";
                text += Loc.T("enemy_desc." + e.id);
            }
            var t = UIKit.Label(box, text, 28, Palette.Text, TextAnchor.UpperLeft);
            t.supportRichText = true;
            t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -440), new Vector2(840, 440));
            if (meta.CanClaimBestiary(id))
            {
                var claim = UIKit.ChunkyButton(box, "claim", Loc.F("bestiary.claim", MetaContent.BestiaryGems), Palette.Green, () =>
                {
                    var r = meta.ClaimBestiary(id);
                    App.SaveProfile();
                    Modal.CloseAll();
                    if (r != null) Modal.Rewards(App, Loc.T("bestiary.title"), r);
                    App.Show(new BestiaryScreen());
                }, 40);
                claim.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(520, 124));
            }
        }
    }

    /// <summary>Achievements (3 tiers each, gems) and the title shown on your match card.</summary>
    public sealed class AchievementsScreen : ListScreen
    {
        protected override void Build()
        {
            var meta = App.Meta;
            Frame(Loc.T("achievements.title"), Art.Hex(0xFFB020), () => App.Show(new ModesScreen()));

            // Title picker
            var head = Card(200, Art.Hex(0x9B5CFF));
            Label(head, Loc.T("titles.title"), 30, Palette.Primary, TextAnchor.UpperLeft).rectTransform.Stretch(24, 24, 12, 140);
            Label(head, string.IsNullOrEmpty(meta.State.title) ? Loc.T("titles.none") : Loc.T("title." + meta.State.title), 40, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(24, 300, 60, 60);
            Label(head, Loc.F("titles.owned", meta.State.titles.Count), 24, Palette.TextDim, TextAnchor.LowerLeft).rectTransform.Stretch(24, 300, 120, 14);
            var pick = Btn(head, Loc.T("titles.change"), Palette.Purple, TitlePicker, new Vector2(-16, 40), new Vector2(260, 110), 32);
            pick.interactable = meta.State.titles.Count > 0;

            foreach (var def in MetaContent.Achievements)
            {
                int claimed = meta.AchievementClaimed(def.id);
                bool max = claimed >= def.tiers.Length;
                int target = def.tiers[Mathf.Min(claimed, def.tiers.Length - 1)];
                int value = meta.Stat(def.stat);
                var c = Card(210, max ? Palette.Green : Art.Hex(0xFFC93C));
                Label(c, Loc.T("ach." + def.id) + "  " + Tiers(claimed, def.tiers.Length), 32, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(24, 24, 12, 150);
                Label(c, Loc.F("ach_desc." + def.id, target), 24, Palette.TextDim, TextAnchor.UpperLeft).rectTransform.Stretch(24, 300, 56, 100);
                var bar = new Bar(c, max ? Palette.Green : Art.Hex(0x7F6BFF), 24);
                bar.Root.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24, 20), new Vector2(660, 48));
                bar.Set(max ? 1f : Mathf.Clamp01(value / (float)target), max ? Loc.T("ach.done") : Mathf.Min(value, target) + " / " + target);
                if (!max)
                {
                    string reward = "+" + def.gems[claimed];
                    var b = Btn(c, meta.CanClaimAchievement(def) ? Loc.T("shop.claim") : reward, meta.CanClaimAchievement(def) ? Palette.Green : Palette.Disabled, () => Claim(def.id), new Vector2(-16, 20), new Vector2(260, 110), 32);
                    b.interactable = meta.CanClaimAchievement(def);
                    if (b.interactable) Badge((RectTransform)c.parent);
                }
                if (!string.IsNullOrEmpty(def.title))
                    Label(c, Loc.F("ach.unlocks", Loc.T("title." + def.title)) + (def.emote >= 0 ? " + " + Loc.F("ach.emote", Loc.T("emote." + def.emote)) : ""), 20, Art.Hex(0xFFE7A8), TextAnchor.UpperRight)
                        .rectTransform.Stretch(300, 24, 14, 150);
            }
            Done();
        }

        static string Tiers(int claimed, int total)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < total; i++) sb.Append(i < claimed ? "<color=#FFD23F>•</color>" : "<color=#5E5670>•</color>");
            return sb.ToString();
        }

        void Claim(string id)
        {
            var r = App.Meta.ClaimAchievement(id);
            App.SaveProfile();
            if (r != null) Modal.Rewards(App, Loc.T("achievements.title"), r);
            App.Show(new AchievementsScreen());
        }

        void TitlePicker()
        {
            var meta = App.Meta;
            var titles = new List<string> { "" };
            titles.AddRange(meta.State.titles);
            var box = Modal.Open(App, Loc.T("titles.title"), 300 + 110 * titles.Count);
            for (int i = 0; i < titles.Count; i++)
            {
                string t = titles[i];
                var b = UIKit.ChunkyButton(box, "t" + i, string.IsNullOrEmpty(t) ? Loc.T("titles.none") : Loc.T("title." + t), t == meta.State.title ? Palette.Primary : Palette.Panel, () =>
                {
                    meta.SetTitle(t);
                    App.SaveProfile();
                    Modal.CloseAll();
                    App.Show(new AchievementsScreen());
                }, 32);
                b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -110 - i * 110), new Vector2(700, 96));
            }
        }
    }

    /// <summary>Last 5 matches on this device: watch them again (the command log replays exactly).</summary>
    public sealed class ReplaysScreen : ListScreen
    {
        protected override void Build()
        {
            Frame(Loc.T("replay.title"), Art.Hex(0x9B5CFF), () => App.Show(new ModesScreen()));
            var list = ReplayStore.Load();
            if (list.Count == 0)
            {
                var c = Card(160, Palette.Disabled);
                Label(c, Loc.T("replay.empty"), 30, Palette.TextDim, TextAnchor.MiddleCenter).rectTransform.Stretch(24, 24, 0, 0);
            }
            foreach (var entry in list)
            {
                var e = entry;
                var mode = (MatchMode)e.mode;
                var c = Card(190, BattleScreen.ModeColor(mode));
                string res = Loc.T(e.result == 0 ? "result.victory" : (e.result == 1 ? "result.defeat" : "result.draw"));
                string modeName = mode == MatchMode.Versus ? Loc.T("mode.versus") : Loc.T("mode." + mode.ToString().ToLowerInvariant());
                Label(c, modeName + " · " + res, 32, e.result == 0 ? Palette.Green : Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(24, 540, 14, 110);
                string vs = e.opponent == "-" ? "" : Loc.F("tournament.vs", e.opponent) + " · ";
                int seconds = e.record != null ? e.record.finalTick / 20 : 0;
                Label(c, vs + Loc.F("replay.length", seconds / 60, (seconds % 60).ToString("00")) + " · " + Fmt.Duration(Math.Max(0, App.Now - e.time)) + " " + Loc.T("replay.ago"), 24, Palette.TextDim, TextAnchor.LowerLeft)
                    .rectTransform.Stretch(24, 540, 80, 18);
                Btn(c, Loc.T("replay.watch"), Palette.Purple, () => ModeLauncher.Replay(App, e), new Vector2(-16, 36), new Vector2(260, 110), 32);
                Btn(c, Loc.T("stats.button"), Palette.Blue, () => MatchStats.Show(App, e.record, e.local), new Vector2(-290, 36), new Vector2(230, 110), 28);
            }
            Done();
        }
    }
}
