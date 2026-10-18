using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    /// <summary>One player's side: 5x4 grid of animals, the enemy path, candy, hearts and send points.</summary>
    public sealed class Board
    {
        public readonly int Index;
        public readonly string PlayerName;
        public readonly UnitInstance[] Cells;
        public readonly int[] LockedUntilWave;
        public readonly List<EnemyInstance> Enemies = new List<EnemyInstance>();
        public readonly int[] Deck;        // unit def indices
        public readonly int[] SendDeck;    // send def indices
        public readonly bool[] SendUsed;
        public readonly int[] Shop;        // unit def index per shop slot
        /// <summary>Send defs the opponent queued for this board's next wave.</summary>
        public readonly List<int> IncomingSends = new List<int>();

        public int Hearts { get; private set; }
        public int Candy { get; private set; }
        public int SendPoints { get; private set; }
        public int Purchases { get; private set; }
        public int Kills { get; private set; }
        public int Leaks { get; private set; }
        public int Merges { get; private set; }

        /// <summary>In-match power-up level per deck slot (parallel to Deck).</summary>
        public readonly int[] DeckLevel;
        /// <summary>In-match upgrade level per Element (index = (int)Element).</summary>
        public readonly int[] ElementLevel = new int[5];
        /// <summary>Keeper spells (spell def indices) and the tick each becomes ready again.</summary>
        public readonly int[] Spells;
        public readonly int[] SpellReadyTick;
        public int RageUntilTick { get; private set; }
        public int RagePercent { get; private set; }
        public int CandyRainUntilTick { get; private set; }
        public int CandyRainPercent { get; private set; }
        /// <summary>Chosen perks, in order.</summary>
        public readonly List<PerkType> Perks = new List<PerkType>();
        /// <summary>Perks offered right now (empty when no choice is pending) and the auto-pick tick.</summary>
        public readonly List<PerkType> PerkOffer = new List<PerkType>();
        public int PerkDeadlineTick { get; private set; }
        /// <summary>Current candy balloon (0 = none), its lane (0..2) and lifetime.</summary>
        public int BalloonId { get; private set; }
        public int BalloonLane { get; private set; }
        public int BalloonSpawnTick { get; private set; }
        public int BalloonExpireTick { get; private set; }
        /// <summary>Parallel to IncomingSends: true when that send was already reflected once (cannot bounce again).</summary>
        public readonly List<bool> IncomingReflected = new List<bool>();
        /// <summary>Parallel to IncomingSends: tick the send arrives on this board.</summary>
        public readonly List<int> IncomingArriveTick = new List<int>();
        /// <summary>Extra candy every wave, earned by sending monsters (PvP investment).</summary>
        public int Income { get; private set; }
        /// <summary>Hearts this board took from the opponent with its sends (for the HUD).</summary>
        public int HeartsDealt { get; internal set; }
        public int SpellsCast { get; private set; }
        public int SkillsUsed { get; private set; }
        public int BalloonsPopped { get; private set; }
        public int Reflects { get; private set; }
        /// <summary>Hits on the main target that countered its element (missions, achievements).</summary>
        public int EffectiveHits { get; private set; }
        public int BossKills { get; private set; }
        int _nextBalloonTick, _pointRemainder;

        readonly Match _m;
        readonly ContentDatabase _c;
        readonly int[] _cardPct;   // damage % from card level, indexed by unit def
        readonly BalanceConfig _b;
        readonly List<PendingSpawn> _spawns = new List<PendingSpawn>();
        readonly List<EnemyInstance> _newEnemies = new List<EnemyInstance>();
        readonly List<int> _scratch = new List<int>();
        readonly List<EnemyInstance> _hitList = new List<EnemyInstance>();
        DetRandom _rng;

        internal Board(Match match, int index, PlayerSetup setup, DetRandom rng)
        {
            _m = match; _c = match.Content; _b = match.Balance; _rng = rng;
            Index = index;
            PlayerName = setup.name;
            Cells = new UnitInstance[_b.gridCols * _b.gridRows];
            LockedUntilWave = new int[Cells.Length];
            Hearts = _b.startHearts;
            Candy = _b.startCandy;

            Deck = ResolveIds(setup.deck, _c.UnitIndex, "unit");
            SendDeck = ResolveIds(setup.sendDeck, _c.SendIndex, "send");
            SendUsed = new bool[SendDeck.Length];
            DeckLevel = new int[Deck.Length];
            _cardPct = new int[_c.Units.Count];
            for (int i = 0; i < _cardPct.Length; i++) _cardPct[i] = 100;
            for (int i = 0; i < Deck.Length; i++)
            {
                int lv = setup.cardLevels != null && i < setup.cardLevels.Length ? Math.Max(1, setup.cardLevels[i]) : 1;
                _cardPct[Deck[i]] = 100 + _b.cardLevelDamagePercent * (lv - 1);
            }
            Shop = new int[_b.shopSlots];
            for (int i = 0; i < Shop.Length; i++) Shop[i] = RandomDeckUnit();

            var spellIds = setup.spells != null && setup.spells.Length > 0 ? setup.spells : DefaultContent.DefaultSpells;
            var spells = new List<int>();
            for (int i = 0; i < spellIds.Length && spells.Count < 4; i++)
            {
                int s = _c.SpellIndex(spellIds[i]);
                if (s >= 0 && !spells.Contains(s)) spells.Add(s);
            }
            Spells = spells.ToArray();
            SpellReadyTick = new int[Spells.Length];
            _nextBalloonTick = _b.balloonFirstDelayTicks + _rng.Next(Math.Max(1, _b.balloonMaxGapTicks - _b.balloonMinGapTicks));
        }

        static int[] ResolveIds(string[] ids, Func<string, int> lookup, string what)
        {
            if (ids == null || ids.Length == 0) throw new ArgumentException("Empty " + what + " deck");
            var result = new int[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                result[i] = lookup(ids[i]);
                if (result[i] < 0) throw new ArgumentException("Unknown " + what + " id in deck: " + ids[i]);
            }
            return result;
        }

        public Board Opponent { get { return _m.Boards[1 - Index]; } }
        /// <summary>Damage % granted by the out-of-match card level of a unit def.</summary>
        public int CardPercent(int unitDef) { return _cardPct[unitDef]; }
        public int CardPrice
        {
            get
            {
                int p = _b.cardBasePrice + _b.cardPriceStep * Purchases;
                return HasPerk(PerkType.CheapCards) ? p * 85 / 100 : p;
            }
        }
        public bool HasPerk(PerkType p) { return Perks.Contains(p); }
        public int ElementUpgradeCost(Element e) { return _b.elementUpgradeBaseCost + _b.elementUpgradeCostStep * ElementLevel[(int)e]; }
        /// <summary>Damage of one hit of a unit right now (for the info panel).</summary>
        public int HitDamage(UnitInstance u)
        {
            var def = _c.Units[u.def];
            return Math.Max(def.damage > 0 ? 1 : 0, def.damage * _b.levelDamagePercent[u.level - 1] / 100 * _cardPct[u.def] / 100 * PositionPercent(u) / 100);
        }
        /// <summary>Ticks between attacks of a unit right now (auras and Rage included).</summary>
        public int AttackInterval(UnitInstance u) { return EffectiveInterval(u, _c.Units[u.def]); }
        public int UnitUpgradeCost(int slot) { return _b.unitUpgradeBaseCost + _b.unitUpgradeCostStep * DeckLevel[slot]; }
        /// <summary>Deck slot of a unit def (-1 when not in the deck).</summary>
        public int DeckSlotOf(int unitDef) { return Array.IndexOf(Deck, unitDef); }
        /// <summary>Damage % of the in-match power-up of a unit def.</summary>
        public int UnitUpgradePercent(int unitDef)
        {
            int slot = DeckSlotOf(unitDef);
            return slot < 0 ? 0 : _b.unitUpgradePercent * DeckLevel[slot];
        }
        public bool IsRaging { get { return _m.Tick < RageUntilTick; } }

        /// <summary>Ticks of charge an animal needs before its skill can be used (0 = no skill at this level).</summary>
        public int SkillCharge(UnitInstance u)
        {
            if (u == null || u.level < _b.skillMinLevel) return 0;
            return Math.Max(120, _b.skillChargeTicks - _b.skillChargeReductionPerLevel * (u.level - _b.skillMinLevel));
        }
        public bool IsSkillReady(UnitInstance u) { int c = SkillCharge(u); return c > 0 && u.energy >= c; }

        public int SpellCooldown(int slot)
        {
            int cd = _c.Spells[Spells[slot]].cooldownTicks;
            return HasPerk(PerkType.SpellHaste) ? cd * 70 / 100 : cd;
        }

        /// <summary>Candy to bounce an incoming send back (a share of its price; free sends like the boss remnant cost 40).</summary>
        public int ReflectCost(int index)
        {
            int price = _c.Sends[IncomingSends[index]].cost;
            return Math.Max(price <= 0 ? 40 : 10, price * _b.reflectCostPercent / 100);
        }

        /// <summary>Income a send adds (Mischief perk: +50%).</summary>
        public int SendIncome(int slot)
        {
            int inc = _c.Sends[SendDeck[slot]].income * _b.sendIncomePercent / 100;
            return HasPerk(PerkType.SendPoints) ? inc * 3 / 2 : inc;
        }

        /// <summary>A send starts travelling to this board (arrives after sendTravelTicks).</summary>
        internal void ReceiveSend(int sendDef, bool reflected)
        {
            IncomingSends.Add(sendDef);
            IncomingReflected.Add(reflected);
            IncomingArriveTick.Add(_m.Tick + _b.sendTravelTicks);
        }

        /// <summary>Sends whose travel time is over enter the path now.</summary>
        void ArriveSends()
        {
            for (int k = 0; k < IncomingSends.Count; k++)
            {
                if (IncomingArriveTick[k] > _m.Tick) continue;
                var send = _c.Sends[IncomingSends[k]];
                int enemy = _c.EnemyIndex(send.enemyId);
                for (int j = 0; j < send.count; j++)
                {
                    _spawns.Add(new PendingSpawn
                    {
                        tick = _m.Tick + j * send.spacingTicks,
                        enemyDef = enemy,
                        element = send.element,
                        hpPermille = (int)((long)_m.WaveHpPermille * _b.sendHpPercent / 100),
                        sender = Opponent.Index,
                    });
                }
                _m.Emit(new SimEvent(SimEventType.SendIncoming, Index, IncomingSends[k], Opponent.Index));
                IncomingSends.RemoveAt(k);
                IncomingReflected.RemoveAt(k);
                IncomingArriveTick.RemoveAt(k);
                k--;
            }
        }

        /// <summary>Damage % from star cell, element links, element upgrades and perks for a unit in a cell.</summary>
        public int PositionPercent(UnitInstance u)
        {
            var d = _c.Units[u.def];
            int pct = 100 + _b.elementUpgradePercent * ElementLevel[(int)d.element] + UnitUpgradePercent(u.def);
            if (_m.IsStarCell(u.cell)) pct += _b.starCellBonusPercent;
            if (u.guardTicks > 0) pct += u.guardBonus;
            int col = u.cell % _b.gridCols, row = u.cell / _b.gridCols;
            pct += _b.elementLinkPercent * (SameElement(col - 1, row, d.element) + SameElement(col + 1, row, d.element)
                + SameElement(col, row - 1, d.element) + SameElement(col, row + 1, d.element));
            if ((d.element == Element.Fire && HasPerk(PerkType.FirePower)) || (d.element == Element.Water && HasPerk(PerkType.WaterPower))
                || (d.element == Element.Grass && HasPerk(PerkType.GrassPower)) || (d.element == Element.Light && HasPerk(PerkType.LightPower)))
                pct += _b.perkElementPercent;
            return pct;
        }

        int SameElement(int col, int row, Element e)
        {
            if (col < 0 || row < 0 || col >= _b.gridCols || row >= _b.gridRows) return 0;
            var n = Cells[row * _b.gridCols + col];
            return n != null && _c.Units[n.def].element == e ? 1 : 0;
        }

        public int LinkCount(int cell)
        {
            var u = Cells[cell];
            if (u == null) return 0;
            var e = _c.Units[u.def].element;
            int col = cell % _b.gridCols, row = cell / _b.gridCols;
            return SameElement(col - 1, row, e) + SameElement(col + 1, row, e) + SameElement(col, row - 1, e) + SameElement(col, row + 1, e);
        }
        public bool IsCellLocked(int cell) { return LockedUntilWave[cell] > _m.Wave; }
        public bool HasComebackDiscount { get { return Hearts + _b.comebackHeartGap <= Opponent.Hearts; } }

        public int SendCost(int slot)
        {
            int cost = _c.Sends[SendDeck[slot]].cost * _b.sendCostPercent / 100;
            if (HasPerk(PerkType.CheapSends)) cost = cost * 80 / 100;
            return HasComebackDiscount ? cost * (100 - _b.comebackDiscountPercent) / 100 : cost;
        }

        public int CountFreeCells()
        {
            int n = 0;
            for (int i = 0; i < Cells.Length; i++) if (Cells[i] == null && !IsCellLocked(i)) n++;
            return n;
        }

        public UnitInstance FindUnit(int uid)
        {
            for (int i = 0; i < Cells.Length; i++) if (Cells[i] != null && Cells[i].uid == uid) return Cells[i];
            return null;
        }

        int RandomDeckUnit() { return Deck[_rng.Next(Deck.Length)]; }

        // ------------------------------------------------------------------ commands

        /// <summary>Buys a shop card. cell = -1 puts it on a random free cell, otherwise on that cell.</summary>
        internal RejectReason Buy(int slot, int cell = -1)
        {
            if (slot < 0 || slot >= Shop.Length) return RejectReason.InvalidSlot;
            int price = CardPrice;
            if (Candy < price) return RejectReason.NotEnoughCandy;
            if (cell >= 0)
            {
                if (cell >= Cells.Length || Cells[cell] != null) return RejectReason.InvalidCell;
                if (IsCellLocked(cell)) return RejectReason.CellLocked;
            }
            else
            {
                _scratch.Clear();
                for (int i = 0; i < Cells.Length; i++) if (Cells[i] == null && !IsCellLocked(i)) _scratch.Add(i);
                if (_scratch.Count == 0) return RejectReason.BoardFull;
                cell = _scratch[_rng.Next(_scratch.Count)];
            }

            Candy -= price;
            Purchases++;
            var def = _c.Units[Shop[slot]];
            var u = new UnitInstance { uid = _m.NewUid(), def = Shop[slot], level = 1, cell = cell, cooldown = def.attackIntervalTicks / 2 };
            Cells[cell] = u;
            _m.Emit(new SimEvent(SimEventType.UnitPlaced, Index, u.uid, cell, u.def));
            Shop[slot] = RandomDeckUnit();
            return RejectReason.None;
        }

        /// <summary>The animal the next Summon will bring (pre-rolled so the player can see it).</summary>
        public int NextSummon { get { return Shop[0]; } }

        /// <summary>How many animals of a unit def stand on the board.</summary>
        public int CountOnBoard(int unitDef)
        {
            int n = 0;
            for (int i = 0; i < Cells.Length; i++) if (Cells[i] != null && Cells[i].def == unitDef) n++;
            return n;
        }

        /// <summary>Price of summoning a chosen deck animal: the random price plus a rarity surcharge.</summary>
        public int SummonPrice(int slot)
        {
            var extra = _b.summonPickExtra;
            int r = (int)_c.Units[Deck[slot]].rarity;
            int add = extra != null && extra.Length > 0 ? extra[Math.Min(r, extra.Length - 1)] : 0;
            return CardPrice + (HasPerk(PerkType.CheapCards) ? add * 85 / 100 : add);
        }

        /// <summary>Random summon: the pre-rolled next animal (NextSummon) at the base price. cell = -1 picks a random free cell.</summary>
        internal RejectReason Summon(int cell)
        {
            int def = Shop[0];
            var r = PlaceNew(def, cell, CardPrice);
            if (r == RejectReason.None) Shop[0] = RandomDeckUnit();
            return r;
        }

        /// <summary>Chosen summon: the deck animal in `slot`, at SummonPrice(slot). cell = -1 picks a random free cell.</summary>
        internal RejectReason SummonUnit(int slot, int cell)
        {
            if (slot < 0 || slot >= Deck.Length) return RejectReason.InvalidSlot;
            return PlaceNew(Deck[slot], cell, SummonPrice(slot));
        }

        RejectReason PlaceNew(int defIndex, int cell, int price)
        {
            if (Candy < price) return RejectReason.NotEnoughCandy;
            if (cell >= 0)
            {
                if (cell >= Cells.Length || Cells[cell] != null) return RejectReason.InvalidCell;
                if (IsCellLocked(cell)) return RejectReason.CellLocked;
            }
            else
            {
                _scratch.Clear();
                for (int i = 0; i < Cells.Length; i++) if (Cells[i] == null && !IsCellLocked(i)) _scratch.Add(i);
                if (_scratch.Count == 0) return RejectReason.BoardFull;
                cell = _scratch[_rng.Next(_scratch.Count)];
            }
            Candy -= price;
            Purchases++;
            var def = _c.Units[defIndex];
            var u = new UnitInstance { uid = _m.NewUid(), def = defIndex, level = 1, cell = cell, cooldown = def.attackIntervalTicks / 2 };
            Cells[cell] = u;
            _m.Emit(new SimEvent(SimEventType.UnitPlaced, Index, u.uid, cell, u.def));
            return RejectReason.None;
        }

        internal RejectReason UpgradeUnit(int slot)
        {
            if (slot < 0 || slot >= Deck.Length) return RejectReason.InvalidSlot;
            if (DeckLevel[slot] >= _b.unitUpgradeMax) return RejectReason.MaxLevel;
            // Power-ups are for animals you are actually using: at least one must stand on the board.
            if (CountOnBoard(Deck[slot]) == 0) return RejectReason.NotOnBoard;
            int cost = UnitUpgradeCost(slot);
            if (Candy < cost) return RejectReason.NotEnoughCandy;
            Candy -= cost;
            DeckLevel[slot]++;
            _m.Emit(new SimEvent(SimEventType.UnitUpgraded, Index, slot, DeckLevel[slot]));
            return RejectReason.None;
        }

        internal RejectReason Reroll()
        {
            if (Candy < _b.rerollCost) return RejectReason.NotEnoughCandy;
            Candy -= _b.rerollCost;
            for (int i = 0; i < Shop.Length; i++) Shop[i] = RandomDeckUnit();
            return RejectReason.None;
        }

        internal RejectReason Move(int from, int to)
        {
            if (from < 0 || to < 0 || from >= Cells.Length || to >= Cells.Length || from == to) return RejectReason.InvalidCell;
            var a = Cells[from];
            if (a == null) return RejectReason.InvalidCell;
            if (IsCellLocked(to)) return RejectReason.CellLocked;
            var b = Cells[to];

            if (b == null)
            {
                Cells[from] = null; Cells[to] = a; a.cell = to;
                _m.Emit(new SimEvent(SimEventType.UnitMoved, Index, a.uid, from, to));
                return RejectReason.None;
            }

            if (b.def == a.def && b.level == a.level)
            {
                if (b.level >= _b.maxLevel) return RejectReason.MaxLevel;
                b.level++;
                b.energy = Math.Max(b.energy, a.energy);
                Cells[from] = null;
                Merges++;
                _m.Emit(new SimEvent(SimEventType.UnitMerged, Index, b.uid, b.level, a.uid));
                if (HasPerk(PerkType.MergeCandy)) GainCandy(_b.perkMergeCandy);
                return RejectReason.None;
            }

            if (IsCellLocked(from)) return RejectReason.CellLocked;
            Cells[from] = b; b.cell = from;
            Cells[to] = a; a.cell = to;
            _m.Emit(new SimEvent(SimEventType.UnitsSwapped, Index, a.uid, b.uid));
            return RejectReason.None;
        }

        internal RejectReason SendMonster(int slot)
        {
            if (slot < 0 || slot >= SendDeck.Length) return RejectReason.InvalidSlot;
            var def = _c.Sends[SendDeck[slot]];
            if (def.oncePerMatch && SendUsed[slot]) return RejectReason.AlreadyUsed;
            if (Opponent.IncomingSends.Count >= _b.maxIncomingSendsPerWave) return RejectReason.OpponentWaveFull;
            int cost = SendCost(slot);
            if (Candy < cost) return RejectReason.NotEnoughCandy;

            Candy -= cost;
            SendUsed[slot] = true;
            Income += SendIncome(slot);
            Opponent.ReceiveSend(SendDeck[slot], false);
            _m.Emit(new SimEvent(SimEventType.SendQueued, Index, SendDeck[slot], Opponent.Index));
            return RejectReason.None;
        }

        // ------------------------------------------------------------------ wave flow

        /// <summary>True while monsters of this wave are still waiting to come out.</summary>
        public bool HasPendingSpawns { get { return _spawns.Count > 0; } }

        internal EnemyInstance SpawnRaidBoss(int def, int hp, int speedPercent, Element element)
        {
            var e = CreateEnemy(def, element, 1000, -1, 0);
            e.hp = e.maxHp = hp;
            e.raid = true;
            e.speedPercent = speedPercent;
            return e;
        }

        internal void SyncRaidBoss(int hp)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (!e.raid || !e.alive) continue;
                e.hp = hp;
                if (hp <= 0) Kill(e);
            }
        }

        // ------------------------------------------------------------------ raid assists

        public const int AssistCandyCost = 40, AssistCandyGift = 50, AssistRageCost = 60;

        /// <summary>Raid: 0 = gift candy to the partner, 1 = make the partner's animals rage for 5 s.</summary>
        internal RejectReason Assist(int kind)
        {
            var partner = Opponent;
            if (kind == 0)
            {
                if (Candy < AssistCandyCost) return RejectReason.NotEnoughCandy;
                Candy -= AssistCandyCost;
                partner.GainCandy(AssistCandyGift);
                _m.Emit(new SimEvent(SimEventType.AssistGiven, Index, 0, partner.Index, AssistCandyGift));
                _m.Emit(new SimEvent(SimEventType.AssistGiven, partner.Index, 0, partner.Index, AssistCandyGift));
                return RejectReason.None;
            }
            if (kind == 1)
            {
                if (Candy < AssistRageCost) return RejectReason.NotEnoughCandy;
                if (partner.IsRaging) return RejectReason.Cooldown;
                Candy -= AssistRageCost;
                partner.RagePercent = 50;
                partner.RageUntilTick = _m.Tick + 100;
                _m.Emit(new SimEvent(SimEventType.AssistGiven, Index, 1, partner.Index, 50));
                _m.Emit(new SimEvent(SimEventType.AssistGiven, partner.Index, 1, partner.Index, 50));
                return RejectReason.None;
            }
            return RejectReason.InvalidSlot;
        }

        internal void BeginTick()
        {
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].prevDistance = Enemies[i].distance;
        }

        internal void GainCandy(int amount)
        {
            if (amount <= 0) return;
            Candy += amount;
            _m.Emit(new SimEvent(SimEventType.CandyGained, Index, amount));
        }

        internal void OnWaveStart(List<PendingSpawn> shared)
        {
            for (int i = 0; i < shared.Count; i++) _spawns.Add(shared[i]);

            int wave = _m.Wave;
            if (wave > 1 && Income > 0) GainCandy(Income);   // what your sends earn
            if (wave > 1 && HasPerk(PerkType.Interest)) GainCandy(Math.Min(60, Candy / 10));
            for (int i = 0; i < Cells.Length; i++)
            {
                var u = Cells[i];
                if (u == null) continue;
                var def = _c.Units[u.def];
                if (def.skill == UnitSkill.CandyPerWave && wave > 1)
                    GainCandy(def.skillValue * u.level);
                else if (def.skill == UnitSkill.PhoenixBlessing && def.skillValue > 0 && wave % def.skillValue == 0)
                    PhoenixBlessing(u);
            }
        }

        void PhoenixBlessing(UnitInstance phoenix)
        {
            _scratch.Clear();
            for (int i = 0; i < Cells.Length; i++)
            {
                var u = Cells[i];
                if (u == null || u == phoenix || u.level >= _b.maxLevel) continue;
                var d = _c.Units[u.def];
                if (d.element == Element.Fire && d.skill != UnitSkill.PhoenixBlessing) _scratch.Add(i);
            }
            if (_scratch.Count > 0)
            {
                var target = Cells[_scratch[_rng.Next(_scratch.Count)]];
                target.level++;
                _m.Emit(new SimEvent(SimEventType.UnitLevelUp, Index, target.uid, target.level, phoenix.uid));
            }
            bool unlocked = false;
            for (int i = 0; i < LockedUntilWave.Length; i++)
                if (LockedUntilWave[i] > 0) { unlocked |= IsCellLocked(i); LockedUntilWave[i] = 0; }
            if (unlocked) _m.Emit(new SimEvent(SimEventType.CellsUnlocked, Index));
        }

        // ------------------------------------------------------------------ simulation

        internal void Simulate()
        {
            ArriveSends();
            SpawnDue();
            UpdateEnemies();
            UpdateUnits();
            Enemies.RemoveAll(e => !e.alive);
            UpdateBalloon();
            if (PerkOffer.Count > 0 && _m.Tick >= PerkDeadlineTick) ChoosePerk(0, true);
        }

        // ------------------------------------------------------------------ element upgrades

        internal RejectReason UpgradeElement(int element)
        {
            if (element < (int)Element.Fire || element > (int)Element.Light) return RejectReason.InvalidSlot;
            if (ElementLevel[element] >= _b.elementUpgradeMax) return RejectReason.MaxLevel;
            int cost = ElementUpgradeCost((Element)element);
            if (Candy < cost) return RejectReason.NotEnoughCandy;
            Candy -= cost;
            ElementLevel[element]++;
            _m.Emit(new SimEvent(SimEventType.ElementUpgraded, Index, element, ElementLevel[element]));
            return RejectReason.None;
        }

        // ------------------------------------------------------------------ keeper spells

        internal RejectReason CastSpell(int slot, int targetDistance)
        {
            if (slot < 0 || slot >= Spells.Length) return RejectReason.InvalidSlot;
            var def = _c.Spells[Spells[slot]];
            if (_m.Tick < SpellReadyTick[slot]) return RejectReason.Cooldown;
            if (Candy < def.cost) return RejectReason.NotEnoughCandy;
            if (def.kind == SpellKind.Lightning && StrongestEnemy() == null) return RejectReason.NoTarget;
            int target = Math.Max(0, Math.Min(_m.PathLength, targetDistance));

            Candy -= def.cost;
            SpellReadyTick[slot] = _m.Tick + SpellCooldown(slot);
            SpellsCast++;
            int hpScale = _m.WaveHpPermille;
            switch (def.kind)
            {
                case SpellKind.Freeze:
                    ForEachNear(target, def.radius, e =>
                    {
                        var ed = _c.Enemies[e.def];
                        e.stunTicks = Math.Max(e.stunTicks, IsBossLike(ed) ? def.value / 2 : def.value);
                    });
                    break;
                case SpellKind.Meteor:
                {
                    int dmg = (int)Math.Max(1L, (long)def.value * hpScale / 1000);
                    ForEachNear(target, def.radius, e => ApplyDamage(e, dmg));
                    break;
                }
                case SpellKind.Gust:
                    ForEachNear(target, def.radius, e =>
                    {
                        var ed = _c.Enemies[e.def];
                        e.distance = Math.Max(0, e.distance - def.value * (100 - ed.knockbackResistPercent) / 100);
                    });
                    break;
                case SpellKind.Rage:
                    RageUntilTick = _m.Tick + def.value;
                    RagePercent = def.value2;
                    target = -1;
                    break;
                case SpellKind.CandyRain:
                    CandyRainUntilTick = _m.Tick + def.value;
                    CandyRainPercent = def.value2;
                    target = -1;
                    break;
                case SpellKind.Lightning:
                {
                    var e = StrongestEnemy();
                    target = e.distance;
                    ApplyDamage(e, (int)Math.Max(1L, (long)def.value * hpScale / 1000));
                    break;
                }
            }
            if (!def.NeedsTarget && def.kind != SpellKind.Lightning) target = -1;
            _m.Emit(new SimEvent(SimEventType.SpellCast, Index, Spells[slot], target, slot));
            return RejectReason.None;
        }

        static bool IsBossLike(EnemyDef d) { return d.kind == EnemyKind.Boss || d.bossSkill != BossSkill.None; }

        void ForEachNear(int distance, int radius, Action<EnemyInstance> action)
        {
            int cx, cy;
            PathMath.Point(_b, distance, out cx, out cy);
            long r2 = (long)radius * radius;
            _hitList.Clear();
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (!e.alive) continue;
                int ex, ey;
                PathMath.Point(_b, e.distance, out ex, out ey);
                if (Dist2(cx, cy, ex, ey) <= r2) _hitList.Add(e);
            }
            // Copy: actions may kill (Kill can touch other lists but never _hitList itself).
            var hits = _hitList.ToArray();
            for (int i = 0; i < hits.Length; i++) action(hits[i]);
        }

        EnemyInstance StrongestEnemy()
        {
            EnemyInstance best = null;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e.alive && e.revealed && (best == null || e.hp > best.hp)) best = e;
            }
            return best;
        }

        /// <summary>Path distance of the densest group of visible enemies (-1 when there are none).</summary>
        public int DensestDistance(int radius)
        {
            EnemyInstance best = null;
            int bestCount = 0;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (!e.alive || !e.revealed) continue;
                int n = CountNear(e, radius);
                if (n > bestCount || (n == bestCount && best != null && e.distance > best.distance)) { bestCount = n; best = e; }
            }
            return best == null ? -1 : best.distance;
        }

        // ------------------------------------------------------------------ perks

        internal void OfferPerks()
        {
            if (PerkOffer.Count > 0) ChoosePerk(0, true);   // never stack offers
            _scratchPerks.Clear();
            for (int p = 1; p <= 12; p++) if (!HasPerk((PerkType)p)) _scratchPerks.Add((PerkType)p);
            // Comeback: the player behind on hearts gets one more choice, and Extra Hearts is always among them.
            bool behind = Hearts + _b.comebackHeartGap <= Opponent.Hearts;
            int choices = _b.perkChoices + (behind ? 1 : 0);
            if (behind && _scratchPerks.Remove(PerkType.ExtraHearts)) { PerkOffer.Add(PerkType.ExtraHearts); choices--; }
            for (int k = 0; k < choices && _scratchPerks.Count > 0; k++)
            {
                int pick = _rng.Next(_scratchPerks.Count);
                PerkOffer.Add(_scratchPerks[pick]);
                _scratchPerks.RemoveAt(pick);
            }
            if (PerkOffer.Count == 0) return;
            PerkDeadlineTick = _m.Tick + _b.perkAutoPickTicks;
            _m.Emit(new SimEvent(SimEventType.PerkOffered, Index, PerkOffer.Count));
        }

        readonly List<PerkType> _scratchPerks = new List<PerkType>();

        internal RejectReason ChoosePerk(int index, bool auto)
        {
            if (PerkOffer.Count == 0) return RejectReason.NotOffered;
            if (index < 0 || index >= PerkOffer.Count) return RejectReason.InvalidSlot;
            var perk = PerkOffer[index];
            PerkOffer.Clear();
            Perks.Add(perk);
            if (perk == PerkType.ExtraHearts) Hearts += 2;
            _m.Emit(new SimEvent(SimEventType.PerkChosen, Index, (int)perk, auto ? 1 : 0));
            return RejectReason.None;
        }

        // ------------------------------------------------------------------ balloons

        void UpdateBalloon()
        {
            int now = _m.Tick;
            if (BalloonId != 0 && now >= BalloonExpireTick)
            {
                _m.Emit(new SimEvent(SimEventType.BalloonExpired, Index, BalloonId));
                BalloonId = 0;
                ScheduleBalloon();
            }
            if (BalloonId == 0 && _m.Wave >= 1 && now >= _nextBalloonTick)
            {
                BalloonId = _m.NewUid();
                BalloonLane = _rng.Next(3);
                BalloonSpawnTick = now;
                BalloonExpireTick = now + _b.balloonLifeTicks;
                _m.Emit(new SimEvent(SimEventType.BalloonSpawned, Index, BalloonId, BalloonLane));
            }
        }

        void ScheduleBalloon()
        {
            _nextBalloonTick = _m.Tick + _b.balloonMinGapTicks + _rng.Next(Math.Max(1, _b.balloonMaxGapTicks - _b.balloonMinGapTicks));
        }

        public int BalloonCandy { get { return _b.balloonCandyBase + _b.balloonCandyPerWave * Math.Max(1, _m.Wave); } }

        internal RejectReason PopBalloon(int id)
        {
            if (BalloonId == 0 || id != BalloonId) return RejectReason.NoTarget;
            int candy = BalloonCandy;
            _m.Emit(new SimEvent(SimEventType.BalloonPopped, Index, BalloonId, candy));
            BalloonId = 0;
            BalloonsPopped++;
            ScheduleBalloon();
            GainCandy(candy);
            return RejectReason.None;
        }

        // ------------------------------------------------------------------ reflect

        internal RejectReason Reflect(int index)
        {
            if (index < 0 || index >= IncomingSends.Count) return RejectReason.InvalidSlot;
            if (IncomingReflected[index]) return RejectReason.AlreadyUsed;
            if (Opponent.IncomingSends.Count >= _b.maxIncomingSendsPerWave) return RejectReason.OpponentWaveFull;
            int cost = ReflectCost(index);
            if (Candy < cost) return RejectReason.NotEnoughCandy;
            Candy -= cost;
            int send = IncomingSends[index];
            IncomingSends.RemoveAt(index);
            IncomingReflected.RemoveAt(index);
            IncomingArriveTick.RemoveAt(index);
            Opponent.ReceiveSend(send, true);
            Reflects++;
            _m.Emit(new SimEvent(SimEventType.SendReflected, Index, send, Opponent.Index));
            return RejectReason.None;
        }

        // ------------------------------------------------------------------ active skills

        internal RejectReason UseSkill(int cell)
        {
            if (cell < 0 || cell >= Cells.Length || Cells[cell] == null) return RejectReason.InvalidCell;
            var u = Cells[cell];
            if (IsCellLocked(cell) || u.stunTicks > 0) return RejectReason.CellLocked;
            if (!IsSkillReady(u)) return RejectReason.NotReady;
            var def = _c.Units[u.def];
            int levelPct = _b.levelDamagePercent[u.level - 1];
            int hit = Math.Max(1, def.damage * levelPct / 100 * _cardPct[u.def] / 100 * PositionPercent(u) / 100);
            int target = -1;
            switch (def.role)
            {
                case Role.Damage:
                {
                    var e = StrongestEnemy();
                    if (e == null) return RejectReason.NoTarget;
                    target = e.distance;
                    ApplyDamage(e, hit * 6);
                    break;
                }
                case Role.Area:
                {
                    target = DensestDistance(1500);
                    if (target < 0) return RejectReason.NoTarget;
                    int dmg = hit * 3;
                    ForEachNear(target, 1500, e => ApplyDamage(e, dmg));
                    break;
                }
                case Role.Control:
                {
                    target = DensestDistance(1600);
                    if (target < 0) return RejectReason.NoTarget;
                    ForEachNear(target, 1600, e =>
                    {
                        var ed = _c.Enemies[e.def];
                        e.stunTicks = Math.Max(e.stunTicks, IsBossLike(ed) ? 25 : 50);
                        e.slowPercent = Math.Max(e.slowTicks > 0 ? e.slowPercent : 0, 50);
                        e.slowTicks = Math.Max(e.slowTicks, 100);
                        e.revealed = true;
                    });
                    break;
                }
                default:   // Support: cheer the whole team and find some candy
                    RagePercent = Math.Max(IsRaging ? RagePercent : 0, 40);
                    RageUntilTick = Math.Max(RageUntilTick, _m.Tick + 100);
                    GainCandy(6 * u.level);
                    break;
            }
            u.energy = 0;
            SkillsUsed++;
            _m.Emit(new SimEvent(SimEventType.SkillUsed, Index, u.uid, target, (int)def.role));
            return RejectReason.None;
        }

        void SpawnDue()
        {
            int now = _m.Tick;
            for (int i = 0; i < _spawns.Count; )
            {
                var s = _spawns[i];
                if (s.tick <= now)
                {
                    CreateEnemy(s.enemyDef, s.element, s.hpPermille, s.sender, s.startDistance);
                    _spawns.RemoveAt(i);
                }
                else i++;
            }
        }

        EnemyInstance CreateEnemy(int defIndex, Element element, int hpPermille, int sender, int distance)
        {
            var def = _c.Enemies[defIndex];
            int hp = (int)Math.Max(1L, (long)def.hp * hpPermille / 1000);
            var e = new EnemyInstance
            {
                uid = _m.NewUid(), def = defIndex,
                element = def.element != Element.None ? def.element : element,
                hp = hp, maxHp = hp, distance = distance, prevDistance = distance,
                revealed = !def.invisible, sender = sender,
            };
            Enemies.Add(e);
            _m.Emit(new SimEvent(SimEventType.EnemySpawned, Index, e.uid, defIndex));
            return e;
        }

        void UpdateEnemies()
        {
            _newEnemies.Clear();
            int count = Enemies.Count;
            for (int i = 0; i < count; i++)
            {
                var e = Enemies[i];
                if (!e.alive) continue;
                var def = _c.Enemies[e.def];

                if (e.burnTicks > 0)
                {
                    e.burnTicks--;
                    if (e.burnTicks % 10 == 0) { ApplyDamage(e, e.burnDamage * Math.Max(1, e.burnStacks)); if (!e.alive) continue; }
                    if (e.burnTicks == 0) { e.burnStacks = 0; e.burnDamage = 0; }
                }
                if (e.shredTicks > 0) e.shredTicks--;
                if (e.vulnTicks > 0) e.vulnTicks--;
                if (e.hasteTicks > 0) e.hasteTicks--;
                if (e.silenceTicks > 0) e.silenceTicks--;
                if (e.shieldTicks > 0 && --e.shieldTicks == 0) e.shield = 0;
                UpdatePhases(e, def);

                if (e.stunTicks > 0) e.stunTicks--;
                else
                {
                    int speed = def.speed * _b.enemySpeedPercent / 100;
                    if (e.speedPercent != 100) speed = speed * e.speedPercent / 100;
                    int bonus = (e.hasteTicks > 0 ? e.hastePercent : 0) + (e.enraged ? def.abilityValue : 0) + (e.phaseDone ? 15 : 0);
                    if (bonus > 0) speed = speed * (100 + bonus) / 100;
                    if (e.slowTicks > 0) { speed = speed * (100 - e.slowPercent) / 100; e.slowTicks--; }
                    e.distance += speed;
                }

                if (e.pendingTicks >= 0 && --e.pendingTicks < 0) ResolveBossSkill(e, def);
                // Silenced monsters keep walking but their skill timer stops.
                if (e.silenceTicks == 0)
                {
                    if (def.bossSkill != BossSkill.None && ++e.skillTimer >= def.bossSkillIntervalTicks)
                    {
                        e.skillTimer = 0;
                        UseBossSkill(e, def);
                    }
                    else if (def.ability != EnemyAbility.None && def.abilityIntervalTicks > 0 && ++e.skillTimer >= def.abilityIntervalTicks)
                    {
                        e.skillTimer = 0;
                        UseAbility(e, def);
                    }
                }

                if (e.distance >= _m.PathLength) Leak(e, def);
            }
            for (int i = 0; i < _newEnemies.Count; i++) Enemies.Add(_newEnemies[i]);
        }

        void UseBossSkill(EnemyInstance boss, EnemyDef def)
        {
            switch (def.bossSkill)
            {
                case BossSkill.Summon:
                {
                    int summon = _c.EnemyIndex(def.summonEnemyId);
                    for (int k = 0; k < 2; k++)
                    {
                        var minion = CreateEnemyDeferred(summon, boss.element, _m.WaveHpPermille * 6 / 10, boss.sender, Math.Max(0, boss.distance - 300 * (k + 1)));
                        _newEnemies.Add(minion);
                    }
                    break;
                }
                case BossSkill.StunUnit:
                {
                    _scratch.Clear();
                    for (int i = 0; i < Cells.Length; i++) if (Cells[i] != null && Cells[i].stunTicks == 0) _scratch.Add(i);
                    if (_scratch.Count == 0) break;
                    var u = Cells[_scratch[_rng.Next(_scratch.Count)]];
                    StunUnit(u, _b.unitStunTicks, boss.uid);
                    break;
                }
                case BossSkill.LockCell:
                {
                    if (boss.pendingTicks >= 0) break;
                    _scratch.Clear();
                    for (int i = 0; i < Cells.Length; i++) if (Cells[i] != null && Cells[i].stunTicks == 0) _scratch.Add(i);
                    if (_scratch.Count == 0) break;
                    boss.pendingCell = _scratch[_rng.Next(_scratch.Count)];
                    boss.pendingTicks = 16;
                    _m.Emit(new SimEvent(SimEventType.BossTelegraph, Index, boss.uid, boss.pendingCell, (int)BossSkill.LockCell));
                    break;
                }
                case BossSkill.VoidShield:
                    if (boss.pendingTicks >= 0 || boss.shieldTicks > 0) break;
                    boss.pendingCell = -1;
                    boss.pendingTicks = 16;
                    _m.Emit(new SimEvent(SimEventType.BossTelegraph, Index, boss.uid, -1, (int)BossSkill.VoidShield));
                    break;
                case BossSkill.Heal:
                {
                    int bx, by;
                    PathMath.Point(_b, boss.distance, out bx, out by);
                    for (int i = 0; i < Enemies.Count; i++)
                    {
                        var e = Enemies[i];
                        if (!e.alive) continue;
                        int ex, ey;
                        PathMath.Point(_b, e.distance, out ex, out ey);
                        if (Dist2(bx, by, ex, ey) <= 1500L * 1500L) e.hp = Math.Min(e.maxHp, e.hp + e.maxHp * 15 / 100);
                    }
                    break;
                }
            }
        }

        /// <summary>Second half of a telegraphed boss skill (after the 0.8 s warning).</summary>
        void ResolveBossSkill(EnemyInstance boss, EnemyDef def)
        {
            if (def.bossSkill == BossSkill.LockCell)
            {
                var u = boss.pendingCell >= 0 ? Cells[boss.pendingCell] : null;
                if (u != null) StunUnit(u, 30, boss.uid);
            }
            else if (def.bossSkill == BossSkill.VoidShield)
            {
                boss.shield = Math.Max(1, boss.maxHp * 8 / 100);
                boss.shieldTicks = 60;
                _m.Emit(new SimEvent(SimEventType.EnemyAbilityUsed, Index, boss.uid, 100 + (int)BossSkill.VoidShield, boss.shield));
            }
            boss.pendingCell = -1;
        }

        /// <summary>Stuns an animal unless an Aegis Ram shield absorbs it.</summary>
        void StunUnit(UnitInstance u, int ticks, int sourceUid)
        {
            if (u.guardTicks > 0 && u.guardCharges > 0)
            {
                u.guardCharges--;
                _m.Emit(new SimEvent(SimEventType.GuardBlocked, Index, u.uid));
                return;
            }
            u.stunTicks = Math.Max(u.stunTicks, ticks);
            _m.Emit(new SimEvent(SimEventType.UnitStunned, Index, u.uid, sourceUid));
        }

        /// <summary>One-time HP thresholds: scorpion enrage (30%) and gorilla overdrive (50%).</summary>
        void UpdatePhases(EnemyInstance e, EnemyDef def)
        {
            if (def.ability == EnemyAbility.Enrage && !e.enraged && (long)e.hp * 100 < (long)e.maxHp * 30)
            {
                e.enraged = true;
                _m.Emit(new SimEvent(SimEventType.EnemyAbilityUsed, Index, e.uid, (int)EnemyAbility.Enrage, 0));
            }
            if (def.bossSkill == BossSkill.LockCell && !e.phaseDone && (long)e.hp * 2 < e.maxHp)
            {
                e.phaseDone = true;
                _m.Emit(new SimEvent(SimEventType.EnemyAbilityUsed, Index, e.uid, 100 + (int)BossSkill.LockCell, 0));
            }
        }

        void UseAbility(EnemyInstance e, EnemyDef def)
        {
            switch (def.ability)
            {
                case EnemyAbility.Dash:
                    e.hastePercent = Math.Max(e.hasteTicks > 0 ? e.hastePercent : 0, def.abilityValue);
                    e.hasteTicks = Math.Max(e.hasteTicks, 14);
                    _m.Emit(new SimEvent(SimEventType.EnemyAbilityUsed, Index, e.uid, (int)def.ability, 0));
                    break;
                case EnemyAbility.DrumHaste:
                {
                    int ex, ey, n = 0;
                    PathMath.Point(_b, e.distance, out ex, out ey);
                    for (int i = 0; i < Enemies.Count && n < 3; i++)
                    {
                        var o = Enemies[i];
                        if (o == e || !o.alive) continue;
                        int ox, oy;
                        PathMath.Point(_b, o.distance, out ox, out oy);
                        if (Dist2(ex, ey, ox, oy) > 1200L * 1200L) continue;
                        o.hastePercent = Math.Max(o.hasteTicks > 0 ? o.hastePercent : 0, def.abilityValue);
                        o.hasteTicks = Math.Max(o.hasteTicks, 40);
                        n++;
                    }
                    _m.Emit(new SimEvent(SimEventType.EnemyAbilityUsed, Index, e.uid, (int)def.ability, n));
                    break;
                }
            }
        }

        EnemyInstance CreateEnemyDeferred(int defIndex, Element element, int hpPermille, int sender, int distance)
        {
            // Same as CreateEnemy but not added to the list while it is being iterated.
            var def = _c.Enemies[defIndex];
            int hp = (int)Math.Max(1L, (long)def.hp * hpPermille / 1000);
            var e = new EnemyInstance
            {
                uid = _m.NewUid(), def = defIndex,
                element = def.element != Element.None ? def.element : element,
                hp = hp, maxHp = hp, distance = distance, prevDistance = distance,
                revealed = !def.invisible, sender = sender,
            };
            _m.Emit(new SimEvent(SimEventType.EnemySpawned, Index, e.uid, defIndex));
            return e;
        }

        void Leak(EnemyInstance e, EnemyDef def)
        {
            e.alive = false;
            Leaks++;
            Hearts = e.raid ? 0 : Math.Max(0, Hearts - def.leakHearts);   // the raid boss through the gate ends the raid
            _m.Emit(new SimEvent(SimEventType.EnemyLeaked, Index, e.uid, def.leakHearts, e.sender));
            if (e.sender >= 0 && e.sender != Index) _m.Boards[e.sender].HeartsDealt += def.leakHearts;

            if (def.stealCandy > 0 && e.sender != Index)
            {
                int amount = Math.Min(def.stealCandy, Candy);
                if (amount > 0)
                {
                    Candy -= amount;
                    if (e.sender >= 0) _m.Boards[e.sender].Candy += amount;   // wave thieves (raccoon) just run off with it
                    _m.Emit(new SimEvent(SimEventType.CandyStolen, Index, amount, e.sender));
                }
            }
        }

        void UpdateUnits()
        {
            for (int cell = 0; cell < Cells.Length; cell++)
            {
                var u = Cells[cell];
                if (u == null) continue;
                if (u.hasteTicks > 0) u.hasteTicks--;
                if (u.guardTicks > 0 && --u.guardTicks == 0) { u.guardCharges = 0; u.guardBonus = 0; }
                if (u.stunTicks > 0) { u.stunTicks--; continue; }
                if (IsCellLocked(cell)) continue;
                if (_m.Wave >= 1 && u.level >= _b.skillMinLevel && u.energy < SkillCharge(u)) u.energy++;
                var def = _c.Units[u.def];
                if ((def.skill == UnitSkill.NectarHaste || def.skill == UnitSkill.AegisGuard) && _m.Wave >= 1 && --u.supportTimer <= 0)
                {
                    u.supportTimer = _b.supportPulseTicks;
                    SupportPulse(u, def);
                }
                if (def.damage <= 0) continue;
                if (u.cooldown > 0) { u.cooldown--; if (u.cooldown > 0) continue; }

                var target = SelectTarget(def);
                if (target == null) continue;   // stays ready, fires as soon as something appears
                u.cooldown = EffectiveInterval(u, def);
                u.lastTargetUid = target.uid;
                Attack(u, def, target);
            }
        }

        /// <summary>Nectar Bee (attack speed to the nearest attackers) and Aegis Ram (shields + blessing).</summary>
        void SupportPulse(UnitInstance src, UnitDef def)
        {
            bool bee = def.skill == UnitSkill.NectarHaste;
            int count = bee ? (src.level >= 7 ? 4 : 3) : def.skillValue + (src.level >= 4 ? 1 : 0);
            int col = src.cell % _b.gridCols, row = src.cell / _b.gridCols;
            for (int k = 0; k < count; k++)
            {
                UnitInstance best = null;
                int bestScore = int.MaxValue;
                for (int i = 0; i < Cells.Length; i++)
                {
                    var t = Cells[i];
                    if (t == null || t == src) continue;
                    var td = _c.Units[t.def];
                    if (bee && (td.damage <= 0 || td.role == Role.Support)) continue;   // never buffs supports (no loops)
                    if (bee ? (t.hasteTicks > 40) : (t.guardTicks > 40)) continue;      // spread over the team
                    int dist = Math.Abs(i % _b.gridCols - col) + Math.Abs(i / _b.gridCols - row);
                    int score = dist * 100 + i;
                    if (score < bestScore) { bestScore = score; best = t; }
                }
                if (best == null) break;
                if (bee)
                {
                    int pct = src.level >= 7 ? def.skillValue2 + 5 : (src.level >= 4 ? def.skillValue2 : def.skillValue);
                    best.hastePercent = Math.Max(best.hasteTicks > 0 ? best.hastePercent : 0, pct);
                    best.hasteTicks = 80;
                }
                else
                {
                    best.guardTicks = 80;
                    best.guardCharges = Math.Max(best.guardCharges, src.level >= 7 ? 2 : 1);
                    best.guardBonus = Math.Max(best.guardBonus, def.skillValue2 + (src.level >= 4 ? 5 : 0) + (src.level >= 7 ? 5 : 0));
                }
                _m.Emit(new SimEvent(SimEventType.UnitBuffed, Index, best.uid, bee ? 0 : 1, src.uid));
            }
        }

        int EffectiveInterval(UnitInstance u, UnitDef def)
        {
            int bonus = 0;
            if (u.hasteTicks > 0) bonus += u.hastePercent;
            int col = u.cell % _b.gridCols, row = u.cell / _b.gridCols;
            bonus += AuraFrom(col - 1, row) + AuraFrom(col + 1, row) + AuraFrom(col, row - 1) + AuraFrom(col, row + 1);
            if (bonus > 100) bonus = 100;
            if (IsRaging) bonus += RagePercent;
            return Math.Max(1, def.attackIntervalTicks * 100 / (100 + bonus));
        }

        int AuraFrom(int col, int row)
        {
            if (col < 0 || row < 0 || col >= _b.gridCols || row >= _b.gridRows) return 0;
            var u = Cells[row * _b.gridCols + col];
            if (u == null) return 0;
            var d = _c.Units[u.def];
            return d.skill == UnitSkill.AttackSpeedAura ? d.skillValue + d.skillValue2 * (u.level - 1) : 0;
        }

        EnemyInstance SelectTarget(UnitDef def)
        {
            EnemyInstance best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (!e.alive || (!e.revealed && !def.canHitInvisible)) continue;
                int score;
                switch (def.targetMode)
                {
                    case TargetMode.Strongest: score = e.hp; break;
                    case TargetMode.Densest: score = CountNear(e, def.splashRadius) * 100000 + e.distance; break;
                    default: score = e.distance; break;
                }
                if (score > bestScore) { bestScore = score; best = e; }
            }
            return best;
        }

        int CountNear(EnemyInstance center, int radius)
        {
            int cx, cy, n = 0;
            PathMath.Point(_b, center.distance, out cx, out cy);
            long r2 = (long)radius * radius;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (!e.alive) continue;
                int ex, ey;
                PathMath.Point(_b, e.distance, out ex, out ey);
                if (Dist2(cx, cy, ex, ey) <= r2) n++;
            }
            return n;
        }

        void Attack(UnitInstance u, UnitDef def, EnemyInstance target)
        {
            u.attackCount++;
            int lvl = u.level;
            bool l4 = lvl >= 4, l7 = lvl >= 7;
            int nth = u.attackCount;
            var skill = def.skill;

            _hitList.Clear();
            int radius = def.splashRadius;
            if (l7 && ((skill == UnitSkill.ArmorBreak && nth % 4 == 0) || skill == UnitSkill.RootArea)) radius = 1000;
            bool splash = radius > 0;
            if (splash)
            {
                int tx, ty;
                PathMath.Point(_b, target.distance, out tx, out ty);
                long r2 = (long)radius * radius;
                for (int i = 0; i < Enemies.Count; i++)
                {
                    var e = Enemies[i];
                    if (!e.alive || (!e.revealed && !def.canHitInvisible)) continue;
                    int ex, ey;
                    PathMath.Point(_b, e.distance, out ex, out ey);
                    if (e == target || Dist2(tx, ty, ex, ey) <= r2) _hitList.Add(e);
                }
            }
            else
            {
                _hitList.Add(target);
                if (skill == UnitSkill.Pierce) AddPierced(target, def, def.skillValue + (l4 ? 1 : 0) - 1);
            }

            int targetDamage = 0, matchup = 0;
            int levelPct = _b.levelDamagePercent[lvl - 1];
            int posPct = PositionPercent(u);
            bool crit = HasPerk(PerkType.LuckyCrit) && _rng.Chance(15);
            for (int i = 0; i < _hitList.Count; i++)
            {
                var e = _hitList[i];
                var ed = _c.Enemies[e.def];
                bool bossLike = ed.kind == EnemyKind.Boss || ed.bossSkill != BossSkill.None;
                bool big = bossLike || ed.kind == EnemyKind.Elite;

                if (def.canHitInvisible) e.revealed = true;
                if (def.slowPercent > 0)
                {
                    e.slowPercent = Math.Max(e.slowTicks > 0 ? e.slowPercent : 0, Math.Min(80, def.slowPercent + 2 * (lvl - 1)));
                    e.slowTicks = Math.Max(e.slowTicks, def.slowTicks);
                }
                if (def.stunTicks > 0)
                    e.stunTicks = Math.Max(e.stunTicks, bossLike ? def.stunTicks / 2 : def.stunTicks);
                // Push-backs have a per-monster cooldown, otherwise a monster can be held in place forever.
                if (skill == UnitSkill.Knockback && _m.Tick >= e.pushImmuneUntil)
                {
                    int push = def.skillValue * (100 + 10 * (lvl - 1)) / 100 * (100 - ed.knockbackResistPercent) / 100;
                    e.distance = Math.Max(0, e.distance - push);
                    e.pushImmuneUntil = _m.Tick + _b.pushImmuneTicks;
                }
                if (skill == UnitSkill.PullStun && e == target && _m.Tick >= e.pushImmuneUntil)
                {
                    e.distance = Math.Max(0, e.distance - def.skillValue * (100 - ed.knockbackResistPercent) / 100);
                    e.stunTicks = Math.Max(e.stunTicks, bossLike ? def.skillValue2 / 2 : def.skillValue2);
                    e.pushImmuneUntil = _m.Tick + _b.pushImmuneTicks + 20;
                }
                if (skill == UnitSkill.Burn && l4)
                {
                    e.burnTicks = Math.Max(e.burnTicks, 60);
                    e.burnDamage = Math.Max(e.burnDamage, Math.Max(1, def.skillValue * levelPct / 100 * _cardPct[u.def] / 100 / 2));
                }
                if (skill != UnitSkill.None && skill >= UnitSkill.BurnStack) ApplyExpansionEffect(u, def, e, target, bossLike, nth, levelPct);

                int dmg = def.damage * levelPct / 100 * _cardPct[u.def] / 100 * posPct / 100;
                if (crit) dmg *= 2;
                if (skill == UnitSkill.Pierce && e != target) dmg = dmg * 70 / 100;
                bool always = skill == UnitSkill.AlwaysCounter;
                dmg = dmg * ElementRules.DamagePercent(_b, def.element, e.element, always, big) / 100;
                int armor = ed.armorPercent - (e.shredTicks > 0 ? e.shredPercent : 0);
                if (armor < 0) armor /= 2;   // armor broken below zero: half of it becomes extra damage
                if (armor != 0) dmg = dmg * (100 - armor) / 100;
                if (splash && ed.areaResistPercent > 0) dmg = dmg * (100 - ed.areaResistPercent) / 100;
                if (dmg < 1) dmg = 1;

                if (skill == UnitSkill.Burst && e == target)
                {
                    // Several quick shots; armor counts on each one. The last shot bites slowed enemies at level 7.
                    int shots = def.skillValue + (l4 ? 1 : 0), total = 0;
                    for (int k = 0; k < shots && e.alive; k++)
                    {
                        int shot = (l7 && k == shots - 1 && e.slowTicks > 0) ? dmg * 150 / 100 : dmg;
                        total += shot;
                        ApplyDamage(e, shot);
                    }
                    targetDamage = total;
                    matchup = ElementRules.Matchup(def.element, e.element, always);
                    continue;
                }
                if (e == target) { targetDamage = dmg; matchup = ElementRules.Matchup(def.element, e.element, always); }
                ApplyDamage(e, dmg);
            }
            var ev = new SimEvent(SimEventType.UnitAttacked, Index, u.uid, target.uid, targetDamage);
            ev.d = matchup;
            if (matchup > 0) EffectiveHits++;
            _m.Emit(ev);
        }

        /// <summary>Ember Boar: also hits the next enemies behind the target along the path.</summary>
        void AddPierced(EnemyInstance target, UnitDef def, int extra)
        {
            for (int k = 0; k < extra; k++)
            {
                EnemyInstance best = null;
                int last = _hitList[_hitList.Count - 1].distance;
                for (int i = 0; i < Enemies.Count; i++)
                {
                    var e = Enemies[i];
                    if (!e.alive || _hitList.Contains(e) || (!e.revealed && !def.canHitInvisible)) continue;
                    if (e.distance > last || last - e.distance > 2000) continue;
                    if (best == null || e.distance > best.distance) best = e;
                }
                if (best == null) return;
                _hitList.Add(best);
            }
        }

        /// <summary>Status effects of the Expansion 1 animals (applied before the hit's damage).</summary>
        void ApplyExpansionEffect(UnitInstance u, UnitDef def, EnemyInstance e, EnemyInstance target, bool bossLike, int nth, int levelPct)
        {
            bool l4 = u.level >= 4, l7 = u.level >= 7;
            int now = _m.Tick;
            switch (def.skill)
            {
                case UnitSkill.BurnStack:
                {
                    int pulse = Math.Max(1, def.skillValue * levelPct / 100 * _cardPct[u.def] / 100);
                    AddBurn(e, pulse, l4 ? 80 : 60);
                    if (l7 && nth % 5 == 0 && e == target)
                    {
                        var near = NearestOther(e, 700);
                        if (near != null) AddBurn(near, pulse, l4 ? 80 : 60);
                    }
                    break;
                }
                case UnitSkill.Pierce:
                    if (l7 && e == target && !bossLike && now >= u.knockbackReadyTick && now >= e.pushImmuneUntil)
                    {
                        e.pushImmuneUntil = now + _b.pushImmuneTicks;
                        var ed = _c.Enemies[e.def];
                        e.distance = Math.Max(0, e.distance - 200 * (100 - ed.knockbackResistPercent) / 100);
                        u.knockbackReadyTick = now + 100;
                    }
                    break;
                case UnitSkill.ArmorBreak:
                {
                    int pct = l4 ? def.skillValue2 : def.skillValue;
                    if (e.shredTicks == 0 || pct >= e.shredPercent) { e.shredPercent = pct; e.shredTicks = 80; }
                    if (e == target) _m.Emit(new SimEvent(SimEventType.StatusApplied, Index, e.uid, 4));
                    break;
                }
                case UnitSkill.ChillSlow:
                    Slow(e, l4 ? def.skillValue2 : def.skillValue, 40);
                    if (l7 && nth % 6 == 0 && e == target)
                    {
                        if (bossLike) Slow(e, 15, 40);
                        else { e.stunTicks = Math.Max(e.stunTicks, 10); _m.Emit(new SimEvent(SimEventType.StatusApplied, Index, e.uid, 3)); }
                    }
                    break;
                case UnitSkill.Vulnerable:
                {
                    int pct = l4 ? def.skillValue2 : def.skillValue;
                    MarkVulnerable(e, pct, 60);
                    if (e == target) _m.Emit(new SimEvent(SimEventType.StatusApplied, Index, e.uid, 5));
                    if (l7 && nth % 5 == 0 && e == target)
                    {
                        var a = NearestOther(e, 800);
                        if (a != null) { MarkVulnerable(a, pct, 60); var b2 = NearestOther(e, 800, a); if (b2 != null) MarkVulnerable(b2, pct, 60); }
                    }
                    break;
                }
                case UnitSkill.RootArea:
                    if (bossLike) Slow(e, 12, 30);
                    else if (now >= e.rootImmuneUntil)
                    {
                        int root = l4 ? def.skillValue2 : def.skillValue;
                        e.stunTicks = Math.Max(e.stunTicks, root);
                        e.rootImmuneUntil = now + 80;
                        if (l7) Slow(e, 15, root + 20);
                        _m.Emit(new SimEvent(SimEventType.StatusApplied, Index, e.uid, 1));
                    }
                    break;
                case UnitSkill.Silence:
                    if (e == target && nth % 4 == 0 && now >= e.silenceImmuneUntil)
                    {
                        int dur = l4 ? def.skillValue2 : def.skillValue;
                        if (bossLike) { dur /= 2; if (e.pendingTicks >= 0) e.pendingTicks += 5; }
                        e.silenceTicks = Math.Max(e.silenceTicks, dur);
                        e.silenceImmuneUntil = now + 100;
                        e.hasteTicks = 0;   // silence also ends drum / dash speed-ups
                        if (l7) Slow(e, 20, 30);
                        _m.Emit(new SimEvent(SimEventType.StatusApplied, Index, e.uid, 2));
                    }
                    break;
            }
        }

        static void AddBurn(EnemyInstance e, int pulse, int ticks)
        {
            e.burnStacks = Math.Min(3, e.burnStacks + 1);
            e.burnDamage = Math.Max(e.burnDamage, pulse);
            e.burnTicks = Math.Max(e.burnTicks, ticks);
        }

        static void Slow(EnemyInstance e, int percent, int ticks)
        {
            e.slowPercent = Math.Max(e.slowTicks > 0 ? e.slowPercent : 0, percent);
            e.slowTicks = Math.Max(e.slowTicks, ticks);
        }

        static void MarkVulnerable(EnemyInstance e, int percent, int ticks)
        {
            if (e.vulnTicks == 0 || percent >= e.vulnPercent) e.vulnPercent = percent;   // strongest mark only
            e.vulnTicks = Math.Max(e.vulnTicks, ticks);
        }

        EnemyInstance NearestOther(EnemyInstance center, int radius, EnemyInstance skip = null)
        {
            int cx, cy;
            PathMath.Point(_b, center.distance, out cx, out cy);
            EnemyInstance best = null;
            long bestD = (long)radius * radius;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e == center || e == skip || !e.alive) continue;
                int ex, ey;
                PathMath.Point(_b, e.distance, out ex, out ey);
                long d = Dist2(cx, cy, ex, ey);
                if (d <= bestD) { bestD = d; best = e; }
            }
            return best;
        }

        void ApplyDamage(EnemyInstance e, int dmg)
        {
            if (!e.alive) return;
            if (e.vulnTicks > 0) dmg = dmg * (100 + e.vulnPercent) / 100;
            if (e.raid) { _m.DamageRaid(dmg); return; }   // shared pool: the match kills both copies
            if (e.shield > 0)
            {
                int absorbed = Math.Min(e.shield, dmg);
                e.shield -= absorbed;
                dmg -= absorbed;
                if (e.shield == 0)
                {
                    // Broken void shield: the crab is exposed for 2 s.
                    e.shieldTicks = 0;
                    MarkVulnerable(e, 15, 40);
                    _m.Emit(new SimEvent(SimEventType.ShieldBroken, Index, e.uid));
                }
                if (dmg <= 0) return;
            }
            e.hp -= dmg;
            if (e.hp <= 0) Kill(e);
        }

        void Kill(EnemyInstance e)
        {
            e.alive = false;
            e.hp = 0;
            var def = _c.Enemies[e.def];
            Kills++;
            if (def.kind == EnemyKind.Boss) BossKills++;
            int reward = KillReward(def);
            if (_m.Tick < CandyRainUntilTick) reward = reward * (100 + CandyRainPercent) / 100;
            Candy += reward;
            SendPoints += def.sendPoints;
            if (HasPerk(PerkType.SendPoints))
            {
                _pointRemainder += def.sendPoints * 50;
                SendPoints += _pointRemainder / 100;
                _pointRemainder %= 100;
            }
            _m.Emit(new SimEvent(SimEventType.EnemyKilled, Index, e.uid, reward));
            if (def.kind == EnemyKind.Boss && e.sender < 0 && _m.IsBossDuelOpen) _m.WinBossDuel(Index);

            for (int i = 0; i < Cells.Length; i++)
            {
                var u = Cells[i];
                if (u == null) continue;
                var ud = _c.Units[u.def];
                if (ud.skill == UnitSkill.LuckyCandy && _rng.Chance(ud.skillValue + ud.skillValue2 * (u.level - 1)))
                    GainCandy(reward);
            }

            if (def.locksCellOnDeath) LockRandomCell();
        }

        int KillReward(EnemyDef def)
        {
            return def.candyReward * (100 + _b.killCandyGrowthPercentPerWave * Math.Max(0, _m.Wave - 1)) / 100;
        }

        void LockRandomCell()
        {
            _scratch.Clear();
            for (int i = 0; i < Cells.Length; i++) if (Cells[i] != null && !IsCellLocked(i)) _scratch.Add(i);
            if (_scratch.Count == 0)
                for (int i = 0; i < Cells.Length; i++) if (!IsCellLocked(i)) _scratch.Add(i);
            if (_scratch.Count == 0) return;
            int cell = _scratch[_rng.Next(_scratch.Count)];
            var guarded = Cells[cell];
            if (guarded != null && guarded.guardTicks > 0 && guarded.guardCharges > 0)
            {
                guarded.guardCharges--;
                _m.Emit(new SimEvent(SimEventType.GuardBlocked, Index, guarded.uid));
                return;
            }
            LockedUntilWave[cell] = _m.Wave + _b.lockWaves;
            _m.Emit(new SimEvent(SimEventType.CellLocked, Index, cell, LockedUntilWave[cell]));
        }

        static long Dist2(int ax, int ay, int bx, int by)
        {
            long dx = ax - bx, dy = ay - by;
            return dx * dx + dy * dy;
        }

        internal void HashInto(ref uint h)
        {
            Match.Mix(ref h, Hearts); Match.Mix(ref h, Candy); Match.Mix(ref h, SendPoints);
            Match.Mix(ref h, Purchases); Match.Mix(ref h, Kills); Match.Mix(ref h, Leaks);
            Match.Mix(ref h, (int)_rng.State);
            for (int i = 0; i < ElementLevel.Length; i++) Match.Mix(ref h, ElementLevel[i]);
            for (int i = 0; i < DeckLevel.Length; i++) Match.Mix(ref h, DeckLevel[i]);
            for (int i = 0; i < Perks.Count; i++) Match.Mix(ref h, (int)Perks[i]);
            for (int i = 0; i < SpellReadyTick.Length; i++) Match.Mix(ref h, SpellReadyTick[i]);
            Match.Mix(ref h, BalloonId); Match.Mix(ref h, RageUntilTick); Match.Mix(ref h, CandyRainUntilTick);
            Match.Mix(ref h, IncomingSends.Count); Match.Mix(ref h, Income); Match.Mix(ref h, HeartsDealt);
            for (int i = 0; i < IncomingArriveTick.Count; i++) Match.Mix(ref h, IncomingArriveTick[i]);
            for (int i = 0; i < Cells.Length; i++)
            {
                var u = Cells[i];
                Match.Mix(ref h, u == null ? -1 : u.def * 16 + u.level);
                Match.Mix(ref h, u == null ? 0 : u.energy);
                Match.Mix(ref h, u == null ? 0 : u.hasteTicks * 7 + u.guardCharges * 131 + u.attackCount);
                Match.Mix(ref h, LockedUntilWave[i]);
            }
            for (int i = 0; i < Shop.Length; i++) Match.Mix(ref h, Shop[i]);
            Match.Mix(ref h, Enemies.Count);
            for (int i = 0; i < Enemies.Count; i++)
            {
                Match.Mix(ref h, Enemies[i].uid); Match.Mix(ref h, Enemies[i].hp); Match.Mix(ref h, Enemies[i].distance);
                Match.Mix(ref h, Enemies[i].shield + Enemies[i].burnStacks * 1009 + Enemies[i].silenceTicks * 31);
            }
        }
    }
}
