using System.Collections.Generic;
using System.Linq;
using Cards;
using TMPro;
using UnityEngine;

namespace Map
{
    public enum TerrainKind
    {
        Land,
        Sea,
        Moon
    }

    // Seas sit in the gaps of the land map. The moon is a small board at the top-right.
    // A space station on land opens a route to the moon's near side (LaunchPoint).
    // Names are ours. The land territories they touch are the ones already on the map.
    public static class Frontiers
    {
        static readonly List<Territory> Approaches = new();

        public static TerrainKind? CardTerrain(CardType type) => type switch
        {
            CardType.AssembleModsNa or CardType.ReinforcementsNa or CardType.StealthModsNa
                or CardType.WaterDeathTrap or CardType.RocketStrikeWater or CardType.ScatterBombWater => TerrainKind.Sea,
            CardType.AssembleModsS or CardType.ReinforcementsS or CardType.StealthModsS
                or CardType.OrbitalMines or CardType.RocketStrikeMoon or CardType.ScatterBombMoon => TerrainKind.Moon,
            CardType.AssembleModsL or CardType.ReinforcementsL or CardType.StealthModsL
                or CardType.LandDeathTrap or CardType.RocketStrikeLand or CardType.ScatterBombLand => TerrainKind.Land,
            _ => null,
        };

