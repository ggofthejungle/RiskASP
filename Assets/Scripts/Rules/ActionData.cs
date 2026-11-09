using System;

namespace Actions
{
    // Flat form of a player action: ids instead of scene objects.
    // This is the payload a client sends and a server applies. No Unity types.
    [Serializable]
    public class ActionData
    {
        public const int CurrentVersion = 1;

        public int Version;
        public string Type;
        public int PlayerId;
        public int Turn;
        public int AttackTurn;
        public string Origin;
        public string Target;
        public int Troops;
        public int ReinforceTroops;
        public string Option;
        public string Commanders;
        public int Amount;
        public int Position;
        public string Deck;
        public int CardId;
        public string Targets;
        public string Kind;

        public const string TypeAttack = "Attack";
        public const string TypeAttackReinforce = "AttackReinforce";
        public const string TypeEndPhase = "EndPhase";
        public const string TypeDecisionResponse = "DecisionResponse";
        public const string TypeBid = "Bid";
        public const string TypeChooseTurnPosition = "ChooseTurnPosition";
        public const string TypeReinforce = "Reinforce";
        public const string TypeFortify = "Fortify";
        public const string TypeHire = "Hire";
        public const string TypeBuild = "Build";
        public const string TypeBuy = "Buy";
        public const string TypePlay = "Play";
        public const string TypeCloseShop = "CloseShop";
    }
}
