using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>Settings tab: volumes, language, tutorial replay, profile reset and (dev builds) test cheats.</summary>
    public sealed class SettingsScreen : ScreenBase
    {
        float _y;

        protected override void Build()
        {
            var p = App.Profile;
            UIKit.Panel(Root, "bg", Palette.Background, false).rectTransform.Stretch();
            new CurrencyBar(Root, App, true);
            UIKit.Label(Root, Loc.T("settings.title"), 60, Palette.Primary).rectTransform.Band(true, 90, CurrencyBar.Height + 20);
            _y = CurrencyBar.Height + 130;

            VolumeRow(Loc.T("settings.music"), () => p.musicVolume, v => p.musicVolume = v);
            VolumeRow(Loc.T("settings.sfx"), () => p.sfxVolume, v => p.sfxVolume = v);

            var lang = Row(Loc.T("settings.language"));
            var langBtn = UIKit.Button(lang, "language", Loc.NativeName(Loc.Current), Palette.Blue, CycleLanguage, 34);
            langBtn.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20, 0), new Vector2(380, 100));

            var tut = Row(Loc.T("settings.tutorial"));
            var tutBtn = UIKit.Button(tut, "tutorial", Loc.T("settings.play"), Palette.Green, () => App.Show(BattleScreen.Tutorial()), 34);
            tutBtn.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20, 0), new Vector2(380, 100));

            var reset = Row(Loc.T("settings.reset"));
            var resetBtn = UIKit.Button(reset, "reset", Loc.T("settings.reset_button"), Palette.Red, ConfirmReset, 34);
            resetBtn.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20, 0), new Vector2(380, 100));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugSection();
#endif

            UIKit.Label(Root, Loc.F("settings.version", Application.version, App.Profile.matchesPlayed), 26, Palette.TextDim)
                .rectTransform.Band(false, 40, NavBar.Height + 20);
            NavBar.Build(Root, App, Tab.Settings);
        }

        RectTransform Row(string label)
        {
            var row = UIKit.Panel(Root, "row", Palette.Panel).rectTransform.Band(true, 130, _y, 30);
            UIKit.Label(row, label, 38, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(30, 420, 0, 0);
            _y += 150;
            return row;
        }

        void VolumeRow(string label, System.Func<float> get, System.Action<float> set)
        {
            var row = Row(label);
            var value = UIKit.Label(row, "", 40, Palette.Primary);
            value.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-130, 0), new Vector2(140, 100));
            System.Action refresh = () => value.text = Mathf.RoundToInt(get() * 10f) == 0 ? Loc.T("settings.off") : (Mathf.RoundToInt(get() * 100f) + "%");
            System.Action<float> step = d =>
            {
                set(Mathf.Clamp01(Mathf.Round((get() + d) * 10f) / 10f));
                AudioService.ApplyVolumes();
                App.SaveProfile();
                refresh();
            };
            var minus = UIKit.Button(row, "minus", "-", Palette.PanelDark, () => step(-0.1f), 50);
            minus.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-280, 0), new Vector2(100, 100));
            var plus = UIKit.Button(row, "plus", "+", Palette.PanelDark, () => step(0.1f), 50);
            plus.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20, 0), new Vector2(100, 100));
            refresh();
        }

        void CycleLanguage()
        {
            var all = Loc.Supported;
            int i = System.Array.IndexOf(all, Loc.Current);
            var next = all[(i + 1) % all.Length];
            Loc.Set(next);
            App.Profile.language = Loc.Code(next);
            App.SaveProfile();
            App.Show(new SettingsScreen());
            App.Toast(Loc.T("lobby.language_changed"));
        }

        void ConfirmReset()
        {
            Modal.Confirm(App, Loc.T("settings.reset"), Loc.T("settings.reset_text"), Loc.T("settings.reset_button"), () =>
            {
                App.ResetProfile();
                AudioService.ApplyVolumes();
                App.Show(new LobbyScreen());
            });
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void DebugSection()
        {
            var p = App.Profile;
            UIKit.Label(Root, Loc.T("settings.debug"), 32, Palette.TextDim, TextAnchor.MiddleLeft).rectTransform.Band(true, 60, _y, 40);
            _y += 70;
            var grid = UIKit.Rect("debug", Root).Band(true, 250, _y, 30);
            Cheat(grid, 0, "+1h", () => p.debugTimeOffset += 3600);
            Cheat(grid, 1, "+1 day", () => p.debugTimeOffset += MetaService.SecondsPerDay);
            Cheat(grid, 2, "time 0", () => p.debugTimeOffset = 0);
            Cheat(grid, 3, "+1000 gems", () => App.Meta.State.gems += 1000);
            Cheat(grid, 4, "+5000 gold", () => App.Meta.State.gold += 5000);
            Cheat(grid, 5, "+100 trophies", () => App.Meta.State.trophies += 100);
            Cheat(grid, 6, "+300 pass XP", () => App.Meta.State.passXp += 300);
            Cheat(grid, 7, "+chest", () =>
            {
                var slots = App.Meta.State.chests;
                for (int i = 0; i < slots.Length; i++)
                    if (string.IsNullOrEmpty(slots[i].chestId)) { slots[i] = new ChestSlot { chestId = i % 2 == 0 ? "silver" : "gold" }; break; }
            });
            _y += 270;
        }

        void Cheat(RectTransform grid, int index, string label, System.Action action)
        {
            var btn = UIKit.Button(grid, label, label, Palette.PanelDark, () =>
            {
                action();
                App.Meta.Refresh(App.Now);
                App.SaveProfile();
                App.Show(new SettingsScreen());
                App.Toast(label);
            }, 28);
            var rt = btn.GetComponent<RectTransform>();
            int col = index % 4, row = index / 4;
            rt.anchorMin = new Vector2(col / 4f, row == 0 ? 0.5f : 0f);
            rt.anchorMax = new Vector2((col + 1) / 4f, row == 0 ? 1f : 0.5f);
            rt.offsetMin = new Vector2(6, 6);
            rt.offsetMax = new Vector2(-6, -6);
        }
#endif
    }
}
