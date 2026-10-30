using System;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Cards;
using Decisions;
using Expansions;
using Map;
using player;
using TMPro;
using TurnPhases;
using UnityEngine;
using UnityEngine.UI;
using static UI.TurnHud;

namespace UI
{
    // 2210 command bar (bottom of the screen) for the human whose turn it is:
    //  - their command-card hand (deck colour, cost, when it can be played); click a card to play it,
    //  - Buy card (per commander deck), Hire commander, Build station,
    //  - cards/purchases that need a target switch the map to "click a territory" (SelectionManager targeting).
    // Plus the defender prompt: "Pass the device to Bob" cover -> Bob's defense cards / Pass / Pass for rest of turn
    // -> "Pass the device back to Alice". Built in code under the existing Canvas (no scene edits).
    public class CommandHud : MonoBehaviour
    {
        private GameManager _gm;
        private SelectionManager _sm;

        private RectTransform _bar, _actions, _hand;
        private TMP_Text _status;
        private readonly Dictionary<string, Button> _actionButtons = new();
        private string _handKey = "", _actionsKey = "";

        // Defender prompt
        public enum PromptStage { Hidden, Cover, Choose, HandBack }
        public PromptStage Stage { get; private set; } = PromptStage.Hidden;
        private RectTransform _prompt, _promptButtons;
        private Image _promptBand;
        private TMP_Text _promptTitle, _promptText;
        private Player _handBackTo;
        private string _promptKey = "";

        // For tests / other UI
        public bool IsBarVisible => _bar != null && _bar.gameObject.activeSelf;
        public string Status => _status != null ? _status.text : "";
        public IReadOnlyList<string> PromptOptions { get; private set; } = Array.Empty<string>();
        public string PromptText => _promptText != null ? _promptText.text : "";

        public static CommandHud Create(GameManager gm, Canvas canvas)
        {
            var go = new GameObject("CommandHud", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)go.transform);
            var hud = go.AddComponent<CommandHud>();
            hud._gm = gm;
            hud.Build();
            gm.OnPendingDecisionChanged += hud.OnDecisionChanged;
            return hud;
        }

        private void OnDestroy()
        {
            if (_gm != null) _gm.OnPendingDecisionChanged -= OnDecisionChanged;
        }

        private void Update()
        {
            if (_gm == null) return;
            if (_sm == null) _sm = SelectionManager.Instance;
            RefreshBar();
            RefreshPrompt();
        }

        // ================= Command bar =================

        private Player Me => _gm.CurrentPlayer;

        private bool BarShouldShow =>
            _gm.GameState == GameState.Playing && !_gm.IsBidding && !_gm.IsChoosingOrder
            && _gm.IsLocal(Me) && _gm.PendingDecision == null && Stage == PromptStage.Hidden;

        private void RefreshBar()
        {
            var show = BarShouldShow;
            _bar.gameObject.SetActive(show);
            if (!show)
            {
                if (_sm != null && _sm.IsTargeting) CancelTargeting();
                return;
            }

            // Action buttons (rebuilt only when something changes)
            var cc = _gm.CommandCards;
            var buys = DeckLists.DecksInPlay.Select(d => (deck: d, ok: cc.WhyCantBuy(Me, d) == null)).ToList();
            var hireable = Purchases.HireableCommanders.Where(t => !Me.HasCommanderInPlay(t)).ToList();
            var canHire = _gm.IsCommandStep(Me) && hireable.Any() && Me.GetEnergy() >= Purchases.CommanderCost;
            var canBuild = _gm.IsCommandStep(Me) && Me.GetEnergy() >= Purchases.StationCost && Purchases.StationsOf(Me) < Purchases.MaxStations;
            var key = Me.Name + string.Join("", buys.Select(b => b.ok ? "1" : "0")) + canHire + canBuild + string.Join(",", hireable)
                      + (_sm != null && _sm.IsTargeting);
            if (key != _actionsKey)
            {
                _actionsKey = key;
                Clear(_actions);
                _actionButtons.Clear();
                foreach (var (deck, ok) in buys)
                    if (Me.HasCommanderInPlay(deck))
                        AddAction($"Buy {Short(deck)} card · {CommandCards.BuyCost} energy", DeckColor(deck), ok, () => ClickBuy(deck), "buy" + deck);
                foreach (var type in hireable)
                    AddAction($"Hire {Short(type)} · {Purchases.CommanderCost} energy", DeckColor(type), canHire, () => ClickHire(type), "hire" + type);
                AddAction($"Build station · {Purchases.StationCost} energy", new Color(0.2f, 0.2f, 0.25f), canBuild, ClickBuild, "build");
                if (_sm != null && _sm.IsTargeting)
                    AddAction("Cancel", new Color(0.5f, 0.15f, 0.15f), true, CancelTargeting, "cancel");
            }

            // Hand
            var handKey = Me.Name + ":" + string.Join(",", Me.Cards.Select(c => c.Id + (Playable(c) ? "+" : "-")));
            if (handKey != _handKey)
            {
                _handKey = handKey;
                Clear(_hand);
                foreach (var card in Me.Cards)
                    AddCard(card);
                if (Me.Cards.Count == 0)
                {
                    var t = NewText("Empty", _hand, 18, FontStyles.Italic, TextAlignmentOptions.Left);
                    t.text = "No command cards. Buy them on your turn, before you attack (1 energy each, max 4 per turn).";
                    t.color = new Color(1, 1, 1, 0.75f);
                    t.gameObject.AddComponent<LayoutElement>().preferredWidth = 700;
                }
            }

            if (_sm != null && !_sm.IsTargeting && string.IsNullOrEmpty(_statusMessage))
            {
                var scouts = cc.ScoutedBy(Me).ToList();
                SetStatus(scouts.Any() ? $"Scout Forces (secret): {string.Join(", ", scouts.Select(t => Nice(t.Name)))}" : "");
            }
        }

