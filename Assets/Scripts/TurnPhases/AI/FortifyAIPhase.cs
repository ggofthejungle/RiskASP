using System;
using Actions;
using EmbASP.predicates;
using Extensions;
using it.unical.mat.embasp.@base;
using it.unical.mat.embasp.languages.asp;
using Map;
using Player = player.Player;
using UnityEngine;

namespace TurnPhases.AI
{
    public class FortifyAIPhase : IAIPhase
    {
        private readonly GameManager _gm;
        private readonly ActionReader _ar;
        private readonly TerritoryRepository _tr;

        private FortifyPhase _fortifyPhase => _gm.FortifyPhase;


        public FortifyAIPhase(GameManager gm, ActionReader ar, TerritoryRepository tr)
        {
            _gm = gm;
            _ar = ar;
            _tr = tr;
        }

        public void OnPhaseStart()
        {
        }

        public void OnRequest(Player player, InputProgram inputProgram)
        {
            
        }

        public void OnResponse(Player player, AnswerSet answerSet)
        {
            var queued = false;
            foreach (var atom in answerSet.Atoms)
            {
                PlayerAction action = null;
                if (atom is FortifyPredicate move)
                {
                    var from = _tr.FromName(move.From.StripQuotes());
                    var to = _tr.FromName(move.To.StripQuotes());
                    action = new FortifyAction(player, move.Turn, from, to, move.Troops);
                }

                if (atom is EndFortifyPredicate endFortify)
                    action = new EndPhaseAction(player, endFortify.Turn);

                if (action != null)
                {
                    _ar.AddAction(action);
                    queued = true;
                }
            }
            if (!queued)
                _ar.AddAction(new EndPhaseAction(player, _gm.Turn));
        }

        public void OnFailure(Player player)
        {
            _ar.AddAction(new EndPhaseAction(player, _gm.Turn));
        }

        public void OnPhaseEnd()
        {
        }
    }
}