using System.Linq;
using System.Text;
using Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Every land continent, the oceans, and the moon colonies, with the bonus for holding the whole group.
    public class BonusLegend : MonoBehaviour
    {
        RectTransform _panel;

        public static void Create(Canvas canvas)
        {
            if (canvas == null) return;
            var go = new GameObject("BonusLegend", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            TurnHud.Stretch((RectTransform)go.transform);
            go.AddComponent<BonusLegend>().Build();
        }

        void Build()
        {
            var open = TurnHud.NewImage("Open", transform, new Color(0.12f, 0.13f, 0.16f, 0.96f));
            TurnHud.Place(open.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(384f, -44f), new Vector2(500f, -12f));
            var button = open.gameObject.AddComponent<Button>();
            button.onClick.AddListener(Toggle);
            var word = TurnHud.NewText("Label", open.transform, 16, FontStyles.Bold, TextAlignmentOptions.Center);
            word.text = "Bonuses";
            TurnHud.Stretch(word.rectTransform);

            var panel = TurnHud.NewImage("Panel", transform, new Color(0.10f, 0.11f, 0.14f, 0.97f));
            _panel = panel.rectTransform;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchoredPosition = new Vector2(384f, -52f);
            _panel.gameObject.SetActive(false);

            var body = TurnHud.NewText("Body", _panel, 16, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.richText = true;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.text = Write();
            var lines = body.text.Split('\n').Length;
            _panel.sizeDelta = new Vector2(300f, 16f + lines * 22f);
            TurnHud.Place(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 10f), new Vector2(-12f, -8f));
        }

        void Toggle() => _panel.gameObject.SetActive(!_panel.gameObject.activeSelf);

        static string Write()
        {
            var repo = ContinentRepository.Instance;
            if (repo == null) return "Bonuses are not ready yet.";
            var all = repo.Continents.Values.Where(c => c != null && c.Territories != null && c.Territories.Count > 0).ToList();
            var text = new StringBuilder();
            text.AppendLine("Hold every territory in a group.");
            text.AppendLine("The bonus is MODs and the same energy.");
            text.AppendLine();
            Section(text, "Land", all.Where(c => c.Territories[0].Kind == TerrainKind.Land));
            text.AppendLine();
            Section(text, "Water", all.Where(c => c.Territories[0].Kind == TerrainKind.Sea));
            text.AppendLine();
            Section(text, "Moon", all.Where(c => c.Territories[0].Kind == TerrainKind.Moon));
            return text.ToString().TrimEnd();
        }

        static void Section(StringBuilder text, string title, System.Collections.Generic.IEnumerable<Continent> groups)
        {
            text.Append("<b>").Append(title).AppendLine("</b>");
            var any = false;
            foreach (var group in groups.OrderBy(c => c.Name))
            {
                any = true;
                text.Append(Nice(group.Name)).Append("    +").Append(group.BonusTroops).AppendLine();
            }
            if (!any) text.AppendLine("None this match.");
        }

        public static string Nice(string name) => name switch
        {
            "n_america" => "North America",
            "s_america" => "South America",
            "europe" => "Europe",
            "asia" => "Asia",
            "africa" => "Africa",
            "australia" => "Australia",
            "gulf_stream" => "Gulf Stream",
            "cape_waters" => "Cape Waters",
            "monsoon_sea" => "Monsoon Sea",
            "pacific_passage" => "Pacific Passage",
            "high_ice" => "High Ice",
            "ash_belt" => "Ash Belt",
            "bright_sea" => "Bright Sea",
            "south_scar" => "South Scar",
            _ => string.IsNullOrEmpty(name) ? "" : name.Replace('_', ' '),
        };
    }
}
