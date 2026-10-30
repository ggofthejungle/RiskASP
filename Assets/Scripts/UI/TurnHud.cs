using System.Collections.Generic;
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
        private RectTransform _orderPicker;

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
            gm.OnCommanderKilled += hud.OnCommanderKilled;
            gm.OnTurnPhaseChanged += (_, _) => hud._flash = null;
            return hud;
        }

        private void Start()
        {
            _sm = SelectionManager.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_gm != null)
            {
                _gm.OnPlayerTurnChanged -= OnPlayerTurnChanged;
                _gm.OnCommanderKilled -= OnCommanderKilled;
            }
        }

        // ---------------- Commander killed: red toast ----------------
        private Image _toast;
        private TMP_Text _toastText;
        private float _toastUntil = -1f;
        private readonly List<string> _toastLines = new();
        public string LastToast { get; private set; } = "";

        private void OnCommanderKilled(Player owner, Expansions.CommanderType type, Map.Territory where, string how)
        {
            var name = Map.MapBadges.FullName(type);
            var deck = type switch
            {
                Expansions.CommanderType.Diplomat => "Diplomat",
                Expansions.CommanderType.LandCommander => "Land",
                Expansions.CommanderType.NuclearCommander => "Nuclear",
                _ => null
            };
            string line;
            if (_gm.IsLocal(owner))
                line = $"<b>{owner.Name.ToUpperInvariant()}: YOUR {name.ToUpperInvariant()} WAS KILLED</b> in {Nice(where?.Name)} ({how})." +
                       (deck != null ? $"\n<size=80%>No {deck} cards can be bought or played until you hire a new one.</size>" : "");
            else
                line = $"{owner?.Name}'s {name} was killed in {Nice(where?.Name)} ({how}).";
            LastToast = line;
            _toastLines.Add(line);
            if (_toastLines.Count > 3) _toastLines.RemoveAt(0);
            _toastText.text = string.Join("\n", _toastLines);
            _toast.gameObject.SetActive(true);
            _toastUntil = Time.unscaledTime + 6f;
        }

        private void AnimateToast()
        {
            if (_toastUntil < 0 || Time.unscaledTime < _toastUntil) return;
            _toastUntil = -1f;
            _toastLines.Clear();
            _toast.gameObject.SetActive(false);
        }

        private void Update()
        {
            Refresh();
            AnimateSplash();
            AnimateToast();
        }

        // ---------------- Banner ----------------

        private void Refresh()
        {
            if (_gm == null) return;
            var p = _gm.CurrentPlayer;
            var color = PlayerColor(p);

            _colorBar.color = color;
            SetText(_title, _gm.GameState == GameState.Over ? "GAME OVER"
                : _gm.IsChoosingOrder && p != null ? $"{p.Name.ToUpperInvariant()} CHOOSES"
                : _gm.IsPlacingStartingUnits && p != null ? $"{p.Name.ToUpperInvariant()} PLACES"
                : TurnTitle(p));
            SetText(_sub, $"Year {_gm.Year}/{GameManager.MaxYears}  ·  Turn {Mathf.Max(_gm.Turn, 1)}");

            // Phase stepper
            if (_gm.IsBidding || _gm.IsChoosingOrder || _gm.GameState != GameState.Playing)
            {
                SetChip(0, _gm.IsBidding ? "Bidding for turn order" : _gm.IsChoosingOrder ? "Choosing turn order"
                    : _gm.IsPlacingStartingUnits ? "Placing starting units" : "—", true, color);
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
            RefreshOrderPicker(color);
        }

        // Buttons "1st / 2nd / ..." while a human chooses their turn position.
        private string _orderPickerKey = "";
        private void RefreshOrderPicker(Color color)
        {
            var p = _gm.CurrentPlayer;
            var show = _gm.IsChoosingOrder && p != null && !p.IsBot();
            _orderPicker.gameObject.SetActive(show);
            if (!show) { _orderPickerKey = ""; return; }

            var free = _gm.AvailableTurnPositions;
            var key = p.Name + ":" + string.Join(",", free);
            if (key == _orderPickerKey) return;
            _orderPickerKey = key;

            foreach (Transform child in _orderPicker) Destroy(child.gameObject);
            foreach (var position in free)
            {
                var pos = position;
                var img = NewImage($"Pos{pos}", _orderPicker, color);
                img.raycastTarget = true;
                var button = img.gameObject.AddComponent<Button>();
                button.targetGraphic = img;
                button.onClick.AddListener(() => _gm.ChooseTurnPosition(pos));
                var label = NewText("Label", img.rectTransform, 24, FontStyles.Bold, TextAlignmentOptions.Center);
                label.color = ReadableOn(color);
                label.text = $"Play {GameManager.Ordinal(pos)}";
                Stretch(label.rectTransform);
            }
        }

        private void SetChip(int i, string text, bool active, Color playerColor, bool done = false)
        {
            _chips[i].gameObject.SetActive(true);
            _chips[i].color = active ? playerColor : done ? ChipDone : ChipIdle;
            _chipTexts[i].color = active ? ReadableOn(playerColor) : done ? TextDim : TextDim;
            _chipTexts[i].fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            SetText(_chipTexts[i], text);
        }

        // A message from the game (e.g. "Invalid bid...") shown in the instruction line for a few seconds.
        private string _flash;
        private float _flashUntil;
        public void Flash(string message, float seconds = 5f)
        {
            _flash = message;
            _flashUntil = Time.unscaledTime + seconds;
        }

        // What the player can do right now, in one or two short sentences.
        public string Instruction()
        {
            if (!string.IsNullOrEmpty(_flash) && Time.unscaledTime < _flashUntil)
                return $"<color=#FFD24D>{_flash}</color>";
            if (_gm.GameState == GameState.Over) return "The game is over.";
            var p = _gm.CurrentPlayer;
            if (p == null) return "";

            if (_gm.IsPlacingStartingUnits && _gm.PendingDecision is { Kind: Decisions.DecisionKind.PlaceStartingUnit } placing)
            {
                var unit = placing.Subject == "SpaceStation" ? "Space Station" : Map.MapBadges.FullName(
                    (Expansions.CommanderType)System.Enum.Parse(typeof(Expansions.CommanderType), placing.Subject));
                return _gm.IsLocal(_gm.InputPlayer)
                    ? $"<b>{_gm.InputPlayer.Name}</b>, click one of your territories for your <b>{unit}</b> ({_gm.PlacementIndex} of 3)."
                    : $"Waiting for <b>{_gm.InputPlayer?.Name}</b> to place their {unit}…";
            }

            if (_gm.IsBidding)
                return $"<b>{p.Name}</b>: type a bid from 0 to {p.GetEnergy()} energy and press <b>Submit</b>. " +
                       "Highest bid plays first this year.";

            if (_gm.IsChoosingOrder)
                return $"<b>{p.Name}</b>, you bid highest: choose when you play this year " +
                       $"({string.Join(", ", _gm.AvailableTurnPositions.ConvertAll(GameManager.Ordinal))}).";

            if (_gm.PendingDecision is { Kind: Decisions.DecisionKind.RemoveUnits })
            {
                var who = _gm.InputPlayer;
                var left = _gm.CommandCards.RemovalsLeft;
                return _gm.IsLocal(who)
                    ? $"<b>{who.Name}</b>, MOD Reduction: click your territories to remove <b>{left}</b> more unit{(left == 1 ? "" : "s")}."
                    : $"Waiting for <b>{who?.Name}</b> to remove units (MOD Reduction)…";
            }
            if (_gm.PendingDecision != null)
                return $"Waiting for <b>{_gm.InputPlayer?.Name}</b> to respond to the attack…";

            if (p.IsBot())
                return $"<b>{p.Name}</b> (AI) is playing…";

            switch (_gm.CurrentPhase)
            {
                case ReinforcePhase rp:
                    return rp.RemainingTroopsToPlace > 0
                        ? $"Click your territories to place troops (1 per click). <b>{rp.RemainingTroopsToPlace}</b> left. Buy cards / hire below."
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
                    {
                        var (d8, _) = BattleSimulator.AttackDice(from, Mathf.Min(3, from.GetAvailableTroops()));
                        var aim = _gm.SelectionArrow != null ? _gm.SelectionArrow.ShownTo : null;
                        var defD8 = aim != null ? BattleSimulator.DefenseDice(aim, Mathf.Min(BattleSimulator.MaxDefenseDice, aim.Troops)).d8 : 0;
                        var dice = (d8 > 0 ? $" Your commander{(d8 > 1 ? "s" : "")} here: <b>{d8} × d8</b>." : "")
                                   + (defD8 > 0 ? $" {Nice(aim.Name)} defends with <b>{defD8} × d8</b>." : "");
                        return $"Attacking from <b>{Nice(from.Name)}</b>: click a highlighted enemy territory to roll. " +
                               $"Click {Nice(from.Name)} again to cancel.{dice}";
                    }
                    return $"Rolling: <b>{Nice(from.Name)}</b> -> <b>{Nice(to.Name)}</b>…";
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
            _flash = null; // a message is about the previous player's moment; the banner explains the new one
            if (newPlayer == null || _gm.GameState == GameState.Over) return;

            if (_gm.IsBidding && _gm.Year != _lastSplashYear)
            {
                _lastSplashYear = _gm.Year;
                ShowSplash($"YEAR {_gm.Year}", "Bid energy for turn order", PlayerColor(null));
                return;
            }

            if (newPlayer.IsBot() || _gm.IsChoosingOrder || _gm.IsPlacingStartingUnits) return;

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

            // Commander-killed toast (upper middle, doesn't block clicks).
            _toast = NewImage("CommanderKilled", transform, new Color(0.62f, 0.08f, 0.08f, 0.95f));
            Place(_toast.rectTransform, new Vector2(0.24f, 0.64f), new Vector2(0.76f, 0.79f), Vector2.zero, Vector2.zero);
            _toastText = NewText("Text", _toast.rectTransform, 22, FontStyles.Normal, TextAlignmentOptions.Center);
            Place(_toastText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 6), new Vector2(-14, -6));
            _toastText.textWrappingMode = TextWrappingModes.Normal;
            _toast.gameObject.SetActive(false);

            // Turn-position picker (top-centre).
            _orderPicker = new GameObject("TurnOrderPicker", typeof(RectTransform)).GetComponent<RectTransform>();
            _orderPicker.SetParent(transform, false);
            Place(_orderPicker, new Vector2(0.36f, 0.72f), new Vector2(0.64f, 0.80f), Vector2.zero, Vector2.zero); // top-centre, below the dice panel
            var pickerLayout = _orderPicker.gameObject.AddComponent<HorizontalLayoutGroup>();
            pickerLayout.spacing = 8;
            pickerLayout.childControlWidth = pickerLayout.childControlHeight = true;
            pickerLayout.childForceExpandWidth = pickerLayout.childForceExpandHeight = true;
            _orderPicker.gameObject.SetActive(false);

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

        internal static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        internal static TMP_Text NewText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions align)
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

        internal static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        internal static void Stretch(RectTransform rt) => Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }
}
