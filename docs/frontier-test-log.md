# Frontier test log

Play Mode probes only (the NUnit suite hangs Unity). Seed 21, seas and moon on, unless a row says otherwise. Last run: Nov 7, 2026, ~6:30 PM. Console had no exceptions. Illegal actions still write an error line and then change nothing.

## Already passed

### Nov 7, ~6:30 PM — sea groups and coast lines

Picture of the map after the seas were moved. No sea disc overlapped another territory. No connection line passed through a third territory.

- Groups: Gulf + North Atlantic, South Atlantic + Cape Basin, Indian Ocean + Monsoon Reach, South Pacific + East Passage + North Pacific, Arctic Ocean + High North.
- 11 seas and the 6 moon territories (59 on the board). The moon row is below the land and linked only to its side-by-side neighbours.
- Siam is not connected to Monsoon Reach. Alaska is not connected to the arctic group.

### Nov 7, ~7:10 PM — naval commander, naval cards, moon on screen

Same match scene. The moon row was behind the card bar, so the camera pulls back until that row sits just above it. 37 checks, 0 failures.

- Sea to a neighbouring sea, with the naval commander: allowed. 3 MODs plus that commander: rejected. A sea that does not touch the target: rejected.
- Sea to a neighbouring land: MODs allowed, the naval commander rejected, a diplomat allowed.
- Land to a neighbouring sea: MODs allowed, the land commander rejected. A land that does not touch that sea: rejected.
- A 1-unit sea cannot attack. Fortify can move MODs from your sea onto your coast, and cannot bring the naval commander, empty the sea, or skip to a sea that does not touch.
- Assemble and reinforcements stay on seas. A water rocket refuses land and leaves a 1-unit sea at 1. Hidden Energy pays 4 only while you still hold the drawn sea. Water Death Trap and naval Stealth MODs answer a sea invasion only. The trap cut 4 attackers to 2.
- Moon to a neighbouring moon with the space commander: allowed. That commander cannot enter a sea or land. A station lets MODs on the near side attack that land.

### Nov 7, ~3:40 PM — every new card once, at 5 then 4 then 3 players

First command step. Each seat was given Naval, Space, Land, Diplomat, and Nuclear.

- Standing: Naval on sea only, Space on moon only, Land off the moon, Diplomat on a sea, Nuclear on the moon.
- 55 territories. 5 players ~25 MODs, 4 ~30, 3 ~35. All seats alive.
- Assemble and Reinforcements added units on sea and moon.
- Rocket Strike, Scatter Bomb, Aqua Brother, and Nicky Boy removed units and left at least 1.
- Hidden Energy +4 and Energy Extraction +7 at end of turn (25 energy to 36) when the sea and the whole moon were held.
- Invade Surface opened one land from `landing`.
- Colony Influence refused to play (it scores at the end).
- Stealth MODs raised a defender from 1 to 4. Water Death Trap and Orbital Mines cut the attacker about in half.
- Both Frequency Jams played.
- A 4-player deal that did not own `landing` or `south_ridge` could play Invade Surface after those were handed over.

### Nov 7, ~4:10 PM — 2 players, clicks and edges

Same map. The current seat was driven through the real click path (grey territories, then the selection code, then the action check).

Clicks:

- A click during a bot's turn changes nothing.
- An enemy territory is grey during reinforce. Clicking it does not place a unit. A reinforce action aimed at it is rejected.
- In the attack phase, someone else's territory cannot be the attacker.
- A 1-unit territory cannot start an attack. Your own sea can.
- A land that does not border that sea stays grey. Clicking it does not attack. An attack action aimed at it is rejected.
- You cannot attack your own territory.
- An enemy coast next to your sea can be selected. That attack was queued and not fought.
- A naval commander cannot attack onto land. A space commander cannot attack a sea.
- After Invade Surface, `landing` can click the drawn land (`north_africa` on the last run) and cannot click a different land. `highlands` cannot launch it.
- You cannot start a fortify from an enemy territory.

Cards and stations:

