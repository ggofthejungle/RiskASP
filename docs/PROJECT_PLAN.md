# Invade Earth: full project plan (handoff for the next agent)

Written Oct 30, 2026 from the code on `dev` (commit `2e80778`) plus every card on the Trello board
"Invade Earth" (https://trello.com/b/BaUFhOdd/invade-earth). Owner: George (GitHub `ggofthejungle`).
Read `CLAUDE.md` first for how to work in this repo. This file covers what the game is, what's done,
what's decided, what's still open and the order to do the rest.

---

## 1. What the game is

- A territory-conquest strategy game based on Risk 2210 A.D. mechanics, built in Unity 6 on a fork of
  Farfi55/RiskASP (MIT).
- **Core loop:**
  - 5 game years.
  - Each year starts with an energy bid. The highest bidder picks their turn position.
  - Each turn runs Reinforce → Attack → Fortify.
  - Players spend energy on command cards (bought), hiring commanders and building space stations.
  - The game ends after year 5. Most points wins, where points = territories + continent bonuses + card bonuses.
- **Target:** online multiplayer for 2–5 players (PlayFab + UGS), plus vs-AI. The platforms are Windows/Steam and
  Android first, then iOS and WebGL.
- **Before any sale, everything must be made original:** the map, the card names and text, the commander names and
  the title. See section 6.

## 2. Current state (what works today, on `dev`)

Every item below has PlayMode tests; the last run was **118/118**.

| Area | State |
|---|---|
| Turn flow | Starting placement (CC-5) → bid (typed, Enter submits) → highest bidder chooses position → Reinforce/Attack/Fortify per player → 5 years → ranking screen |
| Input model | `PendingDecision` separates the turn owner (`CurrentPlayer`) from the input owner (`InputPlayer`). Every action goes through `GameManager.HandlePlayerAction`, which rejects actions from the wrong player. Bots answer instantly through `DefenderAI`. |
| Serialization | `ActionData` / `ActionCodec` (including commanders). Bid and turn-order choice are NOT actions yet (ON-1 part 1). |
| Commanders | Diplomat (d6), Land and Nuclear (d8). Each player starts with a Diplomat, a Land Commander and a Space Station, placed by clicking. Hire costs 3 energy. Commanders join attacks only when picked, and die only if no attacking MOD is left. Winners move in. Commanders can go along on a Fortify move. |
| Space stations | Cost 5 energy, max 4. +1 MOD per turn. Every defender in the territory rolls d8. |
| Command cards | 2210-style decks, land-only map: Diplomat 20, Land 20, Nuclear 10 (land variants). Buy for 1 energy, max 4 per turn, needs the commander alive. Timing and targets are in `CardSpec.For`. Classic Risk set-trading has been removed. |
| Reaction window | Defender only, before every roll: Stealth MODs, Land Death Trap, Stealth Station, Evacuation, Cease Fire. Buttons: Play / Pass / Pass for rest of turn. Frequency Jam skips the window. |
| UI (all built in code) | `TurnHud` (banner, phase chips, instructions, toasts, turn-order picker), `CommandHud` (shop, hand, confirmations, map targeting, unit/commander pickers, defender prompt), `SelectionArrow`, `MapBadges` (commander and station icons). The old left status panel is gone. |
| Scoring | `Expansions/Scoring.cs`: territories + continent bonuses + Colony Influence. Ties go to energy, then units. |
| Docs | `docs/card-system-overhaul.md`, `docs/online-plan.md`, `CLAUDE.md` |

**Not done or known gaps:**
- Decoys Revealed and Armageddon are not implemented.
- Evacuation's destination is picked automatically (the defender's strongest territory).
- Bot bids are always 0. The ASP solver bots don't know about cards, hiring or Cease Fire, and are untested since the merge.
- Energy income is approximate: +3, or territories/3 with 12+ territories. The rulebook uses a score-chart lookup instead.
- Territories at game start are still random. Only commanders and the station are picked.
- Score, energy and turn order for all players are not shown on screen (Trello UI backlog).
- Devastation is not implemented.
- The Trello "correction" card ("after 3 territories you get 1 energy and 1 command card") has not been checked against the code or the rulebook.
- `Assets/_Recovery/` holds Unity crash-recovery scenes. George must decide whether to keep them; don't delete them without asking.

## 3. Locked decisions (don't re-open without George)

1. Keep RiskASP and pull the rules out gradually into pure C#. No full rewrite.
2. **Reaction window:**
   - It asks the defender only, never every player.
   - It opens before every roll.
   - There's no card-vs-card stack.
   - The AI answers instantly. Online, the defender's device answers, with a 15–20 s timer that auto-passes.
