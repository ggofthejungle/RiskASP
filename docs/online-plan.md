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

## Host lockstep: host migration and secrets (Nov 6, night)
For now one player's device is the host (`OnlineMatch`); a Cloud Code server is later.

**Host migration (built, not play-tested).**
- Host and join options use `.WithHostMigration(new LockstepMigrationData())`. The room's own snapshot is only the
  entry count, because every device already has the whole game and the log.
- When the host leaves, the room picks a new host (`SessionHostChanged`) and moves the Relay network to it.
- The new host's `OnlineMatch` turns `IsHost` on, drops out-of-order entries it was waiting for, starts its own
  secret random stream, marks the old host's seat as away (a sequenced seat entry), and restarts a bot that was
  mid-turn (`GameManager.ResumeBots`).
- Clients see "Lost the host. Moving the match…", reconnect to the new host (`OnlineNet.ConnectedToHost`), and ask
  for entries from their count.
- A client that has more entries than the new host (it got an entry the new host missed) gets `ahead` and reloads
  and replays the new host's log.
- If the room can't migrate (`MigrationFailed`), players are told to rejoin by code.
- Limits:
  - An action sent to the old host that never came back is lost; the player does it again.
  - Migration data is uploaded every 5 s, and only with 2 or more players.

**Secret dice and draws (built, not play-tested).**
- The public seed only sets up the match (deal and scene start).
- From the first entry, the host draws from `RecordingRandom` with a crypto-random seed. Every number drawn while
  applying an entry goes out in `Envelope.Rolls`.
- Clients apply each entry with `ReplayRandom` returning those numbers in order. They log an error if the counts
  differ; the board hash catches the rest.
- Result: knowing the seed no longer tells anyone future dice, card draws or tie-breaks.

**Hidden hands (built Nov 7, probed, not play-tested online).**
- Every rule whose result depends on a hand goes through `Secrets` (`Cards/Secrets.cs`). `OnlineMatch` is the
  `ISecretSource`.
  - `Secrets.Public(work)`: the host works the answer out and adds it to the entry's `Facts`. The other devices take
    the host's answer in order.
  - `Secrets.Private(owner, work)`: the same, but the answer goes only to the owner's device (`Envelope.Private`,
    stored per seat in `_privateLog`; each client gets its own copy of every entry).
  - Local games have no source, so everything is worked out on the spot (single player, pass and play and the tests
    behave as before).
- Draws (`CommandCards.Draw`, used by buying and the conquest bonus):
  - The host picks the card from `DrawRandom`. That is a second secret stream whose numbers are never sent.
  - The deck it came from is public; the card id is private to the owner.
  - Other devices add a face-down card (`Card.FaceDown(deck)`, `IsHidden`, never playable).
  - Every device lowers the public `CardRepository.DeckCount`. `CardsInDeck` now means "cards this device can't place":
    exactly the deck on the host, the deck plus other players' hidden cards elsewhere.
- Hand-dependent answers that come from the host:
  - whether the reaction window opens, and whether to ask again after a card;
  - whether an Armageddon player has a free card;
  - every AI answer to a decision (`DefenderAI`, for bots and away players).
- Questions to a player whose hand a device can't see list no card options there (`PendingDecision.AnyCard`), so any
  `Card:<id>` answer is accepted. The host has already checked it.
- Reveals:
  - Playing a card reveals it (`CommandCards.Reveal`, called from `CardFromOption` and the play action's decoding).
  - When a player is eliminated, the winner's device is told the card ids.
  - At game over, every hand is revealed so Colony Influence scores agree.
- `BoardHash` covers public state only: hand sizes and deck counts.
- After a host migration the new host has face-down cards too:
  - Each reconnecting client's sync request lists the cards it holds (`Batch`), and the host accepts them if they fit.
  - Face-down cards nobody claims (bots, away players) are dealt from the cards nobody can account for
    (`ResolveFaceDownCards`, also run before the host works out anything hidden).
  - After every sync the host sends the client its hand (`KindHand`), applied once the client has caught up to the
    same entry. If the host had to deal a player's cards again, that player's hand changes; this is rare.
- Still public: Scout Forces' chosen territory (picked with the recorded dice, so every device knows it). A device's
  memory still holds the face-down count and the deck of each card.

## Save / resume (Nov 7)

**SAVE-1, local.** Single player and pass-and-play write `Application.persistentDataPath/saves/local.json`:
seed, match options, seats (name / color / config), and every top-level accepted action. Nested bot bids
and defender AI answers are not stored; they replay from the seed. The Play page shows Continue. Game over
deletes the file. Tests and a scene loaded without the menu do not write a save.

**OL-6, correspondence (started Nov 7).** PlayFab, not a live UGS room.
- Shared Group Data: public meta + private gzipped `MatchSave.File` (chunked). Groups: join by code.
- Host lobby admits applicants. Start writes seats (name|color|config|PlayFabId) and seed.
- Each device downloads, replays, `ReactionTimeoutSeconds = 0`. The player to move uploads.
- Inbox: Groups membership + private user data `ie.matches`.
- Account: display name, optional username/email/password (`AddUsernamePassword` / `LoginWithPlayFab`).
- Live rooms (UGS lockstep) stay on Multiplayer → Play live.
- "Your turn" today: open the game, Your games list. No Firebase.
- Lock-screen push is optional later. Android would use FCM (free message quota). iOS uses APNs (Apple, free). Windows/Steam would not use FCM. Do not add Firebase Auth/Firestore or a paid plan.
- Still later: Cloud Code as the only writer (CAS).
