using System.Collections.Generic;
using UnityEngine;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// Renders one Board from the simulation state. Views are rebuilt from state every frame
    /// (state is the truth); SimEvents only add short-lived effects.
    /// Local coordinates of the "Field" child are tiles: cell (c, r) sits at (c+1, r+1).
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        /// <summary>World size of the board at scale 1 (computed from the path and grid).</summary>
        public float Width { get; private set; }
        public float Height { get; private set; }

        public Board Board { get; private set; }
        public bool Compact { get; private set; }

        MatchRunner _runner;
        ContentDatabase _content;
        BalanceConfig _b;
        FxPool _fx;
        Transform _field;
        SpriteRenderer[] _tiles;
        SpriteRenderer[] _locks;
        SpriteRenderer[] _highlights;
        SpriteRenderer[] _stars;
        SpriteRenderer _selected;
        readonly List<SpriteRenderer> _pathFill = new List<SpriteRenderer>();
        bool _pathGlow;
        /// <summary>Pulse the path (a spell is armed and waits for a tap on the path).</summary>
        public bool PathGlow { get { return _pathGlow; } set { _pathGlow = value; if (!value) foreach (var p in _pathFill) p.color = PathFill; } }
        SpriteRenderer _gate;
        Transform _balloon;
        SpriteRenderer _balloonBody;
        Transform _portalSwirl;
        float _gateFlash;
        Color _gateBase = Art.Hex(0xFFECC8);

        readonly Dictionary<int, UnitView> _units = new Dictionary<int, UnitView>();
        readonly Dictionary<int, EnemyView> _enemies = new Dictionary<int, EnemyView>();
        readonly List<int> _remove = new List<int>();
        readonly Stack<EnemyView> _enemyPool = new Stack<EnemyView>();
        readonly HashSet<int> _killed = new HashSet<int>();
        /// <summary>enemy uid * 16 + note: element / status notes already shown once for that monster.</summary>
        readonly HashSet<int> _noted = new HashSet<int>();
        readonly List<EnemyView> _dying = new List<EnemyView>();

        public int DraggingUid = -1;
        public Vector3 DragWorldPosition;

        // Board palette (zoo garden). Grass follows the arena tint.
        static readonly Color PathEdge = Art.Hex(0x96704A);
        static readonly Color PathFill = Art.Hex(0xECCE96);
        static readonly Color PathStone = Art.Hex(0xD6B47C);
        static readonly Color Chevron = Art.Hex(0xB08856);
        static readonly Color Fence = Art.Hex(0x784F30);
        static readonly Color FencePost = Art.Hex(0x8C603A);

        // Sorting inside the board, all below units (Order.Units = 10).
        const int OBase = -10, ODecor = -9, OPenFence = -8, OPen = -7, OPathEdge = -6, OPathFill = -5,
                  OPathDetail = -4, OCellShadow = -3, OCellLip = -2, OCellRim = -1, OCell = 0, OCellShine = 1,
                  OStar = 2, OGate = 4;

        public void Init(MatchRunner runner, int boardIndex, FxPool fx, bool compact, Color arenaTint)
        {
            _runner = runner;
            Board = runner.Match.Boards[boardIndex];
            _content = runner.Match.Content;
            _b = runner.Match.Balance;
            _fx = fx;
            Compact = compact;

            // Bounds of everything drawn, in field (tile) coordinates.
            float minX = 0.4f, maxX = _b.gridCols + 0.6f, minY = 0.4f, maxY = _b.gridRows + 0.6f;
            for (int i = 0; i < _b.pathX.Length; i++)
            {
                minX = Mathf.Min(minX, _b.pathX[i] / 1000f - 0.55f); maxX = Mathf.Max(maxX, _b.pathX[i] / 1000f + 0.55f);
                minY = Mathf.Min(minY, _b.pathY[i] / 1000f - 0.55f); maxY = Mathf.Max(maxY, _b.pathY[i] / 1000f + 0.55f);
            }
            maxY += 0.15f; // room for the gate arch
            Width = maxX - minX;
            Height = maxY - minY;
            var center = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);

            var grass = Color.Lerp(arenaTint, Art.Hex(0x5C9A4E), 0.5f);
            var grassDark = Color.Lerp(grass, Color.black, 0.38f);
            WorldKit.Box("border", transform, new Vector2(Width + 0.24f, Height + 0.24f), grassDark, OBase, Vector3.zero);
            WorldKit.Box("grass", transform, new Vector2(Width, Height), grass, OBase, Vector3.zero);
            _field = WorldKit.Node("Field", transform, -center).transform;

            if (!compact) ScatterDecor(minX, maxX, minY, maxY, grass);
            BuildPen(grass);
            BuildPath();
            BuildSpawnAndGate();

            // Grid cells: raised grass pedestals (dark lip + light top + soft shine + a few painted tufts).
            int cells = _b.gridCols * _b.gridRows;
            _tiles = new SpriteRenderer[cells];
            _locks = new SpriteRenderer[cells];
            _highlights = new SpriteRenderer[cells];
            _stars = new SpriteRenderer[cells];
            var topA = Art.Hex(0xA9D878);
            var topB = Art.Hex(0x9BCF6C);
            var lip = Art.Hex(0x5E9447);
            var rim = Art.Hex(0x7DB55A);
            var tuft = Art.Hex(0x6FA84E);
            var cellRng = new System.Random(23);
            for (int c = 0; c < cells; c++)
            {
                var pos = CellLocal(c);
                bool alt = (c % _b.gridCols + c / _b.gridCols) % 2 == 0;
                WorldKit.Box("cell_shadow" + c, _field, new Vector2(0.9f, 0.9f), new Color(0f, 0f, 0f, 0.22f), OCellShadow, pos + new Vector3(0.02f, -0.07f, 0f));
                WorldKit.Box("cell_lip" + c, _field, new Vector2(0.88f, 0.88f), lip, OCellLip, pos + new Vector3(0f, -0.04f, 0f));
                WorldKit.Box("cell_rim" + c, _field, new Vector2(0.88f, 0.84f), rim, OCellRim, pos + new Vector3(0f, 0.02f, 0f));
                _tiles[c] = WorldKit.Box("cell" + c, _field, new Vector2(0.8f, 0.76f), alt ? topA : topB, OCell, pos + new Vector3(0f, 0.03f, 0f));
                var shine = WorldKit.Box("cell_shine" + c, _tiles[c].transform, new Vector2(0.66f, 0.16f), new Color(1f, 1f, 1f, 0.16f), OCellShine, new Vector3(0f, 0.25f, 0f));
                if (!Compact)
                    for (int k = 0; k < 3; k++)
                    {
                        var tp = new Vector3((float)cellRng.NextDouble() * 0.56f - 0.28f, (float)cellRng.NextDouble() * 0.4f - 0.28f, 0f);
                        for (int blade = -1; blade <= 1; blade += 2)
                        {
                            var b = WorldKit.Sprite("tuft", _tiles[c].transform, Art.Square, tuft, OCellShine, tp + new Vector3(blade * 0.018f, 0.02f, 0f));
                            b.transform.localScale = new Vector3(0.022f, 0.07f, 1f);
                            b.transform.localRotation = Quaternion.Euler(0f, 0f, -blade * 22f);
                        }
                    }
                _highlights[c] = WorldKit.Box("hl" + c, _field, new Vector2(0.96f, 0.96f), new Color(1f, 1f, 0.4f, 0.5f), Order.CellHighlight + 6, pos);
                _highlights[c].gameObject.SetActive(false);
                // Star cell marker: golden tile rim + a star in the corner (damage bonus spot).
                _stars[c] = WorldKit.Box("star" + c, _field, new Vector2(0.86f, 0.82f), new Color(1f, 0.85f, 0.3f, 0.32f), OStar, pos + new Vector3(0f, 0.03f, 0f));
                for (int side = -1; side <= 1; side += 2)
                {
                    WorldKit.Box("star_rim_h", _stars[c].transform, new Vector2(0.86f, 0.05f), Art.Hex(0xFFD23F), OStar, new Vector3(0f, side * 0.39f, 0f));
                    WorldKit.Box("star_rim_v", _stars[c].transform, new Vector2(0.05f, 0.82f), Art.Hex(0xFFD23F), OStar, new Vector3(side * 0.41f, 0f, 0f));
                }
                var starArt = Art.Icon("star");
                if (starArt != null)
                {
                    var st = WorldKit.Sprite("star_icon", _stars[c].transform, starArt, Color.white, OStar + 1, new Vector3(-0.3f, 0.3f, 0f));
                    WorldKit.FitSprite(st, 0.3f);
                }
                else
                {
                    var starIcon = WorldKit.Sprite("star_icon", _stars[c].transform, Art.Diamond, Art.Hex(0xFFD23F), OStar + 1, new Vector3(-0.3f, 0.3f, 0f), 0.22f);
                    starIcon.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
                _stars[c].gameObject.SetActive(false);
                _locks[c] = WorldKit.Box("lock" + c, _field, new Vector2(0.92f, 0.92f), new Color(0.15f, 0.12f, 0.2f, 0.72f), Order.Lock, pos);
                WorldKit.Text("x", _locks[c].transform, "LOCK", 0.2f, new Color(1f, 1f, 1f, 0.9f), Order.Lock + 1, Vector3.zero);
                _locks[c].gameObject.SetActive(false);
            }
            BuildBalloon();

            // Gold frame around the tapped animal (info panel open).
            _selected = WorldKit.Box("selected", _field, new Vector2(0.01f, 0.01f), new Color(0f, 0f, 0f, 0f), Order.CellHighlight + 6, Vector3.zero);
            var gold = Art.Hex(0xFFD23F);
            for (int side = -1; side <= 1; side += 2)
            {
                WorldKit.Box("bar_h", _selected.transform, new Vector2(1.0f, 0.08f), gold, Order.CellHighlight + 6, new Vector3(0f, side * 0.46f, 0f));
                WorldKit.Box("bar_v", _selected.transform, new Vector2(0.08f, 1.0f), gold, Order.CellHighlight + 6, new Vector3(side * 0.46f, 0f, 0f));
            }
            _selected.gameObject.SetActive(false);
        }

        /// <summary>Gold frame on one cell (-1 = hide).</summary>
        public void SetSelected(int cell)
        {
            _selected.gameObject.SetActive(cell >= 0);
            if (cell >= 0) _selected.transform.localPosition = CellLocal(cell);
        }

        void BuildBalloon()
        {
            _balloon = WorldKit.Node("balloon", _field, Vector3.zero).transform;
            var art = Art.Icon("balloon");
            if (art != null)
            {
                // Optional art ZooArt/UI/balloon.png: pink balloon with a candy, string included.
                _balloonBody = WorldKit.Sprite("balloon_art", _balloon, art, Color.white, Order.FloatingText - 1, new Vector3(0f, -0.1f, 0f));
                WorldKit.FitSprite(_balloonBody, 0.95f);
                _balloon.gameObject.SetActive(false);
                return;
            }
            WorldKit.Sprite("string", _balloon, Art.Square, Art.Hex(0x6B4A30), Order.FloatingText - 2, new Vector3(0f, -0.38f, 0f)).transform.localScale = new Vector3(0.025f, 0.34f, 1f);
            _balloonBody = WorldKit.Sprite("body", _balloon, Art.Circle, Art.Hex(0xFF7AC8), Order.FloatingText - 1, Vector3.zero);
            _balloonBody.transform.localScale = new Vector3(0.5f, 0.6f, 1f);
            WorldKit.Sprite("shine", _balloon, Art.Circle, new Color(1f, 1f, 1f, 0.7f), Order.FloatingText, new Vector3(-0.1f, 0.13f, 0f), 0.12f);
            var candy = Art.Icon("candy");
            if (candy != null) { var ic = WorldKit.Sprite("candy", _balloon, candy, Color.white, Order.FloatingText, new Vector3(0f, -0.02f, 0f)); WorldKit.FitSprite(ic, 0.3f); }
            _balloon.gameObject.SetActive(false);
        }

        /// <summary>Balloon position in field coordinates, from the sim's spawn/expire ticks (presentation only).</summary>
        Vector3 BalloonLocal()
        {
            var m = _runner.Match;
            float life = Mathf.Max(1, Board.BalloonExpireTick - Board.BalloonSpawnTick);
            float t = Mathf.Clamp01((m.Tick - 1 + _runner.Alpha - Board.BalloonSpawnTick) / life);
            bool leftToRight = (Board.BalloonId & 1) == 0;
            float x = Mathf.Lerp(-0.1f, _b.gridCols + 1.1f, leftToRight ? t : 1f - t);
            float y = 1.5f + Board.BalloonLane + 0.15f * Mathf.Sin(t * 12f);
            return new Vector3(x, y, 0f);
        }

        /// <summary>Id of the balloon under a world point (generous hit area), or 0.</summary>
        public int BalloonAt(Vector3 world)
        {
            if (Board.BalloonId == 0 || !_balloon.gameObject.activeSelf) return 0;
            var p = _field.InverseTransformPoint(world);
            return (p - BalloonLocal()).sqrMagnitude <= 0.6f * 0.6f ? Board.BalloonId : 0;
        }

        /// <summary>True when a world point is inside this board's area.</summary>
        public bool Contains(Vector3 world)
        {
            var p = transform.InverseTransformPoint(world);
            return Mathf.Abs(p.x) <= Width * 0.5f && Mathf.Abs(p.y) <= Height * 0.5f;
        }

        /// <summary>Closest point of the enemy path to a world point: its path distance (milli-tiles); off = distance from the path in tiles.</summary>
        public int PathDistanceAt(Vector3 world, out float off)
        {
            var p = _field.InverseTransformPoint(world);
            var pp = new Vector2(p.x, p.y);
            float best = float.MaxValue;
            int bestDistance = 0, walked = 0;
            for (int i = 1; i < _b.pathX.Length; i++)
            {
                var a = new Vector2(_b.pathX[i - 1], _b.pathY[i - 1]) / 1000f;
                var b = new Vector2(_b.pathX[i], _b.pathY[i]) / 1000f;
                var ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(pp - a, ab) / len2) : 0f;
                var q = a + ab * t;
                float d = (pp - q).sqrMagnitude;
                int segLen = Mathf.Abs(_b.pathX[i] - _b.pathX[i - 1]) + Mathf.Abs(_b.pathY[i] - _b.pathY[i - 1]);
                if (d < best) { best = d; bestDistance = walked + Mathf.RoundToInt(segLen * t); }
                walked += segLen;
            }
            off = Mathf.Sqrt(best);
            return bestDistance;
        }

        public Vector3 PathWorld(int distance)
        {
            int x, y;
            PathMath.Point(_b, distance, out x, out y);
            return _field.TransformPoint(new Vector3(x, y) / 1000f);
        }

        void ScatterDecor(float minX, float maxX, float minY, float maxY, Color grass)
        {
            // Presentation only: a fixed seed keeps both boards identical and stable between matches.
            var rng = new System.Random(7);
            var tuftA = Color.Lerp(grass, Color.white, 0.18f);
            var tuftB = Color.Lerp(grass, Color.black, 0.15f);
            Color[] flowers = { Art.Hex(0xFFF0AA), Art.Hex(0xFFBED2), Art.Hex(0xFFFFFF) };
            int count = (int)(Width * Height * 4f);
            for (int i = 0; i < count; i++)
            {
                var p = new Vector3(Mathf.Lerp(minX + 0.15f, maxX - 0.15f, (float)rng.NextDouble()), Mathf.Lerp(minY + 0.15f, maxY - 0.15f, (float)rng.NextDouble()), 0f);
                bool flower = rng.NextDouble() < 0.25;
                var color = flower ? flowers[rng.Next(flowers.Length)] : (rng.NextDouble() < 0.5 ? tuftA : tuftB);
                float s = flower ? 0.05f + 0.03f * (float)rng.NextDouble() : 0.07f + 0.06f * (float)rng.NextDouble();
                WorldKit.Sprite("decor", _field, Art.Circle, color, ODecor, p, s);
            }
        }

        void BuildPen(Color grass)
        {
            // Fenced enclosure around the grid.
            var penCenter = new Vector3((_b.gridCols + 1) * 0.5f, (_b.gridRows + 1) * 0.5f, 0f);
            var penSize = new Vector2(_b.gridCols + 0.24f, _b.gridRows + 0.24f);
            WorldKit.Box("pen_fence", _field, penSize + new Vector2(0.14f, 0.14f), Fence, OPenFence, penCenter);
            WorldKit.Box("pen", _field, penSize, Color.Lerp(grass, Color.black, 0.12f), OPen, penCenter);
            if (Compact) return;
            float x0 = penCenter.x - penSize.x * 0.5f - 0.035f, x1 = penCenter.x + penSize.x * 0.5f + 0.035f;
            float y0 = penCenter.y - penSize.y * 0.5f - 0.035f, y1 = penCenter.y + penSize.y * 0.5f + 0.035f;
            for (float x = x0; x <= x1 + 0.01f; x += (x1 - x0) / 16f)
            {
                WorldKit.Sprite("post", _field, Art.Circle, FencePost, OPen, new Vector3(x, y0), 0.12f);
                WorldKit.Sprite("post", _field, Art.Circle, FencePost, OPen, new Vector3(x, y1), 0.12f);
            }
            for (float y = y0; y <= y1 + 0.01f; y += (y1 - y0) / 13f)
            {
                WorldKit.Sprite("post", _field, Art.Circle, FencePost, OPen, new Vector3(x0, y), 0.12f);
                WorldKit.Sprite("post", _field, Art.Circle, FencePost, OPen, new Vector3(x1, y), 0.12f);
            }
        }

        void BuildPath()
        {
            int n = _b.pathX.Length;
            for (int i = 1; i < n; i++)
            {
                var p0 = new Vector2(_b.pathX[i - 1], _b.pathY[i - 1]) / 1000f;
                var p1 = new Vector2(_b.pathX[i], _b.pathY[i]) / 1000f;
                var mid = (p0 + p1) * 0.5f;
                var len = new Vector2(Mathf.Abs(p1.x - p0.x), Mathf.Abs(p1.y - p0.y));
                WorldKit.Box("path_edge" + i, _field, len + new Vector2(0.94f, 0.94f), PathEdge, OPathEdge, mid);
                _pathFill.Add(WorldKit.Box("path" + i, _field, len + new Vector2(0.78f, 0.78f), PathFill, OPathFill, mid));
            }
            if (Compact) return;

            // Cobblestones and direction chevrons along the path.
            var rng = new System.Random(11);
            int total = PathMath.Length(_b);
            for (int d = 250; d < total; d += 330)
            {
                int px, py;
                PathMath.Point(_b, d, out px, out py);
                for (int k = 0; k < 2; k++)
                {
                    var p = new Vector3(px / 1000f + Mathf.Lerp(-0.26f, 0.26f, (float)rng.NextDouble()), py / 1000f + Mathf.Lerp(-0.26f, 0.26f, (float)rng.NextDouble()), 0f);
                    var stone = WorldKit.Sprite("stone", _field, Art.Circle, PathStone, OPathDetail, p);
                    float s = 0.1f + 0.06f * (float)rng.NextDouble();
                    stone.transform.localScale = new Vector3(s * 1.25f, s, 1f);
                }
            }
            for (int d = 900; d < total - 600; d += 1600)
            {
                int ax, ay, bx, by;
                PathMath.Point(_b, d, out ax, out ay);
                PathMath.Point(_b, d + 10, out bx, out by);
                float angle = Mathf.Atan2(by - ay, bx - ax) * Mathf.Rad2Deg;
                var chevron = WorldKit.Node("chevron", _field, new Vector3(ax, ay) / 1000f).transform;
                chevron.localRotation = Quaternion.Euler(0f, 0f, angle);
                for (int side = -1; side <= 1; side += 2)
                {
                    var arm = WorldKit.Sprite("arm", chevron, Art.Square, Chevron, OPathDetail, new Vector3(-0.05f, side * 0.06f, 0f));
                    arm.transform.localScale = new Vector3(0.18f, 0.05f, 1f);
                    arm.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 40f);
                }
            }
        }

        void BuildSpawnAndGate()
        {
            int n = _b.pathX.Length;
            var start = new Vector3(_b.pathX[0], _b.pathY[0]) / 1000f;
            var end = new Vector3(_b.pathX[n - 1], _b.pathY[n - 1]) / 1000f;

            // Monster portal (optional art ZooArt/UI/board_portal.png)
            var portalArt = Art.Icon("board_portal");
            if (portalArt != null)
            {
                var p = WorldKit.Sprite("portal_art", _field, portalArt, Color.white, OGate, start + new Vector3(0f, 0.12f, 0f));
                WorldKit.FitSprite(p, 1.35f);
            }
            else BuildDrawnPortal(start);

            // Zoo gate with the heart (lives) (optional art ZooArt/UI/board_gate.png)
            var gateArt = Art.Icon("board_gate");
            if (gateArt != null)
            {
                _gate = WorldKit.Sprite("gate_art", _field, gateArt, Color.white, OGate, end + new Vector3(0f, 0.1f, 0f));
                WorldKit.FitSprite(_gate, 1.35f);
                _gateBase = Color.white;
                return;
            }
            BuildDrawnGate(end);
        }

        void BuildDrawnPortal(Vector3 start)
        {
            WorldKit.Sprite("portal_rim", _field, Art.Circle, Art.Hex(0x3C285A), OGate, start, 1.0f);
            var swirl = WorldKit.Sprite("portal", _field, Art.Circle, Art.Hex(0x6E46A0), OGate, start, 0.8f);
            _portalSwirl = swirl.transform;
            WorldKit.Sprite("portal_core", _field, Art.Circle, Art.Hex(0x28193C), OGate, start, 0.52f);
        }

        void BuildDrawnGate(Vector3 end)
        {
            WorldKit.Box("gate_frame", _field, new Vector2(1.0f, 0.84f), Art.Hex(0x784830), OGate, end);
            _gate = WorldKit.Box("gate", _field, new Vector2(0.72f, 0.66f), Art.Hex(0xFFECC8), OGate, end + new Vector3(0f, -0.07f, 0f));
            for (int side = -1; side <= 1; side += 2)
                WorldKit.Box("gate_post", _field, new Vector2(0.18f, 0.92f), Art.Hex(0x603A24), OGate + 1, end + new Vector3(side * 0.5f, 0.04f, 0f));
            var heart = Art.Icon("heart");
            if (heart != null) { var h = WorldKit.Sprite("gate_icon", _gate.transform, heart, Color.white, OGate + 1, Vector3.zero); WorldKit.FitSprite(h, 0.44f); }
            else WorldKit.Sprite("gate_heart", _gate.transform, Art.Circle, Art.Hex(0xFF4D6D), OGate + 1, Vector3.zero, 0.3f);
        }

        public Vector3 CellLocal(int cell)
        {
            return new Vector3(cell % _b.gridCols + 1, cell / _b.gridCols + 1, 0f);
        }

        public Vector3 CellWorld(int cell) { return _field.TransformPoint(CellLocal(cell)); }

        /// <summary>Grid cell under a world position, or -1.</summary>
        public int CellAt(Vector3 world)
        {
            var p = _field.InverseTransformPoint(world);
            int col = Mathf.RoundToInt(p.x) - 1, row = Mathf.RoundToInt(p.y) - 1;
            if (col < 0 || row < 0 || col >= _b.gridCols || row >= _b.gridRows) return -1;
            if (Mathf.Abs(p.x - (col + 1)) > 0.5f || Mathf.Abs(p.y - (row + 1)) > 0.5f) return -1;
            return row * _b.gridCols + col;
        }

        public float WorldScale { get { return transform.lossyScale.x; } }

        public void ShowMergeTargets(int fromCell)
        {
            var a = fromCell >= 0 ? Board.Cells[fromCell] : null;
            for (int c = 0; c < _highlights.Length; c++)
            {
                var u = Board.Cells[c];
                bool on = a != null && c != fromCell && u != null && u.def == a.def && u.level == a.level && u.level < _b.maxLevel && !Board.IsCellLocked(c);
                _highlights[c].color = new Color(1f, 1f, 0.4f, 0.5f);
                _highlights[c].gameObject.SetActive(on);
            }
        }

        /// <summary>Green highlight on the cell a dragged shop card would land in (-1 = none).</summary>
        public void ShowPlaceTarget(int cell)
        {
            for (int c = 0; c < _highlights.Length; c++)
            {
                bool on = c == cell;
                if (on) _highlights[c].color = new Color(0.45f, 1f, 0.45f, 0.6f);
                _highlights[c].gameObject.SetActive(on);
            }
        }

        // ------------------------------------------------------------------ per-frame sync

        void Update()
        {
            if (Board == null) return;
            float alpha = _runner.Alpha;
            float dt = Time.deltaTime;

            // Units
            _remove.Clear();
            foreach (var kv in _units) kv.Value.seen = false;
            for (int c = 0; c < Board.Cells.Length; c++)
            {
                var u = Board.Cells[c];
                _locks[c].gameObject.SetActive(Board.IsCellLocked(c));
                if (u == null) continue;
                UnitView v;
                if (!_units.TryGetValue(u.uid, out v))
                {
                    v = new UnitView(_field, _content.Units[u.def], Compact, u.uid);
                    v.root.transform.localPosition = CellLocal(c);
                    _units.Add(u.uid, v);
                }
                v.seen = true;
                v.SetLevel(u.level);
                v.SetStunned(u.stunTicks > 0 || Board.IsCellLocked(c));
                v.SetBuffs(u.guardTicks > 0 && u.guardCharges > 0, u.hasteTicks > 0);
                int charge = Board.SkillCharge(u);
                v.SetEnergy(charge > 0 ? Mathf.Clamp01((float)u.energy / charge) : -1f, charge > 0 && u.energy >= charge);
                if (u.uid == DraggingUid)
                {
                    v.root.transform.position = DragWorldPosition;
                    v.SetDragging(true);
                }
                else
                {
                    v.SetDragging(false);
                    var target = CellLocal(c);
                    v.root.transform.localPosition = Vector3.Lerp(v.root.transform.localPosition, target, 1f - Mathf.Exp(-dt * 18f));
                }
                v.Tick(dt);
            }
            foreach (var kv in _units) if (!kv.Value.seen) _remove.Add(kv.Key);
            for (int i = 0; i < _remove.Count; i++) { Destroy(_units[_remove[i]].root); _units.Remove(_remove[i]); }

            for (int c = 0; c < _stars.Length; c++)
            {
                bool star = _runner.Match.IsStarCell(c);
                if (_stars[c].gameObject.activeSelf != star) _stars[c].gameObject.SetActive(star);
            }

            // Balloon
            bool balloon = Board.BalloonId != 0;
            if (_balloon.gameObject.activeSelf != balloon) _balloon.gameObject.SetActive(balloon);
            if (balloon)
            {
                _balloon.localPosition = BalloonLocal();
                float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 8f);
                _balloon.localScale = new Vector3(pulse, pulse, 1f);
            }

            // Enemies
            _remove.Clear();
            foreach (var kv in _enemies) kv.Value.seen = false;
            for (int i = 0; i < Board.Enemies.Count; i++)
            {
                var e = Board.Enemies[i];
                if (!e.alive) continue;
                EnemyView v;
                if (!_enemies.TryGetValue(e.uid, out v))
                {
                    v = _enemyPool.Count > 0 ? _enemyPool.Pop() : new EnemyView(_field);
                    v.Bind(_content.Enemies[e.def], e, Compact);
                    _enemies.Add(e.uid, v);
                }
                v.seen = true;
                int x0, y0, x1, y1;
                PathMath.Point(_b, e.prevDistance, out x0, out y0);
                PathMath.Point(_b, e.distance, out x1, out y1);
                v.root.transform.localPosition = Vector3.Lerp(new Vector3(x0, y0) / 1000f, new Vector3(x1, y1) / 1000f, alpha);
                v.Sync(e, dt);
            }
            foreach (var kv in _enemies) if (!kv.Value.seen) _remove.Add(kv.Key);
            for (int i = 0; i < _remove.Count; i++)
            {
                int uid = _remove[i];
                var v = _enemies[uid];
                _enemies.Remove(uid);
                if (_killed.Remove(uid) && v.StartDeath()) _dying.Add(v);   // play the death motion, then pool
                else Recycle(v);
            }
            for (int i = _dying.Count - 1; i >= 0; i--)
            {
                if (_dying[i].TickDeath(dt)) continue;
                Recycle(_dying[i]);
                _dying.RemoveAt(i);
            }
            _killed.Clear();
            if (_noted.Count > 4000) _noted.Clear();

            if (_gateFlash > 0f)
            {
                _gateFlash -= dt;
                _gate.color = Color.Lerp(_gateBase, Art.Hex(0xFF4D4D), Mathf.Clamp01(_gateFlash * 3f));
            }
            if (_pathGlow)
            {
                var glow = Color.Lerp(PathFill, Art.Hex(0x8FE3FF), 0.35f + 0.3f * Mathf.Sin(Time.time * 7f));
                foreach (var p in _pathFill) p.color = glow;
            }
            if (_portalSwirl != null)
            {
                float pulse = 0.8f + 0.05f * Mathf.Sin(Time.time * 3f + Board.Index);
                _portalSwirl.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        void Recycle(EnemyView v)
        {
            v.root.SetActive(false);
            _enemyPool.Push(v);
        }

        // ------------------------------------------------------------------ effects

        public void OnSimEvent(SimEvent e)
        {
            if (e.board != Board.Index) return;
            switch (e.type)
            {
                case SimEventType.UnitAttacked:
                {
                    UnitView u; EnemyView en;
                    if (_units.TryGetValue(e.a, out u) && _enemies.TryGetValue(e.b, out en))
                    {
                        u.Attack();
                        if (!Compact) _fx.Shot(u.MuzzleWorld, en.CenterWorld, u.Def, WorldScale);
                        en.Hit();
                        // Element feedback: a spark on every effective hit, a word the first time per monster.
                        if (!Compact && e.d != 0)
                        {
                            if (e.d > 0) _fx.Puff(en.CenterWorld, Art.Hex(0xFFF27A), 0.55f * WorldScale, 0.22f);
                            if (_noted.Add(e.b * 16 + (e.d > 0 ? 1 : 2)))
                                _fx.FloatText(en.CenterWorld + new Vector3(0f, 0.25f, 0f), Loc.T(e.d > 0 ? "battle.effective" : "battle.resisted"),
                                    e.d > 0 ? Art.Hex(0xB6F25A) : Art.Hex(0xB9B3C9), 0.3f);
                        }
                    }
                    break;
                }
                case SimEventType.UnitBuffed:
                {
                    UnitView src, dst;
                    if (_units.TryGetValue(e.c, out src)) src.Attack();
                    if (_units.TryGetValue(e.a, out dst))
                    {
                        var col = e.b == 0 ? Art.Hex(0xB6F25A) : Art.Hex(0xFFD23F);
                        if (src != null && !Compact)
                        {
                            var art = Art.SpeciesProjectile(src.Def.id);
                            if (art != null) _fx.SpriteFx(art, src.MuzzleWorld, dst.MuzzleWorld, 0.4f * WorldScale, 0.3f * WorldScale, 0.35f, Color.white, 0f, 0f, false, 0.7f * WorldScale);
                            else _fx.Projectile(src.MuzzleWorld, dst.MuzzleWorld, col, 0.25f * WorldScale);
                        }
                        _fx.Puff(dst.root.transform.position, col, 0.9f * WorldScale, 0.35f);
                    }
                    break;
                }
                case SimEventType.GuardBlocked:
                {
                    UnitView u;
                    if (_units.TryGetValue(e.a, out u))
                    {
                        _fx.Puff(u.root.transform.position, Art.Hex(0xFFD23F), 1.5f * WorldScale, 0.45f);
                        if (!Compact) _fx.FloatText(u.root.transform.position, Loc.T("battle.blocked"), Art.Hex(0xFFE066), 0.34f);
                    }
                    break;
                }
                case SimEventType.BossTelegraph:
                {
                    if (e.b >= 0)
                    {
                        // Red target ring on the animal the gorilla will lock (0.8 s warning).
                        var at = CellWorld(e.b);
                        _fx.SpriteFx(Art.Ring, at, at, 1.3f * WorldScale, 0.85f * WorldScale, 0.8f, Art.Hex(0xFF4D4D), 0f, 0f, false, 0f, 0.05f, 0.1f);
                        _fx.SpriteFx(Art.Glow, at, at, 0.6f * WorldScale, 1.1f * WorldScale, 0.8f, new Color(1f, 0.25f, 0.25f, 0.6f));
                    }
                    else
                    {
                        EnemyView boss;
                        if (_enemies.TryGetValue(e.a, out boss))
                            _fx.SpriteFx(Art.Glow, boss.CenterWorld, boss.CenterWorld, 0.5f * WorldScale, 1.8f * WorldScale, 0.8f, new Color(0.7f, 0.4f, 1f, 0.7f));
                    }
                    break;
                }
                case SimEventType.EnemyAbilityUsed:
                {
                    EnemyView en;
                    if (!_enemies.TryGetValue(e.a, out en)) break;
                    if (e.b == (int)EnemyAbility.DrumHaste)
                        _fx.SpriteFx(Art.Ring, en.CenterWorld, en.CenterWorld, 0.4f * WorldScale, 2.4f * WorldScale, 0.5f, Art.Hex(0xFFA040));
                    else if (e.b == (int)EnemyAbility.Dash)
                    {
                        _fx.Puff(en.CenterWorld, Art.Hex(0xBFE8FF), 0.7f * WorldScale, 0.25f);
                        break;   // frequent: no text
                    }
                    else _fx.Puff(en.CenterWorld, e.b >= 100 ? Art.Hex(0xA45CFF) : Art.Hex(0xFF4D4D), 1.3f * WorldScale, 0.4f);
                    if (!Compact)
                        _fx.FloatText(en.CenterWorld + new Vector3(0f, 0.3f, 0f),
                            e.b == 100 + (int)BossSkill.VoidShield ? Loc.T("battle.shield_up") : Loc.T("ability." + e.b), Art.Hex(0xFFB0B0), 0.32f);
                    break;
                }
                case SimEventType.ShieldBroken:
                {
                    EnemyView en;
                    if (_enemies.TryGetValue(e.a, out en))
                    {
                        _fx.Puff(en.CenterWorld, Art.Hex(0xC9A0FF), 2f * WorldScale, 0.45f);
                        if (!Compact) _fx.FloatText(en.CenterWorld + new Vector3(0f, 0.3f, 0f), Loc.T("battle.shield_broken"), Art.Hex(0xE6CCFF), 0.36f);
                    }
                    break;
                }
                case SimEventType.StatusApplied:
                {
                    EnemyView en;
                    if (!_enemies.TryGetValue(e.a, out en)) break;
                    if (e.b == 3) _fx.Puff(en.CenterWorld, Art.Hex(0x9AE6FF), 0.9f * WorldScale, 0.35f);
                    if (!Compact && _noted.Add(e.a * 16 + 3 + e.b))
                        _fx.FloatText(en.CenterWorld + new Vector3(0f, 0.2f, 0f), Loc.T("status." + e.b), StatusColor(e.b), 0.26f);
                    break;
                }
                case SimEventType.EnemyKilled:
                {
                    EnemyView en;
                    if (_enemies.TryGetValue(e.a, out en))
                    {
                        _killed.Add(e.a);
                        _fx.Puff(en.CenterWorld, Color.white, 0.9f * WorldScale);
                        if (!Compact && e.b > 0) _fx.FloatText(en.root.transform.position, "+" + e.b, Art.Hex(0xFF8AD8), 0.32f);
                    }
                    break;
                }
                case SimEventType.EnemyLeaked:
                    _gateFlash = 0.6f;
                    _fx.Puff(_gate.transform.position, Art.Hex(0xFF4D4D), 1.4f * WorldScale, 0.45f);
                    if (!Compact) _fx.FloatText(_gate.transform.position, "-" + e.b, Art.Hex(0xFF4D4D), 0.45f);
                    break;
                case SimEventType.UnitMerged:
                case SimEventType.UnitLevelUp:
                {
                    UnitView u;
                    if (_units.TryGetValue(e.a, out u))
                    {
                        u.Pop();
                        _fx.Puff(u.root.transform.position, Art.Hex(0xFFE066), 1.3f * WorldScale, 0.4f);
                        if (!Compact) _fx.FloatText(u.root.transform.position, "LV " + e.b, Art.Hex(0xFFE066), 0.34f);
                    }
                    break;
                }
                case SimEventType.UnitStunned:
                {
                    UnitView u;
                    if (_units.TryGetValue(e.a, out u))
                    {
                        u.Hit();
                        _fx.Puff(u.root.transform.position, Art.Hex(0x9AD0FF), 1.1f * WorldScale);
                    }
                    break;
                }
                case SimEventType.CellLocked:
                    _fx.Puff(CellWorld(e.a), Art.Hex(0x6B5A88), 1.4f * WorldScale, 0.5f);
                    break;
                case SimEventType.SpellCast:
                    SpellFx(_content.Spells[e.a], e.b);
                    break;
                case SimEventType.SkillUsed:
                {
                    UnitView u;
                    _units.TryGetValue(e.a, out u);
                    var color = u != null ? u.color : Color.white;
                    if (u != null)
                    {
                        u.Pop();
                        _fx.Puff(u.root.transform.position, color, 1.2f * WorldScale, 0.35f);
                    }
                    if (e.b >= 0)
                    {
                        var at = PathWorld(e.b);
                        if (u != null && !Compact) _fx.Projectile(u.MuzzleWorld, at, color, 0.34f * WorldScale);
                        _fx.Puff(at, color, (e.c == (int)Role.Damage ? 1.2f : 2.8f) * WorldScale, 0.45f);
                        _fx.Puff(at, Color.white, (e.c == (int)Role.Damage ? 0.7f : 1.8f) * WorldScale, 0.3f);
                    }
                    else foreach (var kv in _units) _fx.Puff(kv.Value.root.transform.position, Art.Hex(0xFFE066), 0.8f * WorldScale, 0.4f);
                    break;
                }
                case SimEventType.ElementUpgraded:
                    foreach (var kv in _units)
                        if ((int)kv.Value.Def.element == e.a) _fx.Puff(kv.Value.root.transform.position, kv.Value.color, 1.3f * WorldScale, 0.5f);
                    break;
                case SimEventType.BalloonPopped:
                {
                    var at = _field.TransformPoint(BalloonLocal());
                    _fx.Puff(at, Art.Hex(0xFF7AC8), 1.4f * WorldScale, 0.35f);
                    _fx.FloatText(at, "+" + e.b, Art.Hex(0xFF8AD8), Compact ? 0.3f : 0.45f);
                    break;
                }
                case SimEventType.StarCellsMoved:
                    foreach (var cell in _runner.Match.StarCells) _fx.Puff(CellWorld(cell), Art.Hex(0xFFD23F), 1.4f * WorldScale, 0.6f);
                    break;
            }
        }

        static Color StatusColor(int status)
        {
            switch (status)
            {
                case 1: return Art.Hex(0x9BE36B);   // rooted
                case 2: return Art.Hex(0xD6B8FF);   // silenced
                case 3: return Art.Hex(0x9AE6FF);   // frozen
                case 4: return Art.Hex(0xFFB070);   // armor break
                default: return Art.Hex(0xFF9AD5);  // marked
            }
        }

        /// <summary>Big, readable spell effects built from the spell icon art (ZooArt/UI/spell_{id}).</summary>
        void SpellFx(SpellDef def, int target)
        {
            var at = target >= 0 ? PathWorld(target) : transform.position;
            float ws = WorldScale;
            float radius = Mathf.Max(0.8f, def.radius / 1000f) * ws * 2f;
            var icon = Art.Icon("spell_" + def.id);
            if (icon == null) icon = Art.Diamond;
            var up = new Vector3(0f, 1f, 0f);
            switch (def.kind)
            {
                case SpellKind.Freeze:
                {
                    // Ice crystal grows in place, then frost rings and flakes scatter along the area.
                    _fx.SpriteFx(icon, at, at, 0.2f * ws, 1.3f * ws, 0.7f, Color.white, 90f, 0f, false, radius);
                    _fx.Puff(at, Art.Hex(0x9AE6FF), radius, 0.7f);
                    _fx.Puff(at, Color.white, radius * 0.55f, 0.45f);
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * Mathf.PI / 4f;
                        var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * radius * 0.45f;
                        _fx.SpriteFx(icon, at, at + dir, 0.35f * ws, 0.15f * ws, 0.6f, new Color(1f, 1f, 1f, 0.9f), 240f, 0.1f);
                    }
                    break;
                }
                case SpellKind.Meteor:
                {
                    // Meteor falls from the top-left sky, then a fiery blast with flying embers.
                    var from = at + new Vector3(-2.2f, 4.5f, 0f) * ws;
                    _fx.SpriteFx(icon, from, at, 1.1f * ws, 1.5f * ws, 0.45f, Color.white, 0f, 0f, true, 0f, 0.1f, 0.05f);
                    _fx.SpriteFx(Art.Glow, at, at, 0.3f * ws, radius * 1.1f, 0.5f, Art.Hex(0xFFB020), 0f, 0.45f, false, radius, 0f, 0.6f);
                    _fx.SpriteFx(Art.Circle, at, at, 0.2f * ws, radius * 0.5f, 0.3f, Art.Hex(0xFFF1C2), 0f, 0.45f);
                    for (int i = 0; i < 10; i++)
                    {
                        float ang = i * Mathf.PI / 5f + 0.3f;
                        var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f + 0.4f, 0f) * radius * 0.6f;
                        _fx.SpriteFx(Art.Circle, at, at + dir, 0.22f * ws, 0.05f * ws, 0.5f, Art.Hex(0xFF7A3D), 0f, 0.45f, false, 0f, 0f, 0.5f);
                    }
                    break;
                }
                case SpellKind.Gust:
                {
                    // Three wind swirls sweep backwards along the path, pushing enemies back.
                    int back = Mathf.Max(0, target - def.value);
                    for (int i = 0; i < 3; i++)
                    {
                        var a = PathWorld(Mathf.Min(PathMath.Length(_b), target + 400 - i * 300));
                        var b = PathWorld(Mathf.Max(0, back - i * 200));
                        _fx.SpriteFx(icon, a + up * 0.15f * ws * i, b + up * 0.15f * ws * i, 0.9f * ws, 1.2f * ws, 0.75f, new Color(1f, 1f, 1f, 0.95f), 0f, i * 0.12f);
                    }
                    _fx.Puff(at, new Color(0.75f, 1f, 0.85f, 0.8f), radius, 0.6f);
                    break;
                }
                case SpellKind.Lightning:
                {
                    // Bolt strikes from above, white flash and a yellow shock ring.
                    _fx.SpriteFx(icon, at + up * 3.5f * ws, at + up * 0.3f * ws, 1.6f * ws, 1.8f * ws, 0.16f, Color.white, 0f, 0f, true, 0f, 0f, 0.1f);
                    _fx.SpriteFx(icon, at + up * 0.3f * ws, at + up * 0.3f * ws, 1.8f * ws, 2.0f * ws, 0.25f, Color.white, 0f, 0.16f, false, 0f, 0f, 0.8f);
                    _fx.SpriteFx(Art.Glow, at, at, 0.5f * ws, 3.2f * ws, 0.35f, new Color(1f, 1f, 0.75f, 0.9f), 0f, 0.14f);
                    _fx.Puff(at, Art.Hex(0xFFF27A), 2.2f * ws, 0.45f);
                    break;
                }
                case SpellKind.Rage:
                {
                    // Angry paw pops over every animal and a red pulse runs through the zoo.
                    _fx.SpriteFx(Art.Glow, transform.position, transform.position, 1f * ws, Width * ws * 1.1f, 0.6f, new Color(1f, 0.3f, 0.3f, 0.45f), 0f);
                    int n = 0;
                    foreach (var kv in _units)
                    {
                        var p = kv.Value.root.transform.position;
                        _fx.SpriteFx(icon, p, p + up * 0.55f * ws, 0.2f * ws, 0.5f * ws, 0.8f, Color.white, 0f, 0.03f * n++);
                        _fx.Puff(p, Art.Hex(0xFF4D4D), 1.1f * ws, 0.5f);
                    }
                    break;
                }
                case SpellKind.CandyRain:
                {
                    // A candy cloud floats over the zoo and drops candies on random cells.
                    var top = CellWorld(_b.gridCols * (_b.gridRows - 1) + _b.gridCols / 2) + up * 1.1f * ws;
                    _fx.SpriteFx(icon, top + new Vector3(-0.6f, 0f, 0f) * ws, top + new Vector3(0.6f, 0f, 0f) * ws, 1.4f * ws, 1.5f * ws, 2.2f, Color.white, 0f);
                    var candy = Art.Icon("candy");
                    if (candy == null) candy = Art.Circle;
                    var rng = new System.Random(def.cost + _runner.Match.Tick);
                    for (int i = 0; i < 10; i++)
                    {
                        var cell = CellWorld(rng.Next(_highlights.Length));
                        _fx.SpriteFx(candy, cell + up * 2.2f * ws, cell, 0.4f * ws, 0.4f * ws, 0.5f, Color.white, 200f, 0.25f + i * 0.15f, true, 0.8f * ws, 0.05f, 0.15f);
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ views

        /// <summary>
        /// root (cell position, drag scale) -> visual (motion pose, origin at the feet) -> art / placeholder.
        /// The level badge stays on root so it never wobbles.
        /// </summary>
        sealed class UnitView
        {
            const float ArtSize = 1.05f;      // world size of the square art canvas
            const float FeetY = -0.46f;       // feet line inside the cell
            const float BodyCenter = 0.46f;   // placeholder disc centre above the feet

            public readonly GameObject root;
            public readonly Color color;
            public bool seen;
            readonly UnitDef _def;
            readonly Transform _visual, _placeholder;
            readonly SpriteRenderer _body, _ring, _art, _badge;
            readonly TextMesh _initials, _level;
            readonly bool _compact;
            readonly MotionDriver _motion = new MotionDriver();
            readonly SpriteRenderer _energyBg, _energyFill, _readyGlow, _aura;
            int _shownLevel = -1;
            bool _dragging, _stunned, _hasArt, _ready, _guard, _haste;
            Sprite _still;              // art for the current level
            Art.CastClip _cast;         // optional attack animation sheet for the current level
            float _castT = -1f;

            public UnitView(Transform parent, UnitDef def, bool compact, int uid)
            {
                _def = def;
                _compact = compact;
                color = Art.ElementColor(def.element);
                root = WorldKit.Node("unit_" + def.id, parent, Vector3.zero);
                _visual = WorldKit.Node("visual", root.transform, new Vector3(0f, FeetY, 0f)).transform;
                _art = WorldKit.Sprite("art", _visual, null, Color.white, Order.Units + 2, Vector3.zero);
                _placeholder = WorldKit.Node("placeholder", _visual, new Vector3(0f, BodyCenter, 0f)).transform;
                _body = WorldKit.Sprite("body", _placeholder, Art.Circle, color, Order.Units, Vector3.zero, 0.8f);
                _ring = WorldKit.Sprite("ring", _placeholder, Art.Ring, Color.Lerp(Art.RarityColor(def.rarity), Color.black, 0.15f), Order.Units + 1, Vector3.zero, 0.84f);
                _initials = WorldKit.Text("initials", _placeholder, Art.Initials(def.displayName), 0.3f, Art.Hex(0x3B2412), Order.Units + 3, Vector3.zero);
                _badge = WorldKit.Sprite("badge", root.transform, Art.Circle, Art.Hex(0x2B2140), Order.Units + 4, new Vector3(0.32f, -0.32f, 0f), 0.3f);
                _level = WorldKit.Text("level", _badge.transform, "1", 0.6f, Color.white, Order.Units + 5, Vector3.zero);
                // Skill charge: thin bar under the animal, golden glow when ready (tap to use).
                _readyGlow = WorldKit.Sprite("ready", root.transform, Art.Circle, new Color(1f, 0.85f, 0.2f, 0.45f), Order.Units - 1, new Vector3(0f, -0.1f, 0f), 0.95f);
                _energyBg = WorldKit.Sprite("energy_bg", root.transform, Art.Square, new Color(0f, 0f, 0f, 0.55f), Order.Units + 4, new Vector3(0f, -0.43f, 0f));
                _energyBg.transform.localScale = new Vector3(0.6f, 0.07f, 1f);
                _energyFill = WorldKit.Sprite("energy", root.transform, Art.Square, Art.Hex(0x5BD6FF), Order.Units + 5, new Vector3(0f, -0.43f, 0f));
                _readyGlow.gameObject.SetActive(false);
                // Buff ring at the feet: gold = Aegis Ram shield, green = Nectar Bee haste.
                _aura = WorldKit.Sprite("aura", root.transform, Art.Ring, Color.white, Order.Units - 1, new Vector3(0f, FeetY + 0.06f, 0f), 0.9f);
                _aura.transform.localScale = new Vector3(0.9f, 0.32f, 1f);
                _aura.gameObject.SetActive(false);
                _energyBg.gameObject.SetActive(false);
                _energyFill.gameObject.SetActive(false);
                _motion.Bind(MotionLibrary.Get(def.id, false), false, uid);
            }

            public Vector3 MuzzleWorld { get { return root.transform.TransformPoint(new Vector3(0f, 0.05f, 0f)); } }
            public UnitDef Def { get { return _def; } }

            /// <summary>fraction &lt; 0 hides the bar (no skill at this level).</summary>
            public void SetEnergy(float fraction, bool ready)
            {
                bool show = fraction >= 0f && !_dragging;
                _energyBg.gameObject.SetActive(show && !ready);
                _energyFill.gameObject.SetActive(show && !ready);
                if (show && !ready)
                {
                    _energyFill.transform.localScale = new Vector3(0.58f * fraction, 0.05f, 1f);
                    _energyFill.transform.localPosition = new Vector3(-0.29f * (1f - fraction), -0.43f, 0f);
                }
                _ready = ready && !_dragging;
                _readyGlow.gameObject.SetActive(_ready);
            }

            public void SetLevel(int level)
            {
                if (level == _shownLevel) return;
                _shownLevel = level;
                _level.text = level.ToString();
                var sprite = Art.Unit(_def.id, level);
                _art.sprite = sprite;
                _still = sprite;
                _cast = sprite != null ? Art.UnitCast(_def.id, level) : null;
                _castT = -1f;
                _hasArt = sprite != null;
                _art.gameObject.SetActive(_hasArt);
                _placeholder.gameObject.SetActive(!_hasArt);
                // Placeholder grows a little with level so progress is visible without art.
                float s = 0.72f + 0.02f * level;
                _body.transform.localScale = new Vector3(s, s, 1f);
                _ring.transform.localScale = new Vector3(s + 0.06f, s + 0.06f, 1f);
                _badge.gameObject.SetActive(true);
            }

            public void SetStunned(bool stunned) { _stunned = stunned; }

            public void SetBuffs(bool guard, bool haste)
            {
                _guard = guard;
                _haste = haste;
                bool show = (guard || haste) && !_dragging;
                if (_aura.gameObject.activeSelf != show) _aura.gameObject.SetActive(show);
                if (show) _aura.color = guard ? new Color(1f, 0.84f, 0.25f, 0.9f) : new Color(0.7f, 0.95f, 0.35f, 0.85f);
            }

            public void SetDragging(bool dragging)
            {
                if (dragging == _dragging) return;
                _dragging = dragging;
                int bump = dragging ? Order.Drag - Order.Units : 0;
                _body.sortingOrder = Order.Units + bump;
                _ring.sortingOrder = Order.Units + 1 + bump;
                _art.sortingOrder = Order.Units + 2 + bump;
                _initials.GetComponent<MeshRenderer>().sortingOrder = Order.Units + 3 + bump;
                _badge.sortingOrder = Order.Units + 4 + bump;
                _level.GetComponent<MeshRenderer>().sortingOrder = Order.Units + 5 + bump;
            }

            public void Pop() { _motion.Play(ZooDefense.Animation.ZooMotionState.Spawn, true); }
            public void Attack()
            {
                if (_stunned) return;
                if (_cast != null && !_compact && !_dragging) _castT = 0f;   // hand-made cast sheet replaces the procedural lunge
                else _motion.Play(ZooDefense.Animation.ZooMotionState.Attack);
            }
            public void Hit() { _motion.Play(ZooDefense.Animation.ZooMotionState.Hit); }

            public void Tick(float dt)
            {
                _motion.Tick(_dragging ? 0f : dt);
                if (_castT >= 0f && _cast != null)
                {
                    _castT += dt;
                    int f = (int)(_castT * _cast.fps);
                    if (f >= _cast.frames.Length || _dragging) { _castT = -1f; _art.sprite = _still; }
                    else _art.sprite = _cast.frames[f];
                }
                if (_guard || _haste)
                {
                    float p = 0.9f + 0.06f * Mathf.Sin(Time.time * 6f);
                    _aura.transform.localScale = new Vector3(p, p * 0.36f, 1f);
                }
                float artScale = 1f;
                if (_hasArt && _art.sprite != null)
                {
                    float max = Mathf.Max(_art.sprite.bounds.size.x, _art.sprite.bounds.size.y);
                    artScale = max > 0.0001f ? ArtSize / max : 1f;
                }
                _art.transform.localScale = new Vector3(artScale, artScale, 1f);
                _motion.Apply(_visual, new Vector3(0f, FeetY, 0f), 1f, 1f);
                if (_dragging) _visual.localRotation = Quaternion.identity;

                var baseColor = _stunned ? new Color(0.55f, 0.55f, 0.62f, 1f) : Color.white;
                _art.color = _motion.Tint(baseColor);
                var bodyColor = _stunned ? Color.Lerp(color, Color.gray, 0.6f) : color;
                _body.color = _motion.Tint(bodyColor);

                float s = _dragging ? 1.15f : 1f;
                root.transform.localScale = new Vector3(s, s, 1f);
                if (_ready)
                {
                    float g = 0.9f + 0.12f * Mathf.Sin(Time.time * 7f);
                    _readyGlow.transform.localScale = new Vector3(g, g * 0.95f, 1f);
                }
            }
        }

        /// <summary>root (path position, HP bar, sent badge) -> visual (motion pose at the feet) -> art / placeholder.</summary>
        sealed class EnemyView
        {
            public readonly GameObject root;
            public bool seen;
            readonly Transform _visual, _placeholder;
            readonly SpriteRenderer _body, _ring, _art, _barBg, _barFill, _sentMark, _elementRing, _elementDot, _shield;
            bool _silenced, _burning, _marked;
            readonly TextMesh _label;
            readonly MotionDriver _motion = new MotionDriver();
            Color _color;
            float _size, _feetY, _centerY, _alpha = 1f;
            bool _hasArt, _slowed, _stunned;
            Art.WalkClip _walk;     // hand-made walk cycle, if the enemy has one
            float _walkTime;
            int _walkFrame = -1;

            public EnemyView(Transform parent)
            {
                root = WorldKit.Node("enemy", parent, Vector3.zero);
                _visual = WorldKit.Node("visual", root.transform, Vector3.zero).transform;
                _art = WorldKit.Sprite("art", _visual, null, Color.white, Order.Enemies + 2, Vector3.zero);
                _placeholder = WorldKit.Node("placeholder", _visual, Vector3.zero).transform;
                _body = WorldKit.Sprite("body", _placeholder, Art.Circle, Color.white, Order.Enemies, Vector3.zero);
                _ring = WorldKit.Sprite("ring", _placeholder, Art.Ring, Art.Hex(0x2B2140), Order.Enemies + 1, Vector3.zero);
                _label = WorldKit.Text("label", _placeholder, "", 0.24f, Color.white, Order.Enemies + 3, Vector3.zero);
                _barBg = WorldKit.Sprite("hp_bg", root.transform, Art.Square, new Color(0f, 0f, 0f, 0.6f), Order.Bars, Vector3.zero);
                _barFill = WorldKit.Sprite("hp", root.transform, Art.Square, Art.Hex(0x7CFF6B), Order.Bars + 1, Vector3.zero);
                _sentMark = WorldKit.Sprite("sent", root.transform, Art.Diamond, Art.Hex(0xA45CFF), Order.Bars + 1, Vector3.zero, 0.22f);
                // Element marker at the feet: enemy art is neutral purple, the element is rolled per spawn.
                _elementRing = WorldKit.Sprite("element_ring", root.transform, Art.Circle, Art.Hex(0x2B2140), Order.Enemies - 2, Vector3.zero, 0.2f);
                _elementDot = WorldKit.Sprite("element", _elementRing.transform, Art.Circle, Color.white, Order.Enemies - 1, Vector3.zero, 0.72f);
                // Void shield bubble (crab boss).
                _shield = WorldKit.Sprite("shield", root.transform, Art.Glow, new Color(0.65f, 0.4f, 1f, 0.55f), Order.Enemies + 3, Vector3.zero);
                _shield.gameObject.SetActive(false);
            }

            public void Bind(EnemyDef def, EnemyInstance e, bool compact)
            {
                root.SetActive(true);
                root.name = "enemy_" + def.id;
                _size = Mathf.Max(0.3f, def.size / 1000f);
                _color = Color.Lerp(Art.ElementColor(e.element), Art.Hex(0x4B2E6B), def.kind == EnemyKind.Sent ? 0.55f : 0.3f);
                _walk = Art.EnemyWalk(def.id);
                _walkFrame = -1;
                _walkTime = (e.uid * 0.37f) % 1f;   // desync identical enemies walking side by side
                var sprite = _walk != null ? _walk.frames[0] : Art.Enemy(def.id);
                _hasArt = sprite != null;
                _art.sprite = sprite;
                _art.gameObject.SetActive(_hasArt);
                _placeholder.gameObject.SetActive(!_hasArt);
                bool boss = def.kind == EnemyKind.Boss || def.bossSkill != BossSkill.None;

                float top;
                if (_hasArt)
                {
                    // Art canvas is square with the feet at 12% from the bottom (pivot set by ZooArtImporter).
                    // Grows slower than the gameplay size so big send-monsters stay close to the path lane.
                    float canvas = 0.55f + 0.6f * _size;
                    WorldKit.FitSprite(_art, _walk != null ? canvas * _walk.fit : canvas);
                    _feetY = -canvas * 0.38f;
                    top = _feetY + canvas * 0.88f;
                    _centerY = _feetY + canvas * 0.4f;
                }
                else
                {
                    _feetY = -_size * 0.5f;
                    _placeholder.localPosition = new Vector3(0f, _size * 0.5f, 0f);
                    _body.transform.localScale = new Vector3(_size, _size, 1f);
                    _ring.transform.localScale = new Vector3(_size * 1.05f, _size * 1.05f, 1f);
                    _label.text = boss ? "BOSS" : (def.kind == EnemyKind.Elite ? "ELITE" : "");
                    top = _size * 0.5f;
                    _centerY = 0f;
                }

                float barW = Mathf.Max(0.5f, _size);
                _barBg.transform.localPosition = new Vector3(0f, top + 0.08f, 0f);
                _barBg.transform.localScale = new Vector3(barW, 0.12f, 1f);
                _barFill.transform.localPosition = _barBg.transform.localPosition;
                _barFill.transform.localScale = _barBg.transform.localScale;
                _barBg.gameObject.SetActive(true);
                _barFill.gameObject.SetActive(true);
                // Still sent-monster art carries the purple shield badge; walk sheets and placeholders need the marker.
                _sentMark.gameObject.SetActive(e.sender >= 0 && (!_hasArt || _walk != null));
                _sentMark.transform.localPosition = new Vector3(0f, top + 0.32f, 0f);
                _elementDot.color = Art.ElementColor(e.element);
                _elementRing.transform.localPosition = new Vector3(0f, _feetY - 0.02f, 0f);
                _elementRing.gameObject.SetActive(_hasArt && !compact);

                _motion.Bind(_walk != null ? MotionLibrary.GetForWalkSheet(def.id) : MotionLibrary.Get(def.id, true), true, e.uid);
                _motion.Tick(0f);
                Apply();
            }

            public void Hit() { _motion.Play(ZooDefense.Animation.ZooMotionState.Hit); }

            /// <summary>World point projectiles aim at (middle of the body).</summary>
            public Vector3 CenterWorld { get { return root.transform.TransformPoint(new Vector3(0f, _centerY, 0f)); } }

            public bool StartDeath()
            {
                if (!root.activeSelf) return false;
                _barBg.gameObject.SetActive(false);
                _barFill.gameObject.SetActive(false);
                _sentMark.gameObject.SetActive(false);
                _elementRing.gameObject.SetActive(false);
                _shield.gameObject.SetActive(false);
                _motion.Play(ZooDefense.Animation.ZooMotionState.Death, true);
                return true;
            }

            /// <summary>Returns true while the death motion is still playing.</summary>
            public bool TickDeath(float dt)
            {
                _motion.Tick(dt);
                Apply();
                return !_motion.DeathFinished;
            }

            public void Sync(EnemyInstance e, float dt)
            {
                float frac = e.maxHp > 0 ? Mathf.Clamp01((float)e.hp / e.maxHp) : 0f;
                var bg = _barBg.transform;
                float fullW = bg.localScale.x;
                _barFill.transform.localScale = new Vector3(fullW * frac, bg.localScale.y, 1f);
                _barFill.transform.localPosition = bg.localPosition + new Vector3(-fullW * (1f - frac) * 0.5f, 0f, 0f);

                _slowed = e.slowTicks > 0;
                _stunned = e.stunTicks > 0;
                _silenced = e.silenceTicks > 0;
                _burning = e.burnTicks > 0 && e.burnStacks > 0;
                _marked = e.vulnTicks > 0;
                bool shield = e.shield > 0;
                if (_shield.gameObject.activeSelf != shield) _shield.gameObject.SetActive(shield);
                if (shield)
                {
                    float sz = (0.55f + 0.6f * _size) * (1.15f + 0.05f * Mathf.Sin(Time.time * 5f));
                    _shield.transform.localPosition = new Vector3(0f, _centerY, 0f);
                    WorldKit.FitSprite(_shield, sz);
                }
                _alpha = e.revealed ? 1f : 0.3f;
                _motion.Tick(_stunned ? 0f : dt);
                if (_walk != null)
                {
                    // Legs follow the real pace: frozen when stunned, half speed when slowed.
                    _walkTime += dt * (_stunned ? 0f : (_slowed ? 0.5f : 1f));
                    int frame = (int)(_walkTime * _walk.fps) % _walk.frameCount;
                    if (frame != _walkFrame) { _walkFrame = frame; _art.sprite = _walk.frames[frame]; }
                }
                Apply();
            }

            void Apply()
            {
                _motion.Apply(_visual, new Vector3(0f, _feetY, 0f), _size, 1f);
                var tint = Color.white;
                if (_slowed) tint = Color.Lerp(tint, Art.Hex(0x7FE7FF), 0.35f);
                if (_stunned) tint = Color.Lerp(tint, Art.Hex(0xFFF27A), 0.45f);
                if (_burning) tint = Color.Lerp(tint, Art.Hex(0xFF8A3D), 0.25f + 0.15f * Mathf.Sin(Time.time * 12f));
                if (_marked) tint = Color.Lerp(tint, Art.Hex(0xFF9AD5), 0.3f);
                if (_silenced) tint = Color.Lerp(tint, Art.Hex(0x8E7FB0), 0.5f);
                tint.a = _alpha;
                _art.color = _motion.Tint(tint);

                var c = _color;
                if (_slowed) c = Color.Lerp(c, Art.Hex(0x7FE7FF), 0.45f);
                if (_stunned) c = Color.Lerp(c, Art.Hex(0xFFF27A), 0.5f);
                c.a = _alpha;
                _body.color = _motion.Tint(c);
                var rc = _ring.color; rc.a = _body.color.a; _ring.color = rc;
            }
        }
    }
}
