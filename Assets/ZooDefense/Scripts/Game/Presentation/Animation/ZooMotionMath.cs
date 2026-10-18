using System;

namespace ZooDefense.Animation
{
    [Serializable]
    public sealed class ZooMotionProfile
    {
        public string id, name, element, role, description, attackStyle;
        public bool enemy;
        public float idleSeconds = 2f, idleBob = .008f, idleTilt = 1f;
        public float moveHz = 2f, moveBob = .025f, moveTilt = 3f;
        public float attackSeconds = .55f, attackStrength = 1f, cueAt = .45f;
    }

    public enum ZooMotionState { Idle, Move, Attack, Hit, Spawn, Death }

    public struct ZooPose
    {
        // Position is a fraction of the unscaled sprite's height; pivot stays at the feet.
        public float x, y, sx, sy, angle, alpha, tint;
        public static ZooPose Identity => new ZooPose { sx = 1, sy = 1, alpha = 1 };
    }

    /// <summary>Pure, allocation-free presentation math. Contains no gameplay or Unity dependency.</summary>
    public static class ZooMotionMath
    {
        const float Pi = (float)Math.PI;
        static float Sin(float n) => (float)Math.Sin(n);
        static float Clamp(float n) => Math.Max(0f, Math.Min(1f, n));
        static float Smooth(float n) { n = Clamp(n); return n * n * (3f - 2f * n); }
        public static float Duration(ZooMotionProfile p, ZooMotionState state)
        {
            switch (state)
            {
                case ZooMotionState.Attack: return Math.Max(.1f, p.attackSeconds);
                case ZooMotionState.Hit: return .28f;
                case ZooMotionState.Spawn: return .44f;
                case ZooMotionState.Death: return .48f;
                default: return float.PositiveInfinity;
            }
        }
        public static ZooPose Evaluate(ZooMotionProfile p, ZooMotionState state, float seconds, float phase = 0)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            var r = ZooPose.Identity;
            seconds = Math.Max(0f, seconds);
            float t, s;
            switch (state)
            {
                case ZooMotionState.Idle:
                    s = Sin(seconds / Math.Max(.1f, p.idleSeconds) * 2 * Pi + phase);
                    r.sy = 1 + .012f * s; r.sx = 1 - .006f * s;
                    r.y = p.idleBob * (1 + s) * .5f;
                    r.angle = p.idleTilt * s;
                    break;
                case ZooMotionState.Move:
                    s = Sin(seconds * Math.Max(.1f, p.moveHz) * 2 * Pi + phase);
                    r.y = p.moveBob * Math.Abs(s);
                    r.angle = p.moveTilt * s;
                    r.sx = 1 + .018f * s; r.sy = 1 - .018f * s;
                    break;
                case ZooMotionState.Attack:
                    t = Clamp(seconds / Duration(p, state));
                    var wind = t < .28f ? Sin(t / .28f * Pi) : 0;
                    var strike = t >= .28f && t < .76f ? Sin((t - .28f) / .48f * Pi) : 0;
                    var power = p.attackStrength;
                    r.sx += .035f * wind * power; r.sy -= .045f * wind * power;
                    switch (p.attackStyle)
                    {
                        case "recoil": r.x = .050f * strike * power; r.angle = 7 * strike * power; break;
                        case "lunge": r.x = -.065f * strike * power; r.angle = -9 * strike * power; break;
                        case "roll": r.x = -.045f * strike * power; r.angle = 16 * strike * power; break;
                        case "cast": r.y = .040f * strike * power; r.sx += .025f * strike; r.sy += .025f * strike; break;
                        case "pulse": r.sx += .055f * strike * power; r.sy -= .035f * strike * power; break;
                        case "stomp": r.y = .055f * wind * power; r.sx += .045f * strike; r.sy -= .060f * strike; break;
                        default: r.x = -.042f * strike * power; r.angle = 7 * wind - 11 * strike * power; break;
                    }
                    break;
                case ZooMotionState.Hit:
                    t = Clamp(seconds / Duration(p, state));
                    r.x = .022f * Sin(9 * Pi * t) * (1 - t);
                    r.angle = 3 * Sin(7 * Pi * t) * (1 - t);
                    r.sx += .035f * Sin(Pi * t); r.sy -= .035f * Sin(Pi * t);
                    r.tint = Sin(Pi * t);
                    break;
                case ZooMotionState.Spawn:
                    t = Clamp(seconds / Duration(p, state));
                    var q = t - 1;
                    var pop = 1 + 2.70158f * q * q * q + 1.70158f * q * q;
                    r.sx = Math.Max(0, pop); r.sy = Math.Max(0, pop);
                    r.alpha = Smooth(t / .35f);
                    break;
                case ZooMotionState.Death:
                    t = Clamp(seconds / Duration(p, state));
                    r.alpha = 1 - Smooth(t);
                    r.sx = 1 - .20f * t; r.sy = 1 - .55f * t;
                    r.y = -.08f * t; r.angle = -12 * t;
                    break;
            }
            return r;
        }
    }
}