        private bool Playable(Card c) => _gm.CommandCards.WhyCantPlay(Me, c) == null;

        // ---------- Clicks (public so tests can drive the UI) ----------

        public void ClickBuy(CommanderType deck)
        {
            Act(new BuyCommandCardAction(Me, _gm.Turn, deck), _gm.CommandCards.WhyCantBuy(Me, deck), $"Bought a {Short(deck)} card.");
        }

        public void ClickHire(CommanderType type)
        {
            var me = Me;
            Target($"Hire {Short(type)}: click one of your territories.",
                t => t.Owner == me,
                t => { Act(new HireCommanderAction(me, _gm.Turn, type, t), _gm.Purchases.WhyCantHire(me, type, t), $"{Short(type)} hired in {Nice(t.Name)}."); return true; });
        }

        public void ClickBuild()
        {
            var me = Me;
            Target("Build a space station: click one of your territories without a station.",
                t => t.Owner == me && !t.containsSpaceStation,
                t => { Act(new BuildSpaceStationAction(me, _gm.Turn, t), _gm.Purchases.WhyCantBuild(me, t), $"Space station built in {Nice(t.Name)}."); return true; });
        }

        public void ClickCard(Card card)
        {
            var me = Me;
            var why = _gm.CommandCards.WhyCantPlay(me, card);
            if (why != null) { SetStatus(why, 4f); return; }

            void Play(params object[] targets) =>
                Act(new PlayCommandCardAction(me, _gm.Turn, card, targets), _gm.CommandCards.WhyCantPlay(me, card, targets),
                    $"Played {Nice(card.Name)}.");

            switch (card.CardType)
            {
                case CardType.AssembleModsL:
                    Target($"{Nice(card.Name)}: click a territory you occupy (+3 units).", t => t.Owner == me, t => { Play(t); return true; });
                    break;
                case CardType.TerritorialStation:
                    Target($"{Nice(card.Name)}: click a territory of yours without a station.", t => t.Owner == me && !t.containsSpaceStation,
                        t => { Play(t); return true; });
                    break;
                case CardType.ReinforcementsL:
                {
                    var picked = new List<Territory>();
                    Target($"{Nice(card.Name)}: click 3 different territories you occupy (+1 each).", t => t.Owner == me,
                        t =>
                        {
                            if (!picked.Contains(t)) picked.Add(t);
                            SetStatus($"{Nice(card.Name)}: {picked.Count}/3 ({string.Join(", ", picked.Select(x => Nice(x.Name)))})");
                            if (picked.Count < 3) return false;
                            Play(picked[0], picked[1], picked[2]);
                            return true;
                        });
                    break;
                }
                case CardType.RocketStrikeLand:
                    Target($"{Nice(card.Name)}: click an opponent's territory.", t => t.Owner != me, t => { Play(t); return true; });
                    break;
                case CardType.AssassinBomb:
                    Target($"{Nice(card.Name)}: click an opponent's territory with a commander.",
                        t => t.Owner != me && t.CommandersList.Count > 0,
                        t => { Play(t, t.CommandersList.OrderByDescending(c => c.CommanderType == CommanderType.NuclearCommander).First().CommanderType); return true; });
                    break;
                case CardType.FrequencyJamL:
                    ChoosePlayer($"{Nice(card.Name)}: choose the player to jam.", me, p => Play(p));
                    break;
                default:
                    Play();
                    break;
            }
        }

