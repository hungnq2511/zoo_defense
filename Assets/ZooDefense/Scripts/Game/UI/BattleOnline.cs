using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>Online 1v1 parts of the battle: network pump, "waiting for opponent" overlay, leaving cleanly.</summary>
    public sealed partial class BattleScreen
    {
        OnlineLockstepTransport _online;
        RectTransform _waitPanel;
        Text _waitText;
        bool _leftOnline, _desyncLogged, _reconnectLogged;
        double _dropStarted = -1;

        bool IsOnline { get { return _cfg != null && _cfg.online != null; } }

        void OnlineTick(float dt)
        {
            if (!IsOnline || _leftOnline) return;
            if (Online.Pvp != null) Online.Pvp.Update(dt);
            var sync = _online != null ? _online.Sync : null;
            if (sync == null) return;

            bool waiting = !_match.IsOver && !sync.Abandoned && sync.SilentFor > 1.2;
            if (waiting && _dropStarted < 0) _dropStarted = sync.SilentFor;
            if (!waiting && _dropStarted >= 0)
            {
                // The connection came back (or the match ended): one analytics event per drop.
                if (!_match.IsOver || sync.Abandoned) Track.Reconnect(!sync.Abandoned, Mathf.RoundToInt((float)sync.SilentFor));
                _dropStarted = -1;
            }
            if (waiting && _waitPanel == null) BuildWaitPanel();
            if (_waitPanel != null)
            {
                _waitPanel.gameObject.SetActive(waiting);
                if (waiting)
                {
                    int dots = 1 + (int)(_t * 3f) % 3;
                    // Our own network is gone -> "reconnecting"; we are online -> the opponent is the one coming back.
                    string line = sync.WeAreOffline ? Loc.T("online.reconnecting") : Loc.F("online.opp_reconnecting", _opp.PlayerName);
                    int left = Mathf.Max(0, Mathf.CeilToInt((float)(_online.Timeout - sync.SilentFor)));
                    _waitText.text = line + new string('.', dots) + "\n" + left + "s";
                    _waitPanel.SetAsLastSibling();
                }
            }
            if (sync.Abandoned && !_reconnectLogged)
            {
                _reconnectLogged = true;
                if (sync.LostByDisconnect) Debug.LogWarning("[Online] could not reconnect in time: match lost");
            }

            if (sync.Desynced && !_desyncLogged)
            {
                _desyncLogged = true;
                Debug.LogError("[Online] desync at tick " + sync.DesyncTick);
                Services.Analytics.LogEvent("online_desync", new Dictionary<string, object> { { "tick", sync.DesyncTick } });
            }
        }

        void BuildWaitPanel()
        {
            _waitPanel = UIKit.Panel(Root, "onlineWait", new Color(0f, 0f, 0f, 0.55f), false).rectTransform;
            _waitPanel.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(860, 220));
            _waitText = UIKit.Outlined(UIKit.Label(_waitPanel, "", 40, Palette.Text), 2.5f);
            _waitText.rectTransform.Stretch(20, 20, 10, 10);
        }

        /// <summary>Tell the opponent we are done and leave the online game (once).</summary>
        void LeaveOnline()
        {
            if (!IsOnline || _leftOnline) return;
            _leftOnline = true;
            if (_online != null && _online.Sync != null) _online.Sync.SendBye();
            if (Online.Pvp != null) Online.Pvp.Leave();
            if (_waitPanel != null) _waitPanel.gameObject.SetActive(false);
        }
    }
}
