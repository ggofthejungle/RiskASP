using System.Collections.Generic;
using player;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Seats for a Play Mode test. Kept off the StartingPlayersConfiguration asset:
// changing that asset during Play Mode and loading it again locks the editor.
public static class TestSeats
{
    const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";
    static PlayerConfiguration _human;
    static PlayerColor _red;
    static PlayerColor _blue;

    public static void AliceAndBob()
    {
#if UNITY_EDITOR
        // Load once. A later LoadAssetAtPath during Play Mode waits on an asset refresh and the editor locks.
        if (_human == null)
            _human = AssetDatabase.LoadAssetAtPath<PlayerConfiguration>(HumanConfigPath);
        if (_red == null)
            _red = AssetDatabase.LoadAssetAtPath<PlayerColor>("Assets/ScriptableObjects/PlayerColors/Red.asset");
        if (_blue == null)
            _blue = AssetDatabase.LoadAssetAtPath<PlayerColor>("Assets/ScriptableObjects/PlayerColors/Blue.asset");
        GameManager.SeatOverride = new List<PlayerCreationConfiguration>
        {
            new PlayerCreationConfiguration { PlayerConfiguration = _human, PlayerName = "Alice", PlayerColor = _red },
            new PlayerCreationConfiguration { PlayerConfiguration = _human, PlayerName = "Bob", PlayerColor = _blue },
        };
#endif
    }

    public static void Clear() => GameManager.SeatOverride = null;
}
