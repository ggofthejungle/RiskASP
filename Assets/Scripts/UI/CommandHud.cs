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
using TurnPhases.Selection;
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
            RefreshMovePicker();
            ParkPopups();
        }

        // Bids, dice, and the other questions sit in the water west of South America, not across the map.
        private void ParkPopups()
        {
            float stack = 0f;
            if (_prompt != null && _prompt.gameObject.activeSelf && _promptBand != null)
            {
                ParkBySouthAmerica(_promptBand.rectTransform, 0f);
                stack = _promptBand.rectTransform.sizeDelta.y;
            }
            else if (_picker != null && _picker.gameObject.activeSelf)
            {
                ParkBySouthAmerica(_picker, 0f);
                stack = _picker.sizeDelta.y;
            }
            else if (_confirm != null && _confirm.gameObject.activeSelf)
            {
                ParkBySouthAmerica(_confirm, 0f);
                stack = _confirm.sizeDelta.y;
            }
            if (_gm.TurnHud != null) _gm.TurnHud.OceanStack = stack;
        }

        // ================= Unit / commander picker =================
        // One panel for: attacking (how many units, which commanders join), moving in after a conquest,
        // and fortify moves (how many units, which commanders go along).
        public enum PickerMode { None, Attack, MoveIn, Fortify }
        public PickerMode Picker { get; private set; } = PickerMode.None;
        public int PickerAmount { get; private set; }
        public int PickerMin { get; private set; }
        public int PickerMax { get; private set; }
        public IReadOnlyCollection<CommanderType> PickerCommanders => _pickedCommanders;
        private readonly List<CommanderType> _pickedCommanders = new();
        private List<CommanderType> _availableCommanders = new();
        private Territory _pickFrom, _pickTo;
        private Action<int, List<CommanderType>> _onPick;
        private Action _onPickCancel;
        private RectTransform _picker;
        private TMP_Text _pickerTitle, _pickerAmount;
        private RectTransform _pickerToggles, _pickerButtons;
        // Move-in: how many units (MODs or commanders) must still cross. Fortify: 1.
        private int _moveQuota;

        // Called by the attack selection when the attacking territory has commanders.
        public void AskAttack(Player p, Territory from, Territory to, Action<int, List<CommanderType>> onAttack, Action onCancel)
        {
            var available = from.GetAvailableTroops();
            var commanders = from.CommandersList.Select(c => c.CommanderType).ToList();
            // Pre-pick the commanders who roll d8 in this battle. MODs plus commanders never exceed 3 dice.
            var picked = commanders.Where(t => BattleSimulator.AttacksWithD8(t, from, to)).Take(BattleSimulator.MaxAttackDice).ToList();
            var mods = Mathf.Min(BattleSimulator.MaxAttackDice - picked.Count, available);
            OpenPicker(PickerMode.Attack, from, to,
                min: 0, max: Mathf.Min(BattleSimulator.MaxAttackDice, available), amount: mods,
                commanders, picked, onAttack, onCancel);
        }

        private void OpenPicker(PickerMode mode, Territory from, Territory to, int min, int max, int amount,
            List<CommanderType> commanders, List<CommanderType> picked, Action<int, List<CommanderType>> onOk, Action onCancel)
        {
            Picker = mode;
            _pickFrom = from;
            _pickTo = to;
            PickerMin = min;
            PickerMax = max;
            PickerAmount = Mathf.Clamp(amount, min, max);
            _availableCommanders = commanders;
            _pickedCommanders.Clear();
            _pickedCommanders.AddRange(picked);
            _onPick = onOk;
            _onPickCancel = onCancel;
            _moveQuota = mode == PickerMode.Attack ? 0 : min;
            ClampAttackComposition();
            RebuildPicker();
        }

        // Attack: MODs + joining commanders share the 3-dice cap.
        // Move and fortify: one unit stays behind, and a commander counts.
        private int AttackModMax => Picker == PickerMode.Attack
            ? Mathf.Max(PickerMin, Mathf.Min(PickerMax, BattleSimulator.MaxAttackDice - _pickedCommanders.Count))
            : PickerMax;

        private void ClampAttackComposition()
        {
            if (Picker == PickerMode.Attack)
            {
                while (_pickedCommanders.Count > BattleSimulator.MaxAttackDice)
                    _pickedCommanders.RemoveAt(_pickedCommanders.Count - 1);
                if (PickerAmount > AttackModMax)
                    PickerAmount = AttackModMax;
                return;
            }
            if (Picker == PickerMode.None || _pickFrom == null) return;
            var maxMods = AttackReinforceAction.MaxMods(_pickFrom, _pickedCommanders.Count);
            var minMods = Mathf.Max(0, _moveQuota - _pickedCommanders.Count);
            if (minMods > maxMods) minMods = maxMods;
            PickerMin = minMods;
            PickerMax = Mathf.Max(minMods, maxMods);
            PickerAmount = Mathf.Clamp(PickerAmount, PickerMin, maxMods);
        }

        public void SetPickerAmount(int n)
        {
            PickerAmount = Mathf.Clamp(n, PickerMin, AttackModMax);
            RebuildPicker();
        }

        public void TogglePickerCommander(CommanderType type)
        {
            if (_pickedCommanders.Contains(type)) _pickedCommanders.Remove(type);
            else if (_availableCommanders.Contains(type))
            {
                if (Picker == PickerMode.Attack && _pickedCommanders.Count >= BattleSimulator.MaxAttackDice) return;
                if (Picker == PickerMode.Attack && PickerAmount + _pickedCommanders.Count >= BattleSimulator.MaxAttackDice)
                    PickerAmount = Mathf.Max(PickerMin, BattleSimulator.MaxAttackDice - _pickedCommanders.Count - 1);
                if (Picker != PickerMode.Attack && _pickFrom != null)
                {
                    var next = _pickedCommanders.Count + 1;
                    var maxMods = AttackReinforceAction.MaxMods(_pickFrom, next);
                    if (Mathf.Max(0, _moveQuota - next) > maxMods)
                    {
                        var units = _pickFrom.Troops + _pickFrom.CommandersList.Count;
                        _gm.TurnHud?.Flash($"Leave 1 unit in {Nice(_pickFrom.Name)}. A commander counts. You can move {Mathf.Max(0, units - 1)} of {units}.");
                        return;
                    }
                }
                _pickedCommanders.Add(type);
            }
            ClampAttackComposition();
            RebuildPicker();
        }

        public bool ConfirmPicker()
        {
            if (Picker == PickerMode.None) return false;
            if (Picker == PickerMode.Attack && PickerAmount + _pickedCommanders.Count < 1) return false;
            if (Picker != PickerMode.Attack && _pickFrom != null)
            {
                var units = _pickFrom.Troops + _pickFrom.CommandersList.Count;
                var leaving = PickerAmount + _pickedCommanders.Count;
                if (PickerAmount > _pickFrom.Troops || leaving > units - 1)
                {
                    _gm.TurnHud?.Flash($"Leave 1 unit in {Nice(_pickFrom.Name)}. A commander counts. You can move {Mathf.Max(0, units - 1)} of {units}.");
                    RebuildPicker();
                    return false;
                }
                if (leaving < _moveQuota)
                {
                    _gm.TurnHud?.Flash($"Move at least {_moveQuota} into {Nice(_pickTo.Name)}. You have {leaving} selected.");
                    RebuildPicker();
                    return false;
                }
            }
            var ok = _onPick;
            var amount = PickerAmount;
            var cmds = _pickedCommanders.ToList();
            ClosePicker();
            ok?.Invoke(amount, cmds);
            return true;
        }

        public void CancelPicker()
        {
            var cancel = _onPickCancel;
            ClosePicker();
            cancel?.Invoke();
        }

        private void ClosePicker()
        {
            Picker = PickerMode.None;
            _onPick = null;
            _onPickCancel = null;
            if (_picker != null) _picker.gameObject.SetActive(false);
        }

        // Opens the picker by itself after a conquest and when a fortify move has both ends chosen.
        private void RefreshMovePicker()
        {
            var me = Me;
            var mine = _gm.GameState == GameState.Playing && _gm.IsLocal(me) && _gm.PendingDecision == null;

            // After a conquest: how many units move in, and which other commanders follow.
            var conquered = mine && _gm.CurrentPhase is AttackPhase ap && ap.State == AttackState.Fortifying;
            if (conquered && Picker == PickerMode.None)
            {
                var last = _gm.AttackPhase.LastAttackResult;
                var from = last.Origin;
                var min = AttackReinforceAction.MinToMove(last.AttackAction, last.DefendingTroops > 0);
                var max = from.GetAvailableTroops();
                var attack = last.AttackAction;
                OpenPicker(PickerMode.MoveIn, from, last.Target, min, max, max,
                    from.CommandersList.Select(c => c.CommanderType).Where(t => Purchases.CanStand(t, last.Target)).ToList(), new List<CommanderType>(),
                    (n, cmds) => _gm.HandlePlayerAction(new AttackReinforceAction(me, _gm.Turn, _gm.AttackPhase.AttackTurn, attack, n, cmds)),
                    null);
                BeginMoveInClick(last.Target);
            }
            else if (!conquered && Picker == PickerMode.MoveIn) ClosePicker();
            if (Picker != PickerMode.MoveIn) EndMoveInClick();

            // Fortify: both territories chosen.
            var fortify = mine && _gm.CurrentPhase is FortifyPhase && _sm != null
                          && _sm.CurrentSelectionPhase is FortifySelectionPhase fs && fs.From != null && fs.To != null;
            if (fortify && Picker == PickerMode.None)
            {
                var fs2 = (FortifySelectionPhase)_sm.CurrentSelectionPhase;
                var from = fs2.From;
                var to = fs2.To;
                var max = from.GetAvailableTroops();
                OpenPicker(PickerMode.Fortify, from, to, Mathf.Min(1, max), max, max,
                    from.CommandersList.Select(c => c.CommanderType).Where(t => Purchases.CanStand(t, to)).ToList(), new List<CommanderType>(),
                    (n, cmds) => _gm.HandlePlayerAction(new FortifyAction(me, _gm.Turn, from, to, n, cmds)),
                    () => fs2.CancelSelection());
            }
            else if (!fortify && Picker == PickerMode.Fortify) ClosePicker();

            if (Picker == PickerMode.Attack && !(mine && _gm.CurrentPhase is AttackPhase)) ClosePicker();
        }

        string MoveTitle(string from, string to, bool conquered)
        {
            var units = _pickFrom.Troops + _pickFrom.CommandersList.Count;
            var can = Mathf.Max(0, units - 1);
            var head = conquered ? $"You took <b>{to}</b>. Move from <b>{from}</b>." : $"Move from <b>{from}</b> to <b>{to}</b>.";
            var quota = _moveQuota > 1 ? $" At least <b>{_moveQuota}</b>." : "";
            return $"{head}\n<size=75%>{units} units here. You can move <b>{can}</b>. Leave <b>1</b> behind. A commander counts.{quota}</size>";
        }

        string MoveAmountLine()
        {
            var units = _pickFrom.Troops + _pickFrom.CommandersList.Count;
            var leaving = PickerAmount + _pickedCommanders.Count;
            var stay = units - leaving;
            var mods = PickerAmount == 1 ? "1 MOD" : $"{PickerAmount} MODs";
            var with = _pickedCommanders.Count == 0 ? "" : " + " + string.Join(", ", _pickedCommanders.Select(Map.MapBadges.FullName));
            return $"Moving {mods}{with}. <b>{stay}</b> stays in {Nice(_pickFrom.Name)}.";
        }

        private void RebuildPicker()
        {
            _picker.gameObject.SetActive(Picker != PickerMode.None);
            if (Picker == PickerMode.None) return;
            var from = Nice(_pickFrom.Name);
            var to = Nice(_pickTo.Name);
            _pickerTitle.text = Picker switch
            {
                PickerMode.Attack => $"Attack <b>{to}</b> from <b>{from}</b>\n<size=75%>Max 3 dice total — each commander takes one of those slots (d8/d6). " +
                                     "Losses hit MODs first; a commander dies only if no attacking MOD is left.</size>",
                PickerMode.MoveIn => MoveTitle(from, to, true),
                _ => MoveTitle(from, to, false),
            };
            if (Picker == PickerMode.Attack)
            {
                var parts = new List<string>();
                if (PickerAmount > 0) parts.Add($"{PickerAmount} MOD{(PickerAmount == 1 ? "" : "s")}");
                parts.AddRange(_pickedCommanders.Select(Map.MapBadges.FullName));
                var dice = PickerAmount + _pickedCommanders.Count;
                _pickerAmount.text = parts.Count == 0
                    ? "Pick units and/or commanders"
                    : $"{string.Join(" + ", parts)}  ·  {dice} dice";
            }
            else
                _pickerAmount.text = MoveAmountLine();

            Clear(_pickerToggles);
            if (_availableCommanders.Count > 0)
            {
                Header(_pickerToggles, Picker == PickerMode.Attack ? "ATTACK\nWITH" : "MOVE\nTOO", 70);
                foreach (var type in _availableCommanders.Distinct())
                {
                    var t = type;
                    var on = _pickedCommanders.Contains(t);
                    var die = Picker == PickerMode.Attack ? (BattleSimulator.AttacksWithD8(t, _pickFrom, _pickTo) ? " (d8)" : " (d6)") : "";
                    var canToggle = on || Picker != PickerMode.Attack
                        || PickerAmount + _pickedCommanders.Count < BattleSimulator.MaxAttackDice
                        || PickerAmount > PickerMin; // can free a MOD slot by auto-reducing
                    MakeButton(_pickerToggles, $"{(on ? "[x]" : "[  ]")} {Map.MapBadges.FullName(t)}{die}",
                        on ? Map.MapBadges.TypeColor(t) : new Color(0.25f, 0.27f, 0.33f), canToggle || on, () => TogglePickerCommander(t), 0, 40);
                }
            }

            Clear(_pickerButtons);
            var modMax = AttackModMax;
            MakeButton(_pickerButtons, "Min", new Color(0.25f, 0.27f, 0.33f), PickerAmount > PickerMin, () => SetPickerAmount(PickerMin), 70, 44);
            MakeButton(_pickerButtons, "-", new Color(0.25f, 0.27f, 0.33f), PickerAmount > PickerMin, () => SetPickerAmount(PickerAmount - 1), 50, 44);
            MakeButton(_pickerButtons, "+", new Color(0.25f, 0.27f, 0.33f), PickerAmount < modMax, () => SetPickerAmount(PickerAmount + 1), 50, 44);
            MakeButton(_pickerButtons, "Max", new Color(0.25f, 0.27f, 0.33f), PickerAmount < modMax, () => SetPickerAmount(modMax), 70, 44);
            var okLabel = Picker switch { PickerMode.Attack => "Roll dice", PickerMode.MoveIn => "Move in", _ => "Move" };
            var leavingNow = PickerAmount + _pickedCommanders.Count;
            var unitsNow = _pickFrom != null ? _pickFrom.Troops + _pickFrom.CommandersList.Count : 0;
            var canOk = Picker == PickerMode.Attack
                ? leavingNow >= 1
                : leavingNow >= _moveQuota && leavingNow <= unitsNow - 1 && PickerAmount <= _pickFrom.Troops;
            MakeButton(_pickerButtons, okLabel, new Color(0.12f, 0.55f, 0.25f), canOk, () => ConfirmPicker(), 150, 44);
            if (_onPickCancel != null)
                MakeButton(_pickerButtons, "Cancel", new Color(0.45f, 0.15f, 0.15f), true, CancelPicker, 110, 44);
            else
            {
                // Same width as Cancel, so "Move in" lands exactly where "Roll dice" was.
                var spacer = new GameObject("Spacer", typeof(RectTransform));
                spacer.transform.SetParent(_pickerButtons, false);
                spacer.AddComponent<LayoutElement>().preferredWidth = 110;
            }
        }

        // After a conquest, clicking the conquered territory again also confirms "Move in".
        private bool _moveInTargeting;

        private void BeginMoveInClick(Territory conquered)
        {
            if (_sm == null) _sm = SelectionManager.Instance;
            if (_sm == null || _sm.IsTargeting) return;
            _moveInTargeting = true;
            _sm.BeginTargeting(t =>
            {
                if (Picker != PickerMode.MoveIn || t != _pickTo) return false;
                ConfirmPicker();
                return true;
            }, t => t == conquered);
        }

        private void EndMoveInClick()
        {
            if (!_moveInTargeting) return;
            _moveInTargeting = false;
            if (_sm != null && _sm.IsTargeting) _sm.EndTargeting();
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
                CancelConfirm();
                if (_sm != null && _sm.IsTargeting && !_removalTargeting) CancelTargeting();
                return;
            }

            // Action buttons (rebuilt only when something changes)
            var cc = _gm.CommandCards;
            var buys = DeckLists.DecksInPlay.Select(d => (deck: d, ok: cc.WhyCantBuy(Me, d) == null)).ToList();
            var hireable = Purchases.ForThisMap().Where(t => !Me.HasCommanderInPlay(t)).ToList();
            var canHire = _gm.IsCommandStep(Me) && hireable.Any() && Me.GetEnergy() >= Purchases.CommanderCost;
            var canBuild = _gm.IsCommandStep(Me) && Me.GetEnergy() >= Purchases.StationCost && Purchases.StationsOf(Me) < Purchases.MaxStations;
            var unseen = cc.HasUnseen(Me);
            var shopOpen = _gm.IsCommandStep(Me) && !_gm.ShopClosed;
            var key = Me.Name + string.Join("", buys.Select(b => b.ok ? "1" : "0")) + canHire + canBuild + unseen + shopOpen + string.Join(",", hireable)
                      + (_sm != null && _sm.IsTargeting);
            if (key != _actionsKey)
            {
                _actionsKey = key;
                Clear(_actions);
                _actionButtons.Clear();
                Header(_actions, "SHOP", 72);
                foreach (var (deck, ok) in buys)
                    if (Me.HasCommanderInPlay(deck))
                        AddAction($"Buy {Short(deck)}\n{CommandCards.BuyCost} energy", DeckColor(deck), ok, () => ClickBuy(deck), "buy" + deck);
                foreach (var type in hireable)
                    AddAction($"Hire {Short(type)}\n{Purchases.CommanderCost} energy", DeckColor(type), canHire, () => ClickHire(type), "hire" + type,
                        Map.MapBadges.IconFor(type));
                AddAction($"Station\n{Purchases.StationCost} energy", new Color(0.2f, 0.2f, 0.25f), canBuild, ClickBuild, "build",
                    Map.MapBadges.StationIcon());
                if (unseen)
                    AddAction("Look at\nnew cards", new Color(0.45f, 0.35f, 0.1f), true,
                        () => _gm.CommandCards.RevealBought(Me), "look");
                if (shopOpen)
                    AddAction("Done hiring", new Color(0.15f, 0.35f, 0.22f), true,
                        () => Act(new CloseShopAction(Me, _gm.Turn), null, "Hiring and buying are finished. You can play cards."),
                        "closeshop");
                if (_sm != null && _sm.IsTargeting)
                    AddAction("Cancel", new Color(0.5f, 0.15f, 0.15f), true, CancelTargeting, "cancel");
            }

            // Hand
            var handKey = Me.Name + ":" + string.Join(",", Me.Cards.Select(c => c.Id + (c.Sealed ? "?" : "") + (Playable(c) ? "+" : "-")));
            if (handKey != _handKey)
            {
                _handKey = handKey;
                Clear(_hand);
                Header(_hand, "YOUR\nCARDS", 70);
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
            var me = Me;
            var why = _gm.CommandCards.WhyCantBuy(me, deck);
            if (why != null) { SetStatus(why, 4f); return; }
            Ask($"Buy a <b>{Short(deck)}</b> command card for {CommandCards.BuyCost} energy?\n" +
                $"<size=80%>You get a random card from the {Short(deck)} deck ({_gm.CommandCards.CardsLeftInDeck(deck)} left). " +
                $"You have {me.GetEnergy()} energy; {CommandCards.MaxBuysPerTurn - _gm.CommandCards.BuysThisTurn} buys left this turn.</size>",
                "Buy", () =>
                {
                    var before = me.Cards.ToList();
                    Act(new BuyCommandCardAction(me, _gm.Turn, deck), _gm.CommandCards.WhyCantBuy(me, deck), "");
                    var got = me.Cards.Except(before).FirstOrDefault();
                    if (got != null)
                        SetStatus($"You got <b>{Nice(got.Name)}</b> ({TimingLabel(got.Timing)}). It's in your cards below.", 6f);
                });
        }

        // ---------- Confirmation ----------
        private RectTransform _confirm;
        private TMP_Text _confirmText;
        private RectTransform _confirmButtons;
        private Action _onConfirm;
        public bool IsConfirming => _confirm != null && _confirm.gameObject.activeSelf;
        public string ConfirmText => IsConfirming ? _confirmText.text : "";

        private void Ask(string question, string yes, Action onYes)
        {
            _onConfirm = onYes;
            _confirmText.text = question;
            Clear(_confirmButtons);
            MakeButton(_confirmButtons, yes, new Color(0.12f, 0.55f, 0.25f), true, Confirm, 160, 48);
            MakeButton(_confirmButtons, "Cancel", new Color(0.45f, 0.15f, 0.15f), true, CancelConfirm, 140, 48);
            _confirm.gameObject.SetActive(true);
        }

        private void Show(string text)
        {
            _onConfirm = null;
            _confirmText.text = text;
            Clear(_confirmButtons);
            MakeButton(_confirmButtons, "Close", new Color(0.25f, 0.27f, 0.33f), true, CancelConfirm, 140, 48);
            _confirm.gameObject.SetActive(true);
        }

        public void Confirm()
        {
            var action = _onConfirm;
            CancelConfirm();
            action?.Invoke();
        }

        public void CancelConfirm()
        {
            _onConfirm = null;
            if (_confirm != null) _confirm.gameObject.SetActive(false);
        }

        public void ClickHire(CommanderType type)
        {
            var me = Me;
            Ask($"Hire a <b>{Map.MapBadges.FullName(type)}</b> for {Purchases.CommanderCost} energy?\n" +
                $"<size=80%>{CommanderInfo(type)} Then click one of your territories to place it.</size>",
                "Hire", () => Target($"Hire {Short(type)}: click one of your territories.",
                t => t.Owner == me && Purchases.CanStand(type, t),
                t => { Act(new HireCommanderAction(me, _gm.Turn, type, t), _gm.Purchases.WhyCantHire(me, type, t), $"{Short(type)} hired in {Nice(t.Name)}."); return true; }));
        }

        private static string CommanderInfo(CommanderType type) => type switch
        {
            CommanderType.Diplomat => "Opens the Diplomat deck. Defends with an 8-sided die; attacks with a normal one.",
            CommanderType.LandCommander => "Opens the Land deck. Attacks and defends with an 8-sided die.",
            CommanderType.NuclearCommander => "Opens the Nuclear deck. Attacks and defends with an 8-sided die.",
            _ => "",
        };

        public void ClickBuild()
        {
            var me = Me;
            Ask($"Build a <b>space station</b> for {Purchases.StationCost} energy?\n" +
                "<size=80%>+1 unit there every turn, and all its defenders roll 8-sided dice. Max 4. " +
                "Then click one of your territories without a station.</size>",
                "Build", () => Target("Build a space station: click one of your territories without a station.",
                t => t.Owner == me && t.Kind == Map.TerrainKind.Land && !t.containsSpaceStation,
                t => { Act(new BuildSpaceStationAction(me, _gm.Turn, t), _gm.Purchases.WhyCantBuild(me, t), $"Space station built in {Nice(t.Name)}."); return true; }));
        }

        // Clicking a card shows what it does first. Play is only offered when it can be played now.
        public void ClickCard(Card card)
        {
            if (card != null && card.Sealed)
            {
                Ask("You bought these face-down. Look at all of them now?\n<size=80%>You won't be able to buy any more this turn.</size>",
                    "Look", () => _gm.CommandCards.RevealBought(Me));
                return;
            }
            var why = _gm.CommandCards.WhyCantPlay(Me, card);
            var text = $"<b>{card.Name}</b>  <size=75%>{Short(card.Commander)} · {card.PowerUpCost} energy · {TimingLabel(card.Timing)}</size>\n" +
                       $"<size=85%>{Describe(card.CardType)}</size>";
            if (why == null)
                Ask(text, "Play", () => PlayCard(card));
            else if (card.Timing == CardTiming.OpponentInvades)
                Show(text + "\n<size=75%><i>You'll be asked to play it when someone invades you.</i></size>");
            else
                Show(text + $"\n<size=75%><i>{why}</i></size>");
        }

        public void PlayCard(Card card)
        {
            var me = Me;
            var why = _gm.CommandCards.WhyCantPlay(me, card);
            if (why != null) { SetStatus(why, 4f); return; }

            void Play(params object[] targets) =>
                Act(new PlayCommandCardAction(me, _gm.Turn, card, targets), _gm.CommandCards.WhyCantPlay(me, card, targets),
                    $"Played {card.Name}.");

            switch (card.CardType)
            {
                case CardType.AssembleModsL:
                case CardType.AssembleModsNa:
                case CardType.AssembleModsS:
                    Target($"{card.Name}: click a territory you occupy (+3 units).",
                        t => t.Owner == me && (Map.Frontiers.CardTerrain(card.CardType) == null || t.Kind == Map.Frontiers.CardTerrain(card.CardType)),
                        t => { Play(t); return true; }, Wrong(card));
                    break;
                case CardType.TerritorialStation:
                    Target($"{card.Name}: click a territory of yours without a station.", t => t.Owner == me && !t.containsSpaceStation,
                        t => { Play(t); return true; });
                    break;
                case CardType.ReinforcementsL:
                case CardType.ReinforcementsNa:
                case CardType.ReinforcementsS:
                {
                    var kind = Map.Frontiers.CardTerrain(card.CardType);
                    var held = me.Territories.Where(t => kind == null || t.Kind == kind).ToList();
                    if (held.Count == 0)
                    {
                        SetStatus("You don't hold any territories of that kind.", 4f);
                        break;
                    }
                    if (held.Count < 3)
                    {
                        Play(held.Cast<object>().ToArray());
                        break;
                    }
                    var picked = new List<Territory>();
                    Target($"{card.Name}: click 3 different territories you occupy (+1 each).",
                        t => t.Owner == me && (kind == null || t.Kind == kind),
                        t =>
                        {
                            if (!picked.Contains(t)) picked.Add(t);
                            SetStatus($"{card.Name}: {picked.Count}/3 ({string.Join(", ", picked.Select(x => Nice(x.Name)))})");
                            if (picked.Count < 3) return false;
                            Play(picked[0], picked[1], picked[2]);
                            return true;
                        }, Wrong(card));
                    break;
                }
                case CardType.RocketStrikeLand:
                case CardType.RocketStrikeWater:
                case CardType.RocketStrikeMoon:
                    Target($"{card.Name}: click an opponent's territory.",
                        t => t.Owner != me && (Map.Frontiers.CardTerrain(card.CardType) == null || t.Kind == Map.Frontiers.CardTerrain(card.CardType)),
                        t => { Play(t); return true; }, Wrong(card));
                    break;
                case CardType.AssassinBomb:
                    Target($"{card.Name}: click an opponent's territory with a commander.",
                        t => t.Owner != me && t.CommandersList.Count > 0,
                        t => { Play(t, t.CommandersList.OrderByDescending(c => c.CommanderType == CommanderType.NuclearCommander).First().CommanderType); return true; });
                    break;
                case CardType.FrequencyJamL:
                case CardType.FrequencyJamNa:
                case CardType.FrequencyJamS:
                    ChoosePlayer($"{card.Name}: choose the player to jam.", me, p => Play(p));
                    break;
                case CardType.DecoyRevealed:
                    PickDecoyMoves(card, me, moves => Play(moves));
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
            var accepted = _gm.HandlePlayerAction(action);
            if (!accepted) SetStatus("Not allowed right now.", 3f);
            else if (!string.IsNullOrEmpty(ok)) SetStatus(ok, 3f);
            _handKey = _actionsKey = "";
        }

        private void Target(string message, Func<Territory, bool> canTarget, Func<Territory, bool> onClick,
            Func<Territory, string> whyNot = null)
        {
            if (_sm == null) _sm = SelectionManager.Instance;
            ClearChoice();
            _statusMessage = "";
            SetStatus(message);
            _sm.BeginTargeting(t => { var done = onClick(t); if (done) _actionsKey = ""; return done; }, canTarget,
                whyNot ?? (t => "You can't use " + Nice(t.Name) + " for this."));
            _actionsKey = "";
        }

        // A click on a grey territory while aiming a card. The ground comes first, then the card's own rule.
        private Func<Territory, string> Wrong(Card card) => t =>
        {
            var need = Map.Frontiers.CardTerrain(card.CardType);
            if (need != null && t.Kind != need)
            {
                var ground = need == Map.TerrainKind.Sea ? "water" : need == Map.TerrainKind.Moon ? "Moon" : "land";
                return card.Name + " is only played on " + ground + " territories.";
            }
            var why = card.Spec.CheckTargets?.Invoke(Me, new object[] { t });
            return string.IsNullOrEmpty(why) ? "You can't use " + Nice(t.Name) + " for this." : why;
        };

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

        // Decoys Revealed: click a territory with a commander, pick which one if there are several, click where
        // it goes; repeat, then Done. Positions are tracked here so a commander can be moved twice.
        private void PickDecoyMoves(Card card, Player me, Action<object[]> onDone)
        {
            var moves = new List<object>();
            var at = new Dictionary<Territory, List<CommanderType>>();
            List<CommanderType> In(Territory t)
            {
                if (!at.TryGetValue(t, out var list))
                    at[t] = list = t.CommandersList.Select(c => c.CommanderType).ToList();
                return list;
            }
            var title = card.Name;

            void Buttons(IEnumerable<(string label, Action onClick)> extra)
            {
                ClearChoice();
                _choice = new GameObject("Choice", typeof(RectTransform)).GetComponent<RectTransform>();
                _choice.SetParent(_bar, false);
                Place(_choice, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, 4), new Vector2(-8, 44));
                var layout = _choice.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 6; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false;
                foreach (var (label, onClick) in extra)
                    MakeButton(_choice, label, new Color(0.18f, 0.32f, 0.55f), true, onClick, 150);
                if (moves.Count > 0)
                    MakeButton(_choice, $"Done ({moves.Count / 3})", new Color(0.15f, 0.45f, 0.2f), true, () =>
                    {
                        CancelTargeting();
                        onDone(moves.ToArray());
                    }, 110);
                MakeButton(_choice, "Cancel", new Color(0.5f, 0.15f, 0.15f), true, CancelTargeting, 100);
            }

            void PickDestination(Territory from, CommanderType type)
            {
                Target($"{title}: click where your {MapBadges.FullName(type)} goes.", t => t.Owner == me && t != from, to =>
                {
                    In(from).Remove(type);
                    In(to).Add(type);
                    moves.Add(from); moves.Add(type); moves.Add(to);
                    PickSource();
                    return false;
                });
                Buttons(Array.Empty<(string, Action)>());
            }

            void PickSource()
            {
                var done = moves.Count == 0 ? "" : $" ({moves.Count / 3} planned; Done to play)";
                Target($"{title}: click a territory with a commander to move{done}.", t => t.Owner == me && In(t).Count > 0, from =>
                {
                    var types = In(from).Distinct().ToList();
                    if (types.Count == 1)
                    {
                        PickDestination(from, types[0]);
                        return false;
                    }
                    SetStatus($"{title}: which commander in {Nice(from.Name)}?");
                    Buttons(types.Select(type => (MapBadges.FullName(type), (Action)(() => PickDestination(from, type)))));
                    return false;
                });
                Buttons(Array.Empty<(string, Action)>());
            }

            PickSource();
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
            if (decision != null && IsTerritoryDecision(decision))
            {
                if (decision.Kind is DecisionKind.EvacuationDestination or DecisionKind.ArmageddonTarget
                    && (Stage == PromptStage.Choose || Stage == PromptStage.Cover))
                {
                    Stage = PromptStage.Hidden;
                    _promptKey = "";
                }
                BeginRemoval(decision);
                return;
            }
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

        // ---------- MOD Reduction: the local player clicks their territories ----------
        private bool _removalTargeting;

        private void BeginRemoval(PendingDecision decision)
        {
            var who = _gm.GetPlayerById(decision.PlayerId);
            if (!_gm.IsLocal(who)) return; // AI answers instantly; online, it's asked on their device
            if (_sm == null) _sm = SelectionManager.Instance;
            var names = new HashSet<string>(decision.Options);
            _removalTargeting = true;
            _sm.BeginTargeting(t =>
            {
                var d = _gm.PendingDecision;
                if (d == null || !IsTerritoryDecision(d) || !d.HasOption(t.Name)) return false;
                _gm.HandlePlayerAction(new DecisionResponseAction(_gm.GetPlayerById(d.PlayerId), _gm.Turn, t.Name));
                return false; // the next question re-arms the targeting; Update ends it when MOD Reduction is over
            }, t => names.Contains(t.Name));
        }

        private static bool IsTerritoryDecision(PendingDecision d) =>
            d.Kind is DecisionKind.RemoveUnits or DecisionKind.PlaceStartingUnit or DecisionKind.EvacuationDestination
                or DecisionKind.ArmageddonTarget or DecisionKind.ClaimTerritory or DecisionKind.PlaceStartingTroops;

        public bool ClickRemoval(Territory t)
        {
            var d = _gm.PendingDecision;
            if (d == null || !IsTerritoryDecision(d) || !d.HasOption(t.Name)) return false;
            return _gm.HandlePlayerAction(new DecisionResponseAction(_gm.GetPlayerById(d.PlayerId), _gm.Turn, t.Name));
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
            if (_removalTargeting && (_gm.PendingDecision == null || !IsTerritoryDecision(_gm.PendingDecision)
                                      || !_gm.IsLocal(_gm.InputPlayer)))
            {
                _removalTargeting = false;
                if (_sm != null) _sm.EndTargeting();
            }
            if ((Stage == PromptStage.Choose || Stage == PromptStage.Cover) && _gm.PendingDecision == null)
            {
                var attacker = _gm.CurrentPlayer;
                Stage = _gm.PassTheDeviceScreens && _gm.IsLocal(attacker) && _gm.GameState == GameState.Playing
                    ? PromptStage.HandBack : PromptStage.Hidden;
                _handBackTo = attacker;
                _promptKey = "";
            }

            _prompt.gameObject.SetActive(Stage != PromptStage.Hidden);
            if (Stage == PromptStage.Hidden)
            {
                ClearAttackGlow();
                return;
            }

            var d = _gm.PendingDecision;
            var key = Stage + ":" + (d?.Options == null ? "" : string.Join(",", d.Options));
            if (key == _promptKey)
            {
                ShowCountdown();
                return;
            }
            _promptKey = key;
            _promptTitleBase = null;
            Clear(_promptButtons);
            PromptOptions = Array.Empty<string>();

            var attack = _gm.AttackPhase.PendingAttack;
            var defender = d != null ? _gm.GetPlayerById(d.PlayerId) : null;
            switch (Stage)
            {
                case PromptStage.Cover:
                    SetPromptBandColor(WithA(TurnHud.PlayerColor(defender), 0.97f));
                    _promptTitle.text = $"PASS THE DEVICE TO {defender?.Name.ToUpperInvariant()}";
                    _promptText.text = d?.Kind == DecisionKind.ArmageddonCard
                        ? $"Armageddon: {defender?.Name} may play nuclear cards for free."
                        : $"{_gm.CurrentPlayer?.Name} is invading one of {defender?.Name}'s territories.";
                    MakeButton(_promptButtons, $"I'm {defender?.Name}", new Color(0.15f, 0.15f, 0.2f), true, ConfirmCover, 260);
                    break;

                case PromptStage.Choose when d != null && d.Kind == DecisionKind.ArmageddonCard:
                    SetPromptBandColor(WithA(TurnHud.PlayerColor(defender), 0.97f));
                    _promptTitleBase = $"{defender.Name.ToUpperInvariant()}: ARMAGEDDON";
                    _promptTitle.text = _promptTitleBase;
                    _promptText.text = "Play any of your nuclear cards for free (no energy). Pass when you're done.";
                    PromptOptions = d.Options.ToList();
                    foreach (var option in d.Options)
                    {
                        var card = _gm.CommandCards.CardFromOption(defender, option);
                        var opt = option;
                        if (card != null)
                            MakeButton(_promptButtons, $"{card.Name} · free\n<size=70%>{Blurb(card.CardType)}</size>",
                                DeckColor(card.Commander), true, () => Choose(opt), 230, 70);
                        else
                            MakeButton(_promptButtons, "Pass", new Color(0.25f, 0.27f, 0.33f), true, () => Choose(opt), 200, 70);
                    }
                    break;

                case PromptStage.Choose when d != null && d.Kind == DecisionKind.DefenseDice:
                    PaintPrompt(defender);
                    _promptTitleBase = "HOW MANY DICE?";
                    _promptTitle.text = _promptTitleBase;
                    _promptText.text = AttackRoute(attack) + "\nRoll 1 or 2. Each die you roll can lose you a unit.";
                    GlowAttack(attack);
                    PromptOptions = d.Options.ToList();
                    foreach (var option in d.Options)
                    {
                        var opt = option;
                        MakeButton(_promptButtons, opt == "1" ? "1 die" : "2 dice",
                            new Color(0.18f, 0.28f, 0.42f), true, () => Choose(opt), 180, 70);
                    }
                    break;

                case PromptStage.Choose when d != null && attack != null:
                    PaintPrompt(defender);
                    _promptTitleBase = "YOU ARE BEING INVADED";
                    _promptTitle.text = _promptTitleBase;
                    _promptText.text = AttackRoute(attack) + $" They roll {attack.Dice}. Play a defense card?";
                    GlowAttack(attack);
                    PromptOptions = d.Options.ToList();
                    foreach (var option in d.Options)
                    {
                        var card = _gm.CommandCards.CardFromOption(defender, option);
                        var opt = option;
                        if (card != null)
                            MakeButton(_promptButtons, $"{card.Name} · {card.PowerUpCost} energy\n<size=70%>{Blurb(card.CardType)}</size>",
                                DeckColor(card.Commander), true, () => Choose(opt), 230, 70);
                        else
                            MakeButton(_promptButtons, option == DecisionOptions.Pass ? "Pass" : "Pass for rest of turn",
                                new Color(0.25f, 0.27f, 0.33f), true, () => Choose(opt), 200, 70);
                    }
                    break;

                case PromptStage.HandBack:
                    SetPromptBandColor(WithA(TurnHud.PlayerColor(_handBackTo), 0.97f));
                    _promptTitle.text = $"PASS THE DEVICE BACK TO {_handBackTo?.Name.ToUpperInvariant()}";
                    _promptText.text = "The defender has answered.";
                    MakeButton(_promptButtons, $"I'm {_handBackTo?.Name}", new Color(0.15f, 0.15f, 0.2f), true, ConfirmCover, 260);
                    break;
            }
            ShowCountdown();
        }

        private string _promptTitleBase;

        // Online reaction window: "Auto-pass in 14s". Local games have no limit, so nothing is added.
        private void ShowCountdown()
        {
            if (_promptTitleBase == null || Stage != PromptStage.Choose) return;
            var left = _gm.DecisionSecondsLeft;
            _promptTitle.text = double.IsInfinity(left)
                ? _promptTitleBase
                : $"{_promptTitleBase}  <size=75%>· auto-pass in {Mathf.CeilToInt((float)left)}s</size>";
        }

        Territory _glowFrom, _glowTo;

        static string AttackRoute(AttackAction attack)
        {
            if (attack == null || attack.Origin == null || attack.Target == null) return "You are being attacked.";
            return $"{attack.Player.Name} attacks {Nice(attack.Target.Name)} from {Nice(attack.Origin.Name)}.";
        }

        void PaintPrompt(Player defender)
        {
            var color = PlayerColor(defender);
            SetPromptBandColor(WithA(color, 0.97f));
            var ink = ReadableOn(color);
            _promptTitle.color = ink;
            _promptText.color = ink;
        }

        void GlowAttack(AttackAction attack)
        {
            ClearAttackGlow();
            if (attack == null) return;
            _glowFrom = attack.Origin;
            _glowTo = attack.Target;
            Glow(_glowFrom);
            Glow(_glowTo);
        }

        static void Glow(Territory territory)
        {
            var map = SelectionManager.Instance;
            if (map == null || territory == null) return;
            if (map.TerritoryToSelectionMap.TryGetValue(territory, out var selection) && selection != null)
                selection.Select();
        }

        void ClearAttackGlow()
        {
            Unglow(_glowFrom);
            Unglow(_glowTo);
            _glowFrom = _glowTo = null;
        }

        static void Unglow(Territory territory)
        {
            var map = SelectionManager.Instance;
            if (map == null || territory == null) return;
            if (map.TerritoryToSelectionMap.TryGetValue(territory, out var selection) && selection != null && selection.IsSelected)
                selection.Unselect();
        }

        private void SetPromptBandColor(Color fill)
        {
            _promptBand.color = PopupBorder;
            var inner = _promptBand.transform.Find("Fill")?.GetComponent<Image>();
            if (inner != null) inner.color = fill;
            else _promptBand.color = fill;
        }

        // ================= Building =================

        private void Build()
        {
            // Command bar: bottom centre (where the classic card bar was).
            var barImg = NewImage("CommandBar", transform, new Color(0.27f, 0.29f, 0.38f, 0.95f));
            _bar = barImg.rectTransform;
            Place(_bar, new Vector2(0.12f, 0.004f), new Vector2(0.90f, 0.20f), Vector2.zero, Vector2.zero);
            barImg.raycastTarget = true; // clicks on the bar don't reach the map

            _actions = Row("Actions", _bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -78), new Vector2(-10, -8), 8);
            _status = NewText("Status", _bar, 18, FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
            Place(_status.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -108), new Vector2(-14, -80));
            _status.textWrappingMode = TextWrappingModes.Normal;
            _status.overflowMode = TextOverflowModes.Ellipsis;
            _hand = Row("Hand", _bar, new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, 8), new Vector2(-10, -112), 8);

            // Confirmation box (buy / hire / build), just above the bar.
            var confirmImg = NewImage("Confirm", transform, PopupBorder);
            _confirm = confirmImg.rectTransform;
            _confirm.anchorMin = _confirm.anchorMax = new Vector2(0.5f, 0.5f);
            _confirm.pivot = new Vector2(0.5f, 0.5f);
            _confirm.sizeDelta = new Vector2(440f, 170f);
            confirmImg.raycastTarget = true;
            FramePopup(confirmImg, PopupFill).raycastTarget = true;
            _confirmText = NewText("Question", _confirm, 22, FontStyles.Normal, TextAlignmentOptions.Center);
            Place(_confirmText.rectTransform, new Vector2(0, 0.40f), new Vector2(1, 1), new Vector2(16, 0), new Vector2(-16, -8));
            _confirmText.textWrappingMode = TextWrappingModes.Normal;
            _confirmButtons = Row("Buttons", _confirm, new Vector2(0, 0), new Vector2(1, 0.38f), new Vector2(16, 10), new Vector2(-16, 0), 12);
            _confirmButtons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            _confirm.gameObject.SetActive(false);

            // Unit / commander picker (attack, move in, fortify), above the bar.
            var pickerImg = NewImage("Picker", transform, PopupBorder);
            _picker = pickerImg.rectTransform;
            _picker.anchorMin = _picker.anchorMax = new Vector2(0.5f, 0.5f);
            _picker.pivot = new Vector2(0.5f, 0.5f);
            _picker.sizeDelta = new Vector2(480f, 240f);
            pickerImg.raycastTarget = true;
            FramePopup(pickerImg, PopupFill).raycastTarget = true;
            _pickerTitle = NewText("Title", _picker, 24, FontStyles.Normal, TextAlignmentOptions.Center);
            Place(_pickerTitle.rectTransform, new Vector2(0, 0.66f), new Vector2(1, 1), new Vector2(14, 0), new Vector2(-14, -6));
            _pickerTitle.textWrappingMode = TextWrappingModes.Normal;
            _pickerAmount = NewText("Amount", _picker, 26, FontStyles.Bold, TextAlignmentOptions.Center);
            Place(_pickerAmount.rectTransform, new Vector2(0, 0.50f), new Vector2(1, 0.66f), Vector2.zero, Vector2.zero);
            _pickerToggles = Row("Commanders", _picker, new Vector2(0, 0.27f), new Vector2(1, 0.49f), new Vector2(14, 0), new Vector2(-14, 0), 8);
            _pickerToggles.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            _pickerButtons = Row("Buttons", _picker, new Vector2(0, 0.03f), new Vector2(1, 0.25f), new Vector2(14, 0), new Vector2(-14, 0), 8);
            _pickerButtons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            _picker.gameObject.SetActive(false);

            // Defender prompt: a card in the water west of South America. The map stays visible.
            var promptImg = NewImage("DefenderPrompt", transform, new Color(0, 0, 0, 0f));
            _prompt = promptImg.rectTransform;
            Stretch(_prompt);
            promptImg.raycastTarget = false;
            _promptBand = NewImage("Band", _prompt, PopupBorder);
            _promptBand.rectTransform.anchorMin = _promptBand.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _promptBand.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _promptBand.rectTransform.sizeDelta = new Vector2(460f, 230f);
            _promptBand.raycastTarget = true;
            FramePopup(_promptBand, Color.white);
            _promptTitle = NewText("Title", _promptBand.rectTransform, 26, FontStyles.Bold, TextAlignmentOptions.Center);
            _promptTitle.textWrappingMode = TextWrappingModes.Normal;
            Place(_promptTitle.rectTransform, new Vector2(0, 0.72f), new Vector2(1, 0.97f), new Vector2(12, 0), new Vector2(-12, -4));
            _promptText = NewText("Text", _promptBand.rectTransform, 20, FontStyles.Normal, TextAlignmentOptions.Center);
            _promptText.textWrappingMode = TextWrappingModes.Normal;
            Place(_promptText.rectTransform, new Vector2(0.04f, 0.40f), new Vector2(0.96f, 0.72f), Vector2.zero, Vector2.zero);
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

        private void AddAction(string label, Color color, bool interactable, Action onClick, string id, Sprite icon = null)
        {
            _actionButtons[id] = MakeButton(_actions, label, color, interactable, onClick, 0, icon: icon);
        }

        private static void Header(Transform row, string text, float width)
        {
            var t = NewText("Header", row, 18, FontStyles.Bold, TextAlignmentOptions.Center);
            t.text = text;
            t.color = new Color(1f, 0.85f, 0.4f);
            t.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
        }

        public bool IsActionEnabled(string id) => _actionButtons.TryGetValue(id, out var b) && b.interactable;

        private void AddCard(Card card)
        {
            var playable = Playable(card);
            var label = card.Sealed
                ? $"<b>Unseen card</b>\n<size=75%>{Short(card.Commander)} deck · face-down</size>"
                : $"<b>{card.Name}</b>\n<size=75%>{Short(card.Commander)} · {card.PowerUpCost} energy · {TimingLabel(card.Timing)}</size>";
            var b = MakeButton(_hand, label, DeckColor(card.Commander), true, () => ClickCard(card), 165);
            b.GetComponent<Image>().color = WithA(DeckColor(card.Commander), playable ? 1f : 0.45f);
        }

        private static Button MakeButton(Transform parent, string label, Color color, bool interactable, Action onClick,
            float width, float height = 0, Sprite icon = null)
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
            float leftPad = 6f;
            if (icon != null)
            {
                var iconImg = NewImage("Icon", img.rectTransform, Color.white);
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                Place(iconImg.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, -14), new Vector2(36, 14));
                leftPad = 40f;
            }
            var text = NewText("Label", img.rectTransform, 20, FontStyles.Normal, TextAlignmentOptions.Center);
            text.text = label;
            text.color = ReadableOn(color);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(leftPad, 2), new Vector2(-6, -2));
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width > 0 ? width : 176f;
            le.minWidth = width > 0 ? Mathf.Min(width, 110f) : 120f;
            le.preferredHeight = height > 0 ? height : 66f;
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
            CardType.StealthModsL or CardType.StealthModsNa or CardType.StealthModsS => "+3 defenders here",
            CardType.LandDeathTrap or CardType.WaterDeathTrap or CardType.OrbitalMines => "attacking territory loses half",
            CardType.StealthStation => "build a station here (d8 defence)",
            CardType.Evacuation => "move everyone out; they take it",
            CardType.CeaseFire => "stop this attack; no more on you this turn",
            _ => "",
        };

        // Longer text for the card preview.
        public static string Describe(CardType t) => t switch
        {
            CardType.CeaseFire => "When you are invaded: stop the attack. That player can't attack you again this turn.",
            CardType.ColonyInfluenceD or CardType.ColonyInfluenceL or CardType.ColonyInfluenceNa or CardType.ColonyInfluenceS => "End of game: +3 points if this card's commander is still alive.",
            CardType.DecoyRevealed => "Before your first invasion: move any of your commanders to any territories you hold.",
            CardType.EnergyCrisis => "Before your first invasion: take 1 energy from every opponent who has some.",
            CardType.Evacuation => "When you are invaded: move every unit and commander out to another territory of yours. The attacker takes the empty territory.",
            CardType.ModReduction => "Before your first invasion: each opponent removes 4 units, then you remove 2. Everyone picks their own territories.",
            CardType.Redeployment => "In Fortify: make one extra free move after you finish attacking.",
            CardType.TerritorialStation => "Before your first invasion: build a space station on one of your territories.",
            CardType.AssembleModsL or CardType.AssembleModsNa or CardType.AssembleModsS => "Before your first invasion: +3 units on one territory you hold of this commander's ground.",
            CardType.FrequencyJamL or CardType.FrequencyJamNa or CardType.FrequencyJamS => "Before your first invasion: pick a player. They can't play command cards during your turn.",
            CardType.LandDeathTrap or CardType.WaterDeathTrap or CardType.OrbitalMines => "When you are invaded on this commander's ground: the attacking territory loses half its units (rounded up, 1 always stays).",
            CardType.ReinforcementsL or CardType.ReinforcementsNa or CardType.ReinforcementsS => "Before your first invasion: +1 unit on each of 3 territories you hold. If you hold fewer, one on each.",
            CardType.ScoutForces => "Before your first invasion: a secret enemy territory is drawn for you. When you take it, 5 extra units land there.",
            CardType.StealthModsL or CardType.StealthModsNa or CardType.StealthModsS => "When you are invaded on this commander's ground: +3 defending units there.",
            CardType.StealthStation => "When you are invaded: build a space station in that territory (its defenders roll 8-sided dice).",
            CardType.Armageddon => "Everyone, in turn order, may play any number of nuclear cards for free.",
            CardType.AssassinBomb => "Before your first invasion: pick an opponent's commander and roll an 8-sided die. 3 or more kills it.",
            CardType.RocketStrikeLand or CardType.RocketStrikeWater or CardType.RocketStrikeMoon => "Before your first invasion: pick an opponent's territory of this card's ground and roll a die. That many units die (1 always stays).",
            CardType.ScatterBombLand or CardType.ScatterBombWater or CardType.ScatterBombMoon => "Before your first invasion: 3 random territories of this card's ground are hit. Opponents there lose half their units.",
            CardType.TheMother => "Before your first invasion: roll a die for a continent. Every territory there loses 1 unit, yours too.",
            CardType.AquaBrother => "Before your first invasion: roll a die. One group of seas loses 1 unit in each territory (1 always stays). A 6 hits every sea.",
            CardType.NickyBoy => "Before your first invasion: roll a die. That moon territory loses 1 unit (1 always stays).",
            CardType.HiddenEnergy => "Before your first invasion: a sea is drawn. If you still hold it when your turn ends, gain 4 energy.",
            CardType.EnergyExtraction => "Before your first invasion: if you hold every moon territory when your turn ends, gain 7 energy.",
            CardType.InvadeSurface => "Before your first invasion: a land you don't hold is drawn. This turn any moon territory may attack it.",
            _ => Blurb(t),
        };
    }
}