        public static void Build(TerritoryRepository repo)
        {
            if (repo.Territories.Any(t => t.Kind != TerrainKind.Land)) return;
            ClearLines();
            var byName = repo.Territories.ToDictionary(t => t.Name);
            // Five water colonies. The bonus numbers are not printed in the manual, so a pair is +2
            // and the three-territory Pacific is +3, the same kind of placeholder as the moon colonies.
            var gulfStream = ContinentOf(repo, "gulf_stream", 2, new Color(0.12f, 0.32f, 0.48f));
            var capeWaters = ContinentOf(repo, "cape_waters", 2, new Color(0.10f, 0.28f, 0.42f));
            var monsoonSea = ContinentOf(repo, "monsoon_sea", 2, new Color(0.12f, 0.36f, 0.40f));
            var pacificPassage = ContinentOf(repo, "pacific_passage", 3, new Color(0.16f, 0.34f, 0.55f));
            var highIce = ContinentOf(repo, "high_ice", 2, new Color(0.45f, 0.58f, 0.68f));
            var ash = ContinentOf(repo, "ash_belt", 2, new Color(0.42f, 0.40f, 0.38f));
            var bright = ContinentOf(repo, "bright_sea", 2, new Color(0.55f, 0.55f, 0.62f));
            var scar = ContinentOf(repo, "south_scar", 3, new Color(0.48f, 0.42f, 0.40f));

            var seas = new Dictionary<string, Territory>();
            Territory Sea(Continent colony, string name, float x, float y, params string[] coasts)
            {
                var t = Spawn(repo, colony, name, new Vector3(x, y, 0f), TerrainKind.Sea, false, 0.76f);
                foreach (var coast in coasts)
                    if (byName.TryGetValue(coast, out var land)) Link(t, land);
                seas[name] = t;
                return t;
            }

            // Groups of 2 or 3, sitting in water that does not cover a country.
            // A coast is kept only when the line reaches that shore without entering another territory.
            Sea(gulfStream, "gulf", -11.15f, 3.05f, "central_america", "venezuela", "eastern_us");
            Sea(gulfStream, "north_atlantic", -9.35f, 3.45f, "eastern_us", "western_europe", "great_britain", "quebec");
            Link(seas["gulf"], seas["north_atlantic"]);

            Sea(capeWaters, "south_atlantic", -8.85f, -1.9f, "argentina", "brazil");
            Sea(capeWaters, "cape_basin", -7.15f, -1.9f, "south_africa", "north_africa");
            Link(seas["south_atlantic"], seas["cape_basin"]);

            Sea(monsoonSea, "indian_ocean", -2.35f, -1f, "madagascar", "east_africa", "india");
            Sea(monsoonSea, "monsoon_reach", -0.95f, -1f, "indonesia", "india", "western_australia");
            Link(seas["indian_ocean"], seas["monsoon_reach"]);

            Sea(pacificPassage, "south_pacific", 3.55f, 1.2f, "new_guinea", "eastern_australia");
            Sea(pacificPassage, "east_passage", 3.62f, 3.2f);
            Sea(pacificPassage, "north_pacific", 3.7f, 5.2f, "japan", "kamchatka");
            Link(seas["south_pacific"], seas["east_passage"]);
            Link(seas["east_passage"], seas["north_pacific"]);

            Sea(highIce, "arctic_ocean", -13.7f, 8.75f, "northwest_territory");
            Sea(highIce, "high_north", -12.25f, 8.75f, "northwest_territory", "greenland");
            Link(seas["arctic_ocean"], seas["high_north"]);

            // 14 moon territories, three colonies (4, 4 and 6). They live on their own board.
            // The picture that opens that board sits in the open water west of the Americas.
            // Three landing sites, one in each colony, are the only places a space station can reach.
            var off = 0;
            Territory Moon(Continent colony, string name, bool approach)
            {
                var t = Spawn(repo, colony, name, new Vector3(40f + off * 1.2f, 40f, 0f), TerrainKind.Moon, approach, 0.72f);
                off++;
                return t;
            }

            var pale = Moon(ash, "pale_crater", false);
            var rain = Moon(ash, "rain_plain", false);
            var storm = Moon(ash, "storm_ocean", false);
            var cinder = Moon(ash, "cinder_reach", true);
            Link(pale, rain, false);
            Link(pale, storm, false);
            Link(rain, storm, false);
            Link(rain, cinder, false);
            Link(storm, cinder, false);

            var ridge = Moon(bright, "high_ridge", false);
            var serenity = Moon(bright, "serenity_shelf", false);
            var nectar = Moon(bright, "nectar_gulf", false);
            var quiet = Moon(bright, "quiet_basin", true);
            Link(ridge, serenity, false);
            Link(serenity, nectar, false);
            Link(serenity, quiet, false);
            Link(nectar, quiet, false);
            Link(ridge, quiet, false);

            var dust = Moon(scar, "dust_field", false);
            var cloud = Moon(scar, "cloud_sea", false);
            var wall = Moon(scar, "long_wall", false);
            var marsh = Moon(scar, "fever_marsh", false);
            var ray = Moon(scar, "ray_crater", false);
            var impact = Moon(scar, "impact_basin", true);
            Link(dust, cloud, false);
            Link(cloud, wall, false);
            Link(wall, marsh, false);
            Link(marsh, ray, false);
            Link(ray, impact, false);
            Link(impact, cloud, false);
            Link(dust, impact, false);
            Link(wall, impact, false);

            Link(rain, serenity, false);
            Link(cinder, quiet, false);
            Link(quiet, impact, false);
            Link(storm, dust, false);

            Approaches.Clear();
            Approaches.Add(cinder);
            Approaches.Add(quiet);
            Approaches.Add(impact);

            SpawnMoonPicture();

            var cam = Camera.main;
            if (cam != null)
            {
                // Close enough that the sea discs stay readable. The moon column is inside this frame.
                cam.orthographicSize = 7.8f;
                cam.transform.position = new Vector3(-5.6f, 2.4f, cam.transform.position.z);
            }
            Debug.Log($"Frontiers: {seas.Count} seas, {Approaches.Count} moon approaches, {repo.Territories.Count} territories.");
        }

        // One moon picture in the water west of the Americas. It is not a territory. Clicking it opens the moon board.
        static void SpawnMoonPicture()
        {
            var go = new GameObject("Moon");
            go.transform.position = new Vector3(-17.7f, 4.3f, 0f);
            go.transform.localScale = Vector3.one * 1.15f;
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = MoonDisc();
            sprite.color = Color.white;
            sprite.sortingOrder = 3;
            var body = go.AddComponent<CircleCollider2D>();
            body.radius = 0.48f;
            go.AddComponent<MoonGate>();
            Caption(go.transform, "MOON", new Vector3(-17.7f, 3.45f, 0f));
            var word = go.transform.Find("MOON");
            if (word != null) word.localScale = Vector3.one / go.transform.localScale.x;
        }

