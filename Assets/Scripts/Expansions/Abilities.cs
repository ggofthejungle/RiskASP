using player;
using UnityEngine;

namespace Expansions
{
    public class Abilities
    {
        private GameManager gm;
        public delegate void AbilityDelegate();
        
        //Diplomat
        public static void ExecuteAbility(AbilityDelegate ability)
        {
            ability();
        }
        
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

        public void DecoyRevealed()
        {
            //Before First Invasion
            //Move any number of your commanders to any number of territories you control.
        }

        public void EnergyCrisis()
        {
            //Before First Invasion
            //Collect one energy from each opponent
            // int count = 0;
            // for (int x = 0; x < gm.Players.Count; x++)
            // {
            //     if (gm.Players[x].Name != cardOwner.Name)
            //     {
            //         gm.Players[x].SetEnergy(gm.Players[x].GetEnergy() - 1); 
            //         count++;
            //     }
            // }
            //
            // cardOwner.SetEnergy(cardOwner.GetEnergy() + count);

        }

        public void Evacuation()
        {
            // Opponent Invades.
            // Move all units from the attacked territory to any territory you occupy.
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

        public void TerritorialStation()
        {
            //Before First Invasion
            //Place a space station on any land territory you occupy.
            
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

        public void FrequencyJam()
        {
            //Before First Invasion
            //Choose a player. The chosen player cannot play command cards during your turn
        }

        public void LandDeathTrap()
        {
            //Opponent Invades.
            //Your opponent must destroy half the units in the invading territory. Round up.
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

        public void StealthStation()
        {
            //Opponent Invades.
            //Place a space station in the defending land territory.
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

        public void WaterDeathTrap()
        {
            //Opponent Invades.
            //Your opponent must destroy half the units in the invading territory. Round up
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

        public void AssassinBomb()
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

        public void OrbitalMines()
        {
            //Opponent Invades.
            //Your opponent must destroy half the units in the invading territory. Round up. 
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