# Invade Earth (RiskASP fork): handoff for Claude

Read this first. Last updated: Oct 30, 2026 (afternoon). Owner: George (ggofthejungle).

## Where things are
- Unity project: `C:\Users\georg\source\RiskASP`, Unity **6000.4.0f1**. Work on branch **`dev`**. It is local only and NOT pushed yet.
- Trello board "Invade Earth" (https://trello.com/b/BaUFhOdd/invade-earth): lists 📌 Decisions & Rules, 🔨 NOW, ⚡ NEXT: Reaction window, 🎮 NEXT: Menu & modes, 🌐 Online, ✅ Done. Use Trello only for detail; this file is the summary.
- Battle Planes (GitHub `ggofthejungle/Mine-master`) is the source to copy the online stack from:
  - PlayFab via CBS: `AuthController.cs`, `LobbyManager.cs`
  - UGS Lobby join codes: `UGS/LobbyUGSManager.cs`, `UI/Private Lobby/*`
  - Photon PUN turns: `Multiiplayer/TurnManagerMP.cs`

## Tools and how to work here
- **Unity MCP** is set up (`com.unity.ai.assistant`, relay in Claude Desktop config). Tools:
  - `Unity_GetConsoleLogs`
  - `Unity_RunCommand`: C# that must be `internal class CommandScript : IRunCommand`; no System.Reflection.
- After editing .cs files, call `AssetDatabase.Refresh()` through RunCommand. It returns "Unity not detected" during the domain reload; wait about 35s, then continue.
- **Run tests:**
  - Start them: `EditorApplication.ExecuteMenuItem("Invade Earth/Run Hot-Seat Tests")`
  - Wait for `FINISHED` in `Logs/test-results.txt`. A full run takes about 1 minute.
- **Git** from the Linux shell on George's PC:
  - Use `git -c core.hooksPath=/dev/null -c user.name=ggofthejungle -c user.email=george.munt@gmail.com`
  - git-lfs is NOT installed there. Dice 7.jpg/8.png always show as "modified"; ignore them.
  - Needs file-delete permission on the folder, otherwise `.lock` files are left behind.
- George's preferences:
  - Ask before large tasks.
  - Show what you click.
  - Give an update for anything longer than a minute.
  - If a UI click fails, stop and ask instead of retrying.

## Done so far (dev branch)
- `bdcfc32` / `34e377f` / `d3d188a`: Unity 6.4 upgrade and deprecation cleanup. Compiles with 0 errors and 0 warnings.
- `34eef8f`: merged main into dev (commanders, space station, d7/d8 dice, declared cards) together with biddingPhase.
- `2f977be`: rewrote the bidding/turn/year flow in `GameManager` and added tests.
  - `SubmitBid(int)` validates 0..energy.
  - Each alive player gets exactly one turn per year, then `_year++` and bidding reopens.
  - Ties are broken with a stable random key.
  - `GameState.Playing` is set after the first bidding round, so bots now act. Bots auto-bid 0.
  - The game ends after `MaxYears` = 5.
  - Game scripts are now the `InvadeEarth.Game` asmdef.
  - Tests: `Assets/Tests/PlayMode/HotSeatGameTests.cs`, **7/7 passing**. Helper: `Assets/Tests/Editor/TestRunHelper.cs`.

- RW-1 (turn owner vs input owner):
  - `GameManager.InputPlayer` is `CurrentPlayer` unless a `PendingDecision` (`Scripts/Decisions/PendingDecision.cs`: PlayerId, Kind, Options) is open. `PlayerAction.IsValid` checks `InputPlayer`. No open decision (null) means a normal turn action.
  - `HandlePlayerAction` now returns bool. While a decision is open, only a `DecisionResponseAction` from the player being asked is accepted. Use `OpenDecision(decision, onAnswered)`. Phase changes close any open decision, and map clicks are ignored while one is open.
  - `AttackPhase`: `AttackAction` = declare, then the reaction window, then the roll. Hook `AttackPhase.ShouldOpenReactionWindow` defaults to false, so nothing changes yet. Options are `Pass` / `PassForRestOfTurn`. New state `AttackState.AwaitingDefender`.
  - Serializable actions: `Actions/ActionData.cs` (`ActionData` + `ActionCodec`, JsonUtility round trip) for Attack, AttackReinforce, EndPhase, DecisionResponse. Players are identified by index in `Players`, territories by name.
  - Tests: `Assets/Tests/PlayMode/InputOwnerTests.cs` (10 tests). Full suite **17/17 passing**.

- `c3e1c1e`: turn banner (top-right: player name in their color, Reinforce > Attack > Fortify stepper, instruction line), "X'S TURN" / "Pass the device" splash, and a curved attack/move arrow on the map (`UI/TurnHud.cs`, `Map/SelectionArrow.cs`, built in code, no scene edits). +4 tests (`TurnHudTests.cs`).
- Merged `origin/main` (Nikolas: Abilities.cs cards, energy income per turn, commander changes) into dev. Took main's .meta GUIDs for Commander/CommanderType. Updated 2 bidding tests for the energy income. **21/21 passing.**

- Post-merge fixes (main's card code + prefab), with `MergeRegressionTests.cs` (12 tests). **33/33 passing.**
  - Cards ran the wrong ability from card #6 on (the `Abilities.methods` list has `ModReduction`, which has no `CardType`). Cards are now matched to abilities **by name** (`Abilities.FindFor`); suffixes D/L/Na/S are stripped.
  - Commander decks were split at the wrong indexes; now done by enum group (`CardRepository.CommanderFor`): Diplomat 7, Land 8, Naval 7, Nuclear 11, Space 8.
  - `Card.UseAbility` now passes its parameters. `Abilities.gm` was never set (now `GameManager.Instance`).
  - Energy Crisis can't push opponents below 0. Death Traps / Orbital Mines leave at least 1 unit. `Territory.SetTroops` raises `OnTroopsChanged`.
  - Frequency Jam is reset at every turn start. Drawing from an empty deck no longer crashes.
  - `Territory.prefab` had `containsSpaceStation: 1`, so every territory had a station (+1 troop per territory per turn). Set to 0.
  - Arrow no longer disappears between close neighbours.
  - Open questions for Nikolas/George: Evacuation leaves the attacked territory with 0 units (intended?). Card exchange for troops is commented out in `ReinforcePhase`.

- Card system: analysis in **`docs/card-system-overhaul.md`** (classic vs 2210, turn-phase comparison incl. George's Trello table, the Java reference, Land-only map). Pilot 2210 command cards: `Cards/CommandCards.cs`, `Actions/CommandCardActions.cs`. Buy 1 energy, max 4 per turn, commander in play, Naval/Space decks off. Playable: Energy Crisis, Assemble MODs (Land). `CommandCardTests.cs` (14). **47/47 passing.**
- Reference: Java 2210 `avioane/InvadeEarth` has NO license. Read it for rules only, never copy code.

- Oct 30 (afternoon), all on `dev`, **106/106 tests**:
  - `f3cdb4d` choose turn order after bidding;
  - `1287df3` CC-1 real decks, classic cards removed;
  - `b04348d` CC-2 starting units / hire / build / conquest / d8 (defender max 2 dice);
  - `893be6a` reaction window + 5 defense cards + DefenderAI;
  - `ce47408` CC-4 remaining land-map cards, scoring, 4/5-player tests;
  - then the UI (`UI/CommandHud.cs`): command bar (hand, buy/hire/build, map targeting via `SelectionManager.BeginTargeting`) and the defender prompt;
  - `GameManager.LocalPlayer` / `IsLocal`; `PassTheDeviceScreens` (off by default, testing only).
  - Docs: `docs/card-system-overhaul.md`, `docs/online-plan.md`.

## Decisions (locked)
- **Approach:** keep RiskASP and pull the rules out gradually into pure C#. No full rewrite.
- **Reaction window (defender interrupts):**
  - It opens after an attack is declared and before **every** dice roll.
  - It only prompts THE DEFENDER (not other players, even though the rulebook lets anyone play Stealth MODs; asking everyone wastes time). Only if they hold a playable defense card (commander alive, enough energy, not jammed).
  - Multiple cards per invasion are allowed. Buttons: Play / Pass / Pass for rest of turn.
  - Frequency Jam skips the target's window for the rest of that turn.
  - There is no card-vs-card stack.
  - Who answers: the AI decides instantly; online, the defender's own device, with a 15–20 s timer that auto-passes. The "pass the device" screens exist for local testing only (`PassTheDeviceScreens`, off by default).
- **Space station** gives +1 unit (MOD) per turn, per the rules (not energy).
- **Turn order:** after bidding, the highest bidder chooses their position (2210 rule).
- **Map is land-only:** Naval/Space commanders and decks are out; the Nuclear deck has land variants only.
- **Commanders in battle** (George, Oct 30):
  - The attacker chooses which commanders join an attack (picker). Each joining commander is a unit with its own die (Land/Nuclear d8, Diplomat d6), and there are at most 3 dice in total.
  - Losses hit the attacking MODs first, so a commander only dies if no attacking MOD is left (e.g. a commander attacking alone that loses). Attacking commanders that win move in with the army.
  - When moving units (after a conquest or in Fortify), the player picks how many, and which commanders go along.
- **Card effects never empty a territory** (we have no neutral territories). Evacuation is the exception: the attacker takes the emptied territory.
  - Defense cards: Cease Fire, Stealth MODs, Evacuation, Death Trap, Stealth Station.
- **Online** means **PlayFab + UGS** (not hot-seat). **No Photon** (decided Oct 30): the server is the authority (UGS Cloud Code / PlayFab CloudScript running the C# rules). See `docs/online-plan.md`.
- **Players pick, not automation:** MOD Reduction asks each player to click their territories (done). Starting commanders/station placement will be picked by the player too (todo).
- **Automated tests are ON HOLD** (George, Oct 30). Keep code compiling; don't run or extend the suite unless asked.
- **Legal:** before any sale, rewrite all card names and text and use an ORIGINAL map (it's currently Hasbro's Risk/2210). Don't ship DLV2.

## Next steps (in order)
Every task has a unique ID. **CURRENT TASK: PT-2**, unless George says otherwise.

- **PT-2: George play-tests** the new turn flow from the Menu scene:
  - bid, then choose order;
  - command bar: buy/hire/build, play cards;
  - attack with the arrow;
  - defender prompt.

  Fix UI issues he reports.
- Done: RW-1..5, CC-1, CC-2, CC-4, the command-card UI. Still open:
  - Decoys Revealed and Armageddon (not implemented);
  - Evacuation destination is automatic (the defender's strongest territory);
- **CC-5: Pick placement of starting commanders + station** (player clicks territories, like MOD Reduction; via decisions so it works online).
- **ON-1: Online (PlayFab + UGS, no Photon)**, per `docs/online-plan.md`:
  1. bid/choose as actions + full ActionCodec;
  2. `IRandom`;
  3. server authority (needs the pure C# rules);
  4. reaction-window timer;
  5. snapshot/rejoin + hidden info.
- **MM-1: Main menu modes and seat types.** Online / vs AI / local test; seats Local / AI / Remote.
- **MM-2: C# AI for mobile/WebGL and online.** The ASP solver is desktop-only. The ASP bots don't know about command cards, cease fire, or hiring.
- **GIT-1: Push `dev` and open a PR into `main`,** only when George says so.

## Known gaps
- Energy income: +3 per turn, or territories / 3 with 12+ territories (from main, `ReinforcePhase.Start`). The rulebook uses a score-chart lookup instead; this is close enough for now. Bot bids are still always 0.
- Starting units are placed automatically (commanders on the strongest border territory, the station on the next). The rules let players place them.
- Bots: the ASP solver bots are untested since the fix.
