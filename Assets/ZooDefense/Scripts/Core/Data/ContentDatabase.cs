using System;
using System.Collections.Generic;

namespace ZooDefense.Core
{
    /// <summary>
    /// Read-only lookup over ContentData. Index-based access is used inside the simulation
    /// (cheap and deterministic); string ids are used at the edges (UI, save data, network).
    /// </summary>
    public sealed class ContentDatabase
    {
        public readonly ContentData Data;
        public BalanceConfig Balance { get { return Data.balance; } }
        public IReadOnlyList<UnitDef> Units { get { return Data.units; } }
        public IReadOnlyList<EnemyDef> Enemies { get { return Data.enemies; } }
        public IReadOnlyList<SendDef> Sends { get { return Data.sends; } }
        public IReadOnlyList<SpellDef> Spells { get { return Data.spells; } }

        readonly Dictionary<string, int> _unitIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _enemyIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _sendIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _spellIndex = new Dictionary<string, int>();

        public ContentDatabase(ContentData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            for (int i = 0; i < data.units.Count; i++) _unitIndex[data.units[i].id] = i;
            for (int i = 0; i < data.enemies.Count; i++) _enemyIndex[data.enemies[i].id] = i;
            for (int i = 0; i < data.sends.Count; i++) _sendIndex[data.sends[i].id] = i;
            if (data.spells == null) data.spells = new List<SpellDef>();
            if (data.spells.Count == 0) data.spells.AddRange(DefaultContent.CreateSpells());   // older balance JSON
            for (int i = 0; i < data.spells.Count; i++) _spellIndex[data.spells[i].id] = i;
            Validate();
        }

        /// <summary>Same content with another balance (game modes). Units, enemies, sends and spells are shared.</summary>
        public ContentDatabase WithBalance(BalanceConfig balance)
        {
            return new ContentDatabase(new ContentData { balance = balance, units = Data.units, enemies = Data.enemies, sends = Data.sends, spells = Data.spells });
        }

        public int UnitIndex(string id) { int i; return id != null && _unitIndex.TryGetValue(id, out i) ? i : -1; }
        public int EnemyIndex(string id) { int i; return id != null && _enemyIndex.TryGetValue(id, out i) ? i : -1; }
        public int SendIndex(string id) { int i; return id != null && _sendIndex.TryGetValue(id, out i) ? i : -1; }
        public int SpellIndex(string id) { int i; return id != null && _spellIndex.TryGetValue(id, out i) ? i : -1; }
        public SpellDef Spell(string id) { int i = SpellIndex(id); return i < 0 ? null : Data.spells[i]; }

        public UnitDef Unit(string id) { int i = UnitIndex(id); return i < 0 ? null : Data.units[i]; }
        public EnemyDef Enemy(string id) { int i = EnemyIndex(id); return i < 0 ? null : Data.enemies[i]; }
        public SendDef Send(string id) { int i = SendIndex(id); return i < 0 ? null : Data.sends[i]; }

        void Validate()
        {
            var b = Data.balance;
            if (b.pathX == null || b.pathY == null || b.pathX.Length != b.pathY.Length || b.pathX.Length < 2)
                throw new InvalidOperationException("Balance path must have at least 2 points with matching X/Y arrays.");
            if (b.levelDamagePercent == null || b.levelDamagePercent.Length < b.maxLevel)
                throw new InvalidOperationException("levelDamagePercent needs one entry per level.");
            foreach (var id in b.normalEnemyIds) Require(EnemyIndex(id), "normal enemy", id);
            foreach (var id in b.eliteEnemyIds) Require(EnemyIndex(id), "elite enemy", id);
            foreach (var id in b.bossEnemyIds) Require(EnemyIndex(id), "boss enemy", id);
            foreach (var s in Data.sends) Require(EnemyIndex(s.enemyId), "send enemy", s.enemyId);
            foreach (var e in Data.enemies)
                if (e.bossSkill == BossSkill.Summon) Require(EnemyIndex(e.summonEnemyId), "summon enemy", e.summonEnemyId);
            foreach (var u in Data.units)
                if (u.attackIntervalTicks <= 0) throw new InvalidOperationException("Unit " + u.id + " needs attackIntervalTicks > 0");
        }

        static void Require(int index, string what, string id)
        {
            if (index < 0) throw new InvalidOperationException("Unknown " + what + " id: " + id);
        }
    }
}
