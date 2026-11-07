using System.Collections.Generic;
using System.Linq;
using Expansions;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Map
{
    // Icons on the map, next to each territory's troop counter:
    //   Diplomat = laurel, Land = chevron shield, Nuclear = atom, Station = orbital hub.
    // Hovering a territory shows a label spelling it out (and that they roll 8-sided dice).
    public class MapBadges : MonoBehaviour
    {
        private const float BadgeSize = 0.40f;
        private const float Spacing = 0.44f;
        private const float AboveCounter = 0.42f;
        private const int SortingOrder = 30;
        private const int IconPx = 64;

        private static Sprite _circle, _square;
        private static readonly Dictionary<CommanderType, Sprite> _commanderIcons = new();
        private static Sprite _stationIcon;
        private readonly Dictionary<Territory, GameObject> _rows = new();
        private Canvas _canvas;
        private Camera _cam;
        private RectTransform _tooltipPanel;
        private RectTransform _tooltipRows;
        private TMP_Text _tooltipHeader;
        private TMP_Text _tooltipNote;
        private string _tooltipPlain = "";
        private Territory _hovered;

        public static MapBadges Create(GameManager gm, Canvas canvas = null)
        {
            var go = new GameObject("MapBadges");
            var badges = go.AddComponent<MapBadges>();
            badges._canvas = canvas != null ? canvas.rootCanvas : null;
            return badges;
        }

        private void Start()
        {
            if (_canvas == null && GameManager.Instance?.UIGameInfo != null)
            {
                var c = GameManager.Instance.UIGameInfo.GetComponentInParent<Canvas>();
                if (c != null) _canvas = c.rootCanvas;
            }
            _cam = Camera.main;
            foreach (var t in TerritoryRepository.Instance.Territories)
            {
                var territory = t;
                territory.OnCommandersChanged += (_, _) => Rebuild(territory);
                territory.OnSpaceStationChanged += _ => Rebuild(territory);
                territory.OnOwnerChanged += (_, _) => Rebuild(territory);
                Rebuild(territory);
            }
            BuildTooltip();
        }

        // Counts shown on a territory (for tests / other UI).
        public int BadgeCount(Territory t) => _rows.TryGetValue(t, out var row) && row != null ? row.transform.childCount : 0;

        public static string Letter(CommanderType t) => t switch
        {
            CommanderType.Diplomat => "D",
            CommanderType.LandCommander => "L",
            CommanderType.NuclearCommander => "N",
            CommanderType.NavalCommander => "W",
            _ => "S",
        };

        public static string FullName(CommanderType t) => t switch
        {
            CommanderType.Diplomat => "Diplomat",
            CommanderType.LandCommander => "Land Commander",
            CommanderType.NuclearCommander => "Nuclear Commander",
            CommanderType.NavalCommander => "Naval Commander",
            _ => "Space Commander",
        };

        public static Color TypeColor(CommanderType t) => t switch
        {
            CommanderType.Diplomat => new Color(0.05f, 0.45f, 0.35f),
            CommanderType.LandCommander => new Color(0.55f, 0.40f, 0.15f),
            CommanderType.NuclearCommander => new Color(0.92f, 0.45f, 0.05f),
            _ => new Color(0.3f, 0.3f, 0.35f),
        };

        public static Sprite IconFor(CommanderType t)
        {
            if (_commanderIcons.TryGetValue(t, out var s) && s != null) return s;
            s = Sprite.Create(DrawCommanderIcon(t), new Rect(0, 0, IconPx, IconPx), new Vector2(0.5f, 0.5f), IconPx);
            _commanderIcons[t] = s;
            return s;
        }

        public static Sprite StationIcon()
        {
            if (_stationIcon != null) return _stationIcon;
            return _stationIcon = Sprite.Create(DrawStationIcon(), new Rect(0, 0, IconPx, IconPx), new Vector2(0.5f, 0.5f), IconPx);
        }

        private void Rebuild(Territory t)
        {
            if (_rows.TryGetValue(t, out var old) && old != null) Destroy(old);
            var commanders = t.CommandersList ?? new List<Commander>();
            var count = commanders.Count + (t.containsSpaceStation ? 1 : 0);
            if (count == 0) { _rows[t] = null; return; }

            var row = new GameObject($"Badges {t.Name}");
            row.transform.SetParent(transform, false);
            row.transform.position = AnchorOf(t);
            _rows[t] = row;

            var x = -(count - 1) * Spacing / 2f;
            var ring = t.Owner != null && t.Owner.Color != null ? t.Owner.Color.Normal : Color.white;
            foreach (var c in commanders)
            {
                Badge(row.transform, new Vector3(x, 0, 0), Circle(), ring, TypeColor(c.CommanderType), IconFor(c.CommanderType), 0f);
                x += Spacing;
            }
            if (t.containsSpaceStation)
                Badge(row.transform, new Vector3(x, 0, 0), Square(), ring, new Color(0.10f, 0.12f, 0.22f), StationIcon(), 0f);
        }

        private static Vector3 AnchorOf(Territory t)
        {
            // The troop counter ("background" child) if there is one, else the territory centre.
            var counter = t.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => r.name == "background");
            var p = counter != null ? counter.transform.position : t.transform.position;
            return new Vector3(p.x, p.y + AboveCounter, 0f);
        }

        private static void Badge(Transform parent, Vector3 offset, Sprite shape, Color ring, Color fill, Sprite icon, float rotation)
        {
            var outer = new GameObject("Badge").AddComponent<SpriteRenderer>();
            outer.transform.SetParent(parent, false);
            outer.transform.localPosition = offset;
            outer.transform.localRotation = Quaternion.Euler(0, 0, rotation);
            outer.transform.localScale = Vector3.one * BadgeSize;
            outer.sprite = shape;
            outer.color = ring;
            outer.sortingOrder = SortingOrder;

            var inner = new GameObject("Fill").AddComponent<SpriteRenderer>();
            inner.transform.SetParent(outer.transform, false);
            inner.transform.localScale = Vector3.one * 0.78f;
            inner.sprite = shape;
            inner.color = fill;
            inner.sortingOrder = SortingOrder + 1;

            var glyph = new GameObject("Icon").AddComponent<SpriteRenderer>();
            glyph.transform.SetParent(outer.transform, false);
            glyph.transform.localScale = Vector3.one * 0.72f;
            glyph.sprite = icon;
            glyph.color = Color.white;
            glyph.sortingOrder = SortingOrder + 2;
        }

        // ---------- Hover label (Canvas UI so it draws above the dice panel) ----------

        private void BuildTooltip()
        {
            if (_canvas == null) return;
            var panel = TurnHud.NewImage("MapBadgeTooltip", _canvas.transform, TurnHud.PopupBorder);
            _tooltipPanel = panel.rectTransform;
            _tooltipPanel.anchorMin = _tooltipPanel.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltipPanel.pivot = new Vector2(0.5f, 0f);
            _tooltipPanel.sizeDelta = new Vector2(300, 120);
            panel.raycastTarget = false;
            var fill = TurnHud.FramePopup(panel, new Color(0.08f, 0.08f, 0.12f, 0.94f));
            fill.raycastTarget = false;

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(_tooltipPanel, false);
            TurnHud.Place(content, Vector2.zero, Vector2.one, new Vector2(12, 10), new Vector2(-12, -10));
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _tooltipHeader = TurnHud.NewText("Header", content, 20, FontStyles.Bold, TextAlignmentOptions.Left);
            _tooltipHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;

            _tooltipRows = new GameObject("Legend", typeof(RectTransform)).GetComponent<RectTransform>();
            _tooltipRows.SetParent(content, false);
            var rowsLayout = _tooltipRows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 4;
            rowsLayout.childAlignment = TextAnchor.UpperLeft;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;
            _tooltipRows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _tooltipNote = TurnHud.NewText("Note", content, 15, FontStyles.Normal, TextAlignmentOptions.Left);
            _tooltipNote.color = new Color(0.80f, 0.82f, 0.88f, 1f);
            _tooltipNote.textWrappingMode = TextWrappingModes.Normal;

            _tooltipPanel.SetAsLastSibling();
            SetTooltip(null);
        }

        private void Update()
        {
            var sm = SelectionManager.Instance;
            if (sm == null || _tooltipPanel == null) return;
            Territory hovered = null;
            foreach (var kv in sm.TerritoryToSelectionMap)
                if (kv.Value != null && kv.Value.IsHovered) { hovered = kv.Key; break; }
            if (hovered != _hovered) SetTooltip(hovered);
            else if (_hovered != null) PositionTooltip(_hovered);
        }

        public string TooltipText =>
            _tooltipPanel != null && _tooltipPanel.gameObject.activeSelf ? _tooltipPlain : "";

        private void SetTooltip(Territory t)
        {
            _hovered = t;
            var hasStuff = t != null && (t.CommandersList.Count > 0 || t.containsSpaceStation);
            if (_tooltipPanel != null) _tooltipPanel.gameObject.SetActive(hasStuff);
            if (!hasStuff) { _tooltipPlain = ""; return; }

            foreach (Transform child in _tooltipRows) Destroy(child.gameObject);

            var plain = new List<string> { t.Owner?.Name ?? "" };
            _tooltipHeader.text = t.Owner?.Name ?? "";
            _tooltipHeader.color = t.Owner?.Color != null ? t.Owner.Color.Normal : Color.white;

            foreach (var c in t.CommandersList)
            {
                var type = c.CommanderType;
                var die = BattleSimulator.AttacksWithD8(type) ? " · attacks & defends d8" : " · defends d8, attacks d6";
                AddLegendRow(IconFor(type), TypeColor(type), FullName(type) + die, Circle());
                plain.Add(FullName(type));
            }
            if (t.containsSpaceStation)
            {
                AddLegendRow(StationIcon(), new Color(0.10f, 0.12f, 0.22f),
                    "Space Station · +1 MOD/turn, all defenders d8", Square());
                plain.Add("Space Station: all defenders roll 8-sided dice");
            }

            var attackers = t.CommandersList.Count(c => BattleSimulator.AttacksWithD8(c.CommanderType));
            _tooltipNote.text = t.CommandersList.Count > 0
                ? $"Commanders defend with d8{(attackers > 0 ? $"; {attackers} attack with d8" : "")}."
                : "";
            _tooltipNote.gameObject.SetActive(!string.IsNullOrEmpty(_tooltipNote.text));
            _tooltipPlain = string.Join("\n", plain);

            Canvas.ForceUpdateCanvases();
            var rows = t.CommandersList.Count + (t.containsSpaceStation ? 1 : 0);
            var h = 36 + rows * 36 + (string.IsNullOrEmpty(_tooltipNote.text) ? 0 : 28);
            _tooltipPanel.sizeDelta = new Vector2(320, Mathf.Clamp(h, 72, 220));
            _tooltipPanel.SetAsLastSibling();
            PositionTooltip(t);
        }

        private void AddLegendRow(Sprite icon, Color badgeFill, string label, Sprite badgeShape)
        {
            var row = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(_tooltipRows, false);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = false;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

            var badge = TurnHud.NewImage("Badge", row, Color.white);
            badge.sprite = badgeShape;
            badge.color = badgeFill;
            badge.preserveAspect = true;
            badge.raycastTarget = false;
            var badgeLe = badge.gameObject.AddComponent<LayoutElement>();
            badgeLe.preferredWidth = badgeLe.preferredHeight = 28;

            var glyph = TurnHud.NewImage("Icon", badge.rectTransform, Color.white);
            glyph.sprite = icon;
            glyph.preserveAspect = true;
            glyph.raycastTarget = false;
            TurnHud.Place(glyph.rectTransform, new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f), Vector2.zero, Vector2.zero);

            var text = TurnHud.NewText("Label", row, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            text.text = label;
            text.textWrappingMode = TextWrappingModes.Normal;
            var textLe = text.gameObject.AddComponent<LayoutElement>();
            textLe.flexibleWidth = 1;
            textLe.minWidth = 200;
        }

        private void PositionTooltip(Territory t)
        {
            if (_tooltipPanel == null || _canvas == null || t == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            var world = AnchorOf(t) + new Vector3(0, 0.85f, 0);
            var screen = _cam.WorldToScreenPoint(world);
            if (screen.z < 0) { _tooltipPanel.gameObject.SetActive(false); return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_canvas.transform, screen, _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _cam,
                out var local);
            // Nudge left if near the dice panel (top-centre / right).
            var half = _tooltipPanel.sizeDelta * 0.5f;
            var canvasRt = (RectTransform)_canvas.transform;
            var bounds = canvasRt.rect;
            local.x = Mathf.Clamp(local.x, bounds.xMin + half.x + 8, bounds.xMax - half.x - 8);
            local.y = Mathf.Clamp(local.y, bounds.yMin + 8, bounds.yMax - _tooltipPanel.sizeDelta.y - 8);
            _tooltipPanel.anchoredPosition = local;
        }

        // ---------- Generated sprites ----------

        private static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var r = n / 2f - 1;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var d = Mathf.Sqrt((x - n / 2f + 0.5f) * (x - n / 2f + 0.5f) + (y - n / 2f + 0.5f) * (y - n / 2f + 0.5f));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f)));
            }
            tex.Apply();
            return _circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        private static Sprite Square()
        {
            if (_square != null) return _square;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) tex.SetPixel(x, y, Color.white);
            tex.Apply();
            return _square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }

        private static Texture2D DrawCommanderIcon(CommanderType t) => t switch
        {
            CommanderType.Diplomat => DrawDiplomat(),
            CommanderType.LandCommander => DrawLand(),
            CommanderType.NuclearCommander => DrawNuclear(),
            _ => DrawDiplomat(),
        };

        // Laurel wreath around a small center — diplomacy / peace.
        private static Texture2D DrawDiplomat()
        {
            var tex = Blank();
            var c = IconPx / 2f;
            for (int i = 0; i < 9; i++)
            {
                var a = -70f + i * 17.5f;
                DrawLeaf(tex, c, c, a, 1f);
                DrawLeaf(tex, c, c, 180f - a, 1f);
            }
            FillCircle(tex, c, c, 3.2f, 1f);
            tex.Apply();
            return tex;
        }

        // Chevron shield — land / army.
        private static Texture2D DrawLand()
        {
            var tex = Blank();
            var c = IconPx / 2f;
            // Shield outline (filled)
            for (int y = 8; y <= 54; y++)
            for (int x = 16; x <= 48; x++)
            {
                var nx = (x - c) / 16f;
                var top = y >= 12 && y <= 28 && Mathf.Abs(nx) <= 1f;
                var mid = y > 28 && y <= 42 && Mathf.Abs(nx) <= 1f - (y - 28) / 28f;
                var tip = y > 42 && y <= 54 && Mathf.Abs(nx) <= 1f - (y - 28) / 22f;
                if (top || mid || tip) SoftSet(tex, x, y, 1f);
            }
            // Inner chevron
            for (int i = 0; i < 14; i++)
            {
                var y = 22 + i;
                var w = 10 - i * 0.45f;
                DrawLine(tex, c - w, y, c, y + 5, 2.2f);
                DrawLine(tex, c + w, y, c, y + 5, 2.2f);
            }
            tex.Apply();
            return tex;
        }

        // Atom — nucleus + three elliptical orbits.
        private static Texture2D DrawNuclear()
        {
            var tex = Blank();
            var c = IconPx / 2f;
            FillCircle(tex, c, c, 4.5f, 1f);
            DrawEllipse(tex, c, c, 22f, 9f, 0f, 1.8f);
            DrawEllipse(tex, c, c, 22f, 9f, 60f, 1.8f);
            DrawEllipse(tex, c, c, 22f, 9f, 120f, 1.8f);
            // Electron dots
            for (int i = 0; i < 3; i++)
            {
                var ang = (i * 60f + 20f) * Mathf.Deg2Rad;
                var rx = 22f * Mathf.Cos(ang);
                var ry = 9f * Mathf.Sin(ang);
                var rot = i * 60f * Mathf.Deg2Rad;
                var x = c + rx * Mathf.Cos(rot) - ry * Mathf.Sin(rot);
                var y = c + rx * Mathf.Sin(rot) + ry * Mathf.Cos(rot);
                FillCircle(tex, x, y, 2.4f, 1f);
            }
            tex.Apply();
            return tex;
        }

        // Orbital hub with three solar panels — space station.
        private static Texture2D DrawStationIcon()
        {
            var tex = Blank();
            var c = IconPx / 2f;
            DrawCircleOutline(tex, c, c, 7f, 2.2f);
            FillCircle(tex, c, c, 4.2f, 1f);
            for (int i = 0; i < 3; i++)
            {
                var ang = (i * 120f - 90f) * Mathf.Deg2Rad;
                var x1 = c + Mathf.Cos(ang) * 8f;
                var y1 = c + Mathf.Sin(ang) * 8f;
                var x2 = c + Mathf.Cos(ang) * 26f;
                var y2 = c + Mathf.Sin(ang) * 26f;
                DrawLine(tex, x1, y1, x2, y2, 2.4f);
                // Panel at the tip
                var px = Mathf.Cos(ang) * 26f;
                var py = Mathf.Sin(ang) * 26f;
                var tx = -Mathf.Sin(ang);
                var ty = Mathf.Cos(ang);
                for (float u = -6f; u <= 6f; u += 0.6f)
                for (float v = -3.5f; v <= 3.5f; v += 0.6f)
                    SoftSet(tex, c + px + tx * u + Mathf.Cos(ang) * v * 0.3f, c + py + ty * u + Mathf.Sin(ang) * v * 0.3f, 1f);
            }
            // Outer ring
            DrawCircleOutline(tex, c, c, 14f, 1.6f);
            tex.Apply();
            return tex;
        }

        private static Texture2D Blank()
        {
            var tex = new Texture2D(IconPx, IconPx, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var clear = new Color(1, 1, 1, 0);
            for (int y = 0; y < IconPx; y++)
            for (int x = 0; x < IconPx; x++)
                tex.SetPixel(x, y, clear);
            return tex;
        }

        private static void SoftSet(Texture2D tex, float fx, float fy, float a)
        {
            int x = Mathf.RoundToInt(fx), y = Mathf.RoundToInt(fy);
            if (x < 0 || y < 0 || x >= IconPx || y >= IconPx) return;
            var p = tex.GetPixel(x, y);
            tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Max(p.a, Mathf.Clamp01(a))));
        }

        private static void FillCircle(Texture2D tex, float cx, float cy, float r, float a)
        {
            var r2 = r * r;
            for (int y = Mathf.FloorToInt(cy - r - 1); y <= Mathf.CeilToInt(cy + r + 1); y++)
            for (int x = Mathf.FloorToInt(cx - r - 1); x <= Mathf.CeilToInt(cx + r + 1); x++)
            {
                if (x < 0 || y < 0 || x >= IconPx || y >= IconPx) continue;
                var d2 = (x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy);
                if (d2 <= r2) SoftSet(tex, x, y, a);
                else if (d2 <= (r + 0.7f) * (r + 0.7f)) SoftSet(tex, x, y, a * 0.4f);
            }
        }

        private static void DrawCircleOutline(Texture2D tex, float cx, float cy, float r, float thickness)
        {
            for (int i = 0; i < 72; i++)
            {
                var a = i * Mathf.PI * 2f / 72f;
                SoftSet(tex, cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, 1f);
                if (thickness > 1.5f)
                    SoftSet(tex, cx + Mathf.Cos(a) * (r - 1f), cy + Mathf.Sin(a) * (r - 1f), 0.85f);
            }
        }

        private static void DrawEllipse(Texture2D tex, float cx, float cy, float rx, float ry, float rotDeg, float thickness)
        {
            var rot = rotDeg * Mathf.Deg2Rad;
            for (int i = 0; i < 96; i++)
            {
                var a = i * Mathf.PI * 2f / 96f;
                var lx = Mathf.Cos(a) * rx;
                var ly = Mathf.Sin(a) * ry;
                var x = cx + lx * Mathf.Cos(rot) - ly * Mathf.Sin(rot);
                var y = cy + lx * Mathf.Sin(rot) + ly * Mathf.Cos(rot);
                SoftSet(tex, x, y, 1f);
                if (thickness > 1.2f)
                {
                    SoftSet(tex, x + 0.5f, y, 0.7f);
                    SoftSet(tex, x, y + 0.5f, 0.7f);
                }
            }
        }

        private static void DrawLine(Texture2D tex, float x0, float y0, float x1, float y1, float thickness)
        {
            var steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * 2f));
            for (int i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var x = Mathf.Lerp(x0, x1, t);
                var y = Mathf.Lerp(y0, y1, t);
                var half = thickness * 0.5f;
                for (float ox = -half; ox <= half; ox += 0.5f)
                for (float oy = -half; oy <= half; oy += 0.5f)
                    if (ox * ox + oy * oy <= half * half + 0.2f)
                        SoftSet(tex, x + ox, y + oy, 1f);
            }
        }

        private static void DrawLeaf(Texture2D tex, float cx, float cy, float angleDeg, float a)
        {
            var ang = angleDeg * Mathf.Deg2Rad;
            var dist = 18f;
            var tipX = cx + Mathf.Cos(ang) * dist;
            var tipY = cy + Mathf.Sin(ang) * dist;
            var baseX = cx + Mathf.Cos(ang) * 8f;
            var baseY = cy + Mathf.Sin(ang) * 8f;
            DrawLine(tex, baseX, baseY, tipX, tipY, 1.4f);
            var px = -Mathf.Sin(ang);
            var py = Mathf.Cos(ang);
            for (float t = 0.15f; t < 0.85f; t += 0.12f)
            {
                var x = Mathf.Lerp(baseX, tipX, t);
                var y = Mathf.Lerp(baseY, tipY, t);
                var w = Mathf.Sin(t * Mathf.PI) * 3.2f;
                SoftSet(tex, x + px * w, y + py * w, a);
                SoftSet(tex, x - px * w, y - py * w, a);
                SoftSet(tex, x + px * w * 0.5f, y + py * w * 0.5f, a);
                SoftSet(tex, x - px * w * 0.5f, y - py * w * 0.5f, a);
            }
        }
    }
}
