using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    /// <summary>
    /// Simple opponent for practice matches and for matchmaking fallback. It only produces Commands,
    /// exactly like a human, so bot matches use the same code path as PvP.
    /// </summary>
    public sealed class BotBrain
    {
        public readonly int Player;
        /// <summary>0 = easy, 1 = normal, 2 = hard. Controls reaction time and how well it merges and sends.</summary>
        public readonly int Difficulty;

        DetRandom _rng;
        int _nextThinkTick;
        readonly List<int> _tmp = new List<int>();

        public BotBrain(int player, int difficulty, int seed)
        {
            Player = player;
            Difficulty = difficulty < 0 ? 0 : (difficulty > 2 ? 2 : difficulty);
            _rng = new DetRandom(seed);
        }

        int ThinkInterval { get { return Difficulty == 0 ? 30 : (Difficulty == 1 ? 18 : 10); } }

        /// <summary>When false the bot ignores the newer mechanics (used to compare balance with the classic game).</summary>
        public bool UseAdvanced = true;

        public void Think(Match m, List<Command> output)
        {
            if (m.IsOver || m.Tick < _nextThinkTick) return;
            _nextThinkTick = m.Tick + ThinkInterval + _rng.Next(6);
            var b = m.Boards[Player];
            int tick = m.Tick;

            if (UseAdvanced)
            {
                if (b.PerkOffer.Count > 0 && _rng.Chance(Difficulty == 0 ? 30 : 80)) { output.Add(new Command(tick, Player, CommandType.ChoosePerk, PickPerk(m, b))); return; }
                if (b.BalloonId != 0 && m.Tick - b.BalloonSpawnTick > 20 && _rng.Chance(Difficulty == 0 ? 25 : (Difficulty == 1 ? 55 : 85)))
                { output.Add(new Command(tick, Player, CommandType.PopBalloon, b.BalloonId)); return; }
                if (TryReflect(m, b, tick, output)) return;
                if (TrySkill(m, b, tick, output)) return;
                if (TrySpell(m, b, tick, output)) return;
            }
            if (TrySend(m, b, tick, output)) return;
            bool full = b.CountFreeCells() == 0;
            if (UseAdvanced && (full || b.CountFreeCells() <= 3) && _rng.Chance(40) && TryUpgradeUnit(m, b, tick, output, full)) return;
            // Merging only when the board is full turned out stronger in bot-vs-bot tests; easy bots merge carelessly.
            if ((full || (Difficulty == 0 && _rng.Chance(25))) && TryMerge(m, b, tick, output)) return;
            if (!full && b.Candy >= b.CardPrice)
            {
                // Current game: pick an animal (costs a little more) or the cheaper random Summon.
                // Smart bots pick one that merges with a level-1 animal already on the board, into a free star cell.
                int pick = UseAdvanced && Difficulty > 0 ? PickSummonSlot(m, b) : -1;
                if (pick >= 0) output.Add(new Command(tick, Player, CommandType.SummonUnit, pick, FreeStarCell(m, b)));
                else if (UseAdvanced) output.Add(new Command(tick, Player, CommandType.Summon, Difficulty > 0 ? FreeStarCell(m, b) : -1));
                else output.Add(new Command(tick, Player, CommandType.BuyCard, PickShopSlot(b)));
                return;
            }
            if (UseAdvanced && TryUpgradeUnit(m, b, tick, output, full)) return;
            if (full && b.Candy >= b.CardPrice + 60 && TryMerge(m, b, tick, output)) return;
        }

        // ------------------------------------------------------------------ new mechanics

        /// <summary>Deck slot worth paying extra for (a merge partner on the board), or -1 to take the random summon.</summary>
        int PickSummonSlot(Match m, Board b)
        {
            int best = -1, n = b.Deck.Length, start = n > 0 ? _rng.Next(n) : 0;
            for (int k = 0; k < n; k++)
            {
                int s = (start + k) % n;
                if (b.Candy < b.SummonPrice(s)) continue;
                // Pure supports (no damage) help the team but cannot carry it: keep at most two on the board.
                if (m.Content.Units[b.Deck[s]].damage <= 0 && b.CountOnBoard(b.Deck[s]) >= 2) continue;
                for (int i = 0; i < b.Cells.Length; i++)
                    if (b.Cells[i] != null && b.Cells[i].def == b.Deck[s] && b.Cells[i].level == 1) { best = s; break; }
                if (best >= 0) break;
            }
            if (best >= 0 && _rng.Chance(Difficulty == 1 ? 60 : 85)) return best;
            return -1;
        }

        static int FreeStarCell(Match m, Board b)
        {
            for (int i = 0; i < b.Cells.Length; i++)
                if (b.Cells[i] == null && !b.IsCellLocked(i) && m.IsStarCell(i)) return i;
            return -1;
        }

        /// <summary>Power up the deck animal with the most levels on the board.</summary>
        bool TryUpgradeUnit(Match m, Board b, int tick, List<Command> output, bool full)
        {
            if (Difficulty == 0 || (!full && b.CountFreeCells() > 3)) return false;
            var power = new int[b.Deck.Length];
            for (int i = 0; i < b.Cells.Length; i++)
            {
                var u = b.Cells[i];
                if (u == null) continue;
                int slot = b.DeckSlotOf(u.def);
                if (slot >= 0) power[slot] += u.level;
            }
            int best = -1;
            for (int s = 0; s < power.Length; s++)
                if (b.DeckLevel[s] < m.Balance.unitUpgradeMax && power[s] > 0 && (best < 0 || power[s] > power[best])) best = s;   // power > 0: on the board
            if (best < 0) return false;
            if (b.Candy < b.UnitUpgradeCost(best) + (full ? 0 : b.CardPrice)) return false;
            output.Add(new Command(tick, Player, CommandType.UpgradeUnit, best));
            return true;
        }

        int PickCell(Match m, Board b, int unitDef)
        {
            var element = m.Content.Units[unitDef].element;
            int best = -1, bestScore = int.MinValue;
            int cols = m.Balance.gridCols, rows = m.Balance.gridRows;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                if (b.Cells[i] != null || b.IsCellLocked(i)) continue;
                int score = m.IsStarCell(i) ? 40 : 0;
                int c = i % cols, r = i / cols;
                score += 10 * (Same(m, b, c - 1, r, element) + Same(m, b, c + 1, r, element) + Same(m, b, c, r - 1, element) + Same(m, b, c, r + 1, element));
                score += _rng.Next(6);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        static int Same(Match m, Board b, int c, int r, Element e)
        {
            if (c < 0 || r < 0 || c >= m.Balance.gridCols || r >= m.Balance.gridRows) return 0;
            var u = b.Cells[r * m.Balance.gridCols + c];
            return u != null && m.Content.Units[u.def].element == e ? 1 : 0;
        }

        int PickPerk(Match m, Board b)
        {
            if (Difficulty == 0) return _rng.Next(b.PerkOffer.Count);
            // Power for the element with the most unit levels on the board, else a generic good one.
            var power = new int[5];
            for (int i = 0; i < b.Cells.Length; i++) if (b.Cells[i] != null) power[(int)m.Content.Units[b.Cells[i].def].element] += b.Cells[i].level;
            int best = 0, bestScore = -1;
            for (int k = 0; k < b.PerkOffer.Count; k++)
            {
                var p = b.PerkOffer[k];
                int score;
                switch (p)
                {
                    case PerkType.FirePower: score = power[1] * 3; break;
                    case PerkType.WaterPower: score = power[2] * 3; break;
                    case PerkType.GrassPower: score = power[3] * 3; break;
                    case PerkType.LightPower: score = power[4] * 3; break;
                    case PerkType.LuckyCrit: score = 18; break;
                    case PerkType.ExtraHearts: score = b.Hearts <= 5 ? 40 : 12; break;
                    case PerkType.Interest: score = m.Wave < 10 ? 20 : 8; break;
                    case PerkType.CheapCards: score = m.Wave < 10 ? 16 : 6; break;
                    default: score = 10; break;
                }
                score += _rng.Next(5);
                if (score > bestScore) { bestScore = score; best = k; }
            }
            return best;
        }

        bool TryReflect(Match m, Board b, int tick, List<Command> output)
        {
            if (b.IncomingSends.Count == 0 || Difficulty == 0) return false;
            if (!_rng.Chance(Difficulty == 1 ? 25 : 50)) return false;
            for (int i = 0; i < b.IncomingSends.Count; i++)
            {
                if (b.IncomingReflected[i]) continue;
                int cost = b.ReflectCost(i);
                var def = m.Content.Sends[b.IncomingSends[i]];
                // Only bounce heavy sends, and only with candy to spare.
                if ((def.cost >= 60 || def.cost == 0) && b.Candy >= cost + 40)
                {
                    output.Add(new Command(tick, Player, CommandType.Reflect, i));
                    return true;
                }
            }
            return false;
        }

        bool TrySkill(Match m, Board b, int tick, List<Command> output)
        {
            if (b.Enemies.Count == 0) return false;
            if (!_rng.Chance(Difficulty == 0 ? 15 : (Difficulty == 1 ? 45 : 80))) return false;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                var u = b.Cells[i];
                if (u == null || !b.IsSkillReady(u) || u.stunTicks > 0 || b.IsCellLocked(i)) continue;
                var role = m.Content.Units[u.def].role;
                // Area/Control skills wait for a crowd; damage skills fire at anything sturdy.
                if ((role == Role.Area || role == Role.Control) && b.Enemies.Count < 4 && !m.IsBossWave) continue;
                output.Add(new Command(tick, Player, CommandType.UseSkill, i));
                return true;
            }
            return false;
        }

        bool TrySpell(Match m, Board b, int tick, List<Command> output)
        {
            int enemies = 0;
            for (int i = 0; i < b.Enemies.Count; i++) if (b.Enemies[i].alive) enemies++;
            bool danger = enemies >= 7 || (m.IsBossWave && enemies > 0);
            if (!danger || !_rng.Chance(Difficulty == 0 ? 10 : (Difficulty == 1 ? 35 : 60))) return false;
            for (int s = 0; s < b.Spells.Length; s++)
            {
                var def = m.Content.Spells[b.Spells[s]];
                if (m.Tick < b.SpellReadyTick[s] || b.Candy < def.cost + 20) continue;
                if (def.kind == SpellKind.CandyRain && m.Wave > 12) continue;
                int target = def.NeedsTarget ? b.DensestDistance(def.radius) : 0;
                if (def.NeedsTarget && target < 0) continue;
                output.Add(new Command(tick, Player, CommandType.CastSpell, s, target));
                return true;
            }
            return false;
        }

        bool TryUpgradeElement(Match m, Board b, int tick, List<Command> output, bool full)
        {
            if (Difficulty == 0 || (!full && b.CountFreeCells() > 3)) return false;
            var power = new int[5];
            for (int i = 0; i < b.Cells.Length; i++) if (b.Cells[i] != null) power[(int)m.Content.Units[b.Cells[i].def].element] += b.Cells[i].level;
            int best = 0;
            for (int e = 1; e <= 4; e++) if (power[e] > power[best] && b.ElementLevel[e] < m.Balance.elementUpgradeMax) best = e;
            if (best == 0) return false;
            int cost = b.ElementUpgradeCost((Element)best);
            if (b.Candy < cost + (full ? 0 : b.CardPrice)) return false;
            output.Add(new Command(tick, Player, CommandType.UpgradeElement, best));
            return true;
        }

        /// <summary>
        /// PvP 2.0 sends cost candy and raise income. Early on bots invest in cheap high-income sends;
        /// later they save for heavy hitters. They always keep enough candy to summon when cells are free.
        /// </summary>
        bool TrySend(Match m, Board b, int tick, List<Command> output)
        {
            if (b.Opponent.IncomingSends.Count >= m.Balance.maxIncomingSendsPerWave) return false;
            int chance = Difficulty == 0 ? 12 : (Difficulty == 1 ? 30 : 45);
            if (!_rng.Chance(chance)) return false;
            int reserve = b.CountFreeCells() > 0 ? b.CardPrice : 0;
            bool late = m.Wave >= 7;

            int best = -1, bestScore = int.MinValue;
            for (int s = 0; s < b.SendDeck.Length; s++)
            {
                var def = m.Content.Sends[b.SendDeck[s]];
                if (def.oncePerMatch && b.SendUsed[s]) continue;
                int cost = b.SendCost(s);
                if (b.Candy < cost + reserve) continue;
                // Early: income per candy. Late: raw threat (price).
                int score = late ? cost : def.income * 1000 / Math.Max(1, cost);
                score += _rng.Next(5);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            if (best < 0) return false;
            output.Add(new Command(tick, Player, CommandType.SendMonster, best));
            return true;
        }

        bool TryMerge(Match m, Board b, int tick, List<Command> output)
        {
            int bestFrom = -1, bestTo = -1, bestLevel = int.MaxValue;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                var a = b.Cells[i];
                if (a == null || a.level >= m.Balance.maxLevel) continue;
                for (int j = i + 1; j < b.Cells.Length; j++)
                {
                    var c = b.Cells[j];
                    if (c == null || c.def != a.def || c.level != a.level || b.IsCellLocked(j)) continue;
                    if (a.level < bestLevel) { bestLevel = a.level; bestFrom = i; bestTo = j; }
                }
            }
            if (bestFrom < 0) return false;
            output.Add(new Command(tick, Player, CommandType.Move, bestFrom, bestTo));
            return true;
        }

        int PickShopSlot(Board b)
        {
            // Prefer a card that can merge with a level-1 unit already on the board.
            _tmp.Clear();
            for (int s = 0; s < b.Shop.Length; s++)
                for (int i = 0; i < b.Cells.Length; i++)
                    if (b.Cells[i] != null && b.Cells[i].def == b.Shop[s] && b.Cells[i].level == 1) { _tmp.Add(s); break; }
            if (_tmp.Count > 0 && Difficulty > 0) return _tmp[_rng.Next(_tmp.Count)];
            return _rng.Next(b.Shop.Length);
        }

        /// <summary>Random but valid deck for a bot opponent.</summary>
        public static string[] RandomDeck(ContentDatabase content, int seed, int size = 5)
        {
            var rng = new DetRandom(seed);
            var pool = new List<string>();
            for (int i = 0; i < content.Units.Count; i++) pool.Add(content.Units[i].id);
            var deck = new string[size];
            int supports = 0, damage = 0;
            for (int i = 0; i < size; i++)
            {
                // At most one Support and at least two damage dealers keep bot decks playable.
                string pick;
                Role role;
                int guard = 0;
                do
                {
                    pick = pool[rng.Next(pool.Count)];
                    role = content.Unit(pick).role;
                } while (++guard < 50 && ((role == Role.Support && supports >= 1) || (i >= size - 2 && damage < 2 && role != Role.Damage && role != Role.Area)));
                pool.Remove(pick);
                deck[i] = pick;
                if (role == Role.Support) supports++;
                if (role == Role.Damage || role == Role.Area) damage++;
            }
            return deck;
        }
    }
}
