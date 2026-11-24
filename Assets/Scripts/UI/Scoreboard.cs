using System.Collections.Generic;
using System.Linq;
using Expansions;
using player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Top-left strip: every player in this year's turn order with score, energy, cards and territories.
    // The player whose turn it is gets an arrow; eliminated players are dimmed; dropped online players say "away".
    public class Scoreboard : MonoBehaviour
    {
        const float RowHeight = 38f;
        const float Width = 478f;
        const float HeaderHeight = 36f;
        static readonly Color Dim = new(1f, 1f, 1f, 0.38f);
        static readonly Color Muted = new(0.70f, 0.73f, 0.80f, 1f);

        GameManager _gm;
        RectTransform _panel;
        TMP_Text _header;
        readonly List<Row> _rows = new();
        float _nextRefresh;

        sealed class Row
        {
            public Image Swatch;
            public TMP_Text Name, Score, Energy, Cards, Land, Troops;
        }

        public IReadOnlyList<string> RowNames => _rows.Where(r => r.Name.gameObject.activeSelf).Select(r => r.Name.text).ToList();
        public string HeaderText => _header != null ? _header.text : "";

        public static Scoreboard Create(GameManager gm, Canvas canvas)
        {
            var go = new GameObject("Scoreboard", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            TurnHud.Stretch((RectTransform)go.transform);
            var board = go.AddComponent<Scoreboard>();
            board._gm = gm;
            board.Build();
            return board;
        }

        void Build()
        {
            var panel = TurnHud.NewImage("Panel", transform, TurnHud.PopupFill);
            _panel = panel.rectTransform;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 0f);
            _panel.pivot = new Vector2(0f, 0f);
            _panel.anchoredPosition = new Vector2(8f, 8f);
            TurnHud.FramePopup(panel, TurnHud.PopupFill, 2f);

            _header = TurnHud.NewText("Header", _panel, 20, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _header.color = TurnHud.PopupBorder;
            _header.characterSpacing = 2f;
            TurnHud.Place(_header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -HeaderHeight), new Vector2(-8f, -4f));

            ColumnTitle("SCORE", 168f, 52f);
            ColumnTitle("ENERGY", 224f, 64f);
            ColumnTitle("CARDS", 292f, 52f);
            ColumnTitle("LAND", 348f, 48f);
            ColumnTitle("TROOPS", 400f, 64f);

            for (var i = 0; i < 6; i++) _rows.Add(NewRow(i));
            Refresh();
        }

        Row NewRow(int index)
        {
            var top = -(HeaderHeight + 24f + index * RowHeight);
            var row = new Row();
            row.Swatch = TurnHud.NewImage("Swatch" + index, _panel, Color.gray);
            TurnHud.Place(row.Swatch.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, top - RowHeight + 6f), new Vector2(40f, top - 6f));

            row.Name = Cell("Name" + index, TextAlignmentOptions.MidlineLeft, 48f, 116f, top);
            row.Score = Cell("Score" + index, TextAlignmentOptions.MidlineRight, 168f, 52f, top);
            row.Energy = Cell("Energy" + index, TextAlignmentOptions.MidlineRight, 224f, 64f, top);
            row.Cards = Cell("Cards" + index, TextAlignmentOptions.MidlineRight, 292f, 52f, top);
            row.Land = Cell("Land" + index, TextAlignmentOptions.MidlineRight, 348f, 48f, top);
            row.Troops = Cell("Troops" + index, TextAlignmentOptions.MidlineRight, 400f, 64f, top);
            return row;
        }

        void ColumnTitle(string text, float x, float width)
        {
            var t = TurnHud.NewText("Col" + text, _panel, 13, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            t.color = Muted;
            t.text = text;
            var top = -HeaderHeight;
            TurnHud.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, top - 20f), new Vector2(x + width, top));
        }

        TMP_Text Cell(string name, TextAlignmentOptions align, float x, float width, float top)
        {
            var t = TurnHud.NewText(name, _panel, 22, FontStyles.Normal, align);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Truncate;
            TurnHud.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, top - RowHeight), new Vector2(x + width, top));
            return t;
        }

        void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }

        public void Refresh()
        {
            if (_gm == null || _gm.Players == null) return;
            var order = _gm.SeatOrder
                .OrderBy(p => p != null && p.Color != null && p.Color.name == "Black")
                .ToList();
            _header.text = _gm.GameState == GameState.Over
                ? "FINAL SCORES"
                : $"YEAR {Mathf.Min(_gm.Year, GameManager.MaxYears)} / {GameManager.MaxYears}" + (_gm.IsBidding ? "  ·  BIDDING" : "");

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var p = i < order.Count ? order[i] : null;
                var show = p != null;
                row.Swatch.gameObject.SetActive(show);
                foreach (var t in new[] { row.Name, row.Score, row.Energy, row.Cards, row.Land, row.Troops })
                    t.gameObject.SetActive(show);
                if (!show) continue;

                var dead = p.IsDead();
                var color = dead ? Dim : Color.white;
                row.Swatch.color = TurnHud.PlayerColor(p);
                var marker = !dead && _gm.CurrentPlayer == p && _gm.GameState != GameState.Over ? "<color=#FFD866><b>></b></color> " : "";
                var tags = dead ? "  <size=75%>out</size>"
                    : !_gm.IsConnected(p) ? "  <size=75%><color=#E07A6A>away</color></size>"
                    : _gm.IsLocal(p) && _gm.LocalPlayer != null ? "  <size=75%>(you)</size>" : "";
                row.Name.text = marker + p.Name + tags;
                row.Score.text = Scoring.ScoreOf(p).ToString();
                row.Energy.text = p.GetEnergy().ToString();
                row.Cards.text = p.Cards.Count.ToString();
                row.Land.text = p.GetTerritoryCount().ToString();
                row.Troops.text = p.GetTroopsCountInTerritories().ToString();
                foreach (var t in new[] { row.Name, row.Score, row.Energy, row.Cards, row.Land, row.Troops })
                    t.color = color;
            }

            var visible = Mathf.Min(order.Count, _rows.Count);
            _panel.sizeDelta = new Vector2(Width, HeaderHeight + 32f + visible * RowHeight);
        }
    }
}
