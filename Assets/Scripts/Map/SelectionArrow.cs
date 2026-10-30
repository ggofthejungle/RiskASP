using System.Collections.Generic;
using Map;
using player;
using TurnPhases;
using TurnPhases.Selection;
using UnityEngine;

namespace Map
{
    // Curved arrow on the map showing what you're about to do:
    //  - Attack: after clicking your territory, the arrow follows the mouse (faded) and snaps
    //    solid onto an enemy territory when you hover a valid target.
    //  - After a conquest (move troops) and in Fortify: from -> to.
    //  - While the defender's reaction window is open: the declared attack.
    //  - Any player's attack (incl. bots) flashes briefly so you can follow what happened.
    public class SelectionArrow : MonoBehaviour
    {
        private const float StartGap = 0.30f;   // keep the troop badges readable
        private const float EndGap = 0.32f;
        private const float HeadLength = 0.42f;
        private const float HeadWidth = 0.46f;
        private const float ShaftWidth = 0.13f;
        private const float Outline = 0.08f;
        private const float Bend = 0.18f;       // curve amount, relative to the arrow's length
        private const float FlashSeconds = 0.9f;
        private const int SortingOrder = 20;

        private GameManager _gm;
        private SelectionManager _sm;
        private Camera _cam;

        private LineRenderer _shaftOutline, _shaft, _headOutline, _head;
        private readonly List<Vector3> _points = new();
        private float _headLength = HeadLength;

        private Territory _flashFrom, _flashTo;
        private Color _flashColor;
        private float _flashUntil = -1f;

        // For tests.
        public bool IsVisible { get; private set; }
        public Territory ShownFrom { get; private set; }
        public Territory ShownTo { get; private set; } // null while following the mouse

        public static SelectionArrow Create(GameManager gm)
        {
            var go = new GameObject("SelectionArrow");
            var arrow = go.AddComponent<SelectionArrow>();
            arrow._gm = gm;
            return arrow;
        }

        private void Awake()
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            _shaftOutline = NewLine("ShaftOutline", mat, SortingOrder);
            _shaft = NewLine("Shaft", mat, SortingOrder + 1);
            _headOutline = NewLine("HeadOutline", mat, SortingOrder);
            _head = NewLine("Head", mat, SortingOrder + 1);
            _shaftOutline.numCapVertices = _shaft.numCapVertices = 4;
            SetVisible(false);
        }

        private void Start()
        {
            if (_gm == null) _gm = GameManager.Instance;
            _sm = SelectionManager.Instance;
            _cam = Camera.main;
            _gm.AttackPhase.OnAttacked += OnAttacked;
        }

        private void OnDestroy()
        {
            if (_gm != null && _gm.AttackPhase != null) _gm.AttackPhase.OnAttacked -= OnAttacked;
        }

        private void OnAttacked(AttackResult result)
        {
            _flashFrom = result.Origin;
            _flashTo = result.Target;
            _flashColor = ColorOf(result.Origin.Owner);
            _flashUntil = Time.unscaledTime + FlashSeconds;
        }

        private void LateUpdate()
        {
            if (_gm == null || _gm.GameState == GameState.Over || _gm.IsBidding || _gm.IsChoosingOrder)
            {
                Hide();
                return;
            }

            var owner = _gm.CurrentPlayer;
            var color = ColorOf(owner);

            // 1) Defender is deciding: show the declared attack.
            var pending = _gm.AttackPhase.PendingAttack;
            if (_gm.CurrentPhase is AttackPhase && pending != null)
            {
                Draw(pending.Origin, pending.Origin.transform.position, pending.Target, pending.Target.transform.position, color, 1f);
                return;
            }

            // 2) Human selection in progress.
            var (from, to) = CurrentSelection();
            if (from != null)
            {
                if (to != null)
                {
                    Draw(from, from.transform.position, to, to.transform.position, color, 1f);
                    return;
                }
                var hovered = HoveredTarget(from);
                if (hovered != null)
                {
                    Draw(from, from.transform.position, hovered, hovered.transform.position, color, 1f);
                    return;
                }
                if (_cam != null)
                {
                    var mouse = _cam.ScreenToWorldPoint(Input.mousePosition);
                    mouse.z = 0f;
                    Draw(from, from.transform.position, null, mouse, color, 0.55f);
                    return;
                }
            }

            // 3) Recent attack (any player, including bots).
            if (_flashFrom != null && Time.unscaledTime < _flashUntil)
            {
                var a = Mathf.Clamp01((_flashUntil - Time.unscaledTime) / 0.3f);
                Draw(_flashFrom, _flashFrom.transform.position, _flashTo, _flashTo.transform.position, _flashColor, a);
                return;
            }

            Hide();
        }

