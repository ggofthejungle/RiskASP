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
        const float RowHeight = 30f;
        const float Width = 360f;
        const float HeaderHeight = 30f;
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
            public TMP_Text Name, Score, Energy, Cards, Land;
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
            _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchoredPosition = new Vector2(12f, -12f);
            TurnHud.FramePopup(panel, TurnHud.PopupFill, 2f);

            _header = TurnHud.NewText("Header", _panel, 15, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _header.color = TurnHud.PopupBorder;
            _header.characterSpacing = 2f;
            TurnHud.Place(_header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -HeaderHeight), new Vector2(-8f, -4f));

            var labels = TurnHud.NewText("Columns", _panel, 12, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            labels.color = Muted;
            labels.text = "SCORE    ENERGY    CARDS    LAND";
            TurnHud.Place(labels.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -HeaderHeight - 18f), new Vector2(-12f, -HeaderHeight));

            for (var i = 0; i < 6; i++) _rows.Add(NewRow(i));
            Refresh();
        }

        Row NewRow(int index)
        {
            var top = -(HeaderHeight + 20f + index * RowHeight);
            var row = new Row();
            row.Swatch = TurnHud.NewImage("Swatch" + index, _panel, Color.gray);
            TurnHud.Place(row.Swatch.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, top - RowHeight + 6f), new Vector2(18f, top - 4f));

            row.Name = Cell("Name" + index, TextAlignmentOptions.MidlineLeft, 26f, 150f, top);
            row.Score = Cell("Score" + index, TextAlignmentOptions.MidlineRight, 176f, 42f, top);
            row.Energy = Cell("Energy" + index, TextAlignmentOptions.MidlineRight, 226f, 46f, top);
            row.Cards = Cell("Cards" + index, TextAlignmentOptions.MidlineRight, 280f, 34f, top);
            row.Land = Cell("Land" + index, TextAlignmentOptions.MidlineRight, 314f, 34f, top);
            return row;
        }

        TMP_Text Cell(string name, TextAlignmentOptions align, float x, float width, float top)
        {
            var t = TurnHud.NewText(name, _panel, 17, FontStyles.Normal, align);
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
            var order = _gm.SeatOrder;
            _header.text = _gm.GameState == GameState.Over
                ? "FINAL SCORES"
                : $"YEAR {Mathf.Min(_gm.Year, GameManager.MaxYears)} / {GameManager.MaxYears}" + (_gm.IsBidding ? "  ·  BIDDING" : "");

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var p = i < order.Count ? order[i] : null;
                var show = p != null;
                row.Swatch.gameObject.SetActive(show);
                foreach (var t in new[] { row.Name, row.Score, row.Energy, row.Cards, row.Land })
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
                foreach (var t in new[] { row.Name, row.Score, row.Energy, row.Cards, row.Land })
                    t.color = color;
            }

            var visible = Mathf.Min(order.Count, _rows.Count);
            _panel.sizeDelta = new Vector2(Width, HeaderHeight + 26f + visible * RowHeight);
        }
    }
}
