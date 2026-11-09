using System;
using System.Collections.Generic;
using System.Linq;
using Cards;
using Expansions;
using Map;
using player;

namespace Actions
{
    // ActionData, the payload, lives in InvadeEarth.Rules so a server can read it with no Unity engine.
    // This codec is the Unity side: ids in, scene objects out.

    public static class ActionCodec
    {
        public static ActionData ToData(PlayerAction action, GameManager gm)
        {
            var data = new ActionData
            {
                Version = ActionData.CurrentVersion,
                PlayerId = gm.GetPlayerId(action.Player),
                Turn = action.Turn
            };
            switch (action)
            {
                case AttackAction a:
                    data.Type = ActionData.TypeAttack;
                    data.AttackTurn = a.AttackTurn;
                    data.Origin = a.Origin.Name;
                    data.Target = a.Target.Name;
                    data.Troops = a.Troops;
                    data.Commanders = JoinCommanders(a.Commanders);
                    break;
                case AttackReinforceAction r:
                    data.Type = ActionData.TypeAttackReinforce;
                    data.AttackTurn = r.AttackTurn;
                    data.Origin = r.From.Name;
                    data.Target = r.To.Name;
                    data.Troops = r.AttackingTroops;
                    data.ReinforceTroops = r.ReinforcingTroops;
                    data.Commanders = JoinCommanders(r.MovingCommanders);
                    break;
                case EndPhaseAction:
                    data.Type = ActionData.TypeEndPhase;
                    break;
                case DecisionResponseAction d:
                    data.Type = ActionData.TypeDecisionResponse;
                    data.Option = d.Option ?? "";
                    break;
                case BidAction b:
                    data.Type = ActionData.TypeBid;
                    data.Amount = b.EnergyThePlayerBid;
                    break;
                case ChooseTurnPositionAction c:
                    data.Type = ActionData.TypeChooseTurnPosition;
                    data.Position = c.Position;
                    break;
                case ReinforceAction rf:
                    data.Type = ActionData.TypeReinforce;
                    data.Origin = rf.Territory.Name;
                    data.Troops = rf.Troops;
                    break;
                case FortifyAction f:
                    data.Type = ActionData.TypeFortify;
                    data.Origin = f.From.Name;
                    data.Target = f.To.Name;
                    data.Troops = f.MovedTroops;
                    data.Commanders = JoinCommanders(f.MovingCommanders);
                    break;
                case HireCommanderAction h:
                    data.Type = ActionData.TypeHire;
                    data.Kind = h.Type.ToString();
                    data.Origin = h.Territory.Name;
                    break;
                case BuildSpaceStationAction build:
                    data.Type = ActionData.TypeBuild;
                    data.Origin = build.Territory.Name;
                    break;
                case BuyCommandCardAction buy:
                    data.Type = ActionData.TypeBuy;
                    data.Deck = buy.Deck.ToString();
                    break;
                case CloseShopAction:
                    data.Type = ActionData.TypeCloseShop;
                    break;
                case PlayCommandCardAction play:
                    data.Type = ActionData.TypePlay;
                    data.CardId = play.Card.Id;
                    data.Targets = EncodeTargets(play.Targets, gm);
                    break;
                default:
                    throw new NotSupportedException($"ActionCodec: {action.GetType().Name} is not serializable yet");
            }
            return data;
        }

