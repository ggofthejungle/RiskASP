using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Antlr4.Runtime.Misc;
using Map;
using player;
using UnityEngine;

namespace Expansions
{
    public class Abilities
    {
        private GameManager gm => GameManager.Instance; // was a field that was never assigned (NullReferenceException)
        public readonly List<Delegate> methods;

        public Abilities()
        {
            methods = new List<Delegate>
            {
                new Action(CeaseFire),
                new Action(ColonyInfluence),
                new Action<Player>(DecoyRevealed),
                new Action<Player>(EnergyCrisis),
                new Action<Territory, Territory>(Evacuation),
                new Action<Player>(ModReduction),
                new Action(Redeployment),
                new Action<Territory>(TerritorialStation),
                new Action<Territory>(AssembleMods),
                new Action(ColonyInfluence),
                new Action<Player>(FrequencyJam),
                new Action<Territory>(LandDeathTrap),
                new Action<Territory, Territory, Territory>(Reinforcements),
                new Action(ScoutForces),
                new Action<Territory>(StealthMods),
                new Action<Territory>(StealthStation),
                new Action<Territory>(AssembleMods),
                new Action(ColonyInfluence),
                new Action<Player>(FrequencyJam),
                new Action(HiddenEnergy),
                new Action<Territory, Territory, Territory>(Reinforcements),
                new Action<Territory>(StealthMods),
                new Action<Territory>(WaterDeathTrap),
                new Action(AquaBrother),
                new Action(Armageddon),
                new Action<Territory, CommanderType>(AssassinBomb),
                new Action(NickyBoy),
                new Action<Territory>(RocketStrikeLand),
                new Action(RocketStrikeMoon),
                new Action(RocketStrikeWater),
                new Action<Player>(ScatterBombLand),
                new Action(ScatterBombMoon),
                new Action(ScatterBombWater),
                new Action(TheMother),
                new Action<Territory>(AssembleMods),
                new Action(ColonyInfluence),
                new Action(EnergyExtraction),
                new Action<Player>(FrequencyJam),
                new Action(InvadeSurface),
                new Action<Territory>(OrbitalMines),
                new Action<Territory, Territory, Territory>(Reinforcements),
                new Action<Territory>(StealthMods)
            };
        }

        // The ability for a card type, matched by NAME (not by list position, which had drifted:
        // the list has ModReduction, which has no CardType, so every card after it ran the wrong ability).
        // Card types that share an ability across commanders carry a suffix: ColonyInfluenceD/L/Na/S, StealthModsL/Na/S...
        public Delegate FindFor(Cards.CardType cardType)
        {
            var name = cardType.ToString();
            var exact = methods.FirstOrDefault(m => m.Method.Name == name);
            if (exact != null) return exact;
            foreach (var suffix in new[] { "Na", "D", "L", "S" })
                if (name.EndsWith(suffix))
                {
                    var baseName = name.Substring(0, name.Length - suffix.Length);
                    var match = methods.FirstOrDefault(m => m.Method.Name == baseName);
                    if (match != null) return match;
                }
            Debug.LogWarning($"Abilities: no ability for card {name}; it will do nothing.");
            return new Action(() => { });
        }

        // "Destroy half the units, round up", but a territory always keeps at least 1 unit.
        private static void DestroyHalfRoundUp(Territory territory)
        {
            var troops = territory.Troops;
            var destroyed = (int)Math.Ceiling(troops / 2.0);
            territory.SetTroops(Math.Max(1, troops - destroyed));
        }

        //Diplomat
        
        
        public void CeaseFire()
        {
            //Play when an opponent declares invasion in any territory
            //stops the invasion and can not attack your territories for the rest of your turn
            
        }

        public void ColonyInfluence()
        {
            //Play at the end of the game
            //If your diplomat commander is still alive move score marker 3 spaces ahead
            
        }

        public void DecoyRevealed(Player cardowner)
        {
            //Before First Invasion
            //Move any number of your commanders to any number of territories you control.
           List<Territory> territoriesList = cardowner.Territories.ToList();
           ArrayList<Commander> commandersToRemove = new ArrayList<Commander>();
           
           
           //Ask user what commanders they want to remove
           //and to where they want to move it
           Territory selectedTerritory = territoriesList[0];

           for (int x = 0; x < commandersToRemove.Count; x++)
           { 
               selectedTerritory.AddCommander(commandersToRemove[x]);
           }
           
           

           
           //How to select certain territories, need to change phase?
        }

        public void EnergyCrisis(Player cardOwner)
        {
            // Before First Invasion
            // Collect one energy from each opponent
             // Only alive opponents, and only if they have energy to give (never below 0).
             int count = 0;
             foreach (var opponent in gm.Players)
             {
                 if (opponent == cardOwner || opponent.IsDead() || opponent.GetEnergy() <= 0)
                     continue;
                 opponent.SetEnergy(opponent.GetEnergy() - 1);
                 count++;
             }

             cardOwner.SetEnergy(cardOwner.GetEnergy() + count);

        }

