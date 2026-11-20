using System;
using System.Collections.Generic;
using LitMotion;
using UnityEngine;

namespace Game.Views
{
    /// <summary>
    /// The 3D play board: authored plaza modules, tube trays and buddies under
    /// the screen's WorldRoot. The board lies on its content's local XZ plane (Y up) and the content is
    /// tilted back by <see cref="_tiltDegrees"/> under the orthographic GamePlay camera — the 3/4 view
    /// (GDD §6) without a second camera.
    ///
    /// Speaks <b>grid cells</b> (game units, rule #10), never screen coordinates; a tap comes back as a
    /// buddy id. Holds no rules: the controller decides what moves where and in which order, this only
    /// lays it out and animates it (rule #8). Look values (sizes, tones, tilt) live on the prefab; the
    /// squad palette comes from the <see cref="LookLibrary"/>.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [Header("Structure")]
        [SerializeField] private Transform _content;
        [SerializeField] private MeshRenderer _ground;

        [Header("Materials")]
        [Tooltip("URP Lit, white; every piece is tinted through a property block.")]
        [SerializeField] private Material _lit;

        [Header("Look")]
        [SerializeField] private LookLibrary _look;
        [Tooltip("How far the board plane leans back from facing the camera, degrees. 0 = top-down, 90 = edge-on.")]
        [SerializeField] private float _tiltDegrees;
        [Tooltip("Centre-to-centre distance between neighbouring cells, world units.")]
        [SerializeField] private float _pitch;
        [SerializeField] private BuddyView.Shape _buddy;
        [Tooltip("How far a buddy turns from standing on the board (0) to facing the camera square-on (1).")]
        [Range(0f, 1f)]
        [SerializeField] private float _buddyUpright;
        [Tooltip("How far a buddy floats toward the camera off the board plane, so the tilted floor never cuts it.")]
        [SerializeField] private float _buddyLift;
        [Tooltip("Plaza tile: width/depth fraction of the pitch, and thickness.")]
        [SerializeField] private float _tileFill;
        [SerializeField] private float _tileThickness;
        [SerializeField] private Color _tileColor;
        [SerializeField] private Color _rejectFlash;
        [Tooltip("Plaza bed under the tiles, and the raised rim around the plaza's outer edge (open where a tube joins).")]
        [SerializeField] private Color _plazaFloorColor;
        [SerializeField] private Color _rimColor;
        [SerializeField] private float _rimWidth;
        [SerializeField] private float _rimHeight;
        [Tooltip("Tube tray: floor thickness, wall thickness and height, extra length past the outer slots.")]
        [SerializeField] private float _trayFloor;
        [SerializeField] private float _trayWall;
        [SerializeField] private float _trayWallHeight;
        [SerializeField] private float _trayPad;
        [SerializeField] private Color _trayColor;
        [SerializeField] private Color _trayWallColor;
        [Tooltip("The open end of a tube: coloured caps on the side walls, and how long they are.")]
        [SerializeField] private Color _trayMouthColor;
        [SerializeField] private float _mouthTipLength;
        [Tooltip("House: height of the walls; the roof shade (blended over the house colour by its alpha) and the door tone.")]
        [SerializeField] private float _houseHeight;
        [SerializeField] private Color _roofColor;
        [SerializeField] private Color _doorColor;
        [SerializeField] private Color _groundColor;
        [Tooltip("World z of the ground plane — behind the tilted board (the camera looks down +z; the rig clips past +1000).")]
        [SerializeField] private float _groundDepth;

        [Header("Authored mockup meshes")]
        [SerializeField] private GameObject _queueModel;
        [SerializeField] private GameObject _cellModel;
        [SerializeField] private GameObject _edgeModel;
        [SerializeField] private float _modelCellSize = 1.15f;
        [SerializeField] private float _characterScale = 100f;