        public void CancelTargeting()
        {
            if (_sm != null) _sm.EndTargeting();
            ClearChoice();
            SetStatus("");
            _actionsKey = "";
        }

        private void Act(PlayerAction action, string why, string ok)
        {
            if (why != null) { SetStatus(why, 4f); return; }
            SetStatus(_gm.HandlePlayerAction(action) ? ok : "Not allowed right now.", 3f);
            _handKey = _actionsKey = "";
        }

        private void Target(string message, Func<Territory, bool> canTarget, Func<Territory, bool> onClick)
        {
            if (_sm == null) _sm = SelectionManager.Instance;
            ClearChoice();
            _statusMessage = "";
            SetStatus(message);
            _sm.BeginTargeting(t => { var done = onClick(t); if (done) _actionsKey = ""; return done; }, canTarget);
            _actionsKey = "";
        }

        // Player choice buttons (Frequency Jam), shown in the action row.
        private RectTransform _choice;
        private void ChoosePlayer(string message, Player me, Action<Player> onChosen)
        {
            ClearChoice();
            SetStatus(message);
            _choice = new GameObject("Choice", typeof(RectTransform)).GetComponent<RectTransform>();
            _choice.SetParent(_bar, false);
            Place(_choice, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, 4), new Vector2(-8, 44));
            var layout = _choice.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false;
            foreach (var p in _gm.Players.Where(p => p != me && !p.IsDead()))
            {
                var target = p;
                MakeButton(_choice, p.Name, TurnHud.PlayerColor(p), true, () => { ClearChoice(); onChosen(target); }, 140);
            }
            MakeButton(_choice, "Cancel", new Color(0.5f, 0.15f, 0.15f), true, CancelTargeting, 100);
        }

        private void ClearChoice()
        {
            if (_choice != null) Destroy(_choice.gameObject);
            _choice = null;
        }

        // ---------- Status line ----------
        private string _statusMessage = "";
        private float _statusUntil;
        private void SetStatus(string message, float seconds = 0f)
        {
            _statusMessage = seconds > 0 ? message : "";
            _statusUntil = Time.unscaledTime + seconds;
            if (_status != null) _status.text = message;
        }

        private void LateUpdate()
        {
            if (!string.IsNullOrEmpty(_statusMessage) && Time.unscaledTime > _statusUntil)
            {
                _statusMessage = "";
                if (_status != null && (_sm == null || !_sm.IsTargeting)) _status.text = "";
            }
        }

        // ================= Defender prompt =================

        private void OnDecisionChanged(PendingDecision decision)
        {
            if (decision != null)
            {
                var defender = _gm.GetPlayerById(decision.PlayerId);
                if (!_gm.IsLocal(defender)) // AI answers instantly; online, the defender's own device asks them
                {
                    if (Stage == PromptStage.Choose || Stage == PromptStage.Cover) Stage = PromptStage.Hidden;
                    _promptKey = "";
                    return;
                }
                if (_sm != null) _sm.EndTargeting();
                var attacker = _gm.CurrentPlayer;
                // Hot-seat testing only: hide the attacker's screen until the defender takes the device.
                var cover = _gm.PassTheDeviceScreens && _gm.IsLocal(attacker) && attacker != defender;
                Stage = Stage == PromptStage.Choose || !cover ? PromptStage.Choose : PromptStage.Cover;
                _promptKey = "";
            }
            // decision == null: handled in RefreshPrompt, because playing a card can immediately open the next
            // question for the same defender (several cards per invasion).
        }

        public void ConfirmCover()
        {
            if (Stage == PromptStage.Cover) Stage = PromptStage.Choose;
            else if (Stage == PromptStage.HandBack) Stage = PromptStage.Hidden;
            _promptKey = "";
        }

        public bool Choose(string option)
        {
            var d = _gm.PendingDecision;
            if (Stage != PromptStage.Choose || d == null) return false;
            var defender = _gm.GetPlayerById(d.PlayerId);
            return _gm.HandlePlayerAction(new DecisionResponseAction(defender, _gm.Turn, option));
        }

        private void RefreshPrompt()
        {
            if ((Stage == PromptStage.Choose || Stage == PromptStage.Cover) && _gm.PendingDecision == null)
            {
                var attacker = _gm.CurrentPlayer;
                Stage = _gm.PassTheDeviceScreens && _gm.IsLocal(attacker) && _gm.GameState == GameState.Playing
                    ? PromptStage.HandBack : PromptStage.Hidden;
                _handBackTo = attacker;
                _promptKey = "";
            }

            _prompt.gameObject.SetActive(Stage != PromptStage.Hidden);
            if (Stage == PromptStage.Hidden) return;

            var d = _gm.PendingDecision;
            var key = Stage + ":" + (d == null ? "" : string.Join(",", d.Options));
            if (key == _promptKey) return;
            _promptKey = key;
            Clear(_promptButtons);
            PromptOptions = Array.Empty<string>();

            var attack = _gm.AttackPhase.PendingAttack;
            var defender = d != null ? _gm.GetPlayerById(d.PlayerId) : null;
            switch (Stage)
            {
                case PromptStage.Cover:
                    _promptBand.color = WithA(TurnHud.PlayerColor(defender), 0.97f);
                    _promptTitle.text = $"PASS THE DEVICE TO {defender?.Name.ToUpperInvariant()}";
                    _promptText.text = $"{_gm.CurrentPlayer?.Name} is invading one of {defender?.Name}'s territories.";
                    MakeButton(_promptButtons, $"I'm {defender?.Name}", new Color(0.15f, 0.15f, 0.2f), true, ConfirmCover, 260);
                    break;

                case PromptStage.Choose when d != null && attack != null:
                    _promptBand.color = WithA(TurnHud.PlayerColor(defender), 0.97f);
                    _promptTitle.text = $"{defender.Name.ToUpperInvariant()}: YOU ARE BEING INVADED";
                    _promptText.text = $"{attack.Player.Name} attacks {Nice(attack.Target.Name)} ({attack.Target.Troops} units) " +
                                       $"from {Nice(attack.Origin.Name)} ({attack.Origin.Troops} units, rolling {attack.Troops}). Play a defense card?";
                    PromptOptions = d.Options.ToList();
                    foreach (var option in d.Options)
                    {
                        var card = _gm.CommandCards.CardFromOption(defender, option);
                        var opt = option;
                        if (card != null)
                            MakeButton(_promptButtons, $"{Nice(card.Name)} · {card.PowerUpCost} energy\n<size=70%>{Blurb(card.CardType)}</size>",
                                DeckColor(card.Commander), true, () => Choose(opt), 230, 70);
                        else
                            MakeButton(_promptButtons, option == DecisionOptions.Pass ? "Pass" : "Pass for rest of turn",
                                new Color(0.25f, 0.27f, 0.33f), true, () => Choose(opt), 200, 70);
                    }
                    break;

                case PromptStage.HandBack:
                    _promptBand.color = WithA(TurnHud.PlayerColor(_handBackTo), 0.97f);
                    _promptTitle.text = $"PASS THE DEVICE BACK TO {_handBackTo?.Name.ToUpperInvariant()}";
                    _promptText.text = "The defender has answered.";
                    MakeButton(_promptButtons, $"I'm {_handBackTo?.Name}", new Color(0.15f, 0.15f, 0.2f), true, ConfirmCover, 260);
                    break;
            }
        }

        // ================= Building =================

        private void Build()
        {
            // Command bar: bottom centre (where the classic card bar was).
            var barImg = NewImage("CommandBar", transform, new Color(0.27f, 0.29f, 0.38f, 0.95f));
            _bar = barImg.rectTransform;
            Place(_bar, new Vector2(0.17f, 0.005f), new Vector2(0.865f, 0.165f), Vector2.zero, Vector2.zero);
            barImg.raycastTarget = true; // clicks on the bar don't reach the map

            _actions = Row("Actions", _bar, new Vector2(0, 1), new Vector2(0.62f, 1), new Vector2(8, -44), new Vector2(0, -6), 6);
            _status = NewText("Status", _bar, 17, FontStyles.Italic, TextAlignmentOptions.MidlineRight);
            Place(_status.rectTransform, new Vector2(0.62f, 1), new Vector2(1, 1), new Vector2(6, -44), new Vector2(-10, -6));
            _status.textWrappingMode = TextWrappingModes.Normal;
            _hand = Row("Hand", _bar, new Vector2(0, 0), new Vector2(1, 1), new Vector2(8, 6), new Vector2(-8, -50), 6);

            // Defender prompt: full screen, blocks clicks.
            var promptImg = NewImage("DefenderPrompt", transform, new Color(0, 0, 0, 0.55f));
            _prompt = promptImg.rectTransform;
            Stretch(_prompt);
            promptImg.raycastTarget = true;
            _promptBand = NewImage("Band", _prompt, Color.white);
            Place(_promptBand.rectTransform, new Vector2(0, 0.30f), new Vector2(1, 0.70f), Vector2.zero, Vector2.zero);
            _promptTitle = NewText("Title", _promptBand.rectTransform, 46, FontStyles.Bold, TextAlignmentOptions.Center);
            Place(_promptTitle.rectTransform, new Vector2(0, 0.72f), new Vector2(1, 0.97f), Vector2.zero, Vector2.zero);
            _promptText = NewText("Text", _promptBand.rectTransform, 24, FontStyles.Normal, TextAlignmentOptions.Center);
            Place(_promptText.rectTransform, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.72f), Vector2.zero, Vector2.zero);
            _promptButtons = Row("Buttons", _promptBand.rectTransform, new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.42f), Vector2.zero, Vector2.zero, 10);
            _promptButtons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            _prompt.gameObject.SetActive(false);
        }

        private static RectTransform Row(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, float spacing)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Place(rt, aMin, aMax, oMin, oMax);
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return rt;
        }

        private void AddAction(string label, Color color, bool interactable, Action onClick, string id)
        {
            _actionButtons[id] = MakeButton(_actions, label, color, interactable, onClick, 0);
        }

        public bool IsActionEnabled(string id) => _actionButtons.TryGetValue(id, out var b) && b.interactable;

        private void AddCard(Card card)
        {
            var playable = Playable(card);
            var label = $"<b>{Nice(card.Name)}</b>\n<size=75%>{Short(card.Commander)} · {card.PowerUpCost} energy · {TimingLabel(card.Timing)}</size>";
            var b = MakeButton(_hand, label, DeckColor(card.Commander), true, () => ClickCard(card), 165);
            b.GetComponent<Image>().color = WithA(DeckColor(card.Commander), playable ? 1f : 0.45f);
        }

        private static Button MakeButton(Transform parent, string label, Color color, bool interactable, Action onClick,
            float width, float height = 0)
        {
            var img = NewImage("Button", parent, color);
            img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.interactable = interactable;
            var colors = button.colors;
            colors.disabledColor = new Color(1, 1, 1, 0.35f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            var text = NewText("Label", img.rectTransform, 17, FontStyles.Normal, TextAlignmentOptions.Center);
            text.text = label;
            text.color = ReadableOn(color);
            text.textWrappingMode = TextWrappingModes.Normal;
            Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 2), new Vector2(-6, -2));
            var le = img.gameObject.AddComponent<LayoutElement>();
            if (width > 0) le.preferredWidth = width;
            else le.preferredWidth = Mathf.Max(120, text.GetPreferredValues(label).x + 20);
            if (height > 0) le.preferredHeight = height;
            return button;
        }

        private static void Clear(Transform t)
        {
            foreach (Transform child in t) Destroy(child.gameObject);
        }

        // ================= Labels =================

        private static Color WithA(Color c, float a) => new(c.r, c.g, c.b, a);

        public static Color DeckColor(CommanderType t) => t switch
        {
            CommanderType.Diplomat => new Color(0.05f, 0.45f, 0.35f),
            CommanderType.LandCommander => new Color(0.55f, 0.42f, 0.18f),
            CommanderType.NuclearCommander => new Color(0.9f, 0.45f, 0.08f),
            _ => new Color(0.3f, 0.3f, 0.35f),
        };

        public static string Short(CommanderType t) => t switch
        {
            CommanderType.Diplomat => "Diplomat",
            CommanderType.LandCommander => "Land",
            CommanderType.NuclearCommander => "Nuclear",
            CommanderType.NavalCommander => "Naval",
            _ => "Space",
        };

        private static string TimingLabel(CardTiming t) => t switch
        {
            CardTiming.BeforeFirstInvasion => "before attacking",
            CardTiming.OpponentInvades => "when invaded",
            CardTiming.EndOfTurn => "in Fortify",
            CardTiming.EndOfGame => "end of game",
            _ => "not yet",
        };

        // Our own short wording (card names/texts get rewritten before release anyway).
        public static string Blurb(CardType t) => t switch
        {
            CardType.StealthModsL => "+3 defenders here",
            CardType.LandDeathTrap => "attacking territory loses half",
            CardType.StealthStation => "build a station here (d8 defence)",
            CardType.Evacuation => "move everyone out; they take it",
            CardType.CeaseFire => "stop this attack; no more on you this turn",
            _ => "",
        };
    }
}
