# Invade Earth (RiskASP fork): handoff for Claude / Cursor

Read this first. Last updated: Nov 7, 2026, ~1 AM. Everything through the Nov 7 session logs below is **committed and pushed on `dev`** (George asked). Owner: George (ggofthejungle).
**Full project plan (Trello + code, phases, open questions): `docs/PROJECT_PLAN.md`.**

## Where things are
- Unity project: `C:\Users\georg\source\RiskASP`, Unity **6000.4.0f1**. Work on branch **`dev`**. It's on GitHub (`ggofthejungle/RiskASP`); George pushes from Windows.
- Trello board "Invade Earth" (https://trello.com/b/BaUFhOdd/invade-earth): lists 📌 Decisions & Rules, 🔨 NOW, ⚡ NEXT: Reaction window, 🎮 NEXT: Menu & modes, 🌐 Online, ✅ Done. Use Trello only for detail; this file is the summary.
- Battle Planes (GitHub `ggofthejungle/Mine-master`) is the source to copy the online stack from:
  - PlayFab via CBS: `AuthController.cs`, `LobbyManager.cs`
  - UGS Lobby join codes: `UGS/LobbyUGSManager.cs`, `UI/Private Lobby/*`
  - (Photon PUN turns: `Multiiplayer/TurnManagerMP.cs`. NOT used: Photon was dropped on Oct 30.)

## Tools and how to work here
- **Unity MCP** (Cursor + Claude): `com.unity.ai.assistant` + `C:\Users\georg\.unity\relay\relay_win.exe`.
  - Cursor config: `C:\Users\georg\.cursor\mcp.json` → server `unity-mcp` with `--mcp --project-path C:\Users\georg\source\RiskASP`.
  - Namespace in Cursor: `user-unity-mcp`. Tools: `Unity_GetConsoleLogs`, `Unity_RunCommand` (and camera/scene helpers).
  - `Unity_RunCommand`: C# must be `internal class CommandScript : IRunCommand`; no System.Reflection.
- After editing .cs files, call `AssetDatabase.Refresh()` through RunCommand. It returns "Unity not detected" during the domain reload; wait about 35s, then continue.
- **Run tests (only the classes a change can break, George Oct 30):**
  - Write the test class names, one per line, to `Logs/test-filter.txt`. The next run uses that file once and then deletes it.
  - `Logs/test-results.txt` shows a `RUNNING <test>` line while a test runs, so a freeze names the test.
  - Never add an NUnit `[SetUpFixture]`: it froze Unity between PlayMode tests (3 restarts). Each test class sets `GameManager.AutoPlaceStartingUnits = true` in its own SetUp.
  - The runner switches the Console's Error Pause off.
- **Run tests:**
  - Start them: `EditorApplication.ExecuteMenuItem("Invade Earth/Run Hot-Seat Tests")`
  - Wait for `FINISHED` in `Logs/test-results.txt`. A full run takes about 1 minute.
- **Editor freezes (not an exception — try/catch will not fire):**
  - Play Mode tests edit `StartingPlayersConfiguration` while a scene is running. Unity then tries to reimport that asset and waits for Play Mode to end. The editor locks up with the CPU idle. The log stops at the next test's `UnitySetUp`, on `AssetDatabase.LoadAssetAtPath`, with no error.
  - `TestRunHelper` calls `AssetDatabase.DisallowAutoRefresh()` for the run and `AllowAutoRefresh()` when Play Mode returns to edit mode. That change is written and **not yet verified** (the editor was restarted before a long class ran).
  - If it locks again: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\restart-unity.ps1`
  - That script kills only this project's editor, deletes `Temp\UnityLockfile` and `Temp\__Backupscenes`, then starts Unity. Deleting `__Backupscenes` skips the "Recovering Scene Backups?" dialog (Unity 6.4 has no preference for it). Do not print the Unity process command line; it contains a Hub token.
  - Do not click Yes on that dialog. `Assets/_Recovery/` stays George's to keep or delete.
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

## Session log Oct 30 (evening), last commit `a6239a3`, pushed
- Commanders:
  - Map badges (D/L/N, S = station) with a hover label; d8 dice tagged "d8" in the dice panel.
  - The player picks which commanders attack and how many units; commanders move along on move-in and Fortify.
  - Red toast when a commander dies (`OnCommanderKilled`).
- Command bar:
  - "SHOP" / "YOUR CARDS" labels; confirmation before buy/hire/build ("You got X").
  - Bid box: bigger, Enter submits.
  - Left status panel and black message box removed; messages flash in the banner.
- MOD Reduction asks each player to click their territories.
- CC-5: players place their starting units by clicking.
- Tests: targeted runs only. All classes touched today pass. A global [SetUpFixture] froze Unity (now removed).
- Untracked in the folder: `Assets/_Recovery/` (Unity crash-recovery scenes from the freezes). George to keep or delete.

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
- **Commanders in battle** (George, Oct 30; clarified late evening):
  - Max **3 dice total**. MODs + joining commanders share that cap (`AttackAction` rejects Troops + Commanders > 3). UI picker clamps the same way.
  - Each joining commander is a unit with its own die (Land/Nuclear d8, Diplomat d6).
  - Losses hit the attacking MODs first, so a commander only dies if no attacking MOD is left (e.g. a commander alone). Attacking commanders that win move in.
  - When moving units (after a conquest or in Fortify), the player picks how many, and which commanders go along.
- **Year-1 order** (fixed Oct 30 late): **bid → choose turn order → place starting Diplomat/Land/Station (CC-5) → reinforce**. Not place-then-bid.
- **Card effects never empty a territory** (we have no neutral territories). Evacuation is the exception: the attacker takes the emptied territory.
  - Defense cards: Cease Fire, Stealth MODs, Evacuation, Death Trap, Stealth Station.
- **Online** means **PlayFab + UGS** (not hot-seat). **No Photon** (decided Oct 30): the server is the authority (UGS Cloud Code / PlayFab CloudScript running the C# rules). See `docs/online-plan.md`.
- **Players pick, not automation:** MOD Reduction and starting commanders/station placement are clicked by the player (done).
- **Automated tests:** suite was **118/118** earlier today. After this session's attack-cap + placement-order changes, re-run at least `StartingPlacementTests` and `CommanderBattleTests` before claiming green.
- **Legal:** before any sale, rewrite all card names and text and use an ORIGINAL map (it's currently Hasbro's Risk/2210). Don't ship DLV2.

## Session log Oct 30 (late evening, Cursor) — UNCOMMITTED on `dev` (ahead of origin by 2 already, plus these edits)
PT-2 playtest fixes in working tree (not committed unless George asked again after this note):
- **Lobby:** Menu pre-fills 2 Humans (Player 1 red, Player 2 blue) — `UIPlayerCreationManager` / `UIPlayerCreationSlot`.
- **Bid UI:** typed box retired; `TurnHud` shows buttons 0..energy. Gold borders on bid / order / confirm / attack / defender / toast / splash popups.
- **Attack dice cap:** label is `2 MODs + Land Commander · 3 dice` (not "3 units + 1 commander"); picker + `AttackAction` enforce MODs+commanders ≤ 3.
- **Year order:** bid → choose seats → CC-5 placement → reinforce (`GameManager`). Tests updated in `StartingPlacementTests`.
- **Map badges:** procedural icons (Diplomat laurel, Land shield, Nuclear atom, Station orbital hub). Hire/build shop buttons use same icons. Hover tooltip is Canvas UI legend (icon + name); placement instruction in TurnHud shows the unit icon.
- **Still reported by George, NOT done this session:**
  - Card click should preview text + **Play** button (not instant play / targeting).
  - Same-spot click after conquer should hit **Move in** without moving the mouse.
  - Commander survival on 3v2/2v1 when excess losses would otherwise kill a commander with few MODs (may need extra rule beyond MOD-first; clarify with George if still broken after the dice-cap fix).

## Next steps (in order)
Every task has a unique ID. **CURRENT TASK: ON-1 parts 2–3, in progress, uncommitted.** George said yes to both large slices (pure rules library, then clients send actions and the server rolls). Do not start a full GameManager rewrite in one step. Do not commit unless he asks. He pushes `dev` himself.

Pick up by reading the Nov 6 session log below, then:
1. Confirm Unity is open. If it is frozen, run `tools\restart-unity.ps1`.
2. Compile. Run `DiceRulesTests` (new, not run yet), then `CommandCardTests` (froze on test 16 of 16 before the auto-refresh hold). If that class reaches `FINISHED`, the freeze fix worked.
3. Keep moving pure rules. Attacks, cards, and placement are still applied by the Unity scene. `MatchBoard` can fight a tiny map by itself; `GameManager` does not use it yet.

- **PT-2 still open, not this thread:** card preview + Play button, sticky Move-in click, commander-death edge cases.
- Still open cards: Decoys Revealed, Armageddon; Evacuation destination still auto.
- Phase A: CC-6, CC-7, RULE-1/2, UI-1 (see `docs/PROJECT_PLAN.md`).
- **OL-1 / OL-2** still need a PlayFab title and UGS packages. Do not claim a Cloud Code deploy. Discord invite is still empty (`GameLinks.DiscordInvite`).

## Known gaps
- Energy income: +3 per turn, or territories / 3 with 12+ territories (from main, `ReinforcePhase.Start`). The rulebook uses a score-chart lookup instead; this is close enough for now. Bot bids are still always 0.
- Territories at game start are still random (only commanders and the station are picked, CC-5).
- Bots: the ASP solver bots are untested since the merge; they don't know cards/hiring/Cease Fire.
- Untracked: `Assets/_Recovery/` (Unity crash-recovery scenes) — ask George before deleting.
- Working tree may include `ProjectSettings/SceneTemplateSettings.json` noise — don't commit unless intentional.

## Session log Nov 6 — UNCOMMITTED on `dev`. Chat to pick up.
George: implement multiplayer from the plan, keep single-player vs bots, industry-standard main menu, then "yes and yes" to the pure rules library and to clients sending actions while the server rolls. Then keep going. Then save the chat.

**Main menu (code, no scene edits), tested 7/7** (`MainMenuTests`):
- `UI/MainMenu.cs` builds Play / Single Player / Pass and Play / Online / Options / Credits / Quit / Discord. Online page says rooms are not connected. Discord button does nothing useful until `GameLinks.DiscordInvite` is filled in.
- Options persist in PlayerPrefs (`ie.volume`, `ie.fullscreen`, `ie.quality`, `ie.vsync`, `ie.w`, `ie.h`).
- Single Player and Pass and Play reuse the existing seat picker. `MatchSession` arms once; `GameManager.ApplyMatchSession` consumes it. One human → `LocalPlayer`, no pass-the-device. Two or more humans → pass-the-device.

**ON-1 part 1, tested:** bids and seat choice are actions. Full JSON codec. One `GameRandom` stream for dice, draws, the opening deal, and bid ties. Menu color picks stay on `UnityEngine.Random`.

**`InvadeEarth.Rules`** (`Assets/Scripts/Rules/`, `noEngineReferences: true`). Game and PlayMode tests reference it directly (asmdef refs are not transitive).
- `GameRandom` / `IRandom` / `SeededRandom` (global namespace, so existing call sites did not need a using).
- `ActionData` (namespace `Actions`). Codec stays in the Game assembly.
- `BattleRules` (rolls, ties to the defender, MOD losses before commanders). Caps: `MaxAttackDice = 3`, `MaxDefenseDice = 2`. `BattleSimulator` calls it.
- `DiceRules` (Land/Nuclear attack d8; station defense is all d8; otherwise one d8 per commander). `BattleSimulator.AttackDice` / `DefenseDice` call it. **`DiceRulesTests` not run yet.**
- `ScoringRules` + `YearOrder`. `Scoring` and `GameManager` bidding/seat choice call them. Bots still bid 0 and the last seat is still auto-taken in the scene.
- `IncomeRules`, `TroopMovement`, `OpeningDeal`. Turn income, station +1, place/move/move-in, and the opening deal go through these. Same seed still deals the same map (`SeededRandomTests` passed after the move).
- `MatchBoard`: a tiny map that can reinforce, attack, move in, and fortify with no scene. Last territory taken eliminates that player. Not wired to `GameManager`.
- `CardCatalog`: costs, timings, buy cost 1, max 4 per turn. `CardSpec.For` reads it. `CardCatalogTests` passed.
- `MatchView` / `HiddenInfo`: other players' card ids and scouted territories are stripped. Last battle losses and "conquered" are public.

**In-process server** (`Online/GameAuthority.cs`): `Submit(ActionData)` → codec → `HandlePlayerAction`. `ViewFor(playerId)` is the filtered snapshot. Not a deployed Cloud Code or PlayFab server. Attacks are still applied by `AttackPhase` in the scene.

**Tests that passed this thread (targeted, not the full suite):** menu 7; codec + seeded deal + hot-seat + placement + input owner (27, earlier); battle rules 3, authority bid+hidden hand, commander battles through the d8/d6 cases (then the editor froze); scoring + year order 9 and hot-seat 10; board + catalog 8; seeded deal again; hot-seat 10 again after income/deal; command cards 15 of 16, then the editor froze on `Play_OnSomeoneElsesTurn_IsRejected` during setup.

**Not verified after the last edits:** `DiceRulesTests`, the auto-refresh hold in `TestRunHelper`, and command-card test 16.

## Session log Nov 6 (late) — UNCOMMITTED on `dev`
- **George: no more test runs for now** (they kept hanging Unity). Implement, compile, and check with a small `Unity_RunCommand` probe instead.
- `DiceRulesTests` passed 3/3. `CommandCardTests` froze again on the SAME test, `Play_OnSomeoneElsesTurn_IsRejected`, after 15 passes, so the auto-refresh hold did NOT fix it. The cause is that test, not the next test's setup. It is now `[Ignore]`d. Lead, unconfirmed: it is the only test in the class that logs an error on purpose.
- `Logs/test-results.txt` stayed at `QUEUED` for the passing run; Unity's own `%USERPROFILE%\AppData\LocalLow\DefaultCompany\RiskASP\TestResults.xml` had the results.
- **ON-1 part 4 (reaction-window timer), compiled, not play-tested:**
  - `Rules/DecisionClock.cs` (pure, `OnlineReactionSeconds = 20`).
  - `GameManager.ReactionTimeoutSeconds` (0 = no limit, the default for local play). `OpenDecision` starts the clock for a human, connected defender on `DefenseReaction`. `Update` auto-answers **Pass** on timeout. `DecisionSecondsLeft` for the UI.
  - Defender prompt title shows "auto-pass in Ns"; the TurnHud "Waiting for X to respond…" line adds "(Ns)". Both only when a limit is set.
- **ON-1 part 5 (snapshot / rejoin / dropped seat), compiled, not play-tested:**
  - `Rules/MatchSnapshot.cs` + `SnapshotFilter.ForPlayer` (other hands become a count only, scouting hidden).
  - `Online/MatchSnapshots.cs`: `Capture`, JSON with a version check, `ApplyBoard` (owners, units, stations, commanders, energy, this player's hand). Year/turn/phase are NOT rewound by `ApplyBoard`.
  - `GameManager.SetConnected` / `IsConnected`: a disconnected player's open question is answered by `DefenderAI` at once, and later ones too.
  - `GameAuthority`: `SnapshotFor`, `UseOnlineReactionTimer`, `Disconnect`, `Reconnect` (returns the filtered snapshot).
- **PT-2 fixes (compiled, not play-tested):** clicking a card opens a preview (full text from `CommandHud.Describe`, cost, timing) with Play, or Close plus the reason. `PlayCard` is the old instant path; `CommandHudTests` now call `ClickCard` then `Confirm`. Move-in: the picker keeps "Move in" where "Roll dice" was (spacer for the missing Cancel), and clicking the conquered territory again confirms.
- **OL-1 login (compiled, not run):** PlayFab title **25C2C = Invade Earth** (Battle Planes is E1BA8, separate). `Assets/PlayFabSDK` (2.187, copied from Mine-master, TitleId changed, no secret key). `Online/PlayFabLogin.cs`: `LoginWithCustomID` with a random id kept in PlayerPrefs `ie.playfab.device`. The Multiplayer page signs in when opened.
- **OL-2 rooms (compiled, not run):** package `com.unity.services.multiplayer` 2.4.1 (Sessions; replaces Lobby 1.2.2 on Unity 6). `Online/OnlineRoom.cs`: anonymous UGS sign-in, private session 2–5 players with join code, player props name / PlayFab id / ready, host Start writes a seed (`MatchStarted` event). Menu page `Room`: code, members, Ready / Start / Leave.
- **UGS link:** Unity Cloud project **InvadeEarth**, id `4db62910-d22d-45ce-858f-9e2fe5d3ed63`, org `2473936957658`. George links it in the editor (Project Settings → Services). Not linked as of 10:30 PM; rooms fail with a clear message until then.
- **CC-6 Evacuation destination (compiled, not play-tested):** a human defender who plays Evacuation gets a second decision, `DecisionKind.EvacuationDestination` (options = their other territories, Subject = attacked territory), answered by clicking the map (same path as MOD Reduction / placement). Bots and timeouts use `CommandCards.EvacuationDestination` (strongest). `PlayReaction(..., evacuateTo)`. `ReactionWindowTests` Evacuation test now answers the second question.
- **George, Nov 6 10:20 PM:** keep working through the plan for an hour (past multiplayer too). Don't wait for his OK; ask when input is needed, then move to something else. Only compile + small `RunCommand` probes, no test runs.
- **Next up (Phase A, no input needed):** UI-1 scoreboard strip (score / energy / cards / turn order for every player), bot bidding (bots still bid 0), CC-7 Decoys Revealed + Armageddon, commander-death edge case (PT-2). RULE-1/RULE-2 need the rulebook.
- **UGS linked** (George, Nov 6 ~10:35 PM): `CloudProjectSettings.projectId` = InvadeEarth `4db62910-…`.

## Session log Nov 6 (night) — ON-2 online match, host-authoritative lockstep. UNCOMMITTED, compiled, NOT play-tested
George chose: **one player's game is the host for now** (Cloud Code server later).
- Package **`com.unity.netcode.gameobjects` 2.13.3** added (Sessions' Relay handler needs `NetworkManager.Singleton` + `UnityTransport`). No NetworkObjects; only named messages.
- `Online/OnlineNet.cs`: builds the NetworkManager in code (DontDestroyOnLoad, scene management off), JSON text messages `ie.toHost` / `ie.toClients`, `ie.hello` maps network client → UGS player id. Events `PlayerLeft` (host), `HostLost` (client).
- `OnlineRoom`: host/join use `.WithRelayNetwork()`. Start writes `seats` (UGS ids in order) + `seed`.
- `MainMenu.OnMatchStarted`: `UIPlayerCreationManager.PrepareOnline(names)` (humans, colors Red/Blue/Green/Yellow/Purple by seat), `OnlineMatch.Arm(seed, seats, mySeat, isHost)` (also `GameRandom.UseSeed`), `MatchKind.Online`, load `Main`.
- `Online/OnlineMatch.cs` (`IActionRelay`): every device runs the full game from the same seed. Client actions → host; host applies with `GameManager.ApplyAgreed` and, if accepted, broadcasts with a sequence number; clients apply in order. Messages that arrive before the scene loads are queued until `Bind`. Host checks sender seat == `ActionData.PlayerId`. Rejections flash on the sender's TurnHud. Host-side: dropped player → sequenced `seat` event (`ApplyAgreedConnection`), AI answers their questions, and `HostTick` bids 0 / takes first free seat / ends their phases (they lose their turn).
- `GameManager`: `Relay`; `HandlePlayerAction` sends to the relay unless inside `ApplyAgreed` (depth counter; `Start` runs inside it too). Only the host expires the reaction timer. Online sets `LocalPlayer` = my seat and `ReactionTimeoutSeconds` = 20. Added `OnDestroy` (unbind, clear `Instance`).
- `SelectionManager`: map clicks ignored when `!IsLocal(CurrentPlayer)` (also blocks clicking during bot turns locally).
- **Known gaps:** host leaving ends the match (no migration); no rejoin yet (host keeps a log, so replay-on-rejoin is the next step); ~~online is humans only~~ (bots now allowed, played by the host — see the 11:20 PM log); no desync check yet (idea: hash of board after each action); hidden info is NOT secret in lockstep (every device holds all hands + the seed). Server authority (Cloud Code / `GameAuthority`) fixes the last one later.
- **Later the same night (11 PM–midnight), also compiled, not play-tested online:**
 - **Out-of-sync check:** every host entry carries `Hash` = FNV-1a of the full `MatchSnapshot` JSON plus the draw deck's card order (`BoardHash`). A client that differs reloads `Main` once from the same seed and replays the host's log (`Resync`, max 1 per session); after that it flags `OutOfSync`, flashes "Leave and rejoin", and stops sending.
 - **Determinism probe (Play Mode, no network):** loading `Main` twice with seed 42 gave the same `BoardHash` (-633290057 before the deck was added), same first bidder, same deal. A different-seed load was not measured (Play Mode exited).
 - **Rejoin / late join = replay:** clients apply entries strictly by sequence (`_waiting` sorted buffer). On `Bind` a client sends `sync` with how many it has; the host answers with log batches (150 per message). Room is no longer locked at start, so a dropped player joins again by code (menu pre-fills `OnlineRoom.LastCode`, PlayerPrefs `ie.room.last`). Host's hello handler raises `PlayerJoined` → sequenced "seat connected" event.
 - Host Start now waits until every member's Relay hello has arrived ("Waiting for X's connection"). `OnlineRoom.MyId` returns null when UGS isn't initialized (it used to throw).
 - **UI-1 scoreboard** (`UI/Scoreboard.cs`, top-left): year / bidding, then each player in this year's seat order (`GameManager.SeatOrder`) with score, energy, cards, territories, "(you)", "away", "out". Screenshot-checked in Play Mode.
 - **Pause menu** (`UI/PauseMenu.cs`, Esc): Resume / Leave match with a confirm step; online it leaves the room (host warned it ends the match; clients told the rejoin code). `MainMenuLoader` (game-over button) also leaves the room. Screenshot-checked.
 - **Bot bidding:** `Rules/BotBidding.cs`: keep 3 energy, bid ~1/3 of the rest (+0/1 random from `GameRandom`), ~1/2 in the last year. `BotBiddingTests` written, NOT run. Human-only tests don't depend on it.
 - `TurnHud.Refresh` got a null guard: one NRE in `SetChip` fired once ~2 s after `Main` loaded from the menu; cause not found.
 - CC-7 (Decoys Revealed, Armageddon) skipped: Decoys isn't in `DeckLists` (adding it changes deck-size tests) and Armageddon needs a multi-player prompt; both need a test run.
 - Untracked junk: `Assets/InitTestScene*.unity` (3, from frozen test runs) can be deleted. `Assets/DefaultNetworkPrefabs.asset` is NGO's and should be committed.
- **To play-test:** two instances needed: a standalone build + the editor, or Multiplayer Play Mode (package `com.unity.multiplayer.playmode`, not installed).

## Session log Nov 6 (11:20 PM) — UNCOMMITTED on `dev`
- Unity was open and compiling. Removed a duplicate `using Expansions` in `CardRepository.cs`; now 0 errors, 0 warnings.
- Commander-death review (PT-2): code matches the locked rule (`BattleRules`: losses hit the *attacking* MODs, then attacking commanders; defending commanders die only on conquest, `Purchases.OnTerritoryConquered`). No bug found. Open rule question for George: should MODs that stay behind in the origin also shield attacking commanders?
- Fixed the stale "free card on conquest" row in `docs/card-system-overhaul.md` (it was removed in CC-1).
- George on the commander question: **keep it as is** (only attacking MODs shield attacking commanders); may revisit later.
- All below compiled (0 errors/0 warnings) and probed with `RunCommand`, **no test runs, not play-tested**:
 - **RULE-1 done** (George: yes). `IncomeRules.EarnsConquestBonus` (exactly the 3rd conquest of a turn, so once per turn) → `CommandCards.GrantConquestBonus`: +1 energy and 1 free card, from a deck whose commander the player has in play, else any deck in play; uses `GameRandom` (lockstep-safe). Called from `AttackPhase.ResolveAttack`. `TurnHud` flashes it (card name only to its owner). Event `CommandCards.OnConquestBonus`.
 - **Multiplayer Play Mode 2.0.2 installed** (`com.unity.multiplayer.playmode`, George chose this). Window → Multiplayer → Multiplayer Play Mode, tick a virtual player. Additional editors run from `Library/VP/<id>/`; `OnlineRoom.LocalProfile` turns that into a UGS auth profile (`InitializationOptions.SetProfile`) so each copy is a different anonymous player. The display name pref (`ie.name.<profile>`) and the PlayFab device id (`ie.playfab.device.<profile>`) are per profile too. Builds can pass `-ieprofile <name>` for the same effect.
 - **Bots in online rooms:** host has "Add a bot" / "Remove a bot" in the room (session property `bots`, everyone sees "Bot N · AI, played by the host"). Seats list appends `bot:1`, `bot:2`…; `PrepareOnline(names, isBot)` makes those seats "Dumb BOT". `GameManager.RunsBotAI` (= local game or host) gates `BotPlayer`, so only the host runs the ASP solver; its moves go out as normal sequenced entries. Clients never send bot actions (`OnlineMatch.Send`). Bot bids / seat picks / defender answers / placement already run inside `ApplyAgreed` on every device (same `GameRandom`), so they stay in sync. Host must be desktop (DLV2).
 - **CC-7 Decoys Revealed done:** catalog `BeforeFirstInvasion`, cost 0. Targets are (from, CommanderType, to) triples applied in order (`CommandCards.DecoyMoves` validates, a commander may move twice). UI `CommandHud.PickDecoyMoves`: click a territory with a commander → pick which one if several → click destination → repeat → **Done (n)** / Cancel. Test `CardEffectsTests.DecoyRevealed_MovesCommandersBetweenOwnTerritories` written, NOT run; `NotImplementedCards_CannotBePlayed` now only checks Armageddon.
 - **CC-7 Armageddon done** (Nov 6, ~11:50 PM; compiled + probed, not run/play-tested). Cost 4, `BeforeFirstInvasion`. `CommandCards.StartArmageddon`: players in turn order starting with the one who played it; each gets `DecisionKind.ArmageddonCard` (options `Card:<id>` for other nuclear cards they could play + `Pass`) again and again until they pass or run out. A card with a target asks `DecisionKind.ArmageddonTarget` (options = territory names, Subject = card name; Rocket Strike = any opponent territory, Assassin Bomb = opponent territory with a commander, aimed via `CommandCards.AssassinTarget`). Free = no energy and any timing; still needs the Nuclear Commander in play and not jammed (`WhyCantPlayFree`). `Play` now = checks + energy + `Resolve` (shared with free plays). UI: defender-style prompt "X: ARMAGEDDON" with card buttons + Pass; the target is a map click (added to `IsTerritoryDecision`); TurnHud instruction lines for both. AI (`DefenderAI`): a bot plays every free card, best target = most commanders then most units; an away/timed-out human passes. Both kinds use the online reaction clock (timeout = Pass / AI target). Test `CardEffectsTests.Armageddon_LetsPlayersPlayNuclearCardsForFree` (replaces the not-implemented test); `CardCatalogTests` and `CommandCardTests.NuclearCards_CanBeBought_AndArePlayable` updated. None run.
 - `TurnHud.Refresh` guard widened to the chip labels, color bar and title texts (likely cause of the one-off `SetChip` NRE; unconfirmed).

## Session log Nov 7 (after midnight): host migration + secret dice. UNCOMMITTED, compiled (0/0), NOT play-tested
George: "keep iterating on both of these large tasks" (host migration; server authority / hidden info), but not the original map or card text. Details: `docs/online-plan.md`, section "Host lockstep: host migration and secrets".
- **Host migration:**
 - `OnlineRoom` host and join options use `.WithHostMigration(new LockstepMigrationData())` (`Online/LockstepMigrationData.cs`, which only carries the entry count).
 - New events: `HostChanged`, `Migrated`, `MigrationFailed`; also `HostId`.
 - `OnlineNet` re-registers its handlers when it starts again as host (`OnServerStarted`/`OnServerStopped` reset `_listening`). New `ConnectedToHost` event.
 - `OnlineMatch.IsHost` is now settable. On becoming host: drop waiting entries, start the secret stream, mark the old host away, `GameManager.ResumeBots` (`BotPlayer.Resume`).
 - Clients re-sync on reconnect. The host answers `ahead` to a client with a longer log, and that client reloads and replays.
 - The pause menu now says another device takes over when the host leaves.
- **Secret dice/draws:**
 - `Rules/GameRandom.cs`: `GameRandom.Use`, `RecordingRandom`, `ReplayRandom`.
 - From the first entry on, the host uses a crypto-seeded recording stream. `Envelope.Rolls` carries each entry's numbers, and clients replay them; extra or missing numbers are logged as errors.
 - The probe confirmed that recording and replay give identical output and that an overrun is detected.
 - The seed now only covers the deal and scene start.
- **Hidden hands: DONE (George: "go ahead with the online plan", Nov 7 ~12:10 AM).** Compiled (0/0) and probed in Play Mode with a fake host and client; not play-tested online. Details: `docs/online-plan.md`, "Hidden hands".
 - `Cards/Secrets.cs` (`ISecretSource`, implemented by `OnlineMatch`). `Secrets.Public` puts the host's answer in `Envelope.Facts`; `Secrets.Private` sends it only to the owner (`Envelope.Private`, per seat in `_privateLog`). Broadcast now sends one copy per client.
 - Face-down cards: `Card.FaceDown(deck)`, `IsHidden`. Public `CardRepository.DeckCount`. `CommandCards.Draw`, `Reveal`, `ResolveFaceDownCards`, `SetOwnHand`. `PendingDecision.AnyCard`.
 - From the host: the reaction-window open and ask-again checks, the Armageddon "has a card" check, and all `DefenderAI` answers. Reveal on play (`ActionCodec`, `CardFromOption`), on elimination (to the winner), and at game over (all hands).
 - `BoardHash` covers public state only. After migration, sync requests claim hands and the host replies `KindHand`.
 - Probe confirmed: a host draw becomes a face-down card on the client, deck counts match, a card answer reveals the card, the owner's private draw arrives, and a new host's resolve and hand sync work.
 - ~~Still public: the Scout Forces territory.~~ Hidden too (see the 12:20 AM log).

## Session log Nov 7 (12:20 AM): easy items. UNCOMMITTED, compiled (0/0), not play-tested
George: "continue with more feature, knock out easy things from the list".
- **LEGAL-3:** Credits → **Licenses** page (`MainMenu.BuildLicenses`): the full RiskASP MIT notice (Copyright (c) 2023 Alessio Farfaglia), plus one line each for the PlayFab SDK (Apache 2.0) and SimpleJson (MIT). Esc goes back to Credits. Open for LEGAL-4: does the full Apache text need to ship?
- **Secret Scout Forces:**
 - The host picks the territory with `Secrets.DrawRandom`; only the scout's device gets its name (`Secrets.Private`).
 - Every device counts each player's scouts (`CommandCards.ScoutsLeft`). One owner-change watcher is attached to all territories on the first Scout.
 - When a player with scouts takes a territory, the host says whether it was scouted (`Secrets.PublicBool`), then every device adds the 5 MODs.
 - `BoardHash` blanks `ScoutedTerritories`. Locally it behaves as before (same single `GameRandom` call).
- **Room kick:** the host gets a "Kick" button on each other member's line (`OnlineRoom.KickAsync` → `IHostSession.RemovePlayerAsync`).
- `docs/PROJECT_PLAN.md` updated:
 - PT-2 leftovers built;
 - CC-6 built;
 - bid question answered;
 - LEGAL-3 status;
 - online status note under Phase B.
- **To play-test (MPPM, 2–3 players):**
 - Host leaves mid-turn: does a client become host and the game continue?
 - Dice still agree (no "board differs" errors).
 - A bot mid-turn resumes on the new host.
 - Hands: another player's hand shows only a count; buying or the conquest bonus gives the card on its owner's device only; a defense card played by a remote defender works on every device; a bot's Armageddon works.

## Session log Nov 7 (12:30–1 AM): DEV-1, PLACE-1, RULE-2, MM-2 started. UNCOMMITTED, compiled (0/0)
George: "dev-1: like a node, if removed all the connections to its neighbors are lost as if it was never there. place-1: an option for the host to select. rule-2: grab it from the rules. mm-2: start that too".
- **Match options** (`UI/MatchSession.cs`): `MatchOptions` (`Devastated` 0–4, `Placement` = `TerritorySetup.Random` / `TakeTurns`), saved in PlayerPrefs `ie.rules.devastated` / `ie.rules.placement`. `GameManager.Options` comes from the menu; a scene loaded without the menu (tests) uses `MatchOptions.Plain` (0, Random), so tests are unchanged.
 - Menu: Play → **Match rules** page. Online: the host gets the same rows in the room (session property `rules`, written on create, on change, and again at Start); others see a read-only line. `OnMatchStarted` decodes the host's rules for every device. The menu resets to this device's saved rules on load.
- **DEV-1** (probed in Play Mode): `TerritoryRepository.Devastate` takes the territory out of `Territories`, every neighbour's list, its own list and its continent; owner null, collider off, drawn dark with "devastated" (`TerritoryGraphics` handles a null owner too). The list of them: `TerritoryRepository.Devastated`. Picked with `GameRandom` before the deal; a pick that would leave a neighbour with no links is skipped. `Continent.IsComplete` / `IsOwnedByPlayer` are false for an empty continent.
- **PLACE-1** (probed with 2 bots only): `GameManager.StartDraft`: random order; `DecisionKind.ClaimTerritory` (empty territories, 1 MOD) until the map is full, then `DecisionKind.PlaceStartingTroops` (+1 MOD per answer, 3 per player per round, army from `OpeningDeal.TroopsForPlayerCount`), then `BeginFirstYear` (bid → seats → CC-5 → reinforce). Loop instead of recursion for instant bot answers. `DefenderAI` answers both (grow next to own land / toward continents; stack the most threatened border). Map clicks via `CommandHud.IsTerritoryDecision`; TurnHud instruction lines. Probe: 38 territories split 19/19, 40 MODs each, game moved on to Playing, no errors.
- **RULE-2:** `IncomeRules.ScoreChart(n)` = max(3, n/3) gives both MODs and energy, plus continent bonuses for both. **Assumed numbers** (the chart's values aren't in the rulebook text); George to confirm. Tests updated (`BoardRulesTests`, `MergeRegressionTests`), not run.
- **MM-2 started:** `TurnPhases/AI/BotEconomy.cs`, queued through `ActionReader` at the start of the bot's Reinforce (so online it goes through the relay like solver moves). Order: re-hire lost commanders (Land, Nuclear, Diplomat), play hand cards with simple targets (skips MOD Reduction, Armageddon and Decoys, which would block the solver's queued moves), a station in years 1–4, buy up to 2 cards; keeps 2 energy. Probe (2 solver bots, seed 11, to year 3, no errors): they re-hired the Nuclear Commander, built 2 stations each, bought cards and played Rocket Strike, Scout Forces, Scatter Bomb and Assemble MODs. Next for MM-2: C# reinforce / attack / fortify so bots work without the solver (mobile).
- **Bug fixes found by the probes:**
 - The solver bot added every d8 commander on top of 3 MODs, so its attack broke the 3-dice cap and was rejected. `AttackAIPhase` now adds only the commanders that fit.
 - `BotBrain` sent `card(...)` facts built from a commented-out constructor (all null), which threw `IllegalTermException` as soon as a bot held a card. Removed (they were for classic set trading).
 - Null guards for scene teardown: `BotPlayer.OnEnable` (no `GameManager`) and `SelectionArrow.LateUpdate` (no `AttackPhase`).
- **To play-test:** a human in the take-turns setup (claim clicks, 3-MOD batches); the room's rules rows; a bot's purchases in a longer game.

## Nov 7 (~1 AM): committed and pushed
- George: "commit and save progress to handoff note, and push". All the "UNCOMMITTED" work in the Nov 6 / Nov 7 logs above is now in one commit on `dev`, pushed to `origin/dev`.
- Left out on purpose (still untracked or unstaged): `Assets/_Recovery/` (George's call), `Assets/InitTestScene*.unity` (junk from frozen test runs, safe to delete), `.idea/`, `ProjectSettings/SceneTemplateSettings.json` noise.
- Next: play-test online (MPPM) and the take-turns setup; confirm the RULE-2 chart numbers; MM-2 next step is C# reinforce / attack / fortify for bots without the solver.
