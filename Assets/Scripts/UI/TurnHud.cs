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
        public static readonly Color PopupBorder = new(0.95f, 0.84f, 0.38f, 1f);
        public static readonly Color PopupFill = new(0.12f, 0.13f, 0.18f, 0.97f);
        private const float PopupBorderThickness = 3.5f;

        private GameManager _gm;
        private SelectionManager _sm;

        private Image _colorBar;
        private TMP_Text _title;
        private TMP_Text _sub;
        private TMP_Text _instruction;
        private Image _instructionBadge;
        private Image _instructionGlyph;
        private readonly Image[] _chips = new Image[3];
        private readonly TMP_Text[] _chipTexts = new TMP_Text[3];
        private RectTransform _orderPicker;
        private RectTransform _orderButtons;
        private RectTransform _bidPicker;
        private RectTransform _bidButtons;
        private string _bidPickerKey = "";

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
            if (_gm.CommandCards != null) _gm.CommandCards.OnConquestBonus += OnConquestBonus;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_gm != null)
            {
                _gm.OnPlayerTurnChanged -= OnPlayerTurnChanged;
                _gm.OnCommanderKilled -= OnCommanderKilled;
                if (_gm.CommandCards != null) _gm.CommandCards.OnConquestBonus -= OnConquestBonus;
            }
        }

        private void OnConquestBonus(Player p, Cards.Card card)
        {
            var reward = card == null ? "" : _gm.IsLocal(p) ? $" and <b>{card.Name}</b>" : " and a command card";
            Flash($"{p.Name} conquered {InvadeEarth.Rules.IncomeRules.ConquestBonusTerritories} territories this turn: " +
                  $"+{InvadeEarth.Rules.IncomeRules.ConquestBonusEnergy} energy{reward}.");
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
            if (_gm == null || _colorBar == null || _title == null || _sub == null) return;
            for (var i = 0; i < _chips.Length; i++)
                if (_chips[i] == null || _chipTexts[i] == null) return;
            var p = _gm.CurrentPlayer;
            var color = PlayerColor(p);

            _colorBar.color = color;
            SetText(_title, _gm.GameState == GameState.Over ? "GAME OVER"
                : _gm.IsChoosingOrder && p != null ? $"{p.Name.ToUpperInvariant()} CHOOSES"
                : _gm.IsPlacingStartingUnits && p != null ? $"{p.Name.ToUpperInvariant()} PLACES"
                : TurnTitle(p));
            SetText(_sub, $"Year {_gm.Year}/{GameManager.MaxYears}  ·  Turn {Mathf.Max(_gm.Turn, 1)}");

            // Phase stepper
            if (_gm.IsBidding || _gm.IsChoosingOrder || _gm.IsPlacingStartingUnits || _gm.GameState != GameState.Playing)
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
            RefreshInstructionIcon();
            RefreshOrderPicker(color);
            RefreshBidPicker(color);
        }

        private void RefreshInstructionIcon()
        {
            if (_instructionBadge == null) return;
            Sprite icon = null;
            Color fill = Color.white;
            if (_gm.IsPlacingStartingUnits && _gm.PendingDecision is { Kind: Decisions.DecisionKind.PlaceStartingUnit } placing)
            {
                if (placing.Subject == "SpaceStation")
                {
                    icon = Map.MapBadges.StationIcon();
                    fill = new Color(0.10f, 0.12f, 0.22f);
                }
                else if (System.Enum.TryParse(placing.Subject, out Expansions.CommanderType type))
                {
                    icon = Map.MapBadges.IconFor(type);
                    fill = Map.MapBadges.TypeColor(type);
                }
            }
            var show = icon != null;
            _instructionBadge.gameObject.SetActive(show);
            if (!show)
            {
                Place(_instruction.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(28, 8), new Vector2(-12, -106));
                return;
            }
            _instructionBadge.color = fill;
            _instructionGlyph.sprite = icon;
            Place(_instruction.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(66, 8), new Vector2(-12, -106));
        }

        // Buttons 0..energy while a human bids for turn order.
        private void RefreshBidPicker(Color color)
        {
            var p = _gm.CurrentPlayer;
            var show = _gm.IsBidding && p != null && !p.IsBot() && _gm.IsLocal(p);
            _bidPicker.gameObject.SetActive(show);
            if (!show) { _bidPickerKey = ""; return; }

            var max = p.GetEnergy();
            var key = p.Name + ":" + max;
            if (key == _bidPickerKey) return;
            _bidPickerKey = key;

            foreach (Transform child in _bidButtons) Destroy(child.gameObject);
            for (int n = 0; n <= max; n++)
            {
                var bid = n;
                var img = NewImage($"Bid{bid}", _bidButtons, color);
                img.raycastTarget = true;
                var button = img.gameObject.AddComponent<Button>();
                button.targetGraphic = img;
                button.onClick.AddListener(() => _gm.SubmitBid(bid));
                var label = NewText("Label", img.rectTransform, 26, FontStyles.Bold, TextAlignmentOptions.Center);
                label.color = ReadableOn(color);
                label.text = bid == 0 ? "0\n<size=60%>pass</size>" : bid.ToString();
                Stretch(label.rectTransform);
                var le = img.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 72;
                le.minWidth = 56;
            }
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

            foreach (Transform child in _orderButtons) Destroy(child.gameObject);
            foreach (var position in free)
            {
                var pos = position;
                var img = NewImage($"Pos{pos}", _orderButtons, color);
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

            if (_gm.IsDrafting && _gm.PendingDecision is { Kind: Decisions.DecisionKind.ClaimTerritory } claim)
                return _gm.IsLocal(_gm.InputPlayer)
                    ? $"<b>{_gm.InputPlayer.Name}</b>, click an empty (grey) territory to claim it with 1 MOD. " +
                      $"{claim.Options.Count} left."
                    : $"Waiting for <b>{_gm.InputPlayer?.Name}</b> to claim a territory…";
            if (_gm.IsDrafting && _gm.PendingDecision is { Kind: Decisions.DecisionKind.PlaceStartingTroops })
                return _gm.IsLocal(_gm.InputPlayer)
                    ? $"<b>{_gm.InputPlayer.Name}</b>, click your territories to place MODs: <b>{_gm.DraftBatchLeft}</b> more now " +
                      $"({_gm.DraftUnitsLeft(_gm.InputPlayer)} left in your army)."
                    : $"Waiting for <b>{_gm.InputPlayer?.Name}</b> to place MODs…";

            if (_gm.IsBidding)
                return $"<b>{p.Name}</b>: tap a bid from <b>0</b> to <b>{p.GetEnergy()}</b> energy. " +
                       "Highest bid chooses turn order first this year.";

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
            {
                var secondsLeft = _gm.DecisionSecondsLeft;
                var clock = double.IsInfinity(secondsLeft) ? "" : $" ({Mathf.CeilToInt((float)secondsLeft)}s)";
                if (_gm.PendingDecision.Kind == Decisions.DecisionKind.EvacuationDestination)
                    return _gm.IsLocal(_gm.InputPlayer)
                        ? $"<b>{_gm.InputPlayer.Name}</b>, Evacuation: click the territory your units move to.{clock}"
                        : $"Waiting for <b>{_gm.InputPlayer?.Name}</b> to choose where to evacuate…{clock}";
                if (_gm.PendingDecision.Kind == Decisions.DecisionKind.ArmageddonTarget)
                    return _gm.IsLocal(_gm.InputPlayer)
                        ? $"<b>{_gm.InputPlayer.Name}</b>, Armageddon: click the target for <b>{_gm.PendingDecision.Subject}</b>.{clock}"
                        : $"Waiting for <b>{_gm.InputPlayer?.Name}</b> to aim {_gm.PendingDecision.Subject} (Armageddon)…{clock}";
                if (_gm.PendingDecision.Kind == Decisions.DecisionKind.ArmageddonCard)
                    return $"<b>ARMAGEDDON</b>: waiting for <b>{_gm.InputPlayer?.Name}</b> to play nuclear cards for free…{clock}";
                return $"Waiting for <b>{_gm.InputPlayer?.Name}</b> to respond to the attack…{clock}";
            }

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
            _splashBand.color = PopupBorder;
            var fill = _splashBand.transform.Find("Fill")?.GetComponent<Image>();
            if (fill != null) fill.color = new Color(color.r, color.g, color.b, 0.92f);
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

            _instructionBadge = NewImage("InstructionBadge", prt, Color.white);
            Place(_instructionBadge.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 14), new Vector2(58, 44));
            _instructionBadge.preserveAspect = true;
            _instructionGlyph = NewImage("Glyph", _instructionBadge.rectTransform, Color.white);
            _instructionGlyph.preserveAspect = true;
            Place(_instructionGlyph.rectTransform, new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f), Vector2.zero, Vector2.zero);
            _instructionBadge.gameObject.SetActive(false);

            _instruction = NewText("Instruction", prt, 21, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Place(_instruction.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(28, 8), new Vector2(-12, -106));
            _instruction.textWrappingMode = TextWrappingModes.Normal;
            _instruction.overflowMode = TextOverflowModes.Ellipsis;

            // Commander-killed toast (upper middle, doesn't block clicks).
            _toast = NewImage("CommanderKilled", transform, PopupBorder);
            Place(_toast.rectTransform, new Vector2(0.24f, 0.64f), new Vector2(0.76f, 0.79f), Vector2.zero, Vector2.zero);
            FramePopup(_toast, new Color(0.62f, 0.08f, 0.08f, 0.95f));
            _toastText = NewText("Text", _toast.rectTransform, 22, FontStyles.Normal, TextAlignmentOptions.Center);
            Place(_toastText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 6), new Vector2(-14, -6));
            _toastText.textWrappingMode = TextWrappingModes.Normal;
            _toast.gameObject.SetActive(false);

            // Bid picker (top-centre): framed panel, one button per affordable amount.
            var bidImg = NewImage("BidPicker", transform, PopupBorder);
            _bidPicker = bidImg.rectTransform;
            Place(_bidPicker, new Vector2(0.26f, 0.68f), new Vector2(0.74f, 0.84f), Vector2.zero, Vector2.zero);
            bidImg.raycastTarget = true;
            var bidFill = FramePopup(bidImg, PopupFill);
            bidFill.raycastTarget = true;
            _bidButtons = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
            _bidButtons.SetParent(bidFill.transform, false);
            Stretch(_bidButtons);
            var bidLayout = _bidButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
            bidLayout.spacing = 8;
            bidLayout.padding = new RectOffset(14, 14, 12, 12);
            bidLayout.childAlignment = TextAnchor.MiddleCenter;
            bidLayout.childControlWidth = bidLayout.childControlHeight = true;
            bidLayout.childForceExpandWidth = false;
            bidLayout.childForceExpandHeight = true;
            _bidPicker.gameObject.SetActive(false);

            // Turn-position picker (top-centre).
            var orderImg = NewImage("TurnOrderPicker", transform, PopupBorder);
            _orderPicker = orderImg.rectTransform;
            Place(_orderPicker, new Vector2(0.34f, 0.70f), new Vector2(0.66f, 0.82f), Vector2.zero, Vector2.zero);
            orderImg.raycastTarget = true;
            var orderFill = FramePopup(orderImg, PopupFill);
            orderFill.raycastTarget = true;
            _orderButtons = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
            _orderButtons.SetParent(orderFill.transform, false);
            Stretch(_orderButtons);
            var pickerLayout = _orderButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
            pickerLayout.spacing = 8;
            pickerLayout.padding = new RectOffset(12, 12, 10, 10);
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
            _splashBand = NewImage("Band", splashGo.transform, PopupBorder);
            _splashBand.raycastTarget = false;
            Place(_splashBand.rectTransform, new Vector2(0, 0.42f), new Vector2(1, 0.58f), Vector2.zero, Vector2.zero);
            FramePopup(_splashBand, Color.white); // fill color overridden each splash
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

        // Gold rim around a popup: panel Image becomes the border; returns the inset fill.
        public static Image FramePopup(Image panel, Color fillColor, float thickness = PopupBorderThickness)
        {
            panel.color = PopupBorder;
            var existing = panel.transform.Find("Fill")?.GetComponent<Image>();
            if (existing != null)
            {
                existing.color = fillColor;
                return existing;
            }
            var fill = NewImage("Fill", panel.transform, fillColor);
            fill.raycastTarget = panel.raycastTarget;
            Place(fill.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(thickness, thickness), new Vector2(-thickness, -thickness));
            fill.transform.SetAsFirstSibling();
            return fill;
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
