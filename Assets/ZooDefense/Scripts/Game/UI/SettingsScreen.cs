using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// Settings tab: player card, account (link / restore / backup / delete), sound, game options, support and
    /// (dev builds) test cheats. Scrollable cards.
    /// </summary>
    public sealed class SettingsScreen : ScreenBase
    {
        static float _scroll = 1f;
        const float Gap = 22f;

        ScrollRect _scrollRect;
        RectTransform _list;
        float _y;
        RectTransform _busyPanel;
        Text _busyText;
        float _t;

        // One calm palette for the whole screen: muted cards, green only for "on" / the main action.
        static readonly Color CardFill = Art.Hex(0x261C3C);
        static readonly Color CardBorder = Art.Hex(0x4A3D66);
        static readonly Color Neutral = Art.Hex(0x4A3D66);
        static readonly Color AccountColor = CardBorder;
        static readonly Color GameColor = CardBorder;
        static readonly Color SupportColor = CardBorder;

        protected override void Build()
        {
            Deco.ScreenBackground(Root, App, Palette.Background, 0.25f);
            new CurrencyBar(Root, App, true);
            float top = Deco.Header(Root, Loc.T("settings.title"), Art.Hex(0x5A4A80));
            var area = UIKit.Rect("settings", Root).Stretch(24, 24, top, NavBar.Height + 6);
            _list = ScrollKit.Vertical(area);
            _scrollRect = area.GetComponent<ScrollRect>();
            _y = 8;

            ProfileCard();
            AccountCard();
            SoundCard();
            GameCard();
            SupportCard();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugSection();
#endif
            ScrollKit.SetHeight(_list, _y + 30);
            _scrollRect.verticalNormalizedPosition = _scroll;
            NavBar.Build(Root, App, Tab.Settings);
            BuildBusyPanel();
        }

        public override void Close() { if (_scrollRect != null) _scroll = _scrollRect.verticalNormalizedPosition; }

        public override void Tick(float dt)
        {
            _t += dt;
            if (_busyPanel != null)
            {
                bool busy = Accounts.Service != null && Accounts.Service.SigningIn;
                if (_busyPanel.gameObject.activeSelf != busy) _busyPanel.gameObject.SetActive(busy);
                if (busy) _busyText.text = Loc.T("account.waiting") + new string('.', 1 + (int)(_t * 3f) % 3);
            }
        }

        void Rebuild() { App.Show(new SettingsScreen()); }

        // ------------------------------------------------------------------ layout helpers

        /// <summary>Section title + a framed card of the given height. Returns the card's inner rect.</summary>
        RectTransform Card(string title, Color color, float height)
        {
            if (title != null)
            {
                Deco.Section(_list, title, _y, Palette.TextDim);
                _y += 70;
            }
            var holder = UIKit.Rect("card", _list).Band(true, height, _y, 4);
            _y += height + Gap;
            return Deco.Frame(holder, "frame", color, CardFill, 6f);
        }

        /// <summary>One line inside a card: label on the left, controls go on the right of the returned rect.</summary>
        static RectTransform Line(RectTransform card, int index, int count, string label, string hint = null)
        {
            var line = UIKit.Rect("line" + index, card);
            line.anchorMin = new Vector2(0f, 1f - (index + 1) / (float)count);
            line.anchorMax = new Vector2(1f, 1f - index / (float)count);
            line.offsetMin = new Vector2(24, 0);
            line.offsetMax = new Vector2(-20, 0);
            if (index > 0) UIKit.Panel(line, "sep", new Color(1f, 1f, 1f, 0.08f), false).rectTransform.Band(true, 2, 0);
            if (hint == null)
                UIKit.Label(line, label, 36, Palette.Text, TextAnchor.MiddleLeft).rectTransform.Stretch(0, 420, 0, 0);
            else
            {
                UIKit.Label(line, label, 36, Palette.Text, TextAnchor.LowerLeft).rectTransform.Stretch(0, 300, 0, 64);
                UIKit.Label(line, hint, 24, Palette.TextDim, TextAnchor.UpperLeft).rectTransform.Stretch(0, 300, 74, 0);
            }
            return line;
        }

        static Button RightButton(RectTransform line, string text, Color color, System.Action onClick, float width = 330f)
        {
            var b = UIKit.ChunkyButton(line, "btn", text, color, onClick, 30);
            b.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(width, 104));
            return b;
        }

        /// <summary>Rounded on/off switch.</summary>
        static void Toggle(RectTransform line, bool on, System.Action<bool> onChange)
        {
            var track = UIKit.Panel(line, "toggle", on ? Palette.Green : Palette.Disabled).rectTransform;
            track.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10, 0), new Vector2(150, 76));
            var knob = UIKit.Icon(track, "knob", Art.Circle, Color.white).rectTransform;
            knob.Place(new Vector2(on ? 1f : 0f, 0.5f), new Vector2(on ? 1f : 0f, 0.5f), new Vector2(on ? -8 : 8, 0), new Vector2(62, 62));
            var hit = UIKit.Button(track, "hit", "", new Color(0f, 0f, 0f, 0f), () => { AudioService.Play(Sfx.Click); onChange(!on); });
            hit.GetComponent<RectTransform>().Stretch();
        }

        // ------------------------------------------------------------------ cards

        void ProfileCard()
        {
            var p = App.Profile;
            var state = App.Meta.State;
            var card = Card(null, CardBorder, 250);
            var avatar = UIKit.Icon(card, "avatar", Art.Circle, Art.Hex(0xFFF1D6)).rectTransform;
            avatar.Place(new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(125, 0), new Vector2(190, 190));
            UIKit.Icon(avatar, "ring", Art.Ring, CardBorder).rectTransform.Stretch(-6, -6, -6, -6);
            var lead = App.Content.Unit(p.deck[0]);
            if (lead != null) Icons.Unit(avatar, lead, 1, 170).Stretch(10, 10, 6, 14);

            UIKit.Outlined(UIKit.Label(card, p.playerName, 46, Palette.Text, TextAnchor.UpperLeft), 3f).rectTransform.Stretch(250, 20, 24, 150);
            string title = string.IsNullOrEmpty(state.title) ? Loc.T("settings.no_title") : Loc.T("title." + state.title);
            UIKit.Label(card, title, 28, Palette.TextDim, TextAnchor.UpperLeft).rectTransform.Stretch(250, 20, 84, 110);

            var stats = UIKit.Rect("stats", card).Stretch(250, 20, 140, 22);
            Stat(stats, 0, "trophy", state.trophies.ToString());
            Stat(stats, 1, null, Loc.F("settings.battles", p.matchesPlayed));
            Stat(stats, 2, null, Loc.F("settings.wins", p.wins));
        }

        static void Stat(RectTransform row, int index, string icon, string text)
        {
            var chip = UIKit.Panel(row, "stat" + index, new Color(0f, 0f, 0f, 0.4f)).rectTransform;
            chip.anchorMin = new Vector2(index / 3f, 0f);
            chip.anchorMax = new Vector2((index + 1) / 3f, 1f);
            chip.offsetMin = new Vector2(4, 0);
            chip.offsetMax = new Vector2(-4, 0);
            float left = 10;
            if (icon != null)
            {
                Icons.Currency(chip, icon).rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6, 0), new Vector2(54, 54));
                left = 62;
            }
            UIKit.Label(chip, text, 26, Palette.Text).rectTransform.Stretch(left, 6, 0, 0);
        }

        void AccountCard()
        {
            var acc = Accounts.Service;
            if (acc == null)
            {
                var off = Card(Loc.T("account.title"), AccountColor, 150);
                UIKit.Label(off, Loc.T("account.unavailable"), 30, Palette.TextDim).rectTransform.Stretch(24, 24, 0, 0);
                return;
            }

            if (!acc.IsLinked)
            {
                var card = Card(Loc.T("account.title"), AccountColor, 400);
                var pill = UIKit.Panel(card, "status", Neutral).rectTransform;
                pill.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -22), new Vector2(240, 56));
                UIKit.Label(pill, Loc.T("account.guest"), 28, Palette.Text).rectTransform.Stretch();
                UIKit.Label(card, Loc.T("account.guest_text"), 28, Palette.Text, TextAnchor.UpperLeft).rectTransform.Stretch(24, 24, 96, 170);

                var link = UIKit.ChunkyButton(card, "link", Loc.T("account.link"), Palette.Green, DoLink, 36);
                link.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 74), new Vector2(620, 116));
                UIKit.Outlined(link.GetComponentInChildren<Text>(), 2.5f);
                var have = UIKit.Button(card, "signin", Loc.T("account.have_account"), new Color(0f, 0f, 0f, 0f), DoSignInExisting, 28);
                have.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(700, 56));
                have.GetComponentInChildren<Text>().color = Palette.TextDim;
            }
            else
            {
                var card = Card(Loc.T("account.title"), AccountColor, 430);
                var pill = UIKit.Panel(card, "status", Palette.Green).rectTransform;
                pill.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -22), new Vector2(260, 56));
                UIKit.Label(pill, Loc.T("account.linked"), 28, Palette.Text).rectTransform.Stretch();
                UIKit.Label(card, acc.LinkedLabel, 32, Palette.Text, TextAnchor.MiddleLeft).rectTransform
                    .Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300, -22), new Vector2(660, 56));
                long last = acc.LastBackup;
                string backup = last > 0 ? Loc.F("account.backup_ago", Fmt.Duration(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - last)) : Loc.T("account.backup_never");
                UIKit.Label(card, Loc.T("account.linked_text") + "\n" + backup, 26, Palette.TextDim, TextAnchor.UpperLeft).rectTransform.Stretch(24, 24, 96, 200);

                var save = UIKit.ChunkyButton(card, "backup", Loc.T("account.backup"), Neutral, DoBackup, 30);
                save.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24, 86), new Vector2(450, 104));
                var unlink = UIKit.ChunkyButton(card, "unlink", Loc.T("account.unlink"), Neutral, DoUnlink, 30);
                unlink.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24, 86), new Vector2(450, 104));
                var delete = UIKit.Button(card, "delete", Loc.T("account.delete"), new Color(0f, 0f, 0f, 0f), DoDelete, 26);
                delete.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 18), new Vector2(600, 56));
                delete.GetComponentInChildren<Text>().color = Art.Hex(0xFF8A8A);
            }
        }

        void SoundCard()
        {
            var p = App.Profile;
            var card = Card(Loc.T("settings.sound"), CardBorder, 280);
            SoundToggle(card, 0, Loc.T("settings.music"), () => p.musicVolume, v => p.musicVolume = v, 0.6f);
            SoundToggle(card, 1, Loc.T("settings.sfx"), () => p.sfxVolume, v => p.sfxVolume = v, 0.8f);
        }

        /// <summary>Sound on / off (on = the normal volume).</summary>
        void SoundToggle(RectTransform card, int index, string label, System.Func<float> get, System.Action<float> set, float onVolume)
        {
            var line = Line(card, index, 2, label);
            Toggle(line, get() > 0.01f, v =>
            {
                set(v ? onVolume : 0f);
                AudioService.ApplyVolumes();
                App.SaveProfile();
                Rebuild();
            });
        }

        void GameCard()
        {
            var p = App.Profile;
            var card = Card(Loc.T("settings.game"), GameColor, 420);

            var lang = Line(card, 0, 3, Loc.T("settings.language"));
            var all = Loc.Supported;
            for (int i = 0; i < all.Length; i++)
            {
                var l = all[i];
                bool on = l == Loc.Current;
                var b = UIKit.ChunkyButton(lang, "lang" + i, Loc.NativeName(l), on ? Palette.Green : Neutral, () => SetLanguage(l), 28);
                b.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-(all.Length - 1 - i) * 230, 0), new Vector2(220, 96));
            }

            var online = Line(card, 1, 3, Loc.T("settings.online"), Loc.T("settings.online_hint"));
            Toggle(online, p.onlinePvp, v => { p.onlinePvp = v; App.SaveProfile(); Rebuild(); });

            var tut = Line(card, 2, 3, Loc.T("settings.tutorial"));
            RightButton(tut, Loc.T("settings.play"), Neutral, () => App.Show(BattleScreen.Tutorial()));

            PrivacyCard();
        }

        /// <summary>Personalised ads on/off (GDPR consent / CCPA opt-out) and the privacy policy.</summary>
        void PrivacyCard()
        {
            bool hasPolicy = !string.IsNullOrEmpty(Privacy.PolicyUrl);
            var card = Card(Loc.T("settings.privacy"), GameColor, hasPolicy ? 290 : 150);
            int lines = hasPolicy ? 2 : 1;
            var ads = Line(card, 0, lines, Loc.T("settings.personal_ads"), Loc.T("settings.personal_ads_hint"));
            Toggle(ads, Privacy.Personalized, v => { Privacy.Set(v); Rebuild(); });
            if (hasPolicy)
            {
                var policy = Line(card, 1, lines, Loc.T("privacy.policy"));
                RightButton(policy, Loc.T("settings.open"), Neutral, () => Application.OpenURL(Privacy.PolicyUrl), 220);
            }
        }

        void SupportCard()
        {
            var card = Card(Loc.T("settings.support"), SupportColor, 290);
            var id = Line(card, 0, 2, Loc.T("settings.player_id"), ShortId());
            RightButton(id, Loc.T("settings.copy"), Neutral, () =>
            {
                GUIUtility.systemCopyBuffer = FullId();
                App.Toast(Loc.T("settings.copied"));
            }, 220);
            var reset = Line(card, 1, 2, Loc.T("settings.reset"), Loc.F("settings.version", Application.version, App.Profile.matchesPlayed));
            RightButton(reset, Loc.T("settings.reset_button"), Neutral, ConfirmReset, 220).GetComponentInChildren<Text>().color = Art.Hex(0xFF8A8A);
        }

        string FullId()
        {
            var acc = Accounts.Service;
            return acc != null && !string.IsNullOrEmpty(acc.PlayerId) ? acc.PlayerId : SystemInfo.deviceUniqueIdentifier;
        }

        string ShortId()
        {
            var id = FullId();
            return id.Length > 14 ? id.Substring(0, 6) + "…" + id.Substring(id.Length - 6) : id;
        }

        // ------------------------------------------------------------------ actions

        void SetLanguage(Language next)
        {
            if (next == Loc.Current) return;
            Loc.Set(next);
            App.Profile.language = Loc.Code(next);
            App.SaveProfile();
            Rebuild();
            App.Toast(Loc.T("lobby.language_changed"));
        }

        void ConfirmReset()
        {
            Modal.Confirm(App, Loc.T("settings.reset"), Loc.T(Accounts.Service != null && Accounts.Service.IsLinked ? "settings.reset_text_linked" : "settings.reset_text"), Loc.T("settings.reset_button"), () =>
            {
                App.ResetProfile();
                AudioService.ApplyVolumes();
                App.Show(new LobbyScreen());
            });
        }

        void DoLink()
        {
            Accounts.Service.Link(r =>
            {
                if (r == AccountResult.Ok) { App.Toast(Loc.T("account.linked_ok")); Rebuild(); return; }
                if (r == AccountResult.AlreadyLinkedElsewhere)
                {
                    // That account already has progress (another phone): offer to load it here.
                    Modal.Confirm(App, Loc.T("account.exists_title"), Loc.T("account.exists_text"), Loc.T("account.load_it"), RestoreFromCloud);
                    return;
                }
                ShowError(r);
            });
        }

        void DoSignInExisting()
        {
            Modal.Confirm(App, Loc.T("account.have_account"), Loc.T("account.signin_text"), Loc.T("account.signin"), RestoreFromCloud);
        }

        void RestoreFromCloud()
        {
            Accounts.Service.SignInExisting((r, json) =>
            {
                if (r != AccountResult.Ok) { ShowError(r); return; }
                if (json != null && App.ReplaceProfile(json))
                {
                    AudioService.ApplyVolumes();
                    App.Toast(Loc.T("account.restored"));
                    App.Show(new LobbyScreen());
                    return;
                }
                // No cloud save on that account yet: it now owns this phone's progress.
                Accounts.Service.BackupNow(null);
                App.Toast(Loc.T("account.linked_ok"));
                Rebuild();
            });
        }

        void DoBackup()
        {
            Accounts.Service.BackupNow(r =>
            {
                if (r == AccountResult.Ok) { App.Toast(Loc.T("account.backup_ok")); Rebuild(); }
                else ShowError(r);
            });
        }

        void DoUnlink()
        {
            Modal.Confirm(App, Loc.T("account.unlink"), Loc.T("account.unlink_text"), Loc.T("account.unlink"), () =>
                Accounts.Service.Unlink(r => { if (r == AccountResult.Ok) { App.Toast(Loc.T("account.unlinked")); Rebuild(); } else ShowError(r); }));
        }

        void DoDelete()
        {
            Modal.Confirm(App, Loc.T("account.delete"), Loc.T("account.delete_text"), Loc.T("account.delete_button"), () =>
                Accounts.Service.DeleteAccount(r =>
                {
                    if (r != AccountResult.Ok) { ShowError(r); return; }
                    App.ResetProfile();
                    AudioService.ApplyVolumes();
                    App.Toast(Loc.T("account.deleted"));
                    Accounts.Service.Refresh(null);   // start again as a new guest
                    App.Show(new LobbyScreen());
                }));
        }

        void ShowError(AccountResult r)
        {
            if (r == AccountResult.Cancelled) return;
            string key = r == AccountResult.NotConfigured ? "account.err_config" : r == AccountResult.Offline ? "account.err_offline" : "account.err_generic";
            AudioService.Play(Sfx.Error);
            App.Toast(Loc.T(key));
        }

        /// <summary>Covers the screen while the sign-in page is open, with a cancel button.</summary>
        void BuildBusyPanel()
        {
            _busyPanel = UIKit.Panel(Root, "accountBusy", new Color(0.04f, 0.02f, 0.09f, 0.82f), false).rectTransform;
            _busyPanel.Stretch();
            _busyPanel.GetComponent<Image>().raycastTarget = true;
            var ring = UIKit.Icon(_busyPanel, "spin", Art.Ring, Palette.Primary).rectTransform;
            ring.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(150, 150));
            UIKit.Motion(ring).Spin(-220f);
            _busyText = UIKit.Outlined(UIKit.Label(_busyPanel, "", 40, Palette.Text), 2.5f);
            _busyText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(900, 120));
            var cancel = UIKit.ChunkyButton(_busyPanel, "cancel", Loc.T("common.cancel"), Palette.Red, () => { if (Accounts.Service != null) Accounts.Service.CancelSignIn(); }, 34);
            cancel.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -160), new Vector2(380, 110));
            _busyPanel.gameObject.SetActive(false);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void DebugSection()
        {
            var p = App.Profile;
            var grid = Card(Loc.T("settings.debug"), Palette.Disabled, 250);
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
        }

        void Cheat(RectTransform grid, int index, string label, System.Action action)
        {
            var btn = UIKit.Button(grid, label, label, Palette.PanelDark, () =>
            {
                action();
                App.Meta.Refresh(App.Now);
                App.SaveProfile();
                Rebuild();
                App.Toast(label);
            }, 26);
            var rt = btn.GetComponent<RectTransform>();
            int col = index % 4, row = index / 4;
            rt.anchorMin = new Vector2(col / 4f, row == 0 ? 0.5f : 0f);
            rt.anchorMax = new Vector2((col + 1) / 4f, row == 0 ? 1f : 0.5f);
            rt.offsetMin = new Vector2(8, 8);
            rt.offsetMax = new Vector2(-8, -8);
        }
#endif
    }
}
