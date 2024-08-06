﻿using System;
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
        private GameManager gm;
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
                new Action(ModReduction),
                new Action(Redeployment),
                new Action<Territory>(TerritorialStation),
                new Action(AssembleMods),
                new Action(ColonyInfluence),
                new Action<Player>(FrequencyJam),
                new Action<Territory>(LandDeathTrap),
                new Action(Reinforcements),
                new Action(ScoutForces),
                new Action(StealthMods),
                new Action<Territory>(StealthStation),
                new Action(AssembleMods),
                new Action(ColonyInfluence),
                new Action<Player>(FrequencyJam),
                new Action(HiddenEnergy),
                new Action(Reinforcements),
                new Action(StealthMods),
                new Action<Territory>(WaterDeathTrap),
                new Action(AquaBrother),
                new Action(Armageddon),
                new Action<Commander>(AssassinBomb),
                new Action(NickyBoy),
                new Action(RocketStrikeLand),
                new Action(RocketStrikeMoon),
                new Action(RocketStrikeWater),
                new Action(ScatterBombLand),
                new Action(ScatterBombMoon),
                new Action(ScatterBombWater),
                new Action(TheMother),
                new Action(AssembleMods),
                new Action(ColonyInfluence),
                new Action(EnergyExtraction),
                new Action<Player>(FrequencyJam),
                new Action(InvadeSurface),
                new Action<Territory>(OrbitalMines),
                new Action(Reinforcements),
                new Action(StealthMods)
            };
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
             int count = 0;
             for (int x = 0; x < gm.Players.Count; x++)
             {
                 if (gm.Players[x].Name != cardOwner.Name)
                 {
                     gm.Players[x].SetEnergy(gm.Players[x].GetEnergy() - 1); 
                     count++;
                 }
             }
            
             cardOwner.SetEnergy(cardOwner.GetEnergy() + count);

        }

        public void Evacuation(Territory attackedTerritory, Territory destinationTerritory)
        {
            // Opponent Invades.
            // Move all units from the attacked territory to any territory you occupy.
            int troopCount = attackedTerritory.Troops;
            attackedTerritory.RemoveTroops(troopCount);
            destinationTerritory.AddTroops(troopCount);
        }

        public void ModReduction()
        {
            // Before First Invasion
            // All of your opponents must remove 4 MODs in turn order. Then you remove 2 MODs.
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
            destinationTerritory.containsSpaceStation = true;

        }
        
        //LandCommanders
        public void AssembleMods()
        {
            // Before First Invasion
            // Place 3 MODS on any one land territory you occupy
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
            double troopsCount = invadingTerritory.GetAvailableTroops();
            invadingTerritory.SetTroops((int)Math.Ceiling(troopsCount/2));
        }

        public void Reinforcements()
        {
            // Before First Invasion
            // Place 3 MODS, one each on 3 different land territories you occupy
        }

        public void ScoutForces()
        {
            // Before First Invasion
            // Draw a land territory card and secretly place it facedown in front of you. Place 5 MODS on this card. When you occupy this
            // territory immediately place the MODS. Discard the territory card
        }

        public void StealthMods()
        {
            // Before First Invasion
            // Draw a land territory card and secretly place it facedown in front of you. Place 5 MODS on this card. When you occupy this
            // territory immediately place the MODS. Discard the territory card
        }

        public void StealthStation(Territory defendingTerritory)
        {
            //Opponent Invades.
            //Place a space station in the defending land territory.
            defendingTerritory.containsSpaceStation = true;

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
            double troopsCount = invadingTerritory.GetAvailableTroops();
            invadingTerritory.SetTroops((int)Math.Ceiling(troopsCount/2));
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

        public void AssassinBomb(Commander oppenentsCommander)
        {
            // Before First Invasion
            // Choose an opponent's commander. Roll an 8-sided die. If you roll a 3 or higher destroy the chosen commander
            
        }

        public void NickyBoy()
        {
            // Before First Invasion
            // Choose a planet, then roll a 6-sided die. Constul the table to see the lunar or asteroid zone(s) affected. Destroy one unit in
            //each territory in the zone(s) rolled
        }
        
        // Before First Invasion
        // Choose a planet, then roll a 6-sided die. Constul the table to see the lunar or asteroid zone(s) affected. Destroy one unit in
        //each territory in the zone(s) rolled
        public void RocketStrikeLand()
        {
            
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
        public void ScatterBombLand()
        {
            
        }

        public void ScatterBombMoon()
        {
            
        }

        public void ScatterBombWater()
        {
            
        }

        public void TheMother()
        {
            //Before First Invasion Choose a planet, then roll a 6-sided die. Constul the table to see the land zone(s) affected. Destroy one unit in each territory
            //in the zone(s) rolled. 
        }
        
        
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
            double troopsCount = invadingTerritory.GetAvailableTroops();
            invadingTerritory.SetTroops((int)Math.Ceiling(troopsCount/2));
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