        static Sprite _moonDisc;
        static Sprite MoonDisc()
        {
            if (_moonDisc != null) return _moonDisc;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var c = n / 2f;
            var craters = new[] { new Vector2(0.35f, 0.62f), new Vector2(0.62f, 0.4f), new Vector2(0.48f, 0.28f) };
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var p = new Vector2(x / (n - 1f), y / (n - 1f));
                    var d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                    if (d > 0.92f) { tex.SetPixel(x, y, new Color(1, 1, 1, 0)); continue; }
                    var shade = 0.78f;
                    foreach (var crater in craters)
                        if (Vector2.Distance(p, crater) < 0.12f) shade = 0.55f;
                    tex.SetPixel(x, y, new Color(shade, shade, shade * 1.02f, 1f));
                }
            tex.Apply();
            _moonDisc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return _moonDisc;
        }

        // A land space station can attack the three landing sites, and fortify back. The line is not drawn.
        public static void NoteStation(Territory land, bool present)
        {
            if (land == null || land.Kind != TerrainKind.Land) return;
            foreach (var moon in Approaches)
            {
                // The launch is a real route, but a line from the station to the moon row
                // would cut across the land, so it is not drawn.
                if (present) Link(land, moon, draw: false);
                else Unlink(land, moon);
            }
        }

        static Continent ContinentOf(TerritoryRepository repo, string name, int bonus, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(repo.transform, false);
            var continent = go.AddComponent<Continent>();
            continent.Name = name;
            continent.BonusTroops = bonus;
            continent.Color = color;
            continent.Territories = new List<Territory>();
            if (ContinentRepository.Instance != null) ContinentRepository.Instance.Add(continent);
            return continent;
        }

        static Territory Spawn(TerritoryRepository repo, Continent continent, string name, Vector3 at,
            TerrainKind kind, bool launch, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(repo.transform, false);
            go.transform.position = at;
            go.transform.localScale = Vector3.one * scale;

            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = Disc();
            sprite.color = kind == TerrainKind.Sea
                ? new Color(0.45f, 0.72f, 0.9f, 1f)
                : new Color(0.55f, 0.55f, 0.58f, 1f);
            sprite.sortingOrder = 2;
            if (kind == TerrainKind.Sea)
            {
                var ring = new GameObject("Border");
                ring.transform.SetParent(go.transform, false);
                ring.transform.localScale = Vector3.one * 1.28f;
                var edge = ring.AddComponent<SpriteRenderer>();
                edge.sprite = Disc();
                edge.color = new Color(0.02f, 0.07f, 0.14f, 1f);
                edge.sortingOrder = 1;
            }
            if (kind == TerrainKind.Moon) sprite.enabled = false;

            var body = go.AddComponent<CircleCollider2D>();
            if (kind == TerrainKind.Moon) body.enabled = false;
            body.radius = 0.45f;

            var territory = go.AddComponent<Territory>();
            territory.Name = name;
            territory.Continent = continent;
            territory.Kind = kind;
            territory.LaunchPoint = launch;
            territory.CommandersList = new List<Expansions.Commander>();
            territory.NeighbourTerritories = new List<Territory>();
            continent.Territories.Add(territory);

            var click = go.AddComponent<TerritorySelection>();
            click.Bind(territory);
            if (SelectionManager.Instance != null) SelectionManager.Instance.Include(territory);
            go.AddComponent<FrontierView>().Bind(territory, sprite);
            repo.Add(territory);
            return territory;
        }

        static bool Link(Territory a, Territory b, bool draw = true)
        {
            if (a == null || b == null || a == b) return false;
            var start = Vector3.zero;
            var end = Vector3.zero;
            if (draw && !OpenWater(a, b, out start, out end)) return false;
            if (!a.NeighbourTerritories.Contains(b)) a.NeighbourTerritories.Add(b);
            if (!b.NeighbourTerritories.Contains(a)) b.NeighbourTerritories.Add(a);
            if (draw) EnsureLine(a, b, start, end);
            return true;
        }

