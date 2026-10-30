using System.Collections.Generic;
using System.Linq;
using Expansions;
using TMPro;
using UnityEngine;

namespace Map
{
    // Icons on the map, next to each territory's troop counter:
    //   round badge per commander: D = Diplomat, L = Land, N = Nuclear (ring in the owner's colour),
    //   dark diamond "S" = space station.
    // Hovering a territory shows a label spelling it out (and that they roll 8-sided dice).
    public class MapBadges : MonoBehaviour
    {
        private const float BadgeSize = 0.36f;
        private const float Spacing = 0.40f;
        private const float AboveCounter = 0.42f;
        private const int SortingOrder = 30;

        private static Sprite _circle, _square;
        private readonly Dictionary<Territory, GameObject> _rows = new();
        private TextMeshPro _tooltip;
        private SpriteRenderer _tooltipBg;
        private Territory _hovered;

        public static MapBadges Create(GameManager gm)
        {
            var go = new GameObject("MapBadges");
            return go.AddComponent<MapBadges>();
        }

        private void Start()
        {
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
                Badge(row.transform, new Vector3(x, 0, 0), Circle(), ring, TypeColor(c.CommanderType), Letter(c.CommanderType), 0f);
                x += Spacing;
            }
            if (t.containsSpaceStation)
                Badge(row.transform, new Vector3(x, 0, 0), Square(), ring, new Color(0.08f, 0.08f, 0.12f), "S", 45f);
        }

        private static Vector3 AnchorOf(Territory t)
        {
            // The troop counter ("background" child) if there is one, else the territory centre.
            var counter = t.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => r.name == "background");
            var p = counter != null ? counter.transform.position : t.transform.position;
            return new Vector3(p.x, p.y + AboveCounter, 0f);
        }

        private static void Badge(Transform parent, Vector3 offset, Sprite shape, Color ring, Color fill, string letter, float rotation)
        {
            var outer = new GameObject("Badge " + letter).AddComponent<SpriteRenderer>();
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

            var text = new GameObject("Letter").AddComponent<TextMeshPro>();
            text.transform.SetParent(parent, false);
            text.transform.localPosition = offset;
            text.text = letter;
            text.fontSize = 2.4f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(BadgeSize, BadgeSize);
            text.GetComponent<MeshRenderer>().sortingOrder = SortingOrder + 2;
        }

        // ---------- Hover label ----------

        private void BuildTooltip()
        {
            _tooltipBg = new GameObject("TooltipBg").AddComponent<SpriteRenderer>();
            _tooltipBg.transform.SetParent(transform, false);
            _tooltipBg.sprite = Square();
            _tooltipBg.color = new Color(0.08f, 0.08f, 0.12f, 0.88f);
            _tooltipBg.sortingOrder = SortingOrder + 5;
            _tooltip = new GameObject("Tooltip").AddComponent<TextMeshPro>();
            _tooltip.transform.SetParent(transform, false);
            _tooltip.fontSize = 2.6f;
            _tooltip.alignment = TextAlignmentOptions.Center;
            _tooltip.color = Color.white;
            _tooltip.rectTransform.sizeDelta = new Vector2(4.2f, 1.2f);
            _tooltip.GetComponent<MeshRenderer>().sortingOrder = SortingOrder + 6;
            SetTooltip(null);
        }

        private void Update()
        {
            var sm = SelectionManager.Instance;
            if (sm == null || _tooltip == null) return;
            Territory hovered = null;
            foreach (var kv in sm.TerritoryToSelectionMap)
                if (kv.Value != null && kv.Value.IsHovered) { hovered = kv.Key; break; }
            if (hovered != _hovered) SetTooltip(hovered);
        }

        public string TooltipText => _tooltip != null && _tooltip.gameObject.activeSelf ? _tooltip.text : "";

        private void SetTooltip(Territory t)
        {
            _hovered = t;
            var lines = new List<string>();
            if (t != null)
            {
                lines.AddRange(t.CommandersList.Select(c => FullName(c.CommanderType)));
                if (t.containsSpaceStation) lines.Add("Space Station: all defenders roll 8-sided dice");
            }
            var show = lines.Count > 0;
            _tooltip.gameObject.SetActive(show);
            _tooltipBg.gameObject.SetActive(show);
            if (!show) return;

            var attackers = t.CommandersList.Count(c => BattleSimulator.AttacksWithD8(c.CommanderType));
            var header = $"<b>{t.Owner?.Name}</b>";
            var note = t.CommandersList.Count > 0
                ? $"<size=80%>Commanders defend with d8{(attackers > 0 ? $"; {attackers} attack with d8" : "")}</size>"
                : "";
            _tooltip.text = string.Join("\n", new[] { header }.Concat(lines).Concat(note == "" ? new string[0] : new[] { note }));
            var pos = AnchorOf(t) + new Vector3(0, 1.0f, 0);
            _tooltip.transform.position = pos;
            _tooltip.ForceMeshUpdate();
            var size = _tooltip.textBounds.size;
            _tooltipBg.transform.position = pos + new Vector3(0, 0, 0.01f);
            _tooltipBg.transform.localScale = new Vector3(size.x + 0.3f, size.y + 0.2f, 1f);
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
    }
}