        [Header("Fit (world units)")]
        [SerializeField] private float _marginSide;
        [SerializeField] private float _marginTop;
        [SerializeField] private float _marginBottom;
        [Tooltip("Largest scale the fit may grow a small board to.")]
        [SerializeField] private float _maxFitScale = 1f;

        private readonly List<MeshRenderer> _plazaTiles = new List<MeshRenderer>();
        private readonly Dictionary<int, TubeParts> _tubes = new Dictionary<int, TubeParts>();
        private readonly Dictionary<int, BuddyView> _buddies = new Dictionary<int, BuddyView>();
        private readonly List<Vector2Int> _occupiedCells = new List<Vector2Int>();
        private readonly List<GameObject> _plazaPieces = new List<GameObject>();
        private readonly HashSet<Vector2Int> _plazaCells = new HashSet<Vector2Int>();
        private readonly HashSet<(Vector2Int Cell, Vector2Int Side)> _ports = new HashSet<(Vector2Int Cell, Vector2Int Side)>();
        private Transform _rims;
        private Quaternion _buddyTurn = Quaternion.identity;
        private Vector3 _buddyShift;
        private MotionHandle _flash;

        /// <summary>Raised with the buddy id when the player taps a buddy.</summary>
        public event Action<int> BuddyTapped;

        /// <summary>Raised with the tube id when the player taps a tube's tray (empty space in it included).</summary>
        public event Action<int> TubeTapped;

        /// <summary>Raised with the grid cell when the player taps a plaza tile.</summary>
        public event Action<Vector2Int> CellTapped;

        private void Awake()
        {
            _content.localRotation = Quaternion.Euler(-(90f - _tiltDegrees), 0f, 0f);

            // An upright buddy turns back toward the camera and is centred on its cell rather than standing at it.
            _buddyTurn = Quaternion.Euler(_buddyUpright * (90f - _tiltDegrees), 0f, 0f);
            float height = _buddy.Body.y + _buddy.Foot.y * 0.55f;
            Vector3 towardCamera = Quaternion.Inverse(_content.localRotation) * Vector3.back;
            _buddyShift = _buddyTurn * new Vector3(0f, -height * 0.5f * _buddyUpright, 0f) + towardCamera * _buddyLift;
            if (_ground != null) BoardPiece.SetColor(_ground, _groundColor);
        }

        // ------------------------------------------------------------------ layout

        /// <summary>Remove everything the board drew; the ground stays.</summary>
        public void Clear()
        {
            _flash.TryCancel();
            foreach (GameObject p in _plazaPieces) Destroy(p);
            if (_rims != null) Destroy(_rims.gameObject);
            _rims = null;
            _plazaPieces.Clear();
            _plazaCells.Clear();
            _ports.Clear();
            foreach (TubeParts t in _tubes.Values) Destroy(t.Root.gameObject);
            foreach (BuddyView b in _buddies.Values)
                if (b != null) Destroy(b.gameObject);
            _plazaTiles.Clear();
            _tubes.Clear();
            _buddies.Clear();
            _occupiedCells.Clear();
            _content.localScale = Vector3.one;
            _content.localPosition = Vector3.zero;
        }

        /// <summary>
        /// Cover <paramref name="worldRect"/> (the device's full visible area) with the ground, behind the
        /// board. A textured ground keeps its picture's aspect and crops the overflow, never stretches.
        /// </summary>
        public void SetGroundRect(Rect worldRect)
        {
            if (_ground == null) return;
            float w = worldRect.width;
            float h = worldRect.height;
            Texture tex = _ground.sharedMaterial != null ? _ground.sharedMaterial.mainTexture : null;
            if (tex != null && tex.height > 0)
            {
                float aspect = (float)tex.width / tex.height;
                if (w / h > aspect) h = w / aspect;
                else w = h * aspect;
            }

            Transform g = _ground.transform;
            g.position = new Vector3(worldRect.center.x, worldRect.center.y, _groundDepth);
            g.localScale = new Vector3(w, h, 1f);
        }

