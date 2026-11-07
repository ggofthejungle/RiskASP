# Card system overhaul: classic Risk cards -> 2210 A.D. command cards

Written Oct 30, 2026. Status: analysis, plus a working pilot with 2 cards (Energy Crisis = Diplomat, Assemble MODs = Land). See "Plan" at the end.

References:
- 2210 rulebook, Avalon Hill FAQ.
- George's Trello "Turn Phases" comparison table.
- The Java 2210 implementation **avioane/InvadeEarth** (by Jonathan Crosmer). It has **no license**, so we read it for rules and structure only and **copy no code**.

## 1. The two systems are different games

| | Classic Risk (what RiskASP was built on) | Risk 2210 A.D. (what we want) |
|---|---|---|
| How you get cards | Free: 1 card at the end of any turn in which you conquered a territory | **Buy** them: 1 energy each, up to **4 per turn**, during step 3 of your turn |
| Decks | One deck (Infantry / Cavalry / Artillery / Wild, each tied to a territory) | **5 decks**, one per commander (Diplomat, Land, Naval, Nuclear, Space) |
| Requirement | None | The matching **commander must be in play** to buy from its deck **and** to play the card |
| What a card does | Nothing alone. Sets of 3 are traded in for extra troops | Each card has its own effect, a **power-up cost** in energy paid when played, and a **timing** (before your first invasion / when an opponent invades / end of turn / end of game) |
| Running out | Traded-in cards are reshuffled into the deck | Discards are **never reshuffled**. An empty deck can't be bought from |
| Hand | Must trade at 5+ cards | No hand limit. Unplayed cards can be kept for later years |
| Elimination | Winner takes the loser's cards | The loser's cards are **discarded** |

Sources: the official 2210 rulebook (media.wizards.com/2015/rules/risk2210_rules.pdf) and the Avalon Hill 2210 FAQ (risk2210.net/resources/Risk_2210_AD_FAQ.pdf, grognard.com/faqs/risk2210.html).

**Evacuation (rules question), confirmed by the FAQ and by the Java version.** The Java version only allows it if the defender owns more than one territory. It moves the mobile units (MODs and commanders, not stations) to one friendly territory. The FAQ says the defender moves **all** units out to **any one** territory they control (no path needed), and the attacker then **occupies** the emptied territory. It counts as a conquest (it counts toward the 3-territory bonus). In our code: the defender plays it in the reaction window, the troops move out, and the attack then resolves as an **automatic conquest** (no dice against 0 defenders). It must never leave a 0-unit territory that the defender still owns. Today `Abilities.Evacuation` only moves the troops out; the auto-conquest belongs in RW-2.

## 1b. Turn phases: Classic vs 2210 vs our code

George's Trello table, checked against the rulebook and the Java version (which runs 2210 as an extension of its Classic rules, with the same turn loop plus extra steps):

| Step | Classic | 2210 (rulebook / Java) | Our code today |
|---|---|---|---|
| Year start | n/a | **Bid** energy. Then the **highest bidder CHOOSES** their turn position, next-highest chooses next, etc. (Java: `placeBids` then `chooseTurnOrder`; ties random). 5 years. | Bid, then **auto-order by bid** (no choosing). 5 years. ✅ mostly |
| 1. Collect | Reinforcements = territories/3 + continents + traded cards | **MODs and energy: the same number** (score chart by territory count + continent bonuses). Space station: **+1 MOD** each (the rulebook says MOD, not energy; the Trello table says "+1 for each Space Station" under energy, so check which one you want) | Troops from `GetTotalTroopBonus`, energy from main's `territories<12 ? 3 : /3`, +1 troop per station ✅ approx |
| 2. Deploy | Place | Place MODs | ReinforcePhase ✅ |
| 3. Hire commanders / build stations | n/a | Commander 3 energy, station 5 (max 4), placed on own territories | ❌ missing (commanders only test-placed) |
| 4. Buy command cards | n/a | 1 energy each, **max 4 per turn**, only decks whose commander you have | ✅ pilot (`BuyCommandCardAction`) |
| 5. Play command cards | n/a | "Before first invasion" cards: any time on your turn **until your first invasion** (Java allows them in the invade step too, before the first invasion) | ✅ pilot: allowed in Reinforce and in Attack before the first attack |
| 6. Attack | Dice d6 | Commanders/stations roll d8. **After every declared invasion, players get a reaction window** (see below) | d8 ✅ partly, window = RW-1 plumbing ✅ |
| 7. Fortify | 1 move | "Free move", Redeployment card gives an extra one | ✅ |
| 8. End of turn | Draw a card if you conquered | "End of turn" cards (Redeployment). **No free card** | ✅ no free card (removed in CC-1). The "3 conquests → 1 energy + 1 card" correction (RULE-1) is not implemented |
| Game start | n/a | 3 energy. **Every player starts with a Diplomat, a Land Commander and a Space Station.** 4 devastated territories. | 3 energy ✅. Players place their commanders and station (CC-5) ✅. No devastated territories ❌ |