        private (Territory from, Territory to) CurrentSelection()
        {
            if (_sm == null) _sm = SelectionManager.Instance;
            if (_sm == null) return (null, null);
            return _sm.CurrentSelectionPhase switch
            {
                AttackSelectionPhase a => (a.From, a.To),
                FortifySelectionPhase f => (f.From, f.To),
                _ => (null, null)
            };
        }

        // A clickable territory under the mouse, other than the origin.
        private Territory HoveredTarget(Territory from)
        {
            foreach (var kv in _sm.TerritoryToSelectionMap)
            {
                var sel = kv.Value;
                if (sel != null && sel.IsHovered && !sel.IsDisabled && kv.Key != from)
                    return kv.Key;
            }
            return null;
        }

        // ---------------- Drawing ----------------

        private void Draw(Territory from, Vector3 start, Territory to, Vector3 end, Color color, float alpha)
        {
            start.z = end.z = 0f;
            var dir = end - start;
            var len = dir.magnitude;
            if (len < 0.05f)
            {
                Hide();
                return;
            }
            dir /= len;
            // Close neighbours: shrink the gaps and head so the arrow still shows (it used to vanish).
            var startGap = Mathf.Min(StartGap, len * 0.2f);
            var endGap = to != null ? Mathf.Min(EndGap, len * 0.2f) : 0f;
            start += dir * startGap;
            end -= dir * endGap;
            len = (end - start).magnitude;
            _headLength = Mathf.Min(HeadLength, len * 0.45f);

            // Quadratic Bezier, bent to the left of the direction of travel.
            var perp = new Vector3(-dir.y, dir.x, 0f);
            var ctrl = (start + end) * 0.5f + perp * (len * Bend);
            _points.Clear();
            const int n = 32;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1 - t;
                _points.Add(u * u * start + 2 * u * t * ctrl + t * t * end);
            }

            // Cut the shaft where the head starts.
            var total = 0f;
            for (int i = 1; i < _points.Count; i++) total += (_points[i] - _points[i - 1]).magnitude;
            var shaftLen = total - _headLength;
            var shaft = new List<Vector3> { _points[0] };
            var acc = 0f;
            Vector3 headBase = end;
            for (int i = 1; i < _points.Count; i++)
            {
                var seg = (_points[i] - _points[i - 1]).magnitude;
                if (acc + seg >= shaftLen)
                {
                    headBase = Vector3.Lerp(_points[i - 1], _points[i], (shaftLen - acc) / Mathf.Max(seg, 1e-5f));
                    shaft.Add(headBase);
                    break;
                }
                acc += seg;
                shaft.Add(_points[i]);
            }
            var tip = end;
            var tangent = (tip - headBase).normalized;

            var main = new Color(color.r, color.g, color.b, alpha);
            var dark = new Color(0.05f, 0.05f, 0.08f, 0.65f * alpha);

            SetLine(_shaftOutline, shaft, ShaftWidth + Outline, ShaftWidth + Outline, dark);
            SetLine(_shaft, shaft, ShaftWidth, ShaftWidth, main);
            SetLine(_headOutline, new List<Vector3> { headBase - tangent * 0.06f, tip + tangent * 0.09f },
                HeadWidth + Outline * 1.6f, 0f, dark);
            SetLine(_head, new List<Vector3> { headBase, tip }, HeadWidth, 0f, main);

            ShownFrom = from;
            ShownTo = to;
            SetVisible(true);
        }

        private static void SetLine(LineRenderer lr, List<Vector3> pts, float w0, float w1, Color c)
        {
            lr.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) lr.SetPosition(i, pts[i]);
            lr.startWidth = w0;
            lr.endWidth = w1;
            lr.startColor = lr.endColor = c;
        }

        private void Hide()
        {
            if (!IsVisible) return;
            ShownFrom = ShownTo = null;
            SetVisible(false);
        }

        private void SetVisible(bool v)
        {
            IsVisible = v;
            _shaftOutline.enabled = _shaft.enabled = _headOutline.enabled = _head.enabled = v;
        }

        private LineRenderer NewLine(string name, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.sortingOrder = order;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 0;
            return lr;
        }

        private static Color ColorOf(Player p) =>
            p != null && p.Color != null ? p.Color.Normal : new Color(0.9f, 0.2f, 0.2f);
    }
}
