# Invade Earth (RiskASP fork): handoff for Claude

Read this first. Last updated: Oct 30, 2026. Owner: George (ggofthejungle).

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

## Decisions (locked)
- **Approach:** keep RiskASP and pull the rules out gradually into pure C#. No full rewrite.
- **Reaction window (defender interrupts):**
  - It opens after an attack is declared and before **every** dice roll.
  - It only prompts a defender who holds a playable defense card (commander alive and enough energy).
  - Multiple cards per invasion are allowed. Buttons: Play / Pass / Pass for rest of turn.
  - Frequency Jam skips the target's window for the rest of that turn.
  - There is no card-vs-card stack.
  - Who answers: hot-seat shows a "pass the device" screen; the AI decides instantly; online uses an RPC with a 15–20s timer that auto-passes.
  - Defense cards: Cease Fire, Stealth MODs, Evacuation, Death Trap, Stealth Station.
- **Online** uses the Battle Planes stack (PlayFab/CBS + UGS Lobby + Photon PUN) on all platforms. Async play-by-turn comes later.
- **Legal:** before any sale, rewrite all card names and text and use an ORIGINAL map (it's currently Hasbro's Risk/2210). Don't ship DLV2.

## Next steps (in order)
Every task has a unique ID. When George says "do RW-1", that is the task. **CURRENT TASK: RW-2** (RW-1 done), unless George says otherwise.

- **PT-1: Hot-seat play-test (George does it).** Play from the Menu scene with 2 humans. Check the bid rejection message, that both players get a turn, and that year 2 bidding opens. Fix any UI issues he reports. Not a blocker for RW-1.

### Reaction window (defender interrupts): do these in order, each one depends on the one before
- **RW-1: Separate turn owner from input owner (refactor).** ✅ DONE (see "Done so far"; the names differ slightly from this plan).
  - Problem: `GameManager` assumes the only player who ever gives input is `CurrentPlayer`, so a defender can't answer during the attacker's turn. That's why the old interrupt attempt broke everything.
  - Add `PendingDecision { Player Player; DecisionKind Kind; ... }` to `GameManager`, separate from `CurrentPlayer`. Kinds: `TurnAction` (normal) and `Reaction` (defender).
  - Inputs go through `HandlePlayerAction(action)`, which validates `action.Player == PendingDecision.Player` (not just the current player).
  - Start with **AttackPhase only**. Add `DeclareAttack` / `PassReaction` / `PlayReactionCard` actions, but the window doesn't open yet. With no defense cards it behaves exactly as now.
  - Done when: HotSeatGameTests still 7/7, and a new EditMode or PlayMode test proves that an action from a non-pending player is rejected.
- **RW-2: Open the window in AttackPhase before EVERY dice roll.**
  - Implement by setting `AttackPhase.ShouldOpenReactionWindow` and adding `Card:<id>` options to the decision.
  - After `DeclareAttack`: if the defender has a playable defense card (commander alive, enough energy) and isn't jammed or passed for the turn, set `PendingDecision` to the defender (Reaction). Otherwise roll immediately.
  - The defender plays 0+ cards, then Done / Pass / PassRestOfTurn. Recheck that the attack is still valid, then roll.
  - Tests: no card → no window; window opens every roll; PassRestOfTurn is honored.
- **RW-3: Defender prompt UI.**
  - A hot-seat "Pass the device to <Defender>" cover screen.
  - A card list with energy costs and the Play / Pass / Pass-rest-of-turn buttons.
  - A log line for the attacker.
- **RW-4: AI defender.** A simple C# heuristic that answers Reaction decisions instantly.
- **RW-5: Defense cards plus Frequency Jam, each with tests.**
  - Cease Fire (blocks further attacks on that player this turn)
  - Stealth MODs (+3 defenders, can be played several times)
  - Evacuation
  - Death Trap
  - Stealth Station
  - Frequency Jam (the attacker plays it; the target gets no window for the rest of the turn)

### After the reaction window
- **MM-1: Main menu modes and seat types.** Hot-seat / vs AI / online; each seat is Local / AI / Remote.
- **MM-2: C# AI for mobile and WebGL.** The ASP solver is desktop-only.
- **ON-1: Online.** Copy the login, lobby and PUN sync from Battle Planes (see above). The reaction window goes over RPC with a 15–20s timer.
- **GIT-1: Push `dev` and open a PR into `main`,** only when George says so.

## Known gaps
- Energy income each year isn't implemented (only 3 at the start), so later bids are mostly 0.
- Commanders and the space station are test-placed on the first territory in `SetupGame()`; remove that once buying them works.
- Bots: the ASP solver bots are untested since the fix.