        public void Evacuation(Territory attackedTerritory, Territory destinationTerritory)
        {
            // Opponent Invades.
            // Move all units from the attacked territory to any territory you occupy.
            // Units AND commanders leave (stations stay); the attacker then takes the empty territory.
            int troopCount = attackedTerritory.Troops;
            attackedTerritory.RemoveTroops(troopCount);
            destinationTerritory.AddTroops(troopCount);
            foreach (var commander in attackedTerritory.CommandersList.ToList())
            {
                attackedTerritory.RemoveCommander(commander);
                destinationTerritory.AddCommander(commander);
            }
        }

        public void ModReduction(Player cardOwner)
        {
            // All of your opponents must remove 4 MODs in turn order. Then you remove 2 MODs.
            // Automated: units come off each player's biggest territories; a territory always keeps 1.
            foreach (var opponent in gm.GetPlayersInTurnOrder().Where(p => p != cardOwner && !p.IsDead()))
                RemoveUnitsFromLargest(opponent, 4);
            RemoveUnitsFromLargest(cardOwner, 2);
        }

        private static void RemoveUnitsFromLargest(Player player, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var largest = player.Territories.Where(t => t.Troops > 1).OrderByDescending(t => t.Troops).FirstOrDefault();
                if (largest == null) return;
                largest.RemoveTroops(1);
            }
        }

        // Destroy some units but always leave 1 (our game has no empty/neutral territories).
        private static void DestroyUnits(Territory territory, int count)
        {
            var destroy = Math.Min(count, territory.Troops - 1);
            if (destroy > 0) territory.RemoveTroops(destroy);
        }

        public void Redeployment()
        {
            //End of Turn.
            //Take an extra free move this turn. You may only take this free move after you have finished attacking.
        }

        public void TerritorialStation(Territory destinationTerritory)
        {
            //Before First Invasion
            //Place a space station on any land territory you occupy.
            destinationTerritory.AddSpaceStation();

        }
        
        //LandCommanders
        public void AssembleMods(Territory territory)
        {
            // Before First Invasion
            // Place 3 MODS on any one land territory you occupy
            territory.AddTroops(3);
        }

        // private void ColonyInfluence()
        // {
        //     //End of Game.
        //     //If your Land Commander is still alive, move your score marker ahead 3 spaces
        // }

        public void FrequencyJam(Player chosenPlayer)
        {
            //Before First Invasion
            //Choose a player. The chosen player cannot play command cards during your turn
            chosenPlayer.canPlayCommandCards = false;
        }

        public void LandDeathTrap(Territory invadingTerritory)
        {
            //Opponent Invades.
            //Your opponent must destroy half the units in the invading territory. Round up.
            DestroyHalfRoundUp(invadingTerritory);
        }

        public void Reinforcements(Territory a, Territory b, Territory c)
        {
            // Before First Invasion: place 3 MODs, one each on 3 different land territories you occupy.
            a.AddTroops(1);
            b.AddTroops(1);
            c.AddTroops(1);
        }

        public void ScoutForces()
        {
            // Before First Invasion
            // Draw a land territory card and secretly place it facedown in front of you. Place 5 MODS on this card. When you occupy this
            // territory immediately place the MODS. Discard the territory card
        }

        public void StealthMods(Territory defendingTerritory)
        {
            // Opponent invades a land territory: place 3 additional defending MODs there.
            defendingTerritory.AddTroops(3);
        }

        public void StealthStation(Territory defendingTerritory)
        {
            //Opponent Invades.
            //Place a space station in the defending land territory.
            defendingTerritory.AddSpaceStation();

        }
        
        //NavalCommanderAbilities
        
        // private void AssembleMods()
        // {
        //     // Before First Invasion
        //     // Flip over the Arctic board. All units in North Pole Station, Svalbard, and Wendigo remain in those territories.
        //
        // }

        // private void ColonyInfluence()
        // {
        //     // End of Game.
        //     // If your Naval Commander is still alive, move your score marker ahead 3 spaces
        // }

        // private void FrequencyJam()
        // {
        //     //Before First Invasion
        //     //Choose a player. The chosen player cannot play command cards during your turn
        // }

        public void HiddenEnergy()
        {
            //Before First Invasion
            //Draw a water or lava territory card. If you occupy this water or lava territory at the end of your turn, collect 4 energy. Discard
            //the territory card at the end of this turn

        }

        // private void Reinforcements()
        // {
        //     // Before First Invasion
        //     // Place 3 MODS, one each on 3 different water or lava territories you occupy.
        // }

        // private void StealthMods()
        // {
        //     //Opponent Invades.
        //     //Place 3 additional defending MODS in the defending water or lava territory
        // }

        public void WaterDeathTrap(Territory invadingTerritory)
        {
            //Opponent Invades.
            //Your opponent must destroy half the units in the invading territory. Round up
            DestroyHalfRoundUp(invadingTerritory);
        }
        
