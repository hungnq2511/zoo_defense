using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>Everything a game mode needs to start a battle (rules, decks, card levels) or to watch a replay.</summary>
    public sealed class BattleConfig
    {
        public int seed;
        public string opponentName = "Bot";
        public int difficulty = 1;
        public bool ranked;
        public bool revenge;
        public MatchRules rules;
        public string[] playerDeck;
        public int[] playerLevels;
        public string[] botDeck;
        public int[] botLevels;
        /// <summary>Campaign level index (-1 = not a campaign match).</summary>
        public int campaignLevel = -1;
        /// <summary>Arena background (1-5), 0 = the player's arena.</summary>
        public int arena;
        /// <summary>Set to watch a recorded match instead of playing.</summary>
        public MatchRecord replay;

        public MatchMode Mode { get { return replay != null && replay.setup != null && replay.setup.rules != null ? replay.setup.rules.mode : (rules != null ? rules.mode : MatchMode.Versus); } }
    }

    /// <summary>PvE modes: nobody plays board 1.</summary>
    public sealed class SoloTransport : IMatchTransport
    {
        Match _match;
        public void Attach(Match match, int localPlayer) { _match = match; }
        public void SendLocal(Command command) { _match.Submit(command); }
        public void BeforeStep() { }
        public bool CanStep() { return true; }
    }

    /// <summary>Feeds a recorded command log back into a fresh match: the simulation replays it exactly.</summary>
    public sealed class ReplayTransport : IMatchTransport
    {
        readonly List<Command> _commands;
        int _next;
        Match _match;

        public ReplayTransport(MatchRecord record) { _commands = record.commands ?? new List<Command>(); }
        public void Attach(Match match, int localPlayer) { _match = match; }
        public void SendLocal(Command command) { }   // watching only
        public bool CanStep() { return true; }

        public void BeforeStep()
        {
            while (_next < _commands.Count && _commands[_next].tick <= _match.Tick) _match.Submit(_commands[_next++]);
        }
    }

    public sealed partial class BattleScreen
    {
        readonly HashSet<string> _seenEnemies = new HashSet<string>();
        RectTransform _modePanel, _raidPanel, _replayPanel;
        Text _modeTitle, _modeGoal, _modeRule, _raidHpText, _replaySpeed;
        Bar _raidBar;
        Button _assistCandy, _assistRage;
        int _shownGoalWave = -1, _shownRaidHp = -1;

        bool IsReplay { get { return _cfg != null && _cfg.replay != null; } }

        string ModeIntro()
        {
            if (IsReplay) return Loc.T("replay.watching");
            switch (_match.Mode)
            {
                case MatchMode.Daily: return Loc.T("mode.daily");
                case MatchMode.Endless: return Loc.T("mode.endless");
                case MatchMode.Campaign: return Loc.F("campaign.level", _cfg.campaignLevel / Campaign.LevelsPerRegion + 1, _cfg.campaignLevel % Campaign.LevelsPerRegion + 1);
                case MatchMode.Mayhem: return Loc.T("mayhem." + (int)_match.Rules.mayhem);
                case MatchMode.Raid: return Loc.T("mode.raid");
                case MatchMode.Tournament: return Loc.T("mode.tournament");
            }
            return Loc.T("battle.get_ready");
        }

        void BuildModeHud()
        {
            var mode = _match.Mode;
            var tug = Root.Find("tug");
            if (_match.IsSolo || _match.IsCoop) { if (tug != null) tug.gameObject.SetActive(false); }

            if (_match.IsSolo)
            {
                // The opponent strip becomes the mode's goal panel.
                _modePanel = UIKit.Panel(Root, "modePanel", new Color(0.16f, 0.1f, 0.28f, 0.96f)).rectTransform.Band(true, StripHeight, StripTop, 12);
                _modePanel.GetComponent<Image>().raycastTarget = true;
                var frame = Deco.Frame(_modePanel, "frame", ModeColor(mode), Art.Hex(0x2E2150), 6f);
                var icon = UIKit.Rect("icon", frame).Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14, 0), new Vector2(150, 150));
                ModeIcon(icon, mode);
                _modeTitle = UIKit.Outlined(UIKit.Label(frame, ModeIntro(), 36, Palette.Primary, TextAnchor.UpperLeft), 2f);
                _modeTitle.rectTransform.Stretch(186, 16, 10, 120);
                _modeGoal = UIKit.Label(frame, "", 30, Palette.Text, TextAnchor.UpperLeft);
                _modeGoal.rectTransform.Stretch(186, 16, 60, 64);
                _modeGoal.supportRichText = true;
                _modeRule = UIKit.Label(frame, RuleText(), 24, Palette.TextDim, TextAnchor.UpperLeft);
                _modeRule.rectTransform.Stretch(186, 16, 112, 8);
            }
            else if (_match.IsCoop)
            {
                // Raid: no sends. The send buttons become the giant boss's HP and two ways to help the partner.
                if (_sendsRoot != null) _sendsRoot.gameObject.SetActive(false);
                var income = _sendsRoot != null ? _sendsRoot.parent.Find("income") : null;
                if (income != null) income.gameObject.SetActive(false);
                var info = _sendsRoot != null ? (RectTransform)_sendsRoot.parent : Root;
                _raidPanel = UIKit.Panel(info, "raid", new Color(0.12f, 0.05f, 0.16f, 0.9f)).rectTransform
                    .Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(236, 0), new Vector2(556, StripHeight - 16));
                UIKit.Outlined(UIKit.Label(_raidPanel, Loc.T("raid.boss"), 26, Palette.Primary, TextAnchor.UpperLeft), 2f).rectTransform.Stretch(14, 14, 6, 140);
                _raidBar = new Bar(_raidPanel, Palette.Red, 22);
                _raidBar.Root.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14, -42), new Vector2(528, 36));
                _assistCandy = UIKit.ChunkyButton(_raidPanel, "giveCandy", Loc.F("raid.give_candy", Board.AssistCandyGift, Board.AssistCandyCost), Palette.Candy, () => _runner.Submit(CommandType.Assist, 0), 22);
                _assistCandy.GetComponent<RectTransform>().Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14, 8), new Vector2(258, 90));
                _assistRage = UIKit.ChunkyButton(_raidPanel, "rage", Loc.F("raid.rage", Board.AssistRageCost), Palette.Red, () => _runner.Submit(CommandType.Assist, 1), 22);
                _assistRage.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14, 8), new Vector2(258, 90));
            }

            if (IsReplay)
            {
                // Watching: the bottom panel is covered by the replay controls.
                _world.InputEnabled = false;
                _replayPanel = UIKit.Panel(Root, "replay", new Color(0.08f, 0.05f, 0.14f, 0.94f), false).rectTransform.Band(false, BottomHeight);
                _replayPanel.GetComponent<Image>().raycastTarget = true;
                UIKit.Outlined(UIKit.Label(_replayPanel, Loc.T("replay.watching"), 44, Palette.Primary), 2f).rectTransform.Band(true, 90, 30);
                var speed = UIKit.ChunkyButton(_replayPanel, "speed", "x1", Palette.Blue, CycleReplaySpeed, 44);
                speed.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20, -40), new Vector2(300, 140));
                _replaySpeed = speed.GetComponentInChildren<Text>();
                var exit = UIKit.ChunkyButton(_replayPanel, "exit", Loc.T("replay.exit"), Palette.Red, () => App.Show(new ReplaysScreen()), 40);
                exit.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20, -40), new Vector2(300, 140));
            }
        }

        void CycleReplaySpeed()
        {
            _runner.TimeScale = _runner.TimeScale >= 4f ? 1f : _runner.TimeScale * 2f;
            _replaySpeed.text = "x" + Mathf.RoundToInt(_runner.TimeScale);
        }

        string RuleText()
        {
            switch (_match.Mode)
            {
                case MatchMode.Campaign:
                {
                    var lv = Campaign.Get(_cfg.campaignLevel);
                    return CampaignRuleText(lv);
                }
                case MatchMode.Daily: return Loc.T("daily.rule");
                case MatchMode.Endless: return Loc.T("endless.rule");
            }
            return "";
        }

        public static string CampaignRuleText(CampaignLevel lv)
        {
            switch (lv.ruleKey)
            {
                case "campaign.rule.element_wave": return Loc.F(lv.ruleKey, Loc.ElementName((Element)lv.waveElements[0]));
                case "campaign.rule.only_element": return Loc.F(lv.ruleKey, Loc.ElementName(lv.onlyElement));
                case "campaign.rule.few_hearts": return Loc.F(lv.ruleKey, lv.hearts);
                default: return Loc.T(lv.ruleKey);
            }
        }

        void RefreshModeHud(float dt)
        {
            if (_modeGoal != null && _match.Wave != _shownGoalWave)
            {
                _shownGoalWave = _match.Wave;
                var s = App.Meta.State;
                switch (_match.Mode)
                {
                    case MatchMode.Campaign:
                        _modeGoal.text = Loc.F("campaign.goal", Mathf.Min(_match.Wave, _match.WinWave), _match.WinWave);
                        break;
                    case MatchMode.Daily:
                    {
                        int next = 0;
                        foreach (var w in DailyChallenge.MilestoneWaves) if (w > _match.Wave) { next = w; break; }
                        _modeGoal.text = Loc.F("daily.goal", _match.Wave, s.dailyBest) + (next > 0 ? "   " + Loc.F("daily.next", next) : "");
                        break;
                    }
                    case MatchMode.Endless:
                        _modeGoal.text = Loc.F("endless.goal", _match.Wave, s.endlessBest, s.endlessRecord);
                        break;
                }
            }
            if (_raidPanel != null)
            {
                int hp = _match.RaidHp;
                if (hp != _shownRaidHp)
                {
                    _shownRaidHp = hp;
                    if (_match.RaidMaxHp <= 0) _raidBar.Set(1f, Loc.F("raid.arrives", _match.Rules.raidBossWave));
                    else _raidBar.Set(hp / (float)_match.RaidMaxHp, hp + " / " + _match.RaidMaxHp);
                }
                _assistCandy.interactable = _me.Candy >= Board.AssistCandyCost;
                _assistRage.interactable = _me.Candy >= Board.AssistRageCost && !_opp.IsRaging;
            }
        }

        void OnModeEvent(SimEvent e, bool mine)
        {
            switch (e.type)
            {
                case SimEventType.EnemySpawned:
                    if (mine && e.b >= 0 && e.b < App.Content.Enemies.Count) _seenEnemies.Add(App.Content.Enemies[e.b].id);
                    break;
                case SimEventType.RaidBossArrived:
                    if (mine) { AudioService.Play(Sfx.Boss); ShowBanner(Loc.T("raid.arrived"), Palette.Text, 2.2f, Palette.Red); }
                    break;
                case SimEventType.AssistGiven:
                    if (!mine) break;
                    if (e.b == _runner.LocalPlayer) { App.Toast(Loc.T(e.a == 0 ? "raid.got_candy" : "raid.got_rage")); AudioService.Play(Sfx.Reward, 0.7f); }
                    else AudioService.Play(Sfx.Send, 0.7f);   // our help left
                    break;
            }
        }

        public static Color ModeColor(MatchMode mode)
        {
            switch (mode)
            {
                case MatchMode.Daily: return Art.Hex(0xFFB020);
                case MatchMode.Endless: return Art.Hex(0x9B5CFF);
                case MatchMode.Campaign: return Art.Hex(0x4CC46B);
                case MatchMode.Mayhem: return Art.Hex(0xFF5FA2);
                case MatchMode.Raid: return Art.Hex(0xE5484D);
                case MatchMode.Tournament: return Art.Hex(0x3D8BFF);
                default: return Art.Hex(0x8A7FA6);
            }
        }

        /// <summary>Mode emblem from existing art: ZooArt/UI/mode_{name} if present, else a fitting icon.</summary>
        public static void ModeIcon(RectTransform holder, MatchMode mode)
        {
            var art = Art.Icon("mode_" + mode.ToString().ToLowerInvariant());
            var color = ModeColor(mode);
            UIKit.Icon(holder, "glow", Art.Glow, new Color(color.r, color.g, color.b, 0.6f)).rectTransform.Stretch(-20, -20, -20, -20);
            if (art != null) { UIKit.Icon(holder, "art", art, Color.white).rectTransform.Stretch(); return; }
            UIKit.Icon(holder, "disc", Art.Circle, Color.Lerp(color, Color.black, 0.25f)).rectTransform.Stretch(8, 8, 8, 8);
            string fallback;
            switch (mode)
            {
                case MatchMode.Daily: fallback = "star"; break;
                case MatchMode.Endless: fallback = "heart"; break;
                case MatchMode.Campaign: fallback = "chest_wood"; break;
                case MatchMode.Mayhem: fallback = "candy"; break;
                case MatchMode.Raid: fallback = "spell_meteor"; break;
                case MatchMode.Tournament: fallback = "trophy"; break;
                default: fallback = "xp"; break;
            }
            var s = Art.Icon(fallback);
            if (s != null) UIKit.Icon(holder, "icon", s, Color.white).rectTransform.Stretch(22, 22, 22, 22);
        }
    }
}
