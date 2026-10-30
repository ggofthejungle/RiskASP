# Going online: what changes in the code

Written Oct 30, 2026. The game will be played online, one player per device (hot-seat is only for local testing).
Target stack: **PlayFab** (login/accounts, via CBS as in Battle Planes) and **Unity Gaming Services** (Lobby / join codes,
and Cloud Code). CLAUDE.md also lists Photon PUN for turns: whether Photon stays is an open decision (below).

## What is already online-shaped

- **One entry point for every move.** `GameManager.HandlePlayerAction(action)` validates and applies everything:
  reinforce, attack, attack-move, fortify, end phase, buy/play command cards, hire, build, defender answers.
  Online, only the **authority** calls it with actions received from clients.
- **Turn owner vs input owner.** `CurrentPlayer` (whose turn) vs `InputPlayer` / `PendingDecision` (who must answer
  now). The reaction window is a `PendingDecision` addressed to one player: online it's sent to that player's device.
- **Serializable actions.** `ActionData` / `ActionCodec` turn actions into plain ids (player index, territory name,
  card id) for the network.
- **Local player.** `GameManager.LocalPlayer` + `IsLocal(p)`: the UI only shows the local player's hand, buttons and
  prompts. The other players' turns show "Waiting for X…". `PassTheDeviceScreens` is off by default (test only).
- **AI decides instantly** (`DefenderAI`), so the authority can answer for bots and for players who drop out.

## What has to change

1. **Every input becomes an action.** Bidding (`SubmitBid`) and turn order (`ChooseTurnPosition`) are still direct
   method calls: turn them into `BidAction` / `ChooseTurnPositionAction`. Extend `ActionCodec` to all actions:
   - reinforce, fortify, buy/play card (card id + typed targets: territory, player, commander type), hire, build;
   - add a version number to `ActionData`.
2. **One authority: the server.** With PlayFab + UGS, the natural authority is server code running the same C# rules:
   **UGS Cloud Code (C# modules)** or **PlayFab CloudScript (Azure Functions, C#)**. The game state lives server-side
   (UGS Cloud Save / PlayFab entity or group data), which also gives async play-by-turn and rejoin. The server is the
   only one that:
   - runs `HandlePlayerAction`;
   - rolls dice and shuffles;
   - runs bots.

   Clients send `ActionData`; the authority validates (the existing `IsValid` / `WhyCant…` checks) and broadcasts the
   accepted action **plus its outcome** (dice rolls, losses, card drawn). Clients apply outcomes; they never roll.
3. **Randomness through one seeded RNG.** Today `UnityEngine.Random` / `System.Random` are called in:
   - `BattleSimulator` (dice), `Abilities` (Rocket Strike, Scatter Bomb, The Mother, Assassin Bomb);
   - `CommandCards.Buy` (which card), Scout Forces, the bid tie-break, `RandomlyDistributeTroops`, territory dealing.

   Move them all behind an `IRandom` owned by the game state (seeded by the authority). That also gives replays and
   reproducible tests.
4. **Hidden information.** Hands, the bought card's identity and the Scout Forces territory are secret:
   - the authority sends the card id only to its owner;
   - everyone else gets "Bob bought a Land card" (deck + count);
   - never broadcast the deck order.

   The UI already reads hands only for `LocalPlayer`.
5. **Reaction window over the network.**
   - Add `Deadline` to `PendingDecision`.
   - The authority sends the decision to the defender's device, with a 15–20 s timer and auto-Pass on timeout.
   - If the defender disconnects, `DefenderAI` answers.
   - The attacker's device shows "Waiting for Bob…" (already there).
6. **State sync / reconnect.** A serializable snapshot of the game (owners, troops, commanders, stations, energy,
   year/turn/phase, hand sizes, the local hand) to join or rejoin a game, and for async play-by-turn later.
7. **Bots** run only on the authority. The ASP solver bots are desktop-only (they spawn clingo/DLV2): online/mobile
   needs the C# AI (MM-2).
8. **UI events.** The UI already reacts to events (`OnAttacked`, `OnPendingDecisionChanged`, phase/turn changes).
   On clients these fire when the authority's outcome is applied, so most UI code doesn't change.
9. **Longer term** (Trello "clean C# rules engine"): move the rules out of MonoBehaviours into a pure C# library, so
   the same code validates turns on a server. The action/decision/RNG work above is the bridge to that.

## Decided (Oct 30)
- **No Photon.** Every action goes to the server (UGS Cloud Code or PlayFab CloudScript), which runs the C# rules,
  stores the state and returns/pushes the outcome. Turn-based play tolerates the ~0.2–1 s per call, and it gives
  async play, rejoin and anti-cheat for free.
- Therefore the rules must run **without UnityEngine** in Cloud Code/CloudScript: the pure C# rules extraction
  (Trello "clean rules engine") is a prerequisite for server authority.
- Player choices (MOD Reduction picks, later starting placement) are `PendingDecision`s addressed to one player, so
  the server can ask that player's device.

## Suggested order (ON-1)
1. `BidAction`, `ChooseTurnPositionAction`. Full `ActionCodec` with round-trip tests for every action.
2. `IRandom` everywhere + seeded tests.
3. Server authority: client sends `ActionData` to Cloud Code / CloudScript; the server validates, applies, stores the
   state and returns/pushes the outcome. `LocalPlayer` comes from the logged-in PlayFab/UGS player.
4. Reaction-window RPC + timer + AI fallback.
5. Snapshot/rejoin. Hidden-information filtering.
