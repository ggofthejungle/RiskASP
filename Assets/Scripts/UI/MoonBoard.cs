using System.Collections.Generic;
using System.Linq;
using Map;
using TMPro;
using TurnPhases;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // The moon is a second board, like the cardboard moon next to the earth map.
    // The picture in the Pacific opens and closes it. Attacks stay ordinary clicks:
    // a lit landing site is a legal target, a grey one is not.
    public class MoonBoard : MonoBehaviour
    {
        public static MoonBoard Instance { get; private set; }

        RectTransform _panel;
        GameObject _shadow;
        RectTransform _map;
        bool _open;
        bool _need;
        bool _heldShut;
        bool _launchWasLit;
        bool _doorWasLit;
        readonly List<Slot> _slots = new();
        readonly List<LinkView> _links = new();

        class Slot
        {
            public Territory Territory;
            public RectTransform Rect;
            public Image Frame;
            public Image Image;
            public TMP_Text Label;
            public Button Button;
        }

        class LinkView
        {
            public RectTransform A, B, Line;
        }

        // Three colonies, each in its own block, so the groups do not overlap.
        static readonly Dictionary<string, Vector2> Spot = new()
        {
            { "pale_crater", new Vector2(0.16f, 0.82f) },
            { "rain_plain", new Vector2(0.38f, 0.82f) },
            { "storm_ocean", new Vector2(0.16f, 0.64f) },
            { "cinder_reach", new Vector2(0.38f, 0.64f) },
            { "high_ridge", new Vector2(0.64f, 0.82f) },
            { "serenity_shelf", new Vector2(0.86f, 0.82f) },
            { "quiet_basin", new Vector2(0.64f, 0.64f) },
            { "nectar_gulf", new Vector2(0.86f, 0.64f) },
            { "dust_field", new Vector2(0.12f, 0.30f) },
            { "cloud_sea", new Vector2(0.30f, 0.16f) },
            { "impact_basin", new Vector2(0.48f, 0.34f) },
            { "long_wall", new Vector2(0.50f, 0.14f) },
            { "fever_marsh", new Vector2(0.70f, 0.16f) },
            { "ray_crater", new Vector2(0.88f, 0.30f) },
        };

        static readonly (string Name, string Title, int Bonus, Color Accent, Vector2 Min, Vector2 Max)[] Groups =
        {
            ("ash_belt", "Ash Belt", 2, new Color(0.78f, 0.70f, 0.58f), new Vector2(0.02f, 0.52f), new Vector2(0.48f, 0.98f)),
            ("bright_sea", "Bright Sea", 2, new Color(0.78f, 0.82f, 0.90f), new Vector2(0.52f, 0.52f), new Vector2(0.98f, 0.98f)),
            ("south_scar", "South Scar", 3, new Color(0.86f, 0.62f, 0.42f), new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.48f)),
        };

        public static void Create(Canvas canvas)
        {
            if (Instance != null || canvas == null) return;
            var go = new GameObject("MoonBoard", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Instance = go.AddComponent<MoonBoard>();
            Instance.Build();
        }

        public void Toggle()
        {
            SetOpen(!_open);
            if (!_open && _need) _heldShut = true;
        }

        void SetOpen(bool open)
        {
            _open = open;
            if (_panel != null) _panel.gameObject.SetActive(open);
            if (_shadow != null) _shadow.SetActive(open);
        }

        void LateUpdate()
        {
            var need = NeedsMoon();
            var launch = LaunchLit();
            var door = OwnsMoon();
            if (need && !_need && !_heldShut) SetOpen(true);
            // A land click, including a station in India, lights the picture. It does not cover the map.
            if (launch && !_launchWasLit)
                GameManager.Instance?.TurnHud?.Flash("A space station can reach the Moon. Click the moon picture.");
            else if (door && !_doorWasLit && !launch)
                GameManager.Instance?.TurnHud?.Flash("Click the moon picture to use your territories there.");
            if (!need) _heldShut = false;
            _need = need;
            _launchWasLit = launch;
            _doorWasLit = door || launch;
            Refresh();
            PlaceLines();
            var gate = Object.FindAnyObjectByType<MoonGate>();
            if (gate != null) gate.SetLit(launch || door);
        }

        // The board opens when the next click is on the moon itself: the selected origin is up there,
        // or a card is asking for a moon territory. A station on Earth only lights the picture.
        bool NeedsMoon()
        {
            var sel = SelectionManager.Instance;
            if (sel == null || _slots.Count == 0) return false;
            var from = (sel.CurrentSelectionPhase as TurnPhases.Selection.AttackSelectionPhase)?.From
                    ?? (sel.CurrentSelectionPhase as TurnPhases.Selection.FortifySelectionPhase)?.From;
            if (from != null && from.Kind == TerrainKind.Moon) return true;
            return sel.IsTargeting && _slots.Any(s => Selectable(s.Territory));
        }

        bool OwnsMoon()
        {
            var gm = GameManager.Instance;
            var phase = gm?.CurrentPhase;
            if (gm?.CurrentPlayer == null) return false;
            if (phase is not ReinforcePhase && phase is not FortifyPhase && phase is not AttackPhase) return false;
            return gm.CurrentPlayer.Territories.Any(t => t.Kind == TerrainKind.Moon);
        }

        bool LaunchLit() => _slots.Any(s => s.Territory.LaunchPoint && Selectable(s.Territory));

        static bool Selectable(Territory t)
        {
            var sel = t.GetComponent<TerritorySelection>();
            return sel != null && !sel.IsDisabled;
        }

        void Build()
        {
            var shadow = NewImage("Shadow", transform, new Color(0f, 0f, 0f, 0.35f));
            shadow.raycastTarget = false;
            _shadow = shadow.gameObject;
            _shadow.SetActive(false);
            var srt = shadow.rectTransform;
            srt.anchorMin = new Vector2(0.524f, 0.112f);
            srt.anchorMax = new Vector2(0.989f, 0.872f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            var panel = NewImage("Panel", transform, new Color(0.11f, 0.12f, 0.15f, 0.98f));
            _panel = panel.rectTransform;
            _panel.anchorMin = new Vector2(0.52f, 0.12f);
            _panel.anchorMax = new Vector2(0.985f, 0.88f);
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;
            _panel.gameObject.SetActive(false);

            var header = NewText("Title", _panel, 20, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            header.text = "MOON";
            header.characterSpacing = 1.5f;
            header.rectTransform.anchorMin = new Vector2(0, 1);
            header.rectTransform.anchorMax = new Vector2(1, 1);
            header.rectTransform.pivot = new Vector2(0f, 1f);
            header.rectTransform.sizeDelta = new Vector2(160, 36);
            header.rectTransform.anchoredPosition = new Vector2(16, -6);

            var rule = NewText("Rule", _panel, 13, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            rule.color = new Color(0.72f, 0.74f, 0.80f);
            rule.text = "Hold every territory in a group.";
            rule.rectTransform.anchorMin = new Vector2(0, 1);
            rule.rectTransform.anchorMax = new Vector2(1, 1);
            rule.rectTransform.pivot = new Vector2(0f, 1f);
            rule.rectTransform.sizeDelta = new Vector2(280, 20);
            rule.rectTransform.anchoredPosition = new Vector2(78, -14);

            var close = NewImage("Close", _panel, new Color(0.20f, 0.22f, 0.27f));
            var crt = close.rectTransform;
            crt.anchorMin = new Vector2(1, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(1, 1);
            crt.sizeDelta = new Vector2(96, 28);
            crt.anchoredPosition = new Vector2(-12, -10);
            var button = close.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => Toggle());
            var closeLabel = NewText("Label", close.transform, 14, FontStyles.Bold, TextAlignmentOptions.Center);
            closeLabel.text = "Collapse";
            Stretch(closeLabel.rectTransform);

            var mapGo = new GameObject("Map", typeof(RectTransform));
            mapGo.transform.SetParent(_panel, false);
            _map = mapGo.GetComponent<RectTransform>();
            _map.anchorMin = Vector2.zero;
            _map.anchorMax = Vector2.one;
            _map.offsetMin = new Vector2(168, 12);
            _map.offsetMax = new Vector2(-12, -46);

            var legend = new GameObject("Legend", typeof(RectTransform));
            legend.transform.SetParent(_panel, false);
            var legendRt = legend.GetComponent<RectTransform>();
            legendRt.anchorMin = new Vector2(0, 0);
            legendRt.anchorMax = new Vector2(0, 1);
            legendRt.pivot = new Vector2(0, 1);
            legendRt.anchoredPosition = new Vector2(12, -48);
            legendRt.sizeDelta = new Vector2(150, -60);
            for (var i = 0; i < Groups.Length; i++)
                AddLegendRow(legend.transform, Groups[i], i);

            var note = NewText("Note", legend.transform, 12, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            note.richText = true;
            note.color = new Color(0.78f, 0.80f, 0.86f);
            note.textWrappingMode = TextWrappingModes.Normal;
            note.text = "<color=#FFD866>landing</color>  a space station can attack this site.";
            var nrt = note.rectTransform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(0, 1);
            nrt.pivot = new Vector2(0, 1);
            nrt.anchoredPosition = new Vector2(0, -Groups.Length * 58 - 8);
            nrt.sizeDelta = new Vector2(148, 52);

            foreach (var group in Groups)
                AddPlate(group);

            var repo = TerritoryRepository.Instance;
            if (repo == null) return;
            var moons = repo.Territories.Where(t => t.Kind == TerrainKind.Moon).ToList();
            foreach (var t in moons) AddSlot(t);
            var drawn = new HashSet<(Territory, Territory)>();
            foreach (var t in moons)
                foreach (var n in t.NeighbourTerritories)
                {
                    if (n.Kind != TerrainKind.Moon) continue;
                    var key = t.GetInstanceID() < n.GetInstanceID() ? (t, n) : (n, t);
                    if (!drawn.Add(key)) continue;
                    var a = _slots.Find(s => s.Territory == t);
                    var b = _slots.Find(s => s.Territory == n);
                    if (a == null || b == null) continue;
                    var line = NewImage("link", _map, new Color(0.62f, 0.66f, 0.74f, 0.85f));
                    line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    line.raycastTarget = false;
                    _links.Add(new LinkView { A = a.Rect, B = b.Rect, Line = line.rectTransform });
                }
            foreach (var slot in _slots) slot.Rect.SetAsLastSibling();
        }

        void AddLegendRow(Transform parent, (string Name, string Title, int Bonus, Color Accent, Vector2 Min, Vector2 Max) group, int index)
        {
            var row = new GameObject(group.Name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, -index * 58);
            rt.sizeDelta = new Vector2(148, 52);

            var chip = NewImage("Chip", row.transform, group.Accent);
            var crt = chip.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0, 1);
            crt.pivot = new Vector2(0, 1);
            crt.anchoredPosition = new Vector2(0, -4);
            crt.sizeDelta = new Vector2(10, 36);

            var title = NewText("Title", row.transform, 15, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            title.text = group.Title;
            var tr = title.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0, 1);
            tr.pivot = new Vector2(0, 1);
            tr.anchoredPosition = new Vector2(18, -2);
            tr.sizeDelta = new Vector2(128, 20);

            var bonus = NewText("Bonus", row.transform, 13, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            bonus.color = new Color(0.78f, 0.80f, 0.86f);
            bonus.text = $"+{group.Bonus}  MODs and energy";
            var br = bonus.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0, 1);
            br.pivot = new Vector2(0, 1);
            br.anchoredPosition = new Vector2(18, -22);
            br.sizeDelta = new Vector2(128, 18);
        }

        void AddPlate((string Name, string Title, int Bonus, Color Accent, Vector2 Min, Vector2 Max) group)
        {
            var tint = group.Accent;
            tint.a = 0.07f;
            var plate = NewImage(group.Name, _map, tint);
            plate.raycastTarget = false;
            var pr = plate.rectTransform;
            pr.anchorMin = group.Min;
            pr.anchorMax = group.Max;
            pr.offsetMin = Vector2.zero;
            pr.offsetMax = Vector2.zero;

            var bar = NewImage("Bar", plate.transform, group.Accent);
            bar.raycastTarget = false;
            var br = bar.rectTransform;
            br.anchorMin = new Vector2(0, 1);
            br.anchorMax = new Vector2(1, 1);
            br.pivot = new Vector2(0.5f, 1);
            br.sizeDelta = new Vector2(0, 3);

            var label = NewText("Label", plate.transform, 13, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            label.richText = true;
            label.text = $"<color=#{ColorUtility.ToHtmlStringRGB(group.Accent)}>{group.Title}</color>    +{group.Bonus}";
            var lr = label.rectTransform;
            lr.anchorMin = new Vector2(0, 1);
            lr.anchorMax = new Vector2(1, 1);
            lr.pivot = new Vector2(0, 1);
            lr.anchoredPosition = new Vector2(10, -8);
            lr.sizeDelta = new Vector2(-16, 18);
        }

        void AddSlot(Territory territory)
        {
            if (!Spot.TryGetValue(territory.Name, out var at)) at = new Vector2(0.5f, 0.5f);
            var frame = NewImage(territory.Name + " frame", _map, new Color(0.28f, 0.30f, 0.36f));
            frame.sprite = CardSprite();
            frame.type = Image.Type.Sliced;
            var rt = frame.rectTransform;
            rt.anchorMin = rt.anchorMax = at;
            rt.sizeDelta = new Vector2(108, 58);

            var img = NewImage(territory.Name, frame.transform, new Color(0.22f, 0.24f, 0.28f));
            img.sprite = CardSprite();
            img.type = Image.Type.Sliced;
            var ir = img.rectTransform;
            ir.anchorMin = Vector2.zero;
            ir.anchorMax = Vector2.one;
            ir.offsetMin = new Vector2(2, 2);
            ir.offsetMax = new Vector2(-2, -2);

            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var captured = territory;
            button.onClick.AddListener(() => captured.GetComponent<TerritorySelection>()?.ClickFromUi());

            var label = NewText("Label", frame.transform, 13, FontStyles.Bold, TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.richText = true;
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(6, 4);
            label.rectTransform.offsetMax = new Vector2(-6, -4);
            _slots.Add(new Slot { Territory = territory, Rect = rt, Frame = frame, Image = img, Label = label, Button = button });
        }

        Vector2 OnMap(RectTransform rt)
        {
            var r = _map.rect;
            return new Vector2(
                Mathf.Lerp(r.xMin, r.xMax, rt.anchorMin.x),
                Mathf.Lerp(r.yMin, r.yMax, rt.anchorMin.y)) + rt.anchoredPosition;
        }

        void PlaceLines()
        {
            if (_map == null) return;
            var half = new Vector2(54f, 29f);
            foreach (var link in _links)
            {
                if (link.A == null || link.B == null || link.Line == null) continue;
                var a = OnMap(link.A);
                var b = OnMap(link.B);
                var delta = b - a;
                var len = delta.magnitude;
                if (len < 8f)
                {
                    link.Line.gameObject.SetActive(false);
                    continue;
                }
                var dir = delta / len;
                var gap = Edge(half, dir) + 5f;
                if (len < gap * 2f + 4f)
                {
                    link.Line.gameObject.SetActive(false);
                    continue;
                }
                link.Line.gameObject.SetActive(true);
                a += dir * gap;
                b -= dir * gap;
                delta = b - a;
                link.Line.anchorMin = link.Line.anchorMax = new Vector2(0.5f, 0.5f);
                link.Line.anchoredPosition = (a + b) * 0.5f;
                link.Line.sizeDelta = new Vector2(delta.magnitude, 2f);
                link.Line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                link.Line.SetSiblingIndex(Groups.Length);
            }
        }

        static float Edge(Vector2 half, Vector2 dir)
        {
            var tx = Mathf.Abs(dir.x) < 0.001f ? float.PositiveInfinity : half.x / Mathf.Abs(dir.x);
            var ty = Mathf.Abs(dir.y) < 0.001f ? float.PositiveInfinity : half.y / Mathf.Abs(dir.y);
            return Mathf.Min(tx, ty);
        }

        static Sprite _card;
        static Sprite CardSprite()
        {
            if (_card != null) return _card;
            const int w = 64, h = 40, radius = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var px = Mathf.Clamp(x, radius, w - 1 - radius);
                    var py = Mathf.Clamp(y, radius, h - 1 - radius);
                    var dx = x < radius || x >= w - radius ? x - px : 0;
                    var dy = y < radius || y >= h - radius ? y - py : 0;
                    var inside = dx * dx + dy * dy <= radius * radius || (dx == 0 && dy == 0);
                    tex.SetPixel(x, y, inside ? Color.white : new Color(1, 1, 1, 0));
                }
            tex.Apply();
            _card = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return _card;
        }

        void Refresh()
        {
            foreach (var slot in _slots)
            {
                var t = slot.Territory;
                var sel = t.GetComponent<TerritorySelection>();
                var on = sel != null && !sel.IsDisabled;
                var selected = sel != null && sel.IsSelected;
                var owner = t.Owner;
                var fill = owner != null && owner.Color != null
                    ? Color.Lerp(new Color(0.16f, 0.17f, 0.20f), owner.Color.Normal, 0.72f)
                    : new Color(0.24f, 0.25f, 0.29f);
                if (!on) fill = Color.Lerp(fill, new Color(0.16f, 0.17f, 0.20f), 0.35f);
                slot.Image.color = fill;
                slot.Frame.color = selected ? new Color(1f, 0.85f, 0.40f) : new Color(0.34f, 0.36f, 0.42f);
                slot.Button.interactable = on;
                var troops = t.Troops > 0 ? t.Troops.ToString() : "–";
                var landing = t.LaunchPoint ? "   <color=#FFD866>landing</color>" : "";
                slot.Label.text = $"{Title(t.Name)}\n<size=90%>{troops}{landing}</size>";
                slot.Label.color = Color.white;
            }
        }

        static string Title(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var words = name.Replace('_', ' ').Split(' ');
            for (var i = 0; i < words.Length; i++)
                if (words[i].Length > 0)
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
        }

        static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        static TMP_Text NewText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