**Design consequences**
- Keep `Reinforce → Attack → Fortify` as the three big phases (the banner stepper shows them). Add **sub-steps inside the turn**: Collect → Deploy → Command (hire, stations, buy cards, play before-invasion cards) → Attack → Fortify → End of turn. A separate `CommandPhase` between Reinforce and Attack is the cleanest place for hire/build/buy. Card *playing* must stay allowed until the first invasion.
- **Reaction window: who gets asked.** The Java version asks every other living player who holds a playable card (option "only when necessary"), in turn order. If anyone plays a card, **everyone is asked again**. That's because Stealth MODs can be played to help *anyone* (rulebook). Our locked decision says "only the defender". George needs to decide: keep defender-only (simpler UI) or follow the rules. `PendingDecision` already supports asking any player, so it would become a queue.
- **Choosing turn order** after bidding is a new step: the highest bidder picks their position.

## 2. What in the code is classic-only (affected by the change)

| Code | Today | Impact of switching to 2210 |
|---|---|---|
| `GameManager.TryDrawCardOnTurnEnd` | Gives a **free random card** after any turn with a conquest (now drawn from the 2210 command decks, ignoring commanders) | **Remove.** In 2210 cards are bought. This currently hands out command cards for free |
| `ExchangeCardsAction`, `CardExchange`, `CardExchangeType`, `CardRepository.GetBestExchanges/ExchangeTypes` | Trade sets for troops. Already commented out in `ReinforcePhase` by main | **Delete** (dead code) |
| `Player.BestCardExchangeCombinations`, `HasPossibleExchange`, `CardTypeCountMap` | No longer updated (commented out) | Delete. `ReinforcePhase.Start` still calls `HasPossibleExchange()`, which is now always false. Harmless but dead |
| **AI:** `ReinforceAIPhase` + ASP predicates `PossibleCardExchangePredicate`, `ExchangeCardsPredicate`, `CardPredicate`, `CardsCountPredicate` (BotBrain) and the ASP programs | The ASP bots reason about trading sets | Bots must stop trading (they find no exchanges now, so they just don't). Later they need simple card logic: buy/play. That belongs with **MM-2 C# AI**, not the ASP bots |
| **UI:** `UICardsManager` / `UICard` (Exchange button, "bonus: 10", infantry/cavalry/artillery sprites, territory images) | Built for classic sets | Needs a **command card hand**: card name, deck color, power-up cost, a Play button enabled only when playable, and a "Buy card" panel per commander. Biggest UI piece |
| `Abilities.methods` + `CardType` enum | One entry per card *name* (41), no copy counts, no costs, no timing | Needs a data table: name, deck, **copies** (decks have several copies of some cards), **power-up cost**, **timing**, targets |
| `Player.PurchaseCommandersCards` | Subtracts 1 energy and adds a card (no checks) | Replaced by `BuyCommandCardAction` (validated) |
| `Player._CommandersArray` vs `Territory.CommandersList` | Two separate commander lists | "In play" = on the map in a territory you own (`Territory.CommandersList`). The player array should go, or be derived from the map |

## 3. How it ties into the other 2210 features

- **Commanders.** They decide which decks you can buy from and which cards you can still play. That means:
  - commander **death** must be real: when a territory with commanders is conquered and the commander has no MODs left to protect him, he dies;
  - **hiring** commanders (3 energy, placed on your territory) must be a real action;
  - today commanders are only test-placed on the first territory in `SetupGame()`.
  - Without hiring, only that test-placed commander's deck is usable.
- **Energy.** It is now spent in four places: bidding, buying cards (1 each), power-up costs, and commanders/stations. So **energy income** really matters.
  - The rules use a Score Chart lookup by territory count (+ continent/colony bonuses), not `territories / 3`. Main's `territories < 12 ? 3 : territories / 3` is a fine approximation for now.
  - Bots still always bid 0.
- **Space stations.** Not card-driven, but cards create them (Territorial Station, Stealth Station). Per the rules a station:
  - costs 5, max 4 per player;
  - gives +1 MOD per turn (implemented);
  - makes **all defenders roll d8** (not implemented yet; `BattleSimulator` has a TODO);
  - is needed to invade the Moon (no Moon on our map).
- **Reaction window (RW-2..5).** "Opponent invades" cards (Cease Fire, Evacuation, Death Traps, Stealth MODs, Stealth Station) are exactly what the reaction window is for. With command cards in place, RW-2's check becomes concrete: *the defender holds a card with timing OpponentInvades, its commander is in play, and they can pay its power-up cost.*
- **Frequency Jam.** "The chosen player cannot play command cards during your turn." Now enforced for the pilot card (and reset each turn).
- **Turn order / years.** End-of-game cards (Colony Influence) need final scoring (end of year 5). There is no score yet.
- **Legal (from CLAUDE.md).** Card names and texts must be rewritten before any sale, so power-up costs and copy counts are ours to choose. Keep them as **data**, not code.

## 3b. Land-only map: which commanders and cards matter

We have no water or Moon territories, so the **Naval and Space decks are out of play** (`CommandCards.IsDeckInPlay`), and so are the water/Moon versions of shared cards. In play:

| Deck (20 cards each in 2210) | What it brings on a land-only map | Status |
|---|---|---|
| **Land** | Assemble MODs ×3 (cost 1), Reinforcements ×3, Land Death Trap ×1 (cost 3), Stealth MODs ×5 (reaction, cost 0), Stealth Station ×1, Scout Forces ×3, Colony Influence ×2, Frequency Jam ×2 | Assemble MODs ✅ playable. Stealth MODs / Death Trap / Stealth Station wait for RW-2 |
| **Nuclear** | Armageddon (4), Rocket Strike Land ×2 (2), Scatter Bomb Land ×3 (1), The Mother (continental bomb, 3), Assassin Bomb ×3 (1). The water/Moon variants (Aqua Brother, Nicky Boy, Rocket/Scatter water+moon) don't apply | ❌ all stubs today. Buyable but not playable (tested) |
| **Diplomat** | Colony Influence ×4, Territorial Station ×3 (1), Decoys Revealed ×2, Evacuation ×2 (reaction), Redeployment ×3 (end of turn), MOD Reduction ×2 (2), Energy Crisis ×2 (0), Cease Fire ×2 (reaction, 2) | Energy Crisis ✅ playable |

Commanders in battle: the **Nuclear commander always attacks with d8**, and the **Land commander attacks with d8 on land**, which on our map means always. So Land and Nuclear are equally strong attackers here; the difference is only their decks. The Diplomat never attacks with d8. Commanders (and stations) always defend with d8 (stations: not yet implemented).

Note that our `CardType` enum has **one card per name**. The real decks have **copies** (60 cards across the 3 land decks). CC-1 should switch to deck lists with copies.

## 4. Pilot: one command card working end-to-end (done)

- `Cards/CommandCards.cs`: rules (1 energy to buy, max 4 per turn, commander in play to buy and play, power-up cost, timing, discard pile that is never reshuffled).
- `BuyCommandCardAction`, `PlayCommandCardAction`: handled by `GameManager.HandlePlayerAction`, so they go through the same validation as everything else (input owner, pending decision, turn).
- Card data: `Card.PowerUpCost` and `Card.Timing` (from `CardSpec.For`). **Energy Crisis** (Diplomat, cost 0) and **Assemble MODs** (Land, cost 1, +3 units on a territory you occupy), both *before your first invasion*. Costs match the Java reference's deck lists.
- Naval/Space decks rejected on this map.
- Tests: `CommandCardTests.cs` (14). Full suite 47/47.

## 5. Plan (suggested order)

0. **Decide:** (a) the reaction window asks only the defender, or everyone who can play (rules); (b) the station bonus is +1 MOD (rules) or +1 energy (Trello table); (c) add "choose turn order" after bidding.
1. **CC-1** Card data table (name, deck, copies, cost, timing) replacing `CardType` + `Abilities.methods` index wiring. Remove `TryDrawCardOnTurnEnd` and the classic exchange code + ASP exchange predicates.
2. **CC-2** Starting units: each player gets a Diplomat, a Land Commander and a Space Station (replaces the test placement in `SetupGame`). Hire commanders (3 energy) and build stations (5, max 4) as actions in a Command step. Commander death on conquest. Drop `Player._CommandersArray`.
3. **CC-3** Command-card hand UI + buy panel (replace UICardsManager).
4. **RW-2..5** reaction window, with the "OpponentInvades" cards as the defense cards.
5. **CC-4** Remaining "before first invasion" / "end of turn" cards, each with a test.
6. Space station d8 defense + build action (5 energy, max 4).