        /// <summary>
        /// Centre the board's on-screen footprint in <paramref name="safeRect"/> less the HUD margins,
        /// shrinking it (never enlarging) if it does not fit.
        /// </summary>
        public void FitInto(Rect safeRect)
        {
            if (_occupiedCells.Count == 0) return;
            var target = Rect.MinMaxRect(
                safeRect.xMin + _marginSide,
                safeRect.yMin + _marginBottom,
                safeRect.xMax - _marginSide,
                safeRect.yMax - _marginTop);

            // Project the occupied cells' corners and a standing buddy's head through the tilt.
            Quaternion tilt = _content.localRotation;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            float h = _pitch * 0.5f;
            foreach (Vector2Int c in _occupiedCells)
            {
                Vector3 p = CellToLocal(c);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 local = p + new Vector3(corner % 2 == 0 ? -h : h, corner < 2 ? 0f : _buddy.Body.y, corner < 2 ? -h : h);
                    Vector3 s = tilt * local;
                    min = Vector2.Min(min, s);
                    max = Vector2.Max(max, s);
                }
            }

            Vector2 size = max - min;
            float scale = Mathf.Min(Mathf.Max(1f, _maxFitScale), target.width / size.x, target.height / size.y);
            Vector2 centre = (min + max) * 0.5f * scale;
            _content.localScale = new Vector3(scale, scale, scale);
            _content.position = new Vector3(target.center.x - centre.x, target.center.y - centre.y, _content.position.z);
        }

        public void AddPlazaCell(Vector2Int cell)
        {
            Vector3 at = CellToLocal(cell);
            float w = _pitch * _tileFill;
            MeshRenderer tile;
            if (_cellModel != null)
            {
                GameObject model = SpawnModel(_cellModel, _content, at, Vector3.one * (_pitch / _modelCellSize));
                _plazaPieces.Add(model);
                tile = model.GetComponentInChildren<MeshRenderer>();
            }
            else
            {
                MeshRenderer bed = BoardPiece.Create($"Plaza Bed {cell}", _content, _lit, new Vector3(_pitch, _trayFloor, _pitch), 1f,
                    at + new Vector3(0f, -BedTop - _trayFloor * 0.5f, 0f), _plazaFloorColor);
                tile = BoardPiece.Create($"Plaza {cell}", _content, _lit, new Vector3(w, _tileThickness, w), _tileThickness * 0.5f,
                    at + new Vector3(0f, -_tileThickness * 0.5f, 0f), _tileColor);
                _plazaPieces.Add(bed.gameObject);
                _plazaPieces.Add(tile.gameObject);
            }
            TapTarget tap = TapTarget.Create($"Plaza Hit {cell}", _content, at, OnScreen(new Vector2(_pitch, _pitch)));
            tap.Tapped += () => CellTapped?.Invoke(cell);
            _plazaPieces.Add(tap.gameObject);
            _plazaTiles.Add(tile);
            _plazaCells.Add(cell);
            _occupiedCells.Add(cell);
            RebuildRims();
        }

        /// <summary>
        /// A tube of <paramref name="capacity"/> slots whose mouth is <paramref name="mouth"/> and which
        /// runs one cell per slot along <paramref name="towardClosed"/> (a unit grid step).
        /// </summary>
        public void AddTube(int tube, Vector2Int mouth, Vector2Int towardClosed, int capacity)
        {
            var root = new GameObject($"Tube {tube}").transform;
            root.gameObject.layer = gameObject.layer;
            root.SetParent(_content, false);

            Vector2Int closed = mouth + towardClosed * (capacity - 1);
            Vector3 a = CellToLocal(mouth);
            Vector3 b = CellToLocal(closed);
            root.localPosition = (a + b) * 0.5f;
            bool alongX = towardClosed.x != 0;
            float length = (capacity - 1) * _pitch + _pitch + 2f * _trayPad;
            float width = _pitch + 2f * _trayWall;

            // Local frame of the tray: +Z runs from mouth to closed end.
            var frame = new GameObject("Frame").transform;
            frame.gameObject.layer = root.gameObject.layer;
            frame.SetParent(root, false);
            Vector3 dir = (b - a).sqrMagnitude > 0f ? (b - a).normalized : new Vector3(alongX ? 1f : 0f, 0f, alongX ? 0f : 1f);
            frame.localRotation = Quaternion.LookRotation(dir, Vector3.up);

            if (_queueModel != null)
            {
                GameObject tray = SpawnModel(_queueModel, frame, new Vector3(0f, -_pitch / _modelCellSize * 0.04f, 0f),
                    new Vector3(width / 1.28f, _pitch / _modelCellSize, length / 4.6f));
                tray.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                BoardPiece.Create("Floor", frame, _lit, new Vector3(width, _trayFloor, length), _trayFloor * 0.5f, new Vector3(0f, -_trayFloor * 0.5f, 0f), _trayColor);
                float wallY = _trayWallHeight * 0.5f;
                float side = (width - _trayWall) * 0.5f;
                BoardPiece.Create("Wall L", frame, _lit, new Vector3(_trayWall, _trayWallHeight, length), _trayWall * 0.5f, new Vector3(-side, wallY, 0f), _trayWallColor);
                BoardPiece.Create("Wall R", frame, _lit, new Vector3(_trayWall, _trayWallHeight, length), _trayWall * 0.5f, new Vector3(side, wallY, 0f), _trayWallColor);
                BoardPiece.Create("Wall End", frame, _lit, new Vector3(width, _trayWallHeight, _trayWall), _trayWall * 0.5f, new Vector3(0f, wallY, (length - _trayWall) * 0.5f), _trayWallColor);
                float tipZ = -length * 0.5f + _mouthTipLength * 0.5f - 0.5f;
                var tip = new Vector3(_trayWall + 1f, _trayWallHeight + 1f, _mouthTipLength);
                BoardPiece.Create("Tip L", frame, _lit, tip, _trayWall * 0.5f, new Vector3(-side, wallY, tipZ), _trayMouthColor);
                BoardPiece.Create("Tip R", frame, _lit, tip, _trayWall * 0.5f, new Vector3(side, wallY, tipZ), _trayMouthColor);

            }
            TapTarget tap = TapTarget.Create("Hit", root, Vector3.zero, OnScreen(alongX ? new Vector2(length, width) : new Vector2(width, length)));
            tap.Tapped += () => TubeTapped?.Invoke(tube);

            for (int i = 0; i < capacity; i++) _occupiedCells.Add(mouth + towardClosed * i);
            Vector2Int port = mouth - towardClosed;
            if (_plazaCells.Contains(port))
            {
                _ports.Add((port, towardClosed));
                RebuildRims();
            }
            _tubes[tube] = new TubeParts(root, frame, width, length);
        }

        /// <summary>Cover the tube with a closed house of colour <paramref name="colorId"/>.</summary>
        public void SetTubeHouse(int tube, int colorId)
        {
            if (!_tubes.TryGetValue(tube, out TubeParts t) || t.House != null) return;
            var house = new GameObject("House").transform;
            house.gameObject.layer = t.Frame.gameObject.layer;
            house.SetParent(t.Frame, false);
            Color wall = _look.SquadColor(colorId);
            float w = t.Width;
            float len = t.Length;
            BoardPiece.Create("Walls", house, _lit, new Vector3(w, _houseHeight, len), _trayWall, new Vector3(0f, _houseHeight * 0.5f, 0f), wall);
            var roof = BoardPiece.Create("Roof", house, _lit, new Vector3(w * 0.72f, w * 0.72f, len + _trayWall), _trayWall, new Vector3(0f, _houseHeight, 0f), Color.Lerp(wall, _roofColor, _roofColor.a));
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            float door = _pitch * 0.42f;
            BoardPiece.Create("Door", house, _lit, new Vector3(door, door * 1.3f, _trayWall), _trayWall * 0.4f,
                new Vector3(0f, door * 0.65f, -len * 0.5f - _trayWall * 0.3f), _doorColor);
            t.House = house.gameObject;
        }

        /// <summary>Lift the house off: the tube is an ordinary open tube again.</summary>
        public void SetTubeOpen(int tube)
        {
            if (!_tubes.TryGetValue(tube, out TubeParts t) || t.House == null) return;
            GameObject house = t.House;
            t.House = null;
            Transform tr = house.transform;
            LMotion.Create(0f, 1f, MotionTokens.TubeFade)
                .WithOnComplete(() =>
                {
                    if (house != null) Destroy(house);
                })
                .Bind(k =>
                {
                    tr.localPosition = new Vector3(0f, k * _houseHeight, 0f);
                    tr.localScale = Vector3.one * (1f - k);
                });
        }

        /// <summary>Shrink the tube away after <paramref name="delay"/> seconds.</summary>
        public void RemoveTube(int tube, float delay)
        {
            if (!_tubes.TryGetValue(tube, out TubeParts t)) return;
            _tubes.Remove(tube);
            Transform root = t.Root;
            LMotion.Create(1f, 0f, MotionTokens.TubeFade)
                .WithDelay(delay)
                .WithOnComplete(() =>
                {
                    if (root != null) Destroy(root.gameObject);
                })
                .Bind(k =>
                {
                    if (root != null) root.localScale = new Vector3(k, k, k);
                });
        }

        // ------------------------------------------------------------------ buddies

        public void AddBuddy(int id, Vector2Int cell, int colorId, bool hidden, bool awake)
        {
            var go = new GameObject($"Buddy {id}");
            go.layer = gameObject.layer;
            go.transform.SetParent(_content, false);
            BuddyView b = go.AddComponent<BuddyView>();
            b.Build(_buddy, _lit);
            b.SetModel(_look.BuddyModel(colorId), _look.HiddenBuddyModel, _characterScale);
            b.Bind(id);
            b.SetTint(hidden ? _look.HiddenBuddy : _look.SquadColor(colorId), animate: false);
            b.SetHidden(hidden);
            b.SetAwake(awake);
            go.transform.localRotation = _buddyTurn;
            b.PlaceAt(BuddyAt(cell));
            b.Tapped += OnBuddyTapped;
            _buddies[id] = b;
        }

        /// <summary>Show the buddy's real colour (a hidden buddy reaching the front of its tube).</summary>
        public void RevealBuddy(int id, int colorId)
        {
            if (!_buddies.TryGetValue(id, out BuddyView b)) return;
            b.SetModel(_look.BuddyModel(colorId), _look.HiddenBuddyModel, _characterScale);
            b.SetHidden(false);
            b.SetTint(_look.SquadColor(colorId), animate: true);
        }

        public void SetBuddyAwake(int id, bool awake)
        {
            if (_buddies.TryGetValue(id, out BuddyView b)) b.SetAwake(awake);
        }

        public void MoveBuddy(int id, Vector2Int cell, float delay)
        {
            if (_buddies.TryGetValue(id, out BuddyView b)) b.MoveTo(BuddyAt(cell), delay);
        }

        /// <summary>Snap a buddy to a cell with no animation (undo, restore).</summary>
        public void PlaceBuddy(int id, Vector2Int cell)
        {
            if (_buddies.TryGetValue(id, out BuddyView b)) b.PlaceAt(BuddyAt(cell));
        }

        /// <summary>A single buddy's "can't go there" shake.</summary>
        public void WinceBuddy(int id)
        {
            if (_buddies.TryGetValue(id, out BuddyView b)) b.Wince();
        }

        /// <summary>Lift (picked up) or set down a buddy.</summary>
        public void SetBuddySelected(int id, bool selected)
        {
            if (_buddies.TryGetValue(id, out BuddyView b)) b.SetSelected(selected);
        }

        public void CelebrateAndRemoveBuddy(int id, float delay)
        {
            if (!_buddies.TryGetValue(id, out BuddyView b)) return;
            _buddies.Remove(id);
            b.Tapped -= OnBuddyTapped;
            b.CelebrateAndRemove(delay);
        }

        /// <summary>The "no room" reaction: the plaza flashes and, if given, that buddy winces.</summary>
        public void PlayRejected(int buddyId)
        {
            if (_buddies.TryGetValue(buddyId, out BuddyView b)) b.Wince();
            _flash.TryCancel();
            List<MeshRenderer> tiles = _plazaTiles;
            Color rest = _tileColor;
            Color hot = _rejectFlash;
            _flash = LMotion.Create(0f, 1f, MotionTokens.RejectFlash)
                .Bind(t =>
                {
                    Color c = Color.Lerp(hot, rest, t);
                    foreach (MeshRenderer r in tiles) BoardPiece.SetColor(r, c);
                });
        }

        // ------------------------------------------------------------------ internals

        private static readonly Vector2Int[] Sides = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        /// <summary>How far the plaza bed's top sits below the tiles' top (the tiles stand proud of it).</summary>
        private const float BedTop = 4f;

        /// <summary>
        /// Raise the rim along every plaza edge that faces neither another plaza cell nor a tube mouth.
        /// A segment runs past its ends only at an outside corner, so inside corners stay clean.
        /// </summary>
        private void RebuildRims()
        {
            if (_rims != null) Destroy(_rims.gameObject);
            _rims = new GameObject("Plaza Rim").transform;
            _rims.gameObject.layer = gameObject.layer;
            _rims.SetParent(_content, false);

            float bottom = -BedTop - _trayFloor;
            float height = _rimHeight - bottom;
            float half = _pitch * 0.5f;
            foreach (Vector2Int cell in _plazaCells)
            foreach (Vector2Int side in Sides)
            {
                if (_plazaCells.Contains(cell + side) || _ports.Contains((cell, side))) continue;

                var across = new Vector2Int(side.y, side.x); // perpendicular grid step
                float lo = Extends(cell, side, -across) ? _rimWidth : 0f;
                float hi = Extends(cell, side, across) ? _rimWidth : 0f;
                float length = _pitch + lo + hi;
                float shift = (hi - lo) * 0.5f;

                Vector3 outward = GridToLocalDir(side);
                Vector3 along = GridToLocalDir(across);
                Vector3 centre = CellToLocal(cell) + outward * (half + _rimWidth * 0.5f) + along * shift + new Vector3(0f, bottom + height * 0.5f, 0f);
                Vector3 size = side.x != 0 ? new Vector3(_rimWidth, height, length) : new Vector3(length, height, _rimWidth);
                if (_edgeModel != null)
                {
                    GameObject edge = SpawnModel(_edgeModel, _rims,
                        centre + new Vector3(0f, -height * 0.5f, 0f),
                        new Vector3(length / 1.15f, height / 0.26f, _rimWidth / 0.12f));
                    edge.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);
                }
                else BoardPiece.Create("Rim", _rims, _lit, size, _rimWidth * 0.5f, centre, _rimColor);
            }
        }

        /// <summary>An edge's rim reaches over the corner only when that corner is an outside one.</summary>
        private bool Extends(Vector2Int cell, Vector2Int side, Vector2Int across)
            => !_plazaCells.Contains(cell + across) && !_plazaCells.Contains(cell + across + side);

        private GameObject SpawnModel(GameObject prefab, Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject model = Instantiate(prefab, parent, false);
            model.transform.localPosition = position;
            model.transform.localScale = scale;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
            return model;
        }

        private static Vector3 GridToLocalDir(Vector2Int step) => new Vector3(step.x, 0f, -step.y);

        /// <summary>Grid → board plane. Grid Y grows downward on screen, which is toward the camera (−Z).</summary>
        private Vector3 CellToLocal(Vector2Int cell) => new Vector3(cell.x * _pitch, 0f, -cell.y * _pitch);

        /// <summary>On-screen size of a board-plane footprint (x across, y along the grid's depth).</summary>
        private Vector2 OnScreen(Vector2 footprint) => new Vector2(footprint.x, footprint.y * Mathf.Cos(_tiltDegrees * Mathf.Deg2Rad));

        private Vector3 BuddyAt(Vector2Int cell) => CellToLocal(cell) + _buddyShift;

        private void OnBuddyTapped(int id) => BuddyTapped?.Invoke(id);

        private void OnDestroy() => _flash.TryCancel();

        private sealed class TubeParts
        {
            public TubeParts(Transform root, Transform frame, float width, float length)
            {
                Root = root;
                Frame = frame;
                Width = width;
                Length = length;
            }

            public Transform Root { get; }
            public Transform Frame { get; }
            public float Width { get; }
            public float Length { get; }
            public GameObject House { get; set; }
        }
    }

    /// <summary>
    /// Builds and caches rounded-box meshes — the one shape every placeholder board piece is made of
    /// (buddy bodies, tiles, tube walls, houses). Centred on the origin; <c>size</c> is the full extent.
    /// </summary>
    public static class RoundedBoxMesh
    {
        private static readonly Dictionary<Vector4, Mesh> Cache = new Dictionary<Vector4, Mesh>();

        /// <param name="size">Full width, height, depth.</param>
        /// <param name="radius">Corner radius, clamped to half the smallest side.</param>
        public static Mesh Get(Vector3 size, float radius)
        {
            var key = new Vector4(size.x, size.y, size.z, radius);
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            Mesh mesh = Build(size, radius, 6);
            Cache[key] = mesh;
            return mesh;
        }

        private static Mesh Build(Vector3 size, float radius, int segments)
        {
            Vector3 half = size * 0.5f;
            float r = Mathf.Clamp(radius, 0f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)));
            Vector3 inner = half - new Vector3(r, r, r);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // Six faces of a subdivided cube; every vertex is pushed onto the rounded surface.
            Vector3[] faceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int n = segments * 2;
            foreach (Vector3 fn in faceNormals)
            {
                Vector3 u = new Vector3(fn.y, fn.z, fn.x);
                Vector3 v = Vector3.Cross(fn, u);
                int start = vertices.Count;
                for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    Vector3 p = fn + u * (i * 2f / n - 1f) + v * (j * 2f / n - 1f); // on the unit cube
                    Vector3 onBox = Vector3.Scale(p, half);
                    Vector3 core = new Vector3(
                        Mathf.Clamp(onBox.x, -inner.x, inner.x),
                        Mathf.Clamp(onBox.y, -inner.y, inner.y),
                        Mathf.Clamp(onBox.z, -inner.z, inner.z));
                    Vector3 dir = onBox - core;
                    Vector3 normal = dir.sqrMagnitude > 1e-8f ? dir.normalized : fn;
                    vertices.Add(core + normal * r);
                    normals.Add(normal);
                }

                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = start + j * (n + 1) + i;
                    int b = a + 1;
                    int c = a + n + 1;
                    int d = c + 1;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            var mesh = new Mesh { name = $"RoundedBox {size} r{r}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>One coloured piece of placeholder geometry: a rounded box tinted through a property block.</summary>
    public static class BoardPiece
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static MaterialPropertyBlock _block;

        public static MeshRenderer Create(string name, Transform parent, Material material, Vector3 size, float radius, Vector3 localPosition, Color color)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = RoundedBoxMesh.Get(size, radius);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetColor(renderer, color);
            return renderer;
        }

        public static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            _block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColor, color);
            renderer.SetPropertyBlock(_block);
        }
    }
}
