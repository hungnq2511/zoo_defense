using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    /// <summary>
    /// One 1v1 match: two boards that receive the same waves, plus the send-monster link between them.
    /// Fully deterministic: same content + setup + commands => same result on every machine.
    /// Call Step() TickRate times per second (see MatchRunner in the Game assembly).
    /// </summary>
    public sealed class Match
    {
        public const int HardWaveCap = 60;

        public readonly ContentDatabase Content;
        public readonly BalanceConfig Balance;
        public readonly MatchSetup Setup;
        public readonly Board[] Boards = new Board[2];
        public readonly int PathLength;

        public int Tick { get; private set; }
        public int Wave { get; private set; }
        public int WaveStartTick { get; private set; }
        public int NextWaveTick { get; private set; }
        public int WaveHpPermille { get; private set; }
        public bool IsOver { get; private set; }
        /// <summary>Winning player index, or -1 for a draw. Only valid when IsOver.</summary>
        public int Winner { get; private set; }
        public bool IsSuddenDeath { get { return Wave >= Balance.suddenDeathWave; } }
        public int TicksUntilNextWave { get { return Math.Max(0, NextWaveTick - Tick); } }
        /// <summary>Cells with a damage bonus, the same on both boards (fair), moved every few waves.</summary>
        public readonly int[] StarCells;
        /// <summary>Game mode rules (null = normal 1v1).</summary>
        public readonly MatchRules Rules;
        public MatchMode Mode { get { return Rules != null ? Rules.mode : MatchMode.Versus; } }
        /// <summary>PvE: only board 0 plays.</summary>
        public bool IsSolo { get { return Rules != null && Rules.solo; } }
        /// <summary>Raid: both boards are one team.</summary>
        public bool IsCoop { get { return Rules != null && Rules.coop; } }
        /// <summary>Shared HP of the raid boss (both boards hit the same pool). 0 before it arrives.</summary>
        public int RaidHp { get; private set; }
        public int RaidMaxHp { get; private set; }
        public bool RaidDefeated { get; private set; }
        /// <summary>Waves to survive to win (campaign), 0 = none.</summary>
        public int WinWave { get { return Rules != null ? Rules.winWave : 0; } }
        /// <summary>Element most monsters of the current wave have (announced one wave ahead as NextWaveElement).</summary>
        public Element WaveElement { get; private set; }
        public Element NextWaveElement { get; private set; }
        readonly int _bossOffset;

        /// <summary>Events produced by the last Step(). Cleared at the start of every Step.</summary>
        public readonly List<SimEvent> Events = new List<SimEvent>();
        /// <summary>Every command in the order it was executed, with the tick it ran on (replay log).</summary>
        public readonly List<Command> ExecutedCommands = new List<Command>();

        readonly List<Command> _pending = new List<Command>();
        readonly List<PendingSpawn> _waveSpawns = new List<PendingSpawn>();
        DetRandom _rng;
        int _nextUid = 1;

        public Match(ContentDatabase content, MatchSetup setup)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            Rules = setup.rules;
            if (Rules != null) content = content.WithBalance(Rules.Apply(content.Balance));
            Content = content;
            Balance = content.Balance;
            PathLength = PathMath.Length(Balance);
            _rng = new DetRandom(setup.seed);
            WaveHpPermille = Math.Max(100, Balance.waveHpStartPermille);
            NextWaveTick = Balance.firstWaveDelayTicks;
            Winner = -1;
            BossDuelWinner = -1;
            StarCells = new int[Math.Max(0, Balance.starCellCount)];
            PickStarCells();
            NextWaveElement = RandomElement(ref _rng);
            _bossOffset = Balance.bossEnemyIds.Length > 0 ? (setup.seed & 0x7fffffff) % Balance.bossEnemyIds.Length : 0;
            for (int i = 0; i < 2; i++)
                Boards[i] = new Board(this, i, setup.players[i], new DetRandom(setup.seed * 31 + 7 + i * 1013));
            if (!IsSolo && !IsCoop && Balance.waveHpCardScalePercent > 0)
            {
                long sum = 0; int n = 0;
                for (int i = 0; i < 2; i++)
                    foreach (var d in Boards[i].Deck) { sum += Boards[i].CardPercent(d); n++; }
                int avg = n > 0 ? (int)(sum / n) : 100;
                int scale = 100 + (avg - 100) * Balance.waveHpCardScalePercent / 100;
                WaveHpPermille = Math.Max(100, (int)((long)WaveHpPermille * scale / 100));
            }
        }

        void PickStarCells()
        {
            int cells = Balance.gridCols * Balance.gridRows;
            for (int k = 0; k < StarCells.Length; k++)
            {
                int c;
                bool dup;
                do
                {
                    c = _rng.Next(cells);
                    dup = false;
                    for (int j = 0; j < k; j++) if (StarCells[j] == c) dup = true;
                } while (dup);
                StarCells[k] = c;
            }
        }

        public bool IsStarCell(int cell)
        {
            for (int i = 0; i < StarCells.Length; i++) if (StarCells[i] == cell) return true;
            return false;
        }

        /// <summary>Boss duel of the current boss wave: 0 = none, otherwise the wave number.</summary>
        public int BossDuelWave { get; private set; }
        /// <summary>Board that killed the shared boss first (-1 = not yet / nobody).</summary>
        public int BossDuelWinner { get; private set; }
        public bool IsBossDuelOpen { get { return BossDuelWave > 0 && BossDuelWinner < 0; } }

        internal void WinBossDuel(int board)
        {
            if (!IsBossDuelOpen) return;
            BossDuelWinner = board;
            int candy = Balance.bossDuelCandy + 10 * Wave;
            Boards[board].GainCandy(candy);
            int remnant = Content.SendIndex(Balance.bossDuelSendId);
            if (remnant >= 0) Boards[1 - board].ReceiveSend(remnant, true);
            for (int i = 0; i < 2; i++) Emit(new SimEvent(SimEventType.BossDuelWon, i, candy, 1 - board, board));
        }

        public bool IsBossWave { get { return Balance.bossEveryNWaves > 0 && Wave > 0 && Wave % Balance.bossEveryNWaves == 0; } }

        internal int NewUid() { return _nextUid++; }
        internal void Emit(SimEvent e) { Events.Add(e); }

        /// <summary>
        /// Queue a command. Commands for a tick that already ran execute on the current tick.
        /// Order inside a tick is (player, submission order) so both peers agree.
        /// </summary>
        public void Submit(Command c)
        {
            if (c.player < 0 || c.player > 1) return;
            if (c.tick < Tick) c.tick = Tick;
            int i = _pending.Count;
            while (i > 0 && (_pending[i - 1].tick > c.tick || (_pending[i - 1].tick == c.tick && _pending[i - 1].player > c.player))) i--;
            _pending.Insert(i, c);
        }

        public void Step()
        {
            if (IsOver) return;
            Events.Clear();
            Boards[0].BeginTick();
            Boards[1].BeginTick();

            while (_pending.Count > 0 && _pending[0].tick <= Tick && !IsOver)
            {
                var c = _pending[0];
                _pending.RemoveAt(0);
                c.tick = Tick;
                ExecutedCommands.Add(c);
                Execute(c);
            }

            if (!IsOver)
            {
                if (Tick >= NextWaveTick)
                {
                    // Campaign goal: the last wave's timer is over and its monsters are gone.
                    if (WinWave > 0 && Wave >= WinWave)
                    {
                        if (Boards[0].Enemies.Count == 0 && !Boards[0].HasPendingSpawns) End(0);
                    }
                    else StartNextWave();
                }
                if (!IsOver)
                {
                    Boards[0].Simulate();
                    if (!IsSolo) Boards[1].Simulate();
                    CheckEnd();
                }
            }
            Tick++;
        }

        /// <summary>Run until the match ends or maxTicks pass. Used by tests, bots and replay validation.</summary>
        public void RunUntil(int maxTick) { while (!IsOver && Tick < maxTick) Step(); }

        void Execute(Command c)
        {
            var board = Boards[c.player];
            RejectReason r;
            if (IsSolo && c.player == 1) return;   // the idle board takes no commands
            if ((IsSolo || IsCoop) && c.type == CommandType.SendMonster) { Emit(new SimEvent(SimEventType.CommandRejected, c.player, (int)c.type, c.a, c.b, RejectReason.InvalidSlot)); return; }
            switch (c.type)
            {
                case CommandType.BuyCard: r = board.Buy(c.a); break;
                case CommandType.Reroll: r = board.Reroll(); break;
                case CommandType.Move: r = board.Move(c.a, c.b); break;
                case CommandType.SendMonster: r = board.SendMonster(c.a); break;
                case CommandType.Surrender: End(1 - c.player); r = RejectReason.None; break;
                case CommandType.Place: r = board.Buy(c.a, c.b); break;
                case CommandType.UpgradeElement: r = board.UpgradeElement(c.a); break;
                case CommandType.CastSpell: r = board.CastSpell(c.a, c.b); break;
                case CommandType.ChoosePerk: r = board.ChoosePerk(c.a, false); break;
                case CommandType.PopBalloon: r = board.PopBalloon(c.a); break;
                case CommandType.Reflect: r = board.Reflect(c.a); break;
                case CommandType.UseSkill: r = board.UseSkill(c.a); break;
                case CommandType.Summon: r = board.Summon(c.a); break;
                case CommandType.UpgradeUnit: r = board.UpgradeUnit(c.a); break;
                case CommandType.SummonUnit: r = board.SummonUnit(c.a, c.b); break;
                case CommandType.Emote: Emit(new SimEvent(SimEventType.Emote, c.player, c.a)); r = RejectReason.None; break;
                case CommandType.Assist: r = IsCoop ? board.Assist(c.a) : RejectReason.InvalidSlot; break;
                case CommandType.Sell: r = board.Sell(c.a); break;
                case CommandType.SetTarget: r = board.SetTarget(c.a, c.b); break;
                case CommandType.Hex: r = (IsSolo || IsCoop) ? RejectReason.InvalidSlot : board.Hex(c.a); break;
                case CommandType.Evolve: r = board.Evolve(c.a); break;
                default: r = RejectReason.InvalidSlot; break;
            }
            if (r != RejectReason.None)
                Emit(new SimEvent(SimEventType.CommandRejected, c.player, (int)c.type, c.a, c.b, r));
        }

        void StartNextWave()
        {
            if (Wave > 0)
            {
                int bonus = Balance.waveBonusBase + Balance.waveBonusPerWave * Wave;
                Boards[0].GainCandy(bonus);
                if (!IsSolo) Boards[1].GainCandy(bonus);
            }

            Wave++;
            WaveStartTick = Tick;
            NextWaveTick = Tick + Balance.waveDurationTicks;
            if (Wave > 1) WaveHpPermille = (int)((long)WaveHpPermille * (100 + Balance.hpGrowthPercentPerWave) / 100);
            if (Wave >= Balance.suddenDeathWave) WaveHpPermille = (int)((long)WaveHpPermille * (100 + Balance.suddenDeathHpPercent) / 100);

            if (Balance.starCellMoveEveryWaves > 0 && Wave > 1 && (Wave - 1) % Balance.starCellMoveEveryWaves == 0)
            {
                PickStarCells();
                for (int i = 0; i < 2; i++) Emit(new SimEvent(SimEventType.StarCellsMoved, i, StarCells.Length));
            }

            if (!IsSolo && !IsCoop && Balance.bossEveryNWaves > 0 && Wave % Balance.bossEveryNWaves == 0) { BossDuelWave = Wave; BossDuelWinner = -1; }
            else BossDuelWave = 0;

            WaveElement = NextWaveElement;
            NextWaveElement = RandomElement(ref _rng);
            if (Rules != null && Rules.waveElements != null && Rules.waveElements.Length > 0)
            {
                WaveElement = (Element)Rules.waveElements[(Wave - 1) % Rules.waveElements.Length];
                NextWaveElement = (Element)Rules.waveElements[Wave % Rules.waveElements.Length];
            }
            BuildWave(_waveSpawns);
            Boards[0].OnWaveStart(_waveSpawns);
            if (!IsSolo) Boards[1].OnWaveStart(_waveSpawns);
            if (Balance.perkEveryNWaves > 0 && Wave % Balance.perkEveryNWaves == 0)
            {
                Boards[0].OfferPerks();
                if (!IsSolo) Boards[1].OfferPerks();
            }
            if (IsCoop && Wave == Rules.raidBossWave) SpawnRaidBoss();

            for (int i = 0; i < 2; i++)
            {
                Emit(new SimEvent(SimEventType.WaveStarted, i, Wave, IsSuddenDeath ? 1 : 0, (int)WaveElement));
                if (Wave % Balance.bossEveryNWaves == 0) Emit(new SimEvent(SimEventType.BossIncoming, i, Wave));
            }
        }

        /// <summary>The shared part of a wave: both players face exactly the same enemies.</summary>
        void BuildWave(List<PendingSpawn> list)
        {
            list.Clear();
            var b = Balance;
            int count = Math.Min(b.maxEnemiesPerWave, b.baseEnemiesPerWave + b.enemiesPerWaveStep * (Wave - 1));
            for (int i = 0; i < count; i++)
            {
                var sp = Spawn(b.normalEnemyIds[_rng.Next(b.normalEnemyIds.Length)], Tick + i * b.spawnWindowTicks / count);
                if (_rng.Chance(b.waveElementPercent)) sp.element = WaveElement;   // "Water wave": plan your counters
                list.Add(sp);
            }

            if (b.eliteEveryNWaves > 0 && Wave % b.eliteEveryNWaves == 0)
            {
                int elites = 1 + Wave / 9;
                for (int k = 0; k < elites; k++)
                    list.Add(Spawn(b.eliteEnemyIds[_rng.Next(b.eliteEnemyIds.Length)], Tick + b.spawnWindowTicks * (k + 1) / (elites + 1)));
            }

            if (b.bossEveryNWaves > 0 && Wave % b.bossEveryNWaves == 0)
            {
                string bossId = b.bossEnemyIds[(Wave / b.bossEveryNWaves - 1 + _bossOffset) % b.bossEnemyIds.Length];
                list.Add(Spawn(bossId, Tick + b.spawnWindowTicks / 2));
            }
        }

        PendingSpawn Spawn(string enemyId, int tick)
        {
            return new PendingSpawn
            {
                tick = tick,
                enemyDef = Content.EnemyIndex(enemyId),
                element = RandomElement(ref _rng),
                hpPermille = WaveHpPermille,
                sender = -1,
            };
        }

        internal static Element RandomElement(ref DetRandom rng)
        {
            switch (rng.Next(3)) { case 0: return Element.Fire; case 1: return Element.Water; default: return Element.Grass; }
        }

        void SpawnRaidBoss()
        {
            int def = Content.EnemyIndex(Rules.raidBossId);
            if (def < 0) return;
            RaidMaxHp = RaidHp = (int)Math.Max(1L, (long)Content.Enemies[def].hp * WaveHpPermille / 1000 * Rules.raidHpPercent / 100);
            for (int i = 0; i < 2; i++)
            {
                var e = Boards[i].SpawnRaidBoss(def, RaidHp, Rules.raidSpeedPercent, WaveElement);
                Emit(new SimEvent(SimEventType.RaidBossArrived, i, e.uid, RaidHp));
            }
        }

        /// <summary>Damage dealt to the raid boss on either board goes to the shared pool.</summary>
        internal void DamageRaid(int dmg)
        {
            if (RaidHp <= 0) return;
            RaidHp = Math.Max(0, RaidHp - dmg);
            for (int i = 0; i < 2; i++) Boards[i].SyncRaidBoss(RaidHp);
            if (RaidHp == 0) RaidDefeated = true;
        }

        void CheckEnd()
        {
            if (IsSolo)
            {
                if (Boards[0].Hearts <= 0) End(1);
                else if (Wave > 500) End(1);
                return;
            }
            if (IsCoop)
            {
                if (RaidDefeated) End(0);
                else if (Boards[0].Hearts <= 0 || Boards[1].Hearts <= 0) End(1);   // the team falls together
                else if (Wave > HardWaveCap) End(1);
                return;
            }
            bool dead0 = Boards[0].Hearts <= 0, dead1 = Boards[1].Hearts <= 0;
            if (dead0 && dead1) End(-1);
            else if (dead0) End(1);
            else if (dead1) End(0);
            else if (Wave > HardWaveCap)
            {
                // Safety net so a match can never run forever.
                int h0 = Boards[0].Hearts, h1 = Boards[1].Hearts;
                End(h0 == h1 ? -1 : (h0 > h1 ? 0 : 1));
            }
        }

        void End(int winner)
        {
            if (IsOver) return;
            IsOver = true;
            Winner = winner;
            Emit(new SimEvent(SimEventType.MatchEnded, 0, winner));
            Emit(new SimEvent(SimEventType.MatchEnded, 1, winner));
        }

        /// <summary>FNV-1a hash of the full simulation state; compared by the server to validate replays.</summary>
        public uint StateHash()
        {
            uint h = 2166136261u;
            Mix(ref h, Tick); Mix(ref h, Wave); Mix(ref h, WaveHpPermille); Mix(ref h, IsOver ? 1 : 0); Mix(ref h, Winner);
            Mix(ref h, (int)_rng.State);
            for (int i = 0; i < StarCells.Length; i++) Mix(ref h, StarCells[i]);
            Mix(ref h, BossDuelWave); Mix(ref h, BossDuelWinner);
            Mix(ref h, (int)WaveElement * 8 + (int)NextWaveElement);
            Mix(ref h, RaidHp);
            for (int i = 0; i < 2; i++) Boards[i].HashInto(ref h);
            return h;
        }

        internal static void Mix(ref uint h, int v)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++) { h ^= (byte)(v >> (i * 8)); h *= 16777619u; }
            }
        }
    }
}