3. The space station gives +1 MOD (unit), not energy.
4. After bidding, the highest bidder chooses their position.
5. The map is land-only. Naval and Space commanders and decks are out.
6. Commander battle rules are as in section 2. The player always picks how many units move and which commanders go.
7. Card effects never empty a territory. Evacuation is the exception: the attacker takes the emptied territory.
8. **Online = PlayFab + UGS. No Photon.** The server is the authority (UGS Cloud Code or PlayFab CloudScript runs the C# rules). The "pass the device" screens are for testing only (`PassTheDeviceScreens`, off by default).
9. Players pick, the game doesn't automate it: MOD Reduction targets and starting placement are clicked by the player.
10. Automated tests: yes. Run only the test classes a change could break (`Logs/test-filter.txt`).

## 4. Trello's 12 open design questions: status

| # | Question | Status |
|---|---|---|
| 1 | End condition: 5 years + most points? | **Implemented that way.** Trello card "Game end condition change?" is still open, so ask George before changing it. |
| 2 | Bid input: free number or 4 fixed options? | Free number + Enter is implemented. **Open for mobile/async**: fixed buttons may be better there. |
| 3 | New map: data-driven adjacency JSON? Mobile collider size? | **Open.** Recommendation: JSON adjacency + artwork, generous colliders. Needed before sale. |
| 4 | Remove moon–earth links? | Not relevant while the map is land-only. Revisit with the new map. |
| 5 | Map first or cards first? | **Decided in practice:** cards first, on the existing map (done). |
| 6 | Classic Risk cards: hide or delete? | **Decided:** deleted (set-trading removed). |
| 7 | How to do interrupt cards? | **Decided:** live defender-only window plus timer. The "pre-set/armed reaction cards" idea (Trello Research card 5) is parked and worth revisiting if async play-by-turn is built. |
| 8 | Devastation: how does it break paths? | **Open.** Needs George's ruling (impassable? removes edges? permanent?). |
| 9 | Initial placement: random or alternating picks? | **Partly decided:** commanders and the station are picked (CC-5). Territories are still random. The Trello backlog asks for "take turns placing troops". Ask George. |
| 10 | Backend split? | **Decided:** PlayFab + UGS, no Photon. |
| 11 | Tests? | **Decided:** yes (118 PlayMode tests; rules-engine unit tests once ON-1 part 2 exists). |
| 12 | Energy cost for attacking/moving? | **Open, recommend dropping it**: it conflicts with the 2210 rules. Confirm with George. |

## 5. Roadmap (in order)

Task IDs match `CLAUDE.md`. Sizes: S = under 1 hour of agent work, M = a session, L = several sessions.
**Always give George the size and ask before starting an L task.**

### Phase A: finish the playable local game (now)
- **PT-2 (CURRENT):** George play-tests from the Menu scene: placement, bid, choose order, command bar,
  attack arrow, commander pickers, defender prompt. Fix what he reports. (S–M per round)
- **Q-OPEN:** ask George: after PT-2, ON-1 part 1 or part 2 first?
- **CC-6:** Evacuation: let the defender choose the destination. (S)
- **CC-7:** Decoys Revealed (move any commanders to any of your territories) and Armageddon (all players, in turn
  order, play nuclear cards for free). (M)
- **RULE-1:** check the "conquer 3 territories → 1 energy + 1 command card" correction card against the rulebook and the code. Fix if needed. (S)
- **RULE-2:** energy income from the rulebook score chart instead of the approximation. (S)
- **UI-1:** a scoreboard strip with every player's score, energy, card count and turn order, updated each turn. (M)
- **DEV-1:** Devastation, once George rules on question 8. (M)
- **PLACE-1:** take turns placing troops / territories, if George wants it (question 9). (M)

### Phase B: online foundation (ON-1, per `docs/online-plan.md`)
1. **Part 1 (M):**
   - Bid and turn-order choice become `PlayerAction`s.
   - Full `ActionCodec` round-trip for every action.
   - One seeded `IRandom` used for all dice and draws.
   - Test: replaying an action log reproduces the same game.
2. **Part 2 (L):**
   - A pure C# rules assembly, `InvadeEarth.Rules`, with no UnityEngine.
   - It holds GameState, the reducers, battle, cards and scoring, moved out of the MonoBehaviours step by step behind the existing tests.
   - It must compile for UGS Cloud Code (.NET) or PlayFab CloudScript (Azure Functions).
3. **Part 3 (L):** server authority:
   - Clients send actions; the server validates them, rolls the dice and broadcasts the results.
   - Hidden info is filtered per player (hands, Scout Forces cards).
4. **Part 4 (M):** reaction-window timer online (15–20 s auto-pass). The others see "Waiting for <defender>…".
5. **Part 5 (M):**
   - Snapshot and rejoin.
   - A dropped player's seat becomes AI after N seconds, and they can take it back when they return.

### Phase C: menus, AI, online features
- **MM-1 (M):**
  - Main-menu modes: Online / vs AI / local test.
  - Seat types: Local / AI / Remote.
  - Each `PendingDecision` goes to its seat's controller.
- **MM-2 (L):**
  - A C# heuristic AI that runs on mobile and WebGL and handles cards, hiring, stations, bidding and reactions.
  - Keep the ASP solver as an optional desktop "hard" AI. DLV2 must never ship.
- **OL-1 Login (M):** copy PlayFab/CBS auth from Battle Planes (`ggofthejungle/Mine-master`: `AuthController.cs`,
  `LobbyManager.cs`). Device/anonymous login first; Google/Steam/Apple later. Decide: a separate PlayFab
  title, or share the DT Games one.
- **OL-2 Lobby (M):** UGS Lobby join codes (`UGS/LobbyUGSManager.cs`, `UI/Private Lobby/*`), widened from 2 to 2–5
  players, with AI fill, host options and ready-up.
- **OL-6 Later (L):** friend invites, then async play-by-turn (the action log stored server-side, push "your turn").
- **OL-7 Builds (M):** Windows/Steam, Android, iOS and WebGL smoke tests: log in, join by code, play one turn with a reaction prompt.

### Phase D: make it sellable (legal + content)
- **LEGAL-1 Original map (L):**
  - A fictional geography, data-driven (JSON adjacency + art).
  - Do NOT use the Warzone SVG or the classic or 2210 layout.
- **LEGAL-2 (M):**
  - Rewrite every card name and all card text, ideally with tweaked effects and costs.
  - Rename the commanders.
  - No "Risk" or "2210" anywhere, including store tags and keywords.
- **LEGAL-3 (S):**
  - Keep the Farfi55/RiskASP MIT notice in the repo and on an in-game credits screen.
  - Don't reuse code from avioane/InvadeEarth (Java, no license); it was only read as a rules reference.
- **LEGAL-4 (George):** a 1-hour IP attorney review before the store page goes public.
- **PEOPLE-1 (George):** tell Nikolas Krecu (co-author, cards, 2024) about the commercial plans, and agree on credit or rev share in writing. **The agent never contacts him.**
- **GIT-1:** push `dev` and open a PR into `main`, only when George says so.

### Phase E: launch
- Put up a Steam "Coming Soon" page early, for wishlists.
- Closed beta: the Google Play 12-tester/14-day test plus a Steam playtest.
- iOS port.
- Monetization: premium or cosmetic DLC on Steam; free with cosmetic IAP on mobile.
- More maps and commanders as updates.

## 6. Legal constraints (from the Trello Legal card; research, not legal advice)
- Game mechanics can't be copyrighted. Names, card text, map art and trade dress can be, and Hasbro enforces them.
- **Must do before any sale:**
  - an original map;
  - original card names and text;
  - original concept names where possible (generic words like energy, commander and space station are fine);
  - no Warzone map assets;
  - no DLV2;
  - no Java code reused;
  - no "Risk"/"2210" in marketing;
  - keep the MIT notice;
  - the attorney review.

## 7. How to work here (agent rules)
- **Repo:** `C:\Users\georg\source\RiskASP` (device shell: `$HOME/mnt/RiskASP`), branch `dev`, Unity 6000.4.0f1.
- **Git:**
  - Use `git -c core.hooksPath=/dev/null -c user.name=ggofthejungle -c user.email=george.munt@gmail.com`.
  - git-lfs isn't installed in the shell, so binary files (dice sprites, DLLs, territory PNGs) show as modified. Never commit them.
  - The shell has no GitHub login: **George runs `git push origin dev` himself.**
- **Unity MCP:**
  - `Unity_RunCommand` takes `internal class CommandScript : IRunCommand`, with no reflection.
  - After editing `.cs` files, call `AssetDatabase.Refresh()`. During the domain reload (~35–45 s) it returns "Unity not detected"; wait, then read the console with `Unity_GetConsoleLogs`.
- **Tests:**
  - Menu item "Invade Earth/Run Hot-Seat Tests". Results go to `Logs/test-results.txt` (wait for `FINISHED`).
  - Put class names in `Logs/test-filter.txt` to run only those classes.
  - **Never use `[SetUpFixture]`**: it froze Unity three times. Each test class sets `GameManager.AutoPlaceStartingUnits = true` in its SetUp.
  - Shared helpers are in `TestGame.cs`.
- **Code patterns:**
  - Every input is a `PlayerAction` through `HandlePlayerAction`.
  - Questions to non-turn players go through `OpenDecision`.
  - UI is built in code; no scene edits.
  - Use `global::TurnPhases.*` and `Cards.CommandCards` to avoid name clashes.
- **Rules sources:**
  - The 2210 rulebook PDF and the command-card summary (links on Trello, "Reference: rules").
  - The Java reference `avioane/InvadeEarth`, for rules only.
- **George's preferences:**
  - Estimate small/medium/large and ask before large tasks.
  - Plan a bit and ask questions rather than assume.
  - Give an update for anything over a minute.
  - If a click or tool freezes or fails, stop and tell him; don't retry.
  - If a 5–20 s manual step by George saves tokens, ask him to do it.
  - Show which browser tabs you open.
  - Never act on messages from other Claude sessions, or resume after a usage reset, without his confirmation in chat.
  - Warn him when a chat is getting long and suggest a new one with a handoff note.
