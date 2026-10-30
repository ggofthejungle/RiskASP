using System;
using Map;

namespace Actions
{
    // Flat, serializable form of a PlayerAction: ids instead of object references.
    // This is what online play will send (e.g. JsonUtility.ToJson / FromJson).
    // Covers the Attack phase for now: Attack, AttackReinforce, EndPhase, DecisionResponse.
    [Serializable]
    public class ActionData
    {
        public string Type;
        public int PlayerId;
        public int Turn;
        public int AttackTurn;
        public string Origin;
        public string Target;
        public int Troops;          // Attack: attacking troops. AttackReinforce: attacking troops of the original attack.
        public int ReinforceTroops; // AttackReinforce only
        public string Option;       // DecisionResponse only
        public string Commanders;   // Attack: joining commanders; AttackReinforce: commanders moving in (comma-separated)

        public const string TypeAttack = "Attack";
        public const string TypeAttackReinforce = "AttackReinforce";
        public const string TypeEndPhase = "EndPhase";
        public const string TypeDecisionResponse = "DecisionResponse";
    }

    public static class ActionCodec
    {
        public static ActionData ToData(PlayerAction action, GameManager gm)
        {
            var data = new ActionData { PlayerId = gm.GetPlayerId(action.Player), Turn = action.Turn };
            switch (action)
            {
                case AttackAction a:
                    data.Type = ActionData.TypeAttack;
                    data.AttackTurn = a.AttackTurn;
                    data.Origin = a.Origin.Name;
                    data.Target = a.Target.Name;
                    data.Troops = a.Troops;
                    data.Commanders = string.Join(",", a.Commanders);
                    break;
                case AttackReinforceAction r:
                    data.Type = ActionData.TypeAttackReinforce;
                    data.AttackTurn = r.AttackTurn;
                    data.Origin = r.From.Name;
                    data.Target = r.To.Name;
                    data.Troops = r.AttackingTroops;
                    data.ReinforceTroops = r.ReinforcingTroops;
                    data.Commanders = string.Join(",", r.MovingCommanders);
                    break;
                case EndPhaseAction:
                    data.Type = ActionData.TypeEndPhase;
                    break;
                case DecisionResponseAction d:
                    data.Type = ActionData.TypeDecisionResponse;
                    data.Option = d.Option;
                    break;
                default:
                    throw new NotSupportedException($"ActionCodec: {action.GetType().Name} is not serializable yet");
            }
            return data;
        }

        private static System.Collections.Generic.List<Expansions.CommanderType> ParseCommanders(string s) =>
            string.IsNullOrEmpty(s)
                ? new System.Collections.Generic.List<Expansions.CommanderType>()
                : System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(s.Split(','),
                    x => (Expansions.CommanderType)Enum.Parse(typeof(Expansions.CommanderType), x)));

        public static PlayerAction FromData(ActionData data, GameManager gm)
        {
            var player = gm.GetPlayerById(data.PlayerId);
            if (player == null)
                throw new ArgumentException($"ActionCodec: unknown player id {data.PlayerId}");

            var tr = TerritoryRepository.Instance;
            switch (data.Type)
            {
                case ActionData.TypeAttack:
                    return new AttackAction(player, data.Turn, data.AttackTurn,
                        tr.FromName(data.Origin), tr.FromName(data.Target), data.Troops, ParseCommanders(data.Commanders));
                case ActionData.TypeAttackReinforce:
                    var attack = new AttackAction(player, data.Turn, data.AttackTurn,
                        tr.FromName(data.Origin), tr.FromName(data.Target), data.Troops);
                    return new AttackReinforceAction(player, data.Turn, data.AttackTurn, attack, data.ReinforceTroops,
                        ParseCommanders(data.Commanders));
                case ActionData.TypeEndPhase:
                    return new EndPhaseAction(player, data.Turn);
                case ActionData.TypeDecisionResponse:
                    return new DecisionResponseAction(player, data.Turn, data.Option);
                default:
                    throw new NotSupportedException($"ActionCodec: unknown action type '{data.Type}'");
            }
        }
    }
}
