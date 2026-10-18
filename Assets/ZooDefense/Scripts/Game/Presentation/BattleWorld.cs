using UnityEngine;
using UnityEngine.EventSystems;
using ZooDefense.Core;

namespace ZooDefense.Game
{
    /// <summary>
    /// World-space part of a battle: both boards, effects and touch input on the local board.
    /// Layout: the player's board fills the main area between the HUD bars; the opponent's board is a small
    /// live preview inside the opponent strip. "Focus" swaps the two so the opponent can be watched big.
    /// Input on the local board: drag animals to move/merge, tap a glowing animal to use its skill,
    /// tap balloons, and tap the path while a spell is armed.
    /// </summary>
    public sealed class BattleWorld : MonoBehaviour
    {
        public BoardView Local { get; private set; }
        public BoardView Remote { get; private set; }
        public bool InputEnabled = true;
        /// <summary>Tap (not drag) on an animal of the local board.</summary>
        public System.Action<int> OnUnitTapped;
        /// <summary>Tap on the local path while ArmedSpell >= 0: (slot, path distance).</summary>
        public System.Action<int, int> OnSpellTarget;
        /// <summary>Tap anywhere on the remote board (used to toggle the opponent view).</summary>
        public System.Action OnRemoteTapped;
        /// <summary>Spell slot waiting for a target on the path, -1 = none.</summary>
        public int ArmedSpell = -1;
        /// <summary>True while the opponent's board is shown big.</summary>
        public bool FocusRemote;
        /// <summary>PvE modes: the idle second board is not drawn.</summary>
        public bool HideRemote
        {
            get { return _hideRemote; }
            set { _hideRemote = value; if (Remote != null) Remote.gameObject.SetActive(!value); if (value) FocusRemote = false; }
        }
        bool _hideRemote;

        MatchRunner _runner;
        Camera _cam;
        FxPool _fx;
        SpriteRenderer _background;

        int _dragCell = -1, _dragUid = -1;
        Vector3 _pressWorld;
        bool _dragging;
        int _placeHighlight = -1;


        public static BattleWorld Create(MatchRunner runner, Camera cam, Arena arena)
        {
            var go = new GameObject("BattleWorld");
            var w = go.AddComponent<BattleWorld>();
            w._runner = runner;
            w._cam = cam;
            w._fx = WorldKit.Node("Fx", go.transform, Vector3.zero).AddComponent<FxPool>();

            var bgSprite = Art.Background(arena.index);
            if (bgSprite != null) w._background = WorldKit.Sprite("Background", go.transform, bgSprite, Color.white, Order.Background, Vector3.zero);

            w.Local = WorldKit.Node("LocalBoard", go.transform, Vector3.zero).AddComponent<BoardView>();
            w.Local.Init(runner, runner.LocalPlayer, w._fx, false, arena.tint);
            w.Remote = WorldKit.Node("RemoteBoard", go.transform, Vector3.zero).AddComponent<BoardView>();
            w.Remote.Init(runner, 1 - runner.LocalPlayer, w._fx, true, arena.tint);
            runner.OnEvent += w.Local.OnSimEvent;
            runner.OnEvent += w.Remote.OnSimEvent;
            return w;
        }

        void OnDestroy()
        {
            if (_runner != null && Local != null) _runner.OnEvent -= Local.OnSimEvent;
            if (_runner != null && Remote != null) _runner.OnEvent -= Remote.OnSimEvent;
        }

        /// <summary>
        /// Places the boards. Screen-pixel rects: the main board area (between the HUD bars) and the small
        /// opponent preview in the opponent strip. FocusRemote swaps which board uses which rect.
        /// </summary>
        public void Layout(Rect mainPx, Rect thumbPx)
        {
            float aspect = _cam.aspect;
            _cam.orthographicSize = Mathf.Max((Local.Width + 0.4f) / aspect / 2f, 6.6f);
            float worldW = _cam.orthographicSize * 2f * aspect;

            if (_hideRemote) FocusRemote = false;
            var big = FocusRemote ? Remote : Local;
            var small = FocusRemote ? Local : Remote;
            Fit(big, mainPx, 1.35f);
            if (!_hideRemote) Fit(small, thumbPx, 1f);

            if (_background != null)
            {
                var size = _background.sprite.bounds.size;
                float cover = Mathf.Max(worldW / size.x, _cam.orthographicSize * 2f / size.y);
                _background.transform.localScale = new Vector3(cover, cover, 1f);
                _background.transform.position = new Vector3(_cam.transform.position.x, _cam.transform.position.y, 0f);
            }
        }

