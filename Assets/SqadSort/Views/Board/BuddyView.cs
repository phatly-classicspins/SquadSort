using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Views
{
    /// <summary>
    /// One buddy on the 3D board: a rounded body that stands on the board plane (local +Y is up, the face
    /// looks toward local −Z, the camera side), two eyes (open or shut) or a "?" mark, and a flat
    /// screen-facing tap target. Relays a tap as its own id. Every method is order-free and needs no game
    /// state (the two View tests).
    ///
    /// The geometry is placeholder (built in code from <see cref="Shape"/>); the art pass replaces it.
    /// </summary>
    public sealed class BuddyView : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Sizes of the placeholder body, set by the board from its prefab values.</summary>
        [Serializable]
        public struct Shape
        {
            public Vector3 Body;
            public float BodyRadius;
            public float EyeSize;
            public float EyeGap;
            public float EyeHeight;
            public float MarkSize;
            [Tooltip("Arm nub size, and how far it tucks into the body side.")]
            public Vector3 Arm;
            public float ArmTuck;
            [Tooltip("Foot size, and centre-to-centre gap between the two feet. The body stands on them.")]
            public Vector3 Foot;
            public float FootGap;
            [Tooltip("Mouth bar: width and height.")]
            public Vector2 Mouth;
            public Color EyeWhite;
            public Color Pupil;
            public Color Lid;
            public Color Mark;
        }

        private Transform _lift;
        private Transform _visual;
        private readonly System.Collections.Generic.List<MeshRenderer> _skin = new System.Collections.Generic.List<MeshRenderer>();
        private GameObject _eyesOpen;
        private GameObject _eyesClosed;
        private TextMeshPro _mark;
        private Transform _hit;
        private GameObject _model;
        private GameObject _hiddenModel;
        private GameObject _modelClosedEyes;
        private Renderer[] _modelEyes;

        /// <summary>Use authored meshes while retaining the existing motion and EventSystem target.</summary>
        public void SetModel(GameObject prefab, GameObject hiddenPrefab, float scale)
        {
            if (prefab == null) return;
            if (_model != null) Destroy(_model);
            if (_hiddenModel != null) Destroy(_hiddenModel);
            if (_modelClosedEyes != null) Destroy(_modelClosedEyes);
            foreach (Transform child in _visual)
                if (child != _eyesClosed.transform) child.gameObject.SetActive(false);
            _model = Instantiate(prefab, _visual, false);
            _model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _model.transform.localScale = Vector3.one * scale;
            _modelEyes = System.Array.FindAll(_model.GetComponentsInChildren<Renderer>(),
                r => r.name == "Eye_L" || r.name == "Eye_R");
            if (hiddenPrefab != null)
            {
                _hiddenModel = Instantiate(hiddenPrefab, _visual, false);
                _hiddenModel.transform.localRotation = _model.transform.localRotation;
                _hiddenModel.transform.localScale = _model.transform.localScale;
                _modelClosedEyes = Instantiate(_hiddenModel, _visual, false);
                foreach (Renderer r in _modelClosedEyes.GetComponentsInChildren<Renderer>())
                    r.enabled = r.name == "Eye_L" || r.name == "Eye_R";
            }
            foreach (Transform t in _visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
            ApplyFace();
        }

        private MotionHandle _move;
        private MotionHandle _hop;
        private MotionHandle _fx;
        private MotionHandle _tint;
        private MotionHandle _select;
        private bool _selected;
        private Color _color;
        private bool _hidden;
        private bool _awake = true;

        public int Id { get; private set; }

        /// <summary>Raised with <see cref="Id"/> when the player taps this buddy.</summary>
        public event Action<int> Tapped;

        /// <summary>Build the placeholder geometry. Called once, right after the board spawns the buddy.</summary>
        public void Build(Shape shape, Material material)
        {
            // Lift carries the picked-up offset, so it never fights the walk/hop motion on Visual.
            _lift = new GameObject("Lift").transform;
            _lift.gameObject.layer = gameObject.layer;
            _lift.SetParent(transform, false);
            _visual = new GameObject("Visual").transform;
            _visual.gameObject.layer = gameObject.layer;
            _visual.SetParent(_lift, false);

            float h = shape.Body.y;
            float lift = shape.Foot.y * 0.55f; // the body sits on its feet
            float footR = Mathf.Min(shape.Foot.x, shape.Foot.y) * 0.5f;
            for (int side = -1; side <= 1; side += 2)
                _skin.Add(BoardPiece.Create(side < 0 ? "Foot L" : "Foot R", _visual, material, shape.Foot, footR,
                    new Vector3(side * shape.FootGap * 0.5f, shape.Foot.y * 0.5f, -shape.Foot.z * 0.15f), Color.white));
            _skin.Add(BoardPiece.Create("Body", _visual, material, shape.Body, shape.BodyRadius, new Vector3(0f, lift + h * 0.5f, 0f), Color.white));
            float armR = Mathf.Min(shape.Arm.x, shape.Arm.z) * 0.5f;
            for (int side = -1; side <= 1; side += 2)
                _skin.Add(BoardPiece.Create(side < 0 ? "Arm L" : "Arm R", _visual, material, shape.Arm, armR,
                    new Vector3(side * (shape.Body.x * 0.5f + shape.Arm.x * 0.5f - shape.ArmTuck), lift + h * 0.38f, 0f), Color.white));

            float faceZ = -shape.Body.z * 0.5f;
            float eyeY = lift + h * shape.EyeHeight;
            _eyesOpen = new GameObject("Eyes Open");
            _eyesOpen.layer = gameObject.layer;
            _eyesOpen.transform.SetParent(_visual, false);
            _eyesClosed = new GameObject("Eyes Closed");
            _eyesClosed.layer = gameObject.layer;
            _eyesClosed.transform.SetParent(_visual, false);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * shape.EyeGap * 0.5f;
                float e = shape.EyeSize;
                BoardPiece.Create("White", _eyesOpen.transform, material, new Vector3(e, e * 1.2f, e * 0.5f), e * 0.45f, new Vector3(x, eyeY, faceZ), shape.EyeWhite);
                BoardPiece.Create("Pupil", _eyesOpen.transform, material, new Vector3(e * 0.5f, e * 0.6f, e * 0.3f), e * 0.22f, new Vector3(x, eyeY - e * 0.1f, faceZ - e * 0.25f), shape.Pupil);
                BoardPiece.Create("Lid", _eyesClosed.transform, material, new Vector3(e, e * 0.22f, e * 0.3f), e * 0.1f, new Vector3(x, eyeY - e * 0.1f, faceZ), shape.Lid);
            }

            float mouthY = eyeY - shape.EyeSize * 0.95f;
            BoardPiece.Create("Mouth", _visual, material, new Vector3(shape.Mouth.x, shape.Mouth.y, shape.Mouth.y), shape.Mouth.y * 0.5f,
                new Vector3(0f, mouthY, faceZ - 0.5f), shape.Pupil);

            var markGo = new GameObject("Mark");
            markGo.layer = gameObject.layer;
            markGo.transform.SetParent(_visual, false);
            // A hidden buddy sleeps with a "?" on its forehead, above the shut eyes.
            markGo.transform.localPosition = new Vector3(0f, eyeY + shape.EyeSize * 1.25f, faceZ - 1f);
            _mark = markGo.AddComponent<TextMeshPro>();
            _mark.isOrthographic = true; // a Renderer-drawn TMP must be orthographic on this rig (pf-world-space)
            _mark.text = "?";
            _mark.fontSize = shape.MarkSize;
            _mark.color = shape.Mark;
            _mark.alignment = TextAlignmentOptions.Center;
            _mark.rectTransform.sizeDelta = new Vector2(shape.Body.x, shape.Body.y);

            // A flat tap target kept square to the camera: the GamePlay layer raycasts in 2D, and the
            // camera looks straight down world Z, so a world-aligned 2D box is exactly the on-screen body.
            _hit = new GameObject("Hit").transform;
            _hit.gameObject.layer = gameObject.layer;
            _hit.SetParent(transform, false);
            float total = lift + h;
            _hit.localPosition = new Vector3(0f, total * 0.5f, 0f);
            var box = _hit.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(shape.Body.x, total * 1.1f);
            KeepHitSquare();
            ApplyFace();
        }

        public void Bind(int id) => Id = id;

        public void SetTint(Color color, bool animate)
        {
            _tint.TryCancel();
            if (animate && isActiveAndEnabled)
            {
                Color from = _color;
                _tint = LMotion.Create(0f, 1f, MotionTokens.Reveal).Bind(t => Paint(Color.Lerp(from, color, t)));
            }
            else
                Paint(color);

            _color = color;
        }

        public void SetHidden(bool hidden)
        {
            _hidden = hidden;
            ApplyFace();
        }

        public void SetAwake(bool awake)
        {
            _awake = awake;
            ApplyFace();
        }

        /// <summary>Picked up: rise and hold a little above the floor. Set down: drop back.</summary>
        public void SetSelected(bool selected)
        {
            if (_selected == selected) return;
            _selected = selected;
            _select.TryCancel();
            Transform lift = _lift;
            float from = lift.localPosition.y;
            float to = selected ? MotionTokens.SelectLift : 0f;
            _select = LMotion.Create(from, to, MotionTokens.SelectDuration)
                .WithEase(selected ? Ease.OutBack : Ease.InQuad)
                .Bind(y => lift.localPosition = new Vector3(0f, y, 0f));
        }

        public void PlaceAt(Vector3 localPosition)
        {
            _move.TryCancel();
            _hop.TryCancel();
            transform.localPosition = localPosition;
            _visual.localPosition = Vector3.zero;
        }

        /// <summary>Walk the supplied grid waypoints without smoothing across corners. Returns the walk duration.</summary>
        public float MoveAlong(System.Collections.Generic.IReadOnlyList<Vector3> path, float delay, Action arrived)
        {
            _move.TryCancel();
            _hop.TryCancel();
            if (path == null || path.Count == 0) throw new ArgumentException("A walk needs grid waypoints.", nameof(path));
            if (path.Count == 1)
            {
                PlaceAt(path[0]);
                arrived?.Invoke();
                return 0f;
            }
            int segments = path.Count - 1;
            float duration = segments * MotionTokens.GridStepDuration;
            Transform self = transform;
            Transform visual = _visual;
            _move = LMotion.Create(0f, (float)segments, duration)
                .WithDelay(delay)
                .WithEase(Ease.Linear)
                .WithOnComplete(() =>
                {
                    if (self == null) return;
                    self.localPosition = path[segments];
                    visual.localPosition = Vector3.zero;
                    arrived?.Invoke();
                })
                .Bind(progress =>
                {
                    int segment = Mathf.Min(segments - 1, Mathf.FloorToInt(progress));
                    float t = progress - segment;
                    self.localPosition = Vector3.LerpUnclamped(path[segment], path[segment + 1], t);
                    visual.localPosition = new Vector3(0f, Mathf.Sin(t * Mathf.PI) * MotionTokens.HopHeight, 0f);
                });
            return duration;
        }

        /// <summary>Hop in celebration, then shrink away and destroy this object.</summary>
        public void CelebrateAndRemove(float delay)
        {
            _fx.TryCancel();
            Transform visual = _visual;
            float hop = MotionTokens.CelebrateHop;
            float pop = MotionTokens.CelebratePop;
            _fx = LMotion.Create(0f, 1f, hop + pop)
                .WithDelay(delay)
                .WithOnComplete(() =>
                {
                    if (this != null) Destroy(gameObject);
                })
                .Bind(t =>
                {
                    float elapsed = t * (hop + pop);
                    if (elapsed <= hop)
                    {
                        float h = Mathf.Sin(elapsed / hop * Mathf.PI) * MotionTokens.CelebrateHopHeight;
                        visual.localPosition = new Vector3(0f, h, 0f);
                        visual.localScale = Vector3.one;
                    }
                    else
                    {
                        float k = 1f - (elapsed - hop) / pop;
                        visual.localPosition = Vector3.zero;
                        visual.localScale = new Vector3(k, k, k);
                    }
                });
        }

        /// <summary>Shake side to side — the "no room to go" reaction.</summary>
        public void Wince()
        {
            _fx.TryCancel();
            Transform visual = _visual;
            _fx = LMotion.Create(0f, 1f, MotionTokens.WinceDuration)
                .WithOnComplete(() =>
                {
                    if (visual != null) visual.localPosition = Vector3.zero;
                })
                .Bind(t => visual.localPosition = new Vector3(Mathf.Sin(t * Mathf.PI * 4f) * (1f - t) * MotionTokens.WinceAmplitude, 0f, 0f));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Squash();
            Tapped?.Invoke(Id);
        }

        private void LateUpdate() => KeepHitSquare();

        private void KeepHitSquare()
        {
            if (_hit != null) _hit.rotation = Quaternion.identity;
        }

        private void Squash()
        {
            if (_fx.IsActive()) return;
            Transform visual = _visual;
            _fx = LMotion.Create(0f, 1f, MotionTokens.TapSquashDuration)
                .WithOnComplete(() =>
                {
                    if (visual != null) visual.localScale = Vector3.one;
                })
                .Bind(t =>
                {
                    float s = Mathf.Sin(t * Mathf.PI) * MotionTokens.TapSquash;
                    visual.localScale = new Vector3(1f + s, 1f - s, 1f + s);
                });
        }

        private void ApplyFace()
        {
            if (_model != null)
            {
                _model.SetActive(!_hidden || _hiddenModel == null);
                if (_hiddenModel != null) _hiddenModel.SetActive(_hidden);
                foreach (Renderer eye in _modelEyes) eye.enabled = _awake;
                _eyesClosed.SetActive(false);
                if (_modelClosedEyes != null) _modelClosedEyes.SetActive(!_hidden && !_awake);
                return;
            }
            if (_mark == null) return;
            _mark.enabled = _hidden;
            _eyesOpen.SetActive(!_hidden && _awake);
            _eyesClosed.SetActive(_hidden || !_awake);
        }

        private void Paint(Color color)
        {
            foreach (MeshRenderer r in _skin) BoardPiece.SetColor(r, color);
        }

        private void OnDestroy()
        {
            _move.TryCancel();
            _hop.TryCancel();
            _fx.TryCancel();
            _tint.TryCancel();
            _select.TryCancel();
        }
    }
}
