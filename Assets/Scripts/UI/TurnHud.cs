using System.Globalization;
using Map;
using player;
using TMPro;
using TurnPhases;
using TurnPhases.Selection;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    // Turn banner (top-right): whose turn it is (in their color), a phase stepper
    // (Reinforce > Attack > Fortify) and one instruction line for what to do right now.
    // Also shows a short full-width "ALICE'S TURN" splash when a human player's turn starts
    // (with "Pass the device to ..." in hot-seat).
    // Built in code under the existing Canvas, so the scene doesn't need editing.
    public class TurnHud : MonoBehaviour
    {
        private static readonly string[] PhaseNames = { "Reinforce", "Attack", "Fortify" };
        private static readonly Color PanelColor = new(0.27f, 0.29f, 0.38f, 0.95f);
        private static readonly Color ChipIdle = new(1f, 1f, 1f, 0.10f);
        private static readonly Color ChipDone = new(1f, 1f, 1f, 0.22f);
        private static readonly Color TextDim = new(0.80f, 0.82f, 0.88f, 1f);

        private GameManager _gm;
        private SelectionManager _sm;

        private Image _colorBar;
        private TMP_Text _title;
        private TMP_Text _sub;
        private TMP_Text _instruction;
        private readonly Image[] _chips = new Image[3];
        private readonly TMP_Text[] _chipTexts = new TMP_Text[3];

        private CanvasGroup _splash;
        private Image _splashBand;
        private TMP_Text _splashTitle;
        private TMP_Text _splashSub;
        private float _splashTime = -1f;
        private const float SplashDuration = 1.8f;
        private int _lastSplashYear;

        // For tests / other UI.
        public string TitleText => _title != null ? _title.text : "";
        public string InstructionText => _instruction != null ? _instruction.text : "";
        public string SplashTitleText => _splashTitle != null ? _splashTitle.text : "";
        public bool IsSplashVisible => _splashTime >= 0f;

        public static TurnHud Create(GameManager gm, Canvas canvas)
        {
            var go = new GameObject("TurnHud", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)go.transform);
            var hud = go.AddComponent<TurnHud>();
            hud._gm = gm;
            hud.Build();
            gm.OnPlayerTurnChanged += hud.OnPlayerTurnChanged; // now, so the first year's splash isn't missed
            return hud;
        }

        private void Start()
        {
            _sm = SelectionManager.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_gm != null) _gm.OnPlayerTurnChanged -= OnPlayerTurnChanged;
        }

        private void Update()
        {
            Refresh();
            AnimateSplash();
        }

        // ---------------- Banner ----------------

        private void Refresh()
        {
            if (_gm == null) return;
            var p = _gm.CurrentPlayer;
            var color = PlayerColor(p);

            _colorBar.color = color;
            SetText(_title, _gm.GameState == GameState.Over ? "GAME OVER" : TurnTitle(p));
            SetText(_sub, $"Year {_gm.Year}/{GameManager.MaxYears}  ·  Turn {Mathf.Max(_gm.Turn, 1)}");

            // Phase stepper
            if (_gm.IsBidding || _gm.GameState != GameState.Playing)
            {
                SetChip(0, _gm.IsBidding ? "Bidding for turn order" : "—", true, color);
                _chips[1].gameObject.SetActive(false);
                _chips[2].gameObject.SetActive(false);
            }
            else
            {
                var current = _gm.CurrentPhase switch
                {
                    ReinforcePhase => 0,
                    AttackPhase => 1,
                    FortifyPhase => 2,
                    _ => -1
                };
                for (int i = 0; i < 3; i++)
                {
                    _chips[i].gameObject.SetActive(true);
                    var label = $"{i + 1}  {PhaseNames[i]}";
                    if (i < current) label = $"<s>{label}</s>";
                    SetChip(i, label, i == current, color, done: i < current);
                }
            }

            SetText(_instruction, Instruction());
        }

        private void SetChip(int i, string text, bool active, Color playerColor, bool done = false)
        {
            _chips[i].gameObject.SetActive(true);
            _chips[i].color = active ? playerColor : done ? ChipDone : ChipIdle;
            _chipTexts[i].color = active ? ReadableOn(playerColor) : done ? TextDim : TextDim;
            _chipTexts[i].fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            SetText(_chipTexts[i], text);
        }

        // What the player can do right now, in one or two short sentences.
        public string Instruction()
        {
            if (_gm.GameState == GameState.Over) return "The game is over.";
            var p = _gm.CurrentPlayer;
            if (p == null) return "";

            if (_gm.IsBidding)
                return $"<b>{p.Name}</b>: type a bid from 0 to {p.GetEnergy()} energy and press <b>Submit</b>. " +
                       "Highest bid plays first this year.";

            if (_gm.PendingDecision != null)
                return $"Waiting for <b>{_gm.InputPlayer?.Name}</b> to respond to the attack…";

            if (p.IsBot())
                return $"<b>{p.Name}</b> (AI) is playing…";

            switch (_gm.CurrentPhase)
            {
                case ReinforcePhase rp:
                    return rp.RemainingTroopsToPlace > 0
                        ? $"Click your territories to place troops (1 per click). <b>{rp.RemainingTroopsToPlace}</b> left."
                        : "All troops placed. Press <b>Skip phase</b> to start attacking.";

                case AttackPhase ap:
                {
                    var sel = _sm != null ? _sm.CurrentSelectionPhase as AttackSelectionPhase : null;
                    var from = sel?.From;
                    var to = sel?.To;
                    if (ap.State == AttackState.Fortifying && ap.AttackResults != null)
                    {
                        var last = ap.LastAttackResult;
                        return $"You conquered <b>{Nice(last.Target.Name)}</b>! Click <b>{Nice(last.Target.Name)}</b> to move in " +
                               $"all spare troops, or <b>{Nice(last.Origin.Name)}</b> to move the minimum.";
                    }
                    if (from == null)
                        return "Click one of your territories (2+ troops) to attack <b>from</b>. " +
                               "Press <b>Skip phase</b> when you're done attacking.";
                    if (to == null)
                        return $"Attacking from <b>{Nice(from.Name)}</b>: click a highlighted enemy territory to roll. " +
                               $"Click {Nice(from.Name)} again to cancel.";
                    return $"Rolling: <b>{Nice(from.Name)}</b> → <b>{Nice(to.Name)}</b>…";
                }

                case FortifyPhase:
                {
                    var sel = _sm != null ? _sm.CurrentSelectionPhase as FortifySelectionPhase : null;
                    var from = sel?.From;
                    var to = sel?.To;
                    if (from == null)
                        return "Optional: click a territory to move troops <b>from</b>, or press <b>Skip phase</b> to end your turn.";
                    if (to == null)
                        return $"Moving from <b>{Nice(from.Name)}</b>: click one of your connected territories.";
                    return $"Click <b>{Nice(to.Name)}</b> again to move all spare troops there, or {Nice(from.Name)} to cancel.";
                }
            }
            return "";
        }

        // ---------------- Splash ----------------

        private void OnPlayerTurnChanged(Player oldPlayer, Player newPlayer)
        {
            if (newPlayer == null || _gm.GameState == GameState.Over) return;

            if (_gm.IsBidding && _gm.Year != _lastSplashYear)
            {
                _lastSplashYear = _gm.Year;
                ShowSplash($"YEAR {_gm.Year}", "Bid energy for turn order", PlayerColor(null));
                return;
            }

            if (newPlayer.IsBot()) return;

            var handOff = oldPlayer != null && !oldPlayer.IsBot() && oldPlayer != newPlayer
                ? $"Pass the device to {newPlayer.Name}"
                : $"Year {_gm.Year}";
            ShowSplash(_gm.IsBidding ? $"{newPlayer.Name.ToUpperInvariant()}'S BID" : TurnTitle(newPlayer), handOff,
                PlayerColor(newPlayer));
        }

        private void ShowSplash(string title, string sub, Color color)
        {
            _splashBand.color = new Color(color.r, color.g, color.b, 0.92f);
            _splashTitle.color = ReadableOn(color);
            _splashSub.color = ReadableOn(color);
            _splashTitle.text = title;
            _splashSub.text = sub;
            _splashTime = 0f;
            _splash.gameObject.SetActive(true);
        }

        private void AnimateSplash()
        {
            if (_splashTime < 0f) return;
            _splashTime += Time.unscaledDeltaTime;
            var t = _splashTime;
            _splash.alpha = t < 0.2f ? t / 0.2f : t > SplashDuration - 0.4f ? Mathf.Clamp01((SplashDuration - t) / 0.4f) : 1f;
            if (t >= SplashDuration)
            {
                _splashTime = -1f;
                _splash.gameObject.SetActive(false);
            }
        }

        // ---------------- Building the UI ----------------

        private void Build()
        {
            // Banner panel in the empty top-right area, above the player panels.
            var panel = NewImage("TurnBanner", transform, PanelColor);
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.725f, 0.805f);
            prt.anchorMax = new Vector2(0.995f, 0.99f);
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            panel.raycastTarget = false;
            var shadow = panel.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(3, -3);
            shadow.effectColor = new Color(0, 0, 0, 0.35f);

            _colorBar = NewImage("PlayerColor", prt, Color.white);
            Place(_colorBar.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(14, 0));

            _title = NewText("Title", prt, 34, FontStyles.Bold, TextAlignmentOptions.Left);
            Place(_title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, -54), new Vector2(-150, -6));

            _sub = NewText("YearTurn", prt, 18, FontStyles.Normal, TextAlignmentOptions.Right);
            _sub.color = TextDim;
            Place(_sub.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-150, -40), new Vector2(-12, -10));

            var row = new GameObject("PhaseStepper", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(prt, false);
            Place(row, new Vector2(0, 1), new Vector2(1, 1), new Vector2(28, -98), new Vector2(-12, -60));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            for (int i = 0; i < 3; i++)
            {
                _chips[i] = NewImage($"Phase{i + 1}", row, ChipIdle);
                _chipTexts[i] = NewText("Label", _chips[i].rectTransform, 20, FontStyles.Normal, TextAlignmentOptions.Center);
                Stretch(_chipTexts[i].rectTransform);
                _chipTexts[i].textWrappingMode = TextWrappingModes.NoWrap;
                _chipTexts[i].overflowMode = TextOverflowModes.Ellipsis;
            }

            _instruction = NewText("Instruction", prt, 21, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(_instruction.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(28, 8), new Vector2(-12, -106));
            _instruction.textWrappingMode = TextWrappingModes.Normal;
            _instruction.overflowMode = TextOverflowModes.Ellipsis;

            // Splash (full-width band in the middle of the screen; never blocks clicks).
            var splashGo = new GameObject("TurnSplash", typeof(RectTransform), typeof(CanvasGroup));
            splashGo.transform.SetParent(transform, false);
            Stretch((RectTransform)splashGo.transform);
            _splash = splashGo.GetComponent<CanvasGroup>();
            _splash.blocksRaycasts = false;
            _splash.interactable = false;
            _splashBand = NewImage("Band", splashGo.transform, Color.white);
            _splashBand.raycastTarget = false;
            Place(_splashBand.rectTransform, new Vector2(0, 0.42f), new Vector2(1, 0.58f), Vector2.zero, Vector2.zero);
            _splashTitle = NewText("Title", _splashBand.rectTransform, 64, FontStyles.Bold, TextAlignmentOptions.Center);
            Place(_splashTitle.rectTransform, new Vector2(0, 0.35f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            _splashSub = NewText("Sub", _splashBand.rectTransform, 28, FontStyles.Normal, TextAlignmentOptions.Center);
            Place(_splashSub.rectTransform, new Vector2(0, 0), new Vector2(1, 0.4f), Vector2.zero, Vector2.zero);
            splashGo.SetActive(false);
        }

        // ---------------- Helpers ----------------

        public static string TurnTitle(Player p)
        {
            if (p == null) return "";
            return p.Name == "You" ? "YOUR TURN" : $"{p.Name.ToUpperInvariant()}'S TURN";
        }

        public static string Nice(string territoryName) =>
            CultureInfo.InvariantCulture.TextInfo.ToTitleCase(territoryName ?? "");

        public static Color PlayerColor(Player p) =>
            p != null && p.Color != null ? WithAlpha(p.Color.Normal, 1f) : new Color(0.45f, 0.50f, 0.62f, 1f);

        private static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        // Dark text on light colors (yellow), white text otherwise.
        public static Color ReadableOn(Color c) =>
            0.299f * c.r + 0.587f * c.g + 0.114f * c.b > 0.62f ? new Color(0.12f, 0.12f, 0.15f, 1f) : Color.white;

        private static void SetText(TMP_Text t, string s)
        {
            if (t != null && t.text != s) t.text = s;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text NewText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rt) => Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }
}
