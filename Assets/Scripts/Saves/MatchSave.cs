using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Actions;
using player;
using UI;
using UnityEngine;

// Local save for single player and pass-and-play: the seed plus every accepted action, same idea as
// online rejoin. The match is rebuilt by dealing from the seed and replaying the log.
// Online correspondence (each player taking their turn days later) is OL-6: this same file, stored
// per match on PlayFab / UGS, once players have accounts. Live rooms still use OnlineMatch's log.
public static class MatchSave
{
    public const int CurrentVersion = 1;
    const string FileName = "local.json";

    // Tests point this at a temp folder so they don't touch the player's save.
    public static string FolderOverride;

    public static bool IsReplaying { get; private set; }

    [Serializable]
    public class File
    {
        public int Version = CurrentVersion;
        public string Kind = "";
        public int Seed;
        public string Options = "";
        public string SavedUtc = "";
        public int Year = 1;
        public string WhoseTurn = "";
        public string Phase = "";
        public string[] Seats = Array.Empty<string>();
        public string[] Actions = Array.Empty<string>();
        public string Code = "";

        public string Summary
        {
            get
            {
                var vs = WhoseTurn;
                if (string.IsNullOrEmpty(vs)) vs = Kind == MatchKind.HotSeat.ToString() ? "Pass and Play" : "Single Player";
                var phase = string.IsNullOrEmpty(Phase) ? "" : " · " + Phase;
                return $"Year {Year} · {vs}{phase}";
            }
        }
    }

    static File _file;
    static File _pending;
    static readonly List<string> _actions = new();

    public static bool HasSave => TryLoad(out _);
    public static string ContinueSummary => TryLoad(out var file) ? file.Summary : "";
    public static int ActionCount => _actions.Count;
    public static int? Seed => _file?.Seed;
    public static File Current => _file;

    public static string Folder =>
        string.IsNullOrEmpty(FolderOverride)
            ? Path.Combine(Application.persistentDataPath, "saves")
            : FolderOverride;

    static string PathToFile => Path.Combine(Folder, FileName);

    public static bool TryLoad(out File file)
    {
        file = null;
        var path = PathToFile;
        if (!System.IO.File.Exists(path)) return false;
        try
        {
            file = JsonUtility.FromJson<File>(System.IO.File.ReadAllText(path));
            return file != null && file.Version == CurrentVersion && file.Seats != null && file.Seats.Length >= 2;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Couldn't read the saved game: " + e.Message);
            return false;
        }
    }

    // Menu: restore seats, seed the RNG, load Main. GameManager replays the log after setup.
    public static void ArmResume(File file)
    {
        _pending = file ?? throw new ArgumentNullException(nameof(file));
        GameRandom.UseSeed(file.Seed);
        MatchSession.Select(Enum.TryParse(file.Kind, out MatchKind kind) ? kind : MatchKind.SinglePlayer);
        MatchSession.Options = MatchOptions.Decode(file.Options);
        MatchSession.ArmForNextLoad();
    }

    public static void ClearPending() => _pending = null;

    // Called from ApplyMatchSession once the players exist. A resume keeps the loaded log; a new
    // match picks a seed and overwrites the file.
    public static void Attach(GameManager gm, MatchKind kind)
    {
        if (kind != MatchKind.SinglePlayer && kind != MatchKind.HotSeat && kind != MatchKind.Correspondence) return;
        if (_pending != null)
        {
            _file = _pending;
            _actions.Clear();
            if (_file.Actions != null) _actions.AddRange(_file.Actions);
            return;
        }

        var seed = MatchSession.Seed ?? UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        MatchSession.Seed = null;
        GameRandom.UseSeed(seed);
        _file = new File
        {
            Kind = kind.ToString(),
            Seed = seed,
            Options = gm.Options.Encode(),
            Seats = SeatsOf(gm),
            Year = 1,
            WhoseTurn = gm.CurrentPlayer != null ? gm.CurrentPlayer.Name : "",
            Phase = "Setup",
        };
        _actions.Clear();
        Write(gm);
    }

