namespace UI
{
    public enum MatchKind
    {
        None,
        SinglePlayer,
        HotSeat,
        Online,
        Correspondence
    }

    // Set by the main menu, read once when the match scene loads.
    // Tests leave it unarmed, so they keep the old "every human is local" behaviour.
    public static class MatchSession
    {
        public static MatchKind Kind { get; private set; } = MatchKind.None;
        static MatchKind _pending = MatchKind.None;

        // The rules for the next match. Online, the host's choice (from the room) replaces these on every device.
        public static MatchOptions Options
        {
            get => _options ??= MatchOptions.Saved();
            set => _options = value;
        }
        static MatchOptions _options;

        public static void Select(MatchKind kind) => Kind = kind;

        // Optional. Consumed when the match starts; local saves store it so resume can replay.
        public static int? Seed { get; set; }

        public static void ArmForNextLoad() => _pending = Kind;

        public static bool TryConsume(out MatchKind kind)
        {
            kind = _pending;
            _pending = MatchKind.None;
            return kind != MatchKind.None;
        }
    }

    public enum TerritorySetup
    {
        Random,    // territories and units are dealt out
        TakeTurns, // 2210 setup: in turn order, claim empty territories one MOD at a time, then place 3 at a time
    }

    public sealed class MatchOptions
    {
        public const int MaxDevastated = 4;
        public int Devastated;
        public TerritorySetup Placement;
        // Sea zones in the gaps, plus a moon. Off for tests and for a scene loaded with no menu.
        public bool Frontiers;

        // A match scene loaded without the menu (tests, the editor) keeps the old setup.
        public static MatchOptions Plain => new() { Devastated = 0, Placement = TerritorySetup.Random, Frontiers = false };

        const string DevastatedPref = "ie.rules.devastated";
        const string PlacementPref = "ie.rules.placement";
        const string FrontiersPref = "ie.rules.frontiers";

        public static MatchOptions Saved() => new()
        {
            Devastated = UnityEngine.Mathf.Clamp(UnityEngine.PlayerPrefs.GetInt(DevastatedPref, MaxDevastated), 0, MaxDevastated),
            Placement = (TerritorySetup)UnityEngine.Mathf.Clamp(UnityEngine.PlayerPrefs.GetInt(PlacementPref, (int)TerritorySetup.TakeTurns), 0, 1),
            Frontiers = UnityEngine.PlayerPrefs.GetInt(FrontiersPref, 1) == 1,
        };

        public void Save()
        {
            UnityEngine.PlayerPrefs.SetInt(DevastatedPref, Devastated);
            UnityEngine.PlayerPrefs.SetInt(PlacementPref, (int)Placement);
            UnityEngine.PlayerPrefs.SetInt(FrontiersPref, Frontiers ? 1 : 0);
        }

        public string PlacementLabel => Placement == TerritorySetup.TakeTurns ? "Take turns" : "Random";
        public string FrontiersLabel => Frontiers ? "On" : "Off";
        public string Summary => $"{Devastated} devastated territories · starting territories: {PlacementLabel.ToLowerInvariant()} · seas and moon: {FrontiersLabel.ToLowerInvariant()}";

        // For the room's session property: "4,TakeTurns,1".
        public string Encode() => Devastated + "," + Placement + "," + (Frontiers ? "1" : "0");

        public static MatchOptions Decode(string text)
        {
            var o = Plain;
            var parts = (text ?? "").Split(',');
            if (parts.Length > 0 && int.TryParse(parts[0], out var d)) o.Devastated = UnityEngine.Mathf.Clamp(d, 0, MaxDevastated);
            if (parts.Length > 1 && System.Enum.TryParse(parts[1], out TerritorySetup p)) o.Placement = p;
            if (parts.Length > 2) o.Frontiers = parts[2] == "1";
            return o;
        }
    }
}
