using System;
using System.Collections.Generic;
using UnityEngine;
using ZooDefense.Animation;

namespace ZooDefense.Game
{
    /// <summary>
    /// Per-species motion profiles from the Zoo Defense Animation Pack (Resources/ZooArt/motion_profiles.json).
    /// Wave enemies and bosses without a profile get a generic walking profile.
    /// </summary>
    public static class MotionLibrary
    {
        [Serializable]
        sealed class ProfileFile { public ZooMotionProfile[] profiles; }

        static Dictionary<string, ZooMotionProfile> _byId;

        public static readonly ZooMotionProfile DefaultUnit = new ZooMotionProfile
        {
            id = "default_unit", attackStyle = "throw", idleSeconds = 2f, idleBob = .006f, idleTilt = 1f,
            moveHz = 2.4f, moveBob = .03f, moveTilt = 3f, attackSeconds = .5f, attackStrength = 1f, cueAt = .45f,
        };

        public static readonly ZooMotionProfile DefaultEnemy = new ZooMotionProfile
        {
            id = "default_enemy", enemy = true, attackStyle = "stomp", idleSeconds = 1.8f, idleBob = .006f, idleTilt = 1.2f,
            moveHz = 2.2f, moveBob = .035f, moveTilt = 4f, attackSeconds = .5f, attackStrength = 1f, cueAt = .45f,
        };

        public static ZooMotionProfile Get(string id, bool enemy)
        {
            if (_byId == null) Load();
            ZooMotionProfile p;
            if (id != null && _byId.TryGetValue(id, out p)) return p;
            return enemy ? DefaultEnemy : DefaultUnit;
        }

        static readonly Dictionary<string, ZooMotionProfile> _sheet = new Dictionary<string, ZooMotionProfile>();

        /// <summary>
        /// Same profile but without the procedural walk bob/tilt: used when a hand-made walk sheet already animates
        /// the legs. Hit, spawn and death reactions stay.
        /// </summary>
        public static ZooMotionProfile GetForWalkSheet(string id)
        {
            ZooMotionProfile p;
            if (id != null && _sheet.TryGetValue(id, out p)) return p;
            var src = Get(id, true);
            p = JsonUtility.FromJson<ZooMotionProfile>(JsonUtility.ToJson(src)) ?? new ZooMotionProfile();
            p.moveBob = 0f;
            p.moveTilt = 0f;
            p.idleBob = 0f;
            p.idleTilt = 0f;
            if (id != null) _sheet[id] = p;
            return p;
        }

        static void Load()
        {
            _byId = new Dictionary<string, ZooMotionProfile>();
            var text = Resources.Load<TextAsset>("ZooArt/motion_profiles");
            if (text == null) return;
            try
            {
                var file = JsonUtility.FromJson<ProfileFile>(text.text);
                if (file != null && file.profiles != null)
                    foreach (var p in file.profiles) if (p != null && !string.IsNullOrEmpty(p.id)) _byId[p.id] = p;
            }
            catch (Exception e) { Debug.LogWarning("[ZooDefense] motion_profiles.json unreadable: " + e.Message); }
        }
    }

    /// <summary>
    /// Plays the pack's procedural states (idle, move, attack, hit, spawn, death) on a "visual" transform whose
    /// origin sits at the character's feet. Presentation only: gameplay never waits for it.
    /// </summary>
    public sealed class MotionDriver
    {
        ZooMotionProfile _profile;
        ZooMotionState _state;
        float _elapsed, _phase;
        bool _moving;

        public ZooPose Pose { get; private set; }
        public ZooMotionState State { get { return _state; } }
        public bool DeathFinished { get { return _state == ZooMotionState.Death && _elapsed >= ZooMotionMath.Duration(_profile, ZooMotionState.Death); } }

        public void Bind(ZooMotionProfile profile, bool moving, int seed)
        {
            _profile = profile;
            _moving = moving;
            _phase = ((seed & 1023) * .61803398875f % 1f) * Mathf.PI * 2f;
            Play(ZooMotionState.Spawn, true);
        }

        public void Play(ZooMotionState state, bool force = false)
        {
            if (!force)
            {
                if (_state == ZooMotionState.Death) return;
                if (state == ZooMotionState.Attack && _state == ZooMotionState.Attack) return;
                if (state == ZooMotionState.Hit && (_state == ZooMotionState.Hit || _state == ZooMotionState.Spawn)) return;
            }
            _state = state;
            _elapsed = 0f;
        }

        public void Tick(float dt)
        {
            if (_profile == null) { Pose = ZooPose.Identity; return; }
            _elapsed += dt;
            float duration = ZooMotionMath.Duration(_profile, _state);
            if (_elapsed >= duration && _state != ZooMotionState.Death)
            {
                _state = _moving ? ZooMotionState.Move : ZooMotionState.Idle;
                _elapsed = 0f;
            }
            Pose = ZooMotionMath.Evaluate(_profile, _state, Mathf.Min(_elapsed, 1000f), _phase);
        }

        /// <summary>Applies the pose to a feet-anchored transform. height = world height used for offsets.</summary>
        public void Apply(Transform visual, Vector3 basePosition, float height, float scale)
        {
            var p = Pose;
            visual.localPosition = basePosition + new Vector3(p.x * height, p.y * height, 0f);
            visual.localRotation = Quaternion.Euler(0f, 0f, p.angle);
            visual.localScale = new Vector3(scale * p.sx, scale * p.sy, 1f);
        }

        /// <summary>Hit tint and fade from the current pose applied over a base colour.</summary>
        public Color Tint(Color baseColor)
        {
            var p = Pose;
            var c = Color.Lerp(baseColor, new Color(1f, .45f, .45f, baseColor.a), p.tint * .65f);
            c.a = baseColor.a * p.alpha;
            return c;
        }
    }
}
