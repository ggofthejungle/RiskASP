namespace UI
{
    // One place for the links on the main menu. Paste the real invite into DiscordInvite.
    public static class GameLinks
    {
        public const string DiscordInvite = "";

        public static bool HasDiscord =>
            DiscordInvite.StartsWith("http://") || DiscordInvite.StartsWith("https://");
    }
}