        //NuclearCommanderAbilities

        public void AquaBrother()
        {
            // Before First Invasion
            // Choose a planet, then roll a 6-sided die. Constul the table to see the water/lava zone(s) affected. Destroy one unit in each
            //territory in the zone(s) rolled
        }

        public void Armageddon()
        {
            //Before First Invasion
            //All players, in turn order, may play any number of nuclear command cards without paying the energy cost.

        }

        public void AssassinBomb(Territory territory, CommanderType commanderType)
        {
            // Choose an opponent's commander. Roll an 8-sided die. On 3 or higher, destroy the chosen commander.
            var commander = territory.CommandersList.FirstOrDefault(c => c.CommanderType == commanderType);
            if (commander == null) return;
            var roll = GameRandom.Current.Range(1, 9);
            LastRoll = roll;
            if (roll >= 3)
            {
                territory.RemoveCommander(commander);
                GameManager.Instance?.ReportCommanderKilled(territory.Owner, commanderType, territory, "Assassin Bomb");
            }
        }

        public int LastRoll { get; private set; } // last die rolled by a card (for the log / UI / tests)

        public void NickyBoy()
        {
            // Before First Invasion
            // Choose a planet, then roll a 6-sided die. Constul the table to see the lunar or asteroid zone(s) affected. Destroy one unit in
            //each territory in the zone(s) rolled
        }
        
        // Before First Invasion
        // Choose a planet, then roll a 6-sided die. Constul the table to see the lunar or asteroid zone(s) affected. Destroy one unit in
        //each territory in the zone(s) rolled
        public void RocketStrikeLand(Territory target)
        {
            // Choose any opponent's land territory. Roll a 6-sided die. Destroy that many units there.
            var roll = GameRandom.Current.Range(1, 7);
            LastRoll = roll;
            DestroyUnits(target, roll);
        }

        public void RocketStrikeMoon()
        {
            
        }

        public void RocketStrikeWater()
        {
            
        }
        
        //Before First Invasion
        //Choose a planet and turn over 3 land territory cards for that planet. Destroy half the opponents' units on territories drawn.
        //Round up
        public void ScatterBombLand(Player cardOwner)
        {
            // Turn over 3 land territory cards. Destroy half the opponents' units on those territories (round up).
            var all = Map.TerritoryRepository.Instance.Territories;
            var drawn = all.OrderBy(_ => GameRandom.Current.Value).Take(3).ToList();
            LastScatterTargets = drawn;
            foreach (var t in drawn.Where(t => t.Owner != cardOwner))
                DestroyHalfRoundUp(t);
        }

        public List<Territory> LastScatterTargets { get; private set; } = new();

        public void ScatterBombMoon()
        {
            
        }

        public void ScatterBombWater()
        {
            
        }

        public void TheMother()
        {
            // Roll a 6-sided die. Destroy one unit in each territory of the continent rolled (everyone's).
            var continents = Map.ContinentRepository.Instance.Continents.Values.OrderBy(c => c.Name).ToList();
            if (continents.Count == 0) return;
            var roll = GameRandom.Current.Range(1, 7);
            LastRoll = roll;
            var continent = continents[(roll - 1) % continents.Count];
            LastContinentHit = continent;
            foreach (var t in continent.Territories)
                DestroyUnits(t, 1);
        }

        public Map.Continent LastContinentHit { get; private set; }
        
        
        //SpaceCommandersAbilities

        // public void AssembleMods()
        // {
        //     //Before First Invasion
        //     //Place 3 MODs on any one moon or asteroid territory you control
        // }

        // public void ColonyInfluence()
        // {
        //     //End of Game.
        //     //If your Space Commander is still alive, move your score marker ahead 3 spaces.
        // }

        public void EnergyExtraction()
        {
            // Before First Invasion
            // If you occupy all the the lunar or asteroid territoires in a region of space at the end of this turn, collect 7 energy
        }

        // public void FrequencyJam()
        // {
        //     //Before First Invasion
        //     //Choose a player. The chosen player cannot play command cards during your turn.
        // }

        public void InvadeSurface()
        {
            // Before First Invasion
            // Choose a planet. Turn over land territory cards for that planet until you turn over a territory you do not occupy. During this turn
            //you may attack this land territory from any surrounding lunar or asteroid territories you occupy
        }

        public void OrbitalMines(Territory invadingTerritory)
        {
            //Opponent Invades.
            //Your opponent must destroy half the units in the invading territory. Round up. 
            DestroyHalfRoundUp(invadingTerritory);
        }

        // public void Reinforcements()
        // {
        //     //Before First Invasion
        //     //Place 3 MODs, one each on 3 different lunar or asteroid territories you occupy.
        //
        // }

        // public void StealthMods()
        // {
        //     //Opponent Invades.
        //     //Place 3 additional defending MODs in the defending lunar or asteroid territory.
        //
        // }

        


    }
}