        /// <summary>Scale a board to fit inside a screen-pixel rect (keeping its proportions) and centre it there.</summary>
        void Fit(BoardView board, Rect px, float maxScale)
        {
            var a = ScreenToWorld(new Vector2(px.xMin, px.yMin));
            var b = ScreenToWorld(new Vector2(px.xMax, px.yMax));
            float w = Mathf.Abs(b.x - a.x), h = Mathf.Abs(b.y - a.y);
            float scale = Mathf.Max(0.05f, Mathf.Min(maxScale, w / board.Width, h / board.Height));
            board.transform.localScale = new Vector3(scale, scale, 1f);
            board.transform.position = new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, 0f);
        }

        // ------------------------------------------------------------------ helpers for the HUD

        public Vector3 ScreenToWorld(Vector2 screen)
        {
            var w = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            w.z = 0f;
            return w;
        }

        /// <summary>Free, unlocked local cell under a screen point (for dragging a shop card), or -1.</summary>
        public int PlaceCellAt(Vector2 screen)
        {
            if (FocusRemote) return -1;
            int cell = Local.CellAt(ScreenToWorld(screen));
            if (cell < 0 || Local.Board.Cells[cell] != null || Local.Board.IsCellLocked(cell)) return -1;
            return cell;
        }

        public void ShowPlaceTarget(int cell)
        {
            if (cell == _placeHighlight) return;
            _placeHighlight = cell;
            Local.ShowPlaceTarget(cell);
        }

        public Vector3 CellScreen(int cell) { return _cam.WorldToScreenPoint(Local.CellWorld(cell)); }

        // ------------------------------------------------------------------ input

        void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!InputEnabled || _runner.Match.IsOver) { CancelDrag(); return; }
            var board = Local.Board;
            Vector3 world = ScreenToWorld(Input.mousePosition);

            if (Input.GetMouseButtonDown(0) && !PointerOverUI())
            {
                if (FocusRemote)
                {
                    if (OnRemoteTapped != null) OnRemoteTapped();
                    return;
                }
                if (!_hideRemote && Remote.Contains(world)) { if (OnRemoteTapped != null) OnRemoteTapped(); return; }

                // Balloons first: they float over everything.
                int balloon = Local.BalloonAt(world);
                if (balloon != 0) { _runner.Submit(CommandType.PopBalloon, balloon); return; }

                if (ArmedSpell >= 0)
                {
                    float off;
                    int distance = Local.PathDistanceAt(world, out off);
                    if (off <= 1.6f && OnSpellTarget != null) OnSpellTarget(ArmedSpell, distance);
                    return;
                }

                int cell = Local.CellAt(world);
                if (cell >= 0 && board.Cells[cell] != null)
                {
                    _dragCell = cell;
                    _dragUid = board.Cells[cell].uid;
                    _pressWorld = world;
                    _dragging = false;
                }
            }

            if (_dragUid >= 0)
            {
                var stillThere = board.Cells[_dragCell];
                if (stillThere == null || stillThere.uid != _dragUid) { CancelDrag(); return; }

                if (Input.GetMouseButton(0))
                {
                    if (!_dragging && (world - _pressWorld).sqrMagnitude > 0.15f * 0.15f * Local.WorldScale * Local.WorldScale)
                    {
                        _dragging = true;
                        Local.ShowMergeTargets(_dragCell);
                    }
                    if (_dragging)
                    {
                        Local.DraggingUid = _dragUid;
                        Local.DragWorldPosition = world + new Vector3(0f, 0.25f * Local.WorldScale, 0f);
                    }
                }
                if (Input.GetMouseButtonUp(0))
                {
                    if (_dragging)
                    {
                        int to = Local.CellAt(world);
                        if (to >= 0 && to != _dragCell) _runner.Submit(CommandType.Move, _dragCell, to);
                    }
                    else if (OnUnitTapped != null) OnUnitTapped(_dragCell);
                    CancelDrag();
                }
            }
#endif
        }

        void CancelDrag()
        {
            if (_dragUid < 0) return;
            _dragUid = -1;
            _dragCell = -1;
            _dragging = false;
            Local.DraggingUid = -1;
            Local.ShowMergeTargets(-1);
        }

        static bool PointerOverUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
#endif
            return es.IsPointerOverGameObject();
        }
    }
}