- A rocket on a 1-unit sea leaves it at 1. A rocket cannot empty a sea.
- Reinforcements rejects the same sea three times, and a moon mixed into a sea trio.
- A rocket rejects your own sea. Water Death Trap rejects a land invasion.
- Death trap on a 1-unit invading stack stays 1. On 3 units it stays at least 1 (the last run left 1).
- Decoys cannot move a naval commander onto land. Naval cannot be hired on land. Space cannot be hired on a sea. A station cannot be built at sea.
- Two stations: removing one unlinks only that land. The other station's launch link stays.
- Hidden Energy pays nothing if you lose the drawn sea before the turn ends (20 stayed 20).
- Energy Extraction pays nothing while one moon is missing (5/6). Two copies pay 14 when you hold every moon (17 to 31).
- A card you cannot afford is rejected. The other player cannot play on your turn. A jammed player cannot answer with a card.

## Fixed while testing

- Move-in could drop a Space commander onto a land territory. Commanders that cannot stand on the destination now stay behind, and the move-in / fortify picker does not offer them.
- Evacuation could drop a Naval commander onto land. A commander that cannot stand at the destination is lost with the territory instead.
- The attack, reinforce, and fortify click code ignored a territory that was grey or owned by the wrong player only because the collider was off. It now ignores those clicks itself.

## Not covered yet

- Take-turns placement (claiming every territory, then the 3-unit batches). With seas and the moon on, that is 59 territories.
- Clicking with the mouse in the Game view. The probes call the button methods and the territory-click handler, so a grey territory that the collider still hits has not been tried by a real pointer.

### Nov 7, ~4:35 PM — floors, caps, jam, Cease Fire, sea conquest (3 players, then land-only)

3 players, seed 21, two devastated lands, seas and moon on. Then a separate 2-player match with seas and moon off.

- Devastation removed two land territories (never a sea or a moon) and left every remaining sea and moon with a neighbour. The deal left nobody on 0 units.
- Scatter Bomb on a 1-unit enemy sea left it at 1 and did not touch our seas. Aqua Brother, Nicky Boy, and The Mother, each fired while every relevant territory had 1 unit, left every territory at least 1.
- Assassin Bomb on an enemy naval commander either killed that commander or left the stack at the same size.
- Decoys moved a naval commander between two of our seas and a space commander between two moon territories. A space commander could not move onto a sea.
- A second naval commander, a fifth station, and a fifth card buy were all rejected. Invade Surface was refused while we held every land, then drew one once four lands were given back.
- 4 attacking units are rejected. A diplomat on a station land may attack `landing`. A land commander on that land may not.
- A Water Death Trap on a 2-unit sea left 1 and cancelled the invasion.
- Pass for the rest of the turn skipped the next window, and the attack still resolved.
- A Frequency Jammed player got no reaction window. After the jam was lifted for one answer, Cease Fire blocked only that player. The other player's territory could still be attacked.
- Taking an enemy sea with the naval commander joining moved that commander in, and the defender's commander was gone.
- Fortify cannot empty a sea, cannot skip to a non-neighbour, and cannot bring a naval commander onto land. Plain units can move from your sea onto your coast.
- The next turn cleared the jam, and Invade Surface no longer let the moon attack the drawn land.
- Land-only: 42 land territories, naval and space decks at 0, a naval hire refused, and a land territory still had an enemy neighbour.

### Nov 7, ~4:15 PM — card bar, launch, dice, move-in, Cease Fire (2 players)

The buttons themselves: Hire, Build, the card preview, the end-phase button, the attack click, the move-in picker, and the defender prompt.

- Hire asks first. Land is grey for a Naval commander; the sea click places one (South Atlantic). A sea is grey for a Space commander; the moon click places one (South Ridge).
- Build: a sea is grey. The land click builds a station. Alaska was not connected to the moon before; afterward it links to `landing` and `south_ridge`.
- Invade Surface opens from the card preview. Confirming it drew Northern Europe.
- The end-phase button goes to Attack.
- One real roll from Alaska onto `landing`: attacker lost 0, defender lost 1, Alaska took the moon.
- The move-in picker opened. It did not offer a commander who cannot stand on the moon. Move-in left 1 unit on Alaska and put 12 on `landing`. The Space commander stayed on South Ridge.
- Cease Fire from the defender prompt cancelled the next invasion. That player's territory stayed grey, and a second attack on them was rejected.