        public static PlayerAction FromData(ActionData data, GameManager gm)
        {
            if (data.Version > ActionData.CurrentVersion)
                throw new NotSupportedException($"ActionCodec: version {data.Version} is newer than {ActionData.CurrentVersion}");

            var actor = gm.GetPlayerById(data.PlayerId);
            if (actor == null)
                throw new ArgumentException($"ActionCodec: unknown player id {data.PlayerId}");

            var tr = TerritoryRepository.Instance;
            switch (data.Type)
            {
                case ActionData.TypeAttack:
                    return new AttackAction(actor, data.Turn, data.AttackTurn,
                        tr.FromName(data.Origin), tr.FromName(data.Target), data.Troops, ParseCommanders(data.Commanders));
                case ActionData.TypeAttackReinforce:
                    var attack = new AttackAction(actor, data.Turn, data.AttackTurn,
                        tr.FromName(data.Origin), tr.FromName(data.Target), data.Troops);
                    return new AttackReinforceAction(actor, data.Turn, data.AttackTurn, attack, data.ReinforceTroops,
                        ParseCommanders(data.Commanders));
                case ActionData.TypeEndPhase:
                    return new EndPhaseAction(actor, data.Turn);
                case ActionData.TypeDecisionResponse:
                    return new DecisionResponseAction(actor, data.Turn, data.Option);
                case ActionData.TypeBid:
                    return new BidAction(actor, data.Turn, data.Amount);
                case ActionData.TypeChooseTurnPosition:
                    return new ChooseTurnPositionAction(actor, data.Turn, data.Position);
                case ActionData.TypeReinforce:
                    return new ReinforceAction(actor, data.Turn, tr.FromName(data.Origin), data.Troops);
                case ActionData.TypeFortify:
                    return new FortifyAction(actor, data.Turn, tr.FromName(data.Origin), tr.FromName(data.Target),
                        data.Troops, ParseCommanders(data.Commanders));
                case ActionData.TypeHire:
                    return new HireCommanderAction(actor, data.Turn, ParseCommander(data.Kind), tr.FromName(data.Origin));
                case ActionData.TypeBuild:
                    return new BuildSpaceStationAction(actor, data.Turn, tr.FromName(data.Origin));
                case ActionData.TypeBuy:
                    return new BuyCommandCardAction(actor, data.Turn, ParseCommander(data.Deck));
                case ActionData.TypeCloseShop:
                    return new CloseShopAction(actor, data.Turn);
                case ActionData.TypePlay:
                {
                    var card = FindCard(data.CardId);
                    // Online, this device may only have a face-down card for it until now.
                    gm?.CommandCards?.Reveal(actor, card);
                    return new PlayCommandCardAction(actor, data.Turn, card, DecodeTargets(data.Targets, gm));
                }
                default:
                    throw new NotSupportedException($"ActionCodec: unknown action type '{data.Type}'");
            }
        }

        static string JoinCommanders(IEnumerable<CommanderType> types) =>
            types == null ? "" : string.Join(",", types);

        static List<CommanderType> ParseCommanders(string s) =>
            string.IsNullOrEmpty(s)
                ? new List<CommanderType>()
                : s.Split(',').Select(ParseCommander).ToList();

        static CommanderType ParseCommander(string s) =>
            (CommanderType)Enum.Parse(typeof(CommanderType), s);

        static string EncodeTargets(object[] targets, GameManager gm)
        {
            if (targets == null || targets.Length == 0) return "";
            return string.Join(";", targets.Select(t => EncodeOne(t, gm)));
        }

        static string EncodeOne(object target, GameManager gm) => target switch
        {
            Territory territory => "T:" + territory.Name,
            Player p => "P:" + gm.GetPlayerId(p),
            CommanderType commander => "C:" + commander,
            _ => throw new NotSupportedException($"ActionCodec: can't encode a {target?.GetType().Name ?? "null"} target")
        };

        static object[] DecodeTargets(string encoded, GameManager gm)
        {
            if (string.IsNullOrEmpty(encoded)) return Array.Empty<object>();
            var tr = TerritoryRepository.Instance;
            return encoded.Split(';').Select(part =>
            {
                if (part.Length < 2 || part[1] != ':')
                    throw new ArgumentException($"ActionCodec: bad target '{part}'");
                var body = part.Substring(2);
                return part[0] switch
                {
                    'T' => (object)tr.FromName(body),
                    'P' => gm.GetPlayerById(int.Parse(body)),
                    'C' => ParseCommander(body),
                    _ => throw new ArgumentException($"ActionCodec: bad target '{part}'")
                };
            }).ToArray();
        }

        static Card FindCard(int id)
        {
            var cards = CardRepository.Instance != null ? CardRepository.Instance.AllCards : null;
            if (cards == null) throw new InvalidOperationException("ActionCodec: no card repository");
            foreach (var card in cards)
                if (card.Id == id) return card;
            throw new ArgumentException($"ActionCodec: unknown card id {id}");
        }
    }
}