        // The drawn line runs from the edge of A to the first point of B. Any other territory
        // in between rejects the link, so a route cannot cut through a country or another sea.
        static bool OpenWater(Territory a, Territory b, out Vector3 start, out Vector3 end)
        {
            start = a.transform.position;
            end = b.transform.position;
            start.z = end.z = 0f;
            var delta = end - start;
            var len = delta.magnitude;
            if (len < 0.2f) return false;
            var dir = delta / len;
            var origin = (Vector2)start + (Vector2)dir * (Rim(a) + 0.03f);
            var hits = Physics2D.RaycastAll(origin, (Vector2)dir, len);
            System.Array.Sort(hits, (p, q) => p.distance.CompareTo(q.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                var t = hit.collider.GetComponentInParent<Territory>();
                if (t == null || t == a) continue;
                if (t != b) return false;
                start = origin;
                end = hit.point;
                end.z = 0f;
                return (end - start).sqrMagnitude > 0.004f;
            }
            if (b.Kind == TerrainKind.Land) return false;
            start = origin;
            end = (Vector3)((Vector2)end - (Vector2)dir * (Rim(b) + 0.03f));
            end.z = 0f;
            return (end - start).sqrMagnitude > 0.004f;
        }

        static float Rim(Territory t)
        {
            var circle = t.GetComponent<CircleCollider2D>();
            if (circle == null) return 0.2f;
            return circle.radius * t.transform.lossyScale.x;
        }

        static void Unlink(Territory a, Territory b)
        {
            if (a == null || b == null) return;
            a.NeighbourTerritories.Remove(b);
            b.NeighbourTerritories.Remove(a);
            RemoveLine(a, b);
        }

        // A devastated coast drops the line that ran to it.
        public static void DropLinks(Territory territory)
        {
            for (var i = _lines.Count - 1; i >= 0; i--)
            {
                if (_lines[i].A != territory && _lines[i].B != territory) continue;
                if (_lines[i].Line != null) UnityEngine.Object.Destroy(_lines[i].Line.gameObject);
                _lines.RemoveAt(i);
            }
        }

        struct FrontierLink
        {
            public Territory A, B;
            public LineRenderer Line;
        }

        static readonly List<FrontierLink> _lines = new();
        static Material _lineMat;

        static void ClearLines()
        {
            foreach (var link in _lines)
                if (link.Line != null) UnityEngine.Object.Destroy(link.Line.gameObject);
            _lines.Clear();
        }

        static void EnsureLine(Territory a, Territory b, Vector3 start, Vector3 end)
        {
            for (var i = 0; i < _lines.Count; i++)
            {
                var link = _lines[i];
                if (link.Line == null) continue;
                if ((link.A == a && link.B == b) || (link.A == b && link.B == a))
                {
                    SetLinePoints(link.Line, start, end);
                    return;
                }
            }
            var go = new GameObject("link " + a.Name + " " + b.Name);
            go.transform.SetParent(a.transform.parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.numCapVertices = 4;
            line.startWidth = line.endWidth = 0.055f;
            line.material = LineMaterial();
            line.textureMode = LineTextureMode.Stretch;
            line.startColor = line.endColor = LineColor(a, b);
            line.sortingOrder = -1;
            SetLinePoints(line, start, end);
            _lines.Add(new FrontierLink { A = a, B = b, Line = line });
        }

        static void SetLinePoints(LineRenderer line, Vector3 from, Vector3 to)
        {
            from.z = to.z = 0f;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        static void RemoveLine(Territory a, Territory b)
        {
            for (var i = _lines.Count - 1; i >= 0; i--)
            {
                var link = _lines[i];
                if (!((link.A == a && link.B == b) || (link.A == b && link.B == a))) continue;
                if (link.Line != null) UnityEngine.Object.Destroy(link.Line.gameObject);
                _lines.RemoveAt(i);
            }
        }

        static Color LineColor(Territory a, Territory b)
        {
            var moon = a.Kind == TerrainKind.Moon || b.Kind == TerrainKind.Moon;
            var land = a.Kind == TerrainKind.Land || b.Kind == TerrainKind.Land;
            if (moon && land) return new Color(0.78f, 0.64f, 0.22f, 1f);
            if (moon) return new Color(0.28f, 0.28f, 0.32f, 1f);
            return new Color(0.03f, 0.14f, 0.26f, 1f);
        }

        static Material LineMaterial()
        {
            if (_lineMat != null) return _lineMat;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            _lineMat = new Material(shader);
            _lineMat.mainTexture = Texture2D.whiteTexture;
            return _lineMat;
        }

        static void Caption(Transform parent, string text, Vector3 at)
        {
            var go = new GameObject(text);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var label = go.AddComponent<TextMeshPro>();
            var font = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).FirstOrDefault()?.font;
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = 2.6f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.9f, 0.9f, 0.93f);
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(0, 0, 0, 220);
            label.rectTransform.sizeDelta = new Vector2(2f, 0.7f);
            label.sortingOrder = 8;
        }

        static Sprite _disc;
        static Sprite Disc()
        {
            if (_disc != null) return _disc;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var c = n / 2f;
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                    tex.SetPixel(x, y, d <= 0.92f ? Color.white : new Color(1, 1, 1, 0));
                }
            tex.Apply();
            _disc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return _disc;
        }
    }

    // Owner colour and the unit count for a territory that has no land sprite.
    public class FrontierView : MonoBehaviour
    {
        Territory _territory;
        SpriteRenderer _sprite;
        TMP_Text _count;
        TMP_Text _name;
        Color _base;

        public void Bind(Territory territory, SpriteRenderer sprite)
        {
            _territory = territory;
            _sprite = sprite;
            _base = sprite.color;
            var font = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).FirstOrDefault()?.font;
            _count = MakeText("Count", font, 4.6f, new Vector3(0f, -0.02f, 0f));
            _name = MakeText("Name", font, 2.8f, Vector3.zero);
            _name.text = territory.Name.Replace('_', ' ');
            _name.enableWordWrapping = false;
            _name.overflowMode = TextOverflowModes.Overflow;
            _name.fontStyle = FontStyles.Bold;
            _name.outlineWidth = 0.28f;
            _name.rectTransform.sizeDelta = new Vector2(6.2f, 0.85f);
            PlaceName();
            territory.OnTroopsChanged += (_, _) => Refresh();
            territory.OnOwnerChanged += (_, _) => Refresh();
            Refresh();
        }

        TMP_Text MakeText(string label, TMP_FontAsset font, float size, Vector3 local)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            var text = go.AddComponent<TextMeshPro>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineWidth = 0.22f;
            text.outlineColor = new Color32(0, 0, 0, 220);
            text.rectTransform.sizeDelta = new Vector2(1.7f, 0.7f);
            text.sortingOrder = 8;
            return text;
        }

        void LateUpdate() => PlaceName();

        // Sit the name in the open water, away from land and from the neighbouring disc, so it does not cover a country.
        void PlaceName()
        {
            if (_name == null) return;
            var scale = Mathf.Max(0.2f, transform.localScale.x);
            var away = Vector3.zero;
            foreach (var n in _territory.NeighbourTerritories)
            {
                if (n == null) continue;
                var d = transform.position - n.transform.position;
                if (d.sqrMagnitude < 0.01f) continue;
                away += d.normalized * (n.Kind == TerrainKind.Land ? 1.6f : 0.45f);
            }
            if (away.sqrMagnitude < 0.04f)
                away = transform.position.x >= 0f ? Vector3.right : Vector3.left;
            away = away.normalized * 1.65f;
            _name.transform.localScale = Vector3.one / scale;
            _name.transform.localPosition = new Vector3(away.x / scale, away.y / scale, 0f);
        }

        void Refresh()
        {
            if (_count != null) _count.text = _territory.Troops > 0 ? _territory.Troops.ToString() : "";
            var owner = _territory.Owner;
            _sprite.color = owner != null && owner.Color != null ? owner.Color.Normal : _base;
        }
    }

    // The Pacific moon picture. Clicking it opens or closes the moon board.
    public class MoonGate : MonoBehaviour
    {
        SpriteRenderer _sprite;
        bool _lit;

        void Awake()
        {
            _sprite = GetComponent<SpriteRenderer>();
        }

        void OnMouseDown()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;
            UI.MoonBoard.Instance?.Toggle();
        }

        public void SetLit(bool lit)
        {
            if (_sprite == null || _lit == lit) return;
            _lit = lit;
            _sprite.color = lit ? new Color(1f, 0.86f, 0.45f) : Color.white;
        }
    }
}
