using System;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>Talent names / descriptions (language table keys talent.N and talent_desc.N).</summary>
    public static class TalentText
    {
        public static string Name(TalentKind k) { return k == TalentKind.None ? "" : Loc.T("talent." + (int)k); }
        public static string Desc(TalentKind k) { return k == TalentKind.None ? "" : Loc.T("talent_desc." + (int)k); }

        /// <summary>"Boss Hunter · Crit" (skips empty tiers).</summary>
        public static string Name(TalentKind t1, TalentKind t2)
        {
            if (t1 == TalentKind.None) return Name(t2);
            if (t2 == TalentKind.None) return Name(t1);
            return Name(t1) + " · " + Name(t2);
        }
    }

    /// <summary>
    /// Talent picker for one card: two tiers (card level 9 and 12), two branches each. Free to change outside a match,
    /// so players can adapt their deck to what they meet.
    /// </summary>
    public static class TalentPopup
    {
        public static void Open(GameApp app, string unitId, Action onChanged)
        {
            var meta = app.Meta;
            var def = app.Content.Unit(unitId);
            if (def == null) return;
            var b = app.Content.Balance;
            var box = Modal.Open(app, Loc.F("talent.title", Loc.UnitName(def)), 1260, onChanged);
            UIKit.Label(box, Loc.F("talent.intro", Loc.RoleName(def.role)), 26, Palette.TextDim, TextAnchor.UpperCenter).rectTransform
                .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(860, 90));

            for (int tier = 0; tier < Talents.Tiers; tier++)
            {
                int unlockLevel = Talents.UnlockLevel(b, tier);
                bool unlocked = meta.TalentUnlocked(unitId, tier);
                int chosen = meta.TalentChoice(unitId, tier);
                float y = -230 - tier * 500;
                string head = unlocked ? Loc.F("talent.tier", tier + 1) : Loc.F("talent.tier_locked", tier + 1, unlockLevel);
                UIKit.Outlined(UIKit.Label(box, head, 32, unlocked ? Palette.Primary : Palette.TextDim, TextAnchor.MiddleLeft), 2f).rectTransform
                    .Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(860, 50));
                for (int opt = 1; opt <= Talents.Options; opt++)
                {
                    var kind = Talents.Of(def.role, tier, opt);
                    bool selected = unlocked && chosen == opt;
                    int t = tier, o = opt;
                    var card = UIKit.Button(box, "t" + tier + "_" + opt, "", selected ? new Color(0.2f, 0.55f, 0.3f, 0.95f) : new Color(0f, 0f, 0f, 0.35f), () =>
                    {
                        if (!unlocked) { AudioService.Play(Sfx.Error); app.Toast(Loc.F("talent.need_level", unlockLevel)); return; }
                        if (!meta.ChooseTalent(unitId, t, o)) return;
                        app.SaveProfile();
                        AudioService.Play(Sfx.Upgrade);
                        Modal.CloseFrom(box);
                        if (onChanged != null) onChanged();
                        Open(app, unitId, onChanged);
                    }, 24);
                    var rt = card.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(opt == 1 ? -218 : 218, y - 50), new Vector2(424, 410));
                    if (!unlocked) foreach (var g in card.GetComponentsInChildren<Graphic>()) g.color = new Color(g.color.r, g.color.g, g.color.b, g.color.a * 0.55f);
                    var name = UIKit.Outlined(UIKit.Label(rt, TalentText.Name(kind), 32, selected ? Palette.Primary : Palette.Text, TextAnchor.UpperCenter), 2f);
                    name.rectTransform.Stretch(16, 16, 20, 320);
                    UIKit.Label(rt, TalentText.Desc(kind), 26, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(22, 22, 90, 70);
                    var tag = UIKit.Label(rt, Loc.T(selected ? "talent.selected" : (unlocked ? "talent.pick" : "talent.locked")), 26,
                        selected ? Palette.Green : Palette.TextDim, TextAnchor.LowerCenter);
                    tag.rectTransform.Stretch(10, 10, 340, 18);
                }
            }
        }

        /// <summary>Button text for the card popup: "Talents 1/2".</summary>
        public static string ButtonText(MetaService meta, string unitId)
        {
            int picked = 0, open = 0;
            for (int t = 0; t < Talents.Tiers; t++)
            {
                if (!meta.TalentUnlocked(unitId, t)) continue;
                open++;
                if (meta.TalentChoice(unitId, t) > 0) picked++;
            }
            return open == 0 ? Loc.F("talent.button_locked", Talents.UnlockLevel(meta.Content.Balance, 0)) : Loc.F("talent.button", picked, open);
        }

        /// <summary>True when a card has an unlocked tier with no pick yet (red dot on the card).</summary>
        public static bool HasUnpicked(MetaService meta, string unitId)
        {
            for (int t = 0; t < Talents.Tiers; t++)
                if (meta.TalentUnlocked(unitId, t) && meta.TalentChoice(unitId, t) == 0) return true;
            return false;
        }
    }
}
