using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    /// <summary>Deterministic xorshift32 RNG. Never use System.Random or UnityEngine.Random in the simulation.</summary>
    public struct DetRandom
    {
        uint _state;

        public DetRandom(int seed)
        {
            _state = (uint)seed ^ 0x9E3779B9u;
            if (_state == 0) _state = 0xA341316Cu;
            NextUInt(); NextUInt();
        }

        public uint State { get { return _state; } }

        /// <summary>Resume a generator from a saved State (used by the meta economy to persist its RNG).</summary>
        public static DetRandom FromState(uint state)
        {
            var r = new DetRandom(0);
            r._state = state == 0 ? 0xA341316Cu : state;
            return r;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform-ish integer in [0, max).</summary>
        public int Next(int max) { return max <= 1 ? 0 : (int)(NextUInt() % (uint)max); }

        /// <summary>True with the given whole-percent chance.</summary>
        public bool Chance(int percent) { return Next(100) < percent; }
    }

    public enum CommandType : byte
    {
        BuyCard = 1,    // A = shop slot
        Reroll = 2,
        Move = 3,       // A = from cell, B = to cell (merges if same unit + level, swaps otherwise)
        SendMonster = 4,// A = send-deck slot
        Surrender = 5,
        Place = 6,          // A = shop slot, B = cell (buy into a chosen cell)
        UpgradeElement = 7, // A = (int)Element
        CastSpell = 8,      // A = spell slot, B = target distance along the path (milli-tiles)
        ChoosePerk = 9,     // A = offer index
        PopBalloon = 10,    // A = balloon id
        Reflect = 11,       // A = index in this board's IncomingSends
        UseSkill = 12,      // A = cell
        Emote = 13,         // A = emote id (presentation only)
        Summon = 14,        // A = cell (-1 = random free cell): a random animal from the deck
        UpgradeUnit = 15,   // A = deck slot: powers up that animal for the rest of the match
        SummonUnit = 16,    // A = deck slot, B = cell (-1 = random free cell): summon a chosen animal (costs more)
    }

    /// <summary>
    /// A player input. The whole match is a pure function of (seed, setup, command list), which is
    /// what lockstep networking and server-side replay validation rely on.
    /// </summary>
    [Serializable]
    public struct Command
    {
        public int tick;
        public int player;
        public CommandType type;
        public int a;
        public int b;

        public Command(int tick, int player, CommandType type, int a = 0, int b = 0)
        {
            this.tick = tick; this.player = player; this.type = type; this.a = a; this.b = b;
        }

        public override string ToString() { return "[" + tick + "] P" + player + " " + type + " " + a + "," + b; }
    }

    public sealed class UnitInstance
    {
        public int uid;
        public int def;          // index into ContentDatabase.Units
        public int level = 1;
        public int cell;
        public int cooldown;
        public int stunTicks;
        public int lastTargetUid;   // presentation: who it shot last
        public int energy;          // active skill charge (ticks)
    }

    public sealed class EnemyInstance
    {
        public int uid;
        public int def;          // index into ContentDatabase.Enemies
        public Element element;
        public int hp;
        public int maxHp;
        public int distance;     // milli-tiles along the path
        public int prevDistance; // for render interpolation
        public int slowPercent;
        public int slowTicks;
        public int stunTicks;
        public int burnTicks;
        public int burnDamage;   // per burn pulse
        public bool revealed;
        public int sender = -1;  // player index who sent it, -1 for wave enemies
        public int skillTimer;
        public bool alive = true;
    }

    public struct PendingSpawn
    {
        public int tick;
        public int enemyDef;
        public Element element;
        public int hpPermille;
        public int sender;
        public int startDistance;
    }

    public enum SimEventType : byte
    {
        WaveStarted, BossIncoming, SendIncoming, EnemySpawned, UnitAttacked, EnemyKilled, EnemyLeaked,
        UnitPlaced, UnitMoved, UnitsSwapped, UnitMerged, UnitLevelUp, UnitStunned, CellLocked, CellsUnlocked,
        CandyGained, CandyStolen, SendQueued, CommandRejected, MatchEnded,
        // a = element, b = new level
        ElementUpgraded,
        // a = spell def, b = target distance (-1 none), c = slot
        SpellCast,
        // a = offer count (read Board.PerkOffer)
        PerkOffered,
        // a = perk type, b = 1 if auto-picked
        PerkChosen,
        // a = balloon id, b = lane
        BalloonSpawned,
        // a = balloon id, b = candy
        BalloonPopped,
        BalloonExpired,
        // a = send def, b = board it now goes to
        SendReflected,
        // a = unit uid, b = target distance (-1 none), c = (int)Role
        SkillUsed,
        // a = cell count
        StarCellsMoved,
        // a = emote id
        Emote,
        // a = deck slot, b = new level
        UnitUpgraded,
        // a = candy won, b = loser board (boss duel: this board killed the shared boss first)
        BossDuelWon,
    }

    public enum RejectReason : byte
    {
        None, NotEnoughCandy, NotEnoughPoints, BoardFull, InvalidCell, CellLocked, OpponentWaveFull,
        AlreadyUsed, MaxLevel, InvalidSlot, MatchOver,
        Cooldown, NotReady, NoTarget, NotOffered,
        NotOnBoard,
    }

    /// <summary>One-shot notification for presentation (VFX, sounds, toasts). Never read back by the sim.</summary>
    public struct SimEvent
    {
        public SimEventType type;
        public int board;
        public int a, b, c;
        public RejectReason reason;

        public SimEvent(SimEventType type, int board, int a = 0, int b = 0, int c = 0, RejectReason reason = RejectReason.None)
        {
            this.type = type; this.board = board; this.a = a; this.b = b; this.c = c; this.reason = reason;
        }
    }

    [Serializable]
    public class PlayerSetup
    {
        public string name = "Player";
        public string[] deck;
        public string[] sendDeck;
        /// <summary>Out-of-match card level (1-15) per deck entry. Missing = level 1.</summary>
        public int[] cardLevels;
        /// <summary>Keeper spell ids (up to 4). Missing = DefaultContent.DefaultSpells.</summary>
        public string[] spells;
    }

    [Serializable]
    public class MatchSetup
    {
        public int seed;
        public PlayerSetup[] players = new PlayerSetup[2];
    }

    public static class PathMath
    {
        public static int Length(BalanceConfig b)
        {
            int total = 0;
            for (int i = 1; i < b.pathX.Length; i++)
                total += Math.Abs(b.pathX[i] - b.pathX[i - 1]) + Math.Abs(b.pathY[i] - b.pathY[i - 1]);
            return total;
        }

        /// <summary>Position on the path (axis-aligned segments) for a distance in milli-tiles.</summary>
        public static void Point(BalanceConfig b, int distance, out int x, out int y)
        {
            if (distance <= 0) { x = b.pathX[0]; y = b.pathY[0]; return; }
            for (int i = 1; i < b.pathX.Length; i++)
            {
                int dx = b.pathX[i] - b.pathX[i - 1];
                int dy = b.pathY[i] - b.pathY[i - 1];
                int len = Math.Abs(dx) + Math.Abs(dy);
                if (distance <= len)
                {
                    x = b.pathX[i - 1] + Math.Sign(dx) * Math.Min(distance, Math.Abs(dx));
                    y = b.pathY[i - 1] + Math.Sign(dy) * Math.Min(distance, Math.Abs(dy));
                    return;
                }
                distance -= len;
            }
            x = b.pathX[b.pathX.Length - 1];
            y = b.pathY[b.pathY.Length - 1];
        }

        public static void CellCenter(BalanceConfig b, int cell, out int x, out int y)
        {
            x = (cell % b.gridCols + 1) * 1000;
            y = (cell / b.gridCols + 1) * 1000;
        }
    }
}