    public static void ReplayPending(GameManager gm)
    {
        if (_pending == null || gm == null) return;
        var log = _pending.Actions ?? Array.Empty<string>();
        _pending = null;
        IsReplaying = true;
        try
        {
            foreach (var json in log)
            {
                if (string.IsNullOrEmpty(json)) continue;
                var data = JsonUtility.FromJson<ActionData>(json);
                var action = ActionCodec.FromData(data, gm);
                if (gm.ApplyAgreed(action)) continue;
                // The rest of the log was recorded past a state this resume did not reach
                // (a reinforce saved while a placement question was still open). Drop it
                // so the same red error does not fire on every continue.
                var drop = _actions.IndexOf(json);
                if (drop >= 0) _actions.RemoveRange(drop, _actions.Count - drop);
                Debug.LogWarning("Resumed before a saved move that no longer applies. Place the unit you were asked for, then continue.");
                break;
            }
        }
        finally { IsReplaying = false; }
        gm.ResumeBots();
        Write(gm);
        // The cloud meta written at Start still says seat 0. Publish whoever actually has the turn.
        if (Correspondence.IsPlaying) Correspondence.NoteSaved(gm);
    }

    public static void Record(GameManager gm, PlayerAction action, int depth, int agreed)
    {
        if (_file == null || IsReplaying || gm == null || action == null) return;
        if (depth != 1 || agreed != 0 || gm.Relay != null) return;
        if (gm.GameState == GameState.Over)
        {
            if (Correspondence.IsPlaying) Correspondence.NoteSaved(gm);
            else Delete();
            return;
        }
        _actions.Add(JsonUtility.ToJson(ActionCodec.ToData(action, gm)));
        Write(gm);
        if (Correspondence.IsPlaying) Correspondence.NoteSaved(gm);
    }

    public static void Flush(GameManager gm) => Write(gm);

    public static void Detach()
    {
        _file = null;
        _actions.Clear();
        IsReplaying = false;
        // Don't clear _pending or the RNG: loading Main from Continue sets both, then this scene is destroyed.
    }

    public static void Delete()
    {
        _file = null;
        _actions.Clear();
        _pending = null;
        try
        {
            if (System.IO.File.Exists(PathToFile)) System.IO.File.Delete(PathToFile);
        }
        catch (Exception e) { Debug.LogWarning("Couldn't delete the saved game: " + e.Message); }
    }

    static void Write(GameManager gm)
    {
        if (_file == null) return;
        _file.Actions = _actions.ToArray();
        _file.SavedUtc = DateTime.UtcNow.ToString("o");
        if (gm != null)
        {
            _file.Year = gm.Year;
            _file.WhoseTurn = gm.CurrentPlayer != null ? gm.CurrentPlayer.Name : "";
            _file.Phase = gm.IsDrafting ? "Setup" : gm.IsBidding ? "Bidding" : (gm.CurrentPhase?.Name ?? "");
        }
        if (_file.Kind == MatchKind.Correspondence.ToString()) return;
        try
        {
            Directory.CreateDirectory(Folder);
            var path = PathToFile;
            var tmp = path + ".tmp";
            System.IO.File.WriteAllText(tmp, JsonUtility.ToJson(_file, true));
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            System.IO.File.Move(tmp, path);
        }
        catch (Exception e) { Debug.LogWarning("Couldn't save the game: " + e.Message); }
    }

    public static string[] SeatsOf(GameManager gm) =>
        gm.Players.Select(p =>
        {
            var config = "Human";
            if (p.IsBot())
            {
                var bot = p.GetComponent<BotPlayer>();
                config = bot != null && bot.BotConfiguration != null ? bot.BotConfiguration.Name : "Standard BOT";
            }
            var color = p.Color != null ? p.Color.name : "Red";
            return p.Name + "|" + color + "|" + config;
        }).ToArray();

    public static string SeatName(string seat)
    {
        var i = (seat ?? "").IndexOf('|');
        return i < 0 ? seat : seat.Substring(0, i);
    }

    public static string SeatColor(string seat) => Part(seat, 1);
    public static string SeatConfig(string seat) => Part(seat, 2);
    public static string SeatPlayFab(string seat) => Part(seat, 3);

    static string Part(string seat, int index)
    {
        var parts = (seat ?? "").Split('|');
        return index < parts.Length ? parts[index] : "";
    }
}
