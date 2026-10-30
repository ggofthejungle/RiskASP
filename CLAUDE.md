# Invade Earth (RiskASP fork): handoff for Claude

Read this first. Last updated: Oct 29, 2026. Owner: George (ggofthejungle).

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
1. George play-tests hot-seat from the Menu scene: bid validation message, both players get a turn, year 2 bidding. Fix any UI issues he reports.
2. **Reaction window, step 1:** separate turn owner from input owner. Add `PendingDecision { PlayerId, Kind, Options }` and serializable actions, starting with AttackPhase.
3. Step 2: open the window in AttackPhase before each roll. Step 3: defender prompt UI plus the hot-seat cover screen. Step 4: AI defender. Then the 5 defense cards and Frequency Jam.
4. Add tests for each card to the same test setup.
5. Main menu modes (hot-seat / AI / online) and seat types, plus a C# AI for mobile (the ASP solver is desktop-only).
6. Online: copy the login, lobby and PUN sync from Battle Planes.
7. Push `dev` and open a PR into `main` once George approves.

## Known gaps
- Energy income each year isn't implemented (only 3 at the start), so later bids are mostly 0.
- Commanders and the space station are test-placed on the first territory in `SetupGame()`; remove that once buying them works.
- Bots: the ASP solver bots are untested since the fix.
