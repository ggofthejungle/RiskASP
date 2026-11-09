using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.GroupsModels;
using UI;
using UnityEngine;
// OL-6: correspondence (play by mail). The match lives in a PlayFab Shared Group so nobody has to
// stay in a live room. Groups handle join-by-code (apply / host admits). After Start, each device
// downloads the log, replays, and the player whose turn it is uploads. No 20 s timer.
// Client-authoritative with an action-count check; Cloud Code can take over later.
public static class Correspondence
{
    public const int MinPlayers = 2, MaxPlayers = 5;
    public const string GroupPrefix = "ie";
    const int Chunk = 8000;
    const string MetaKey = "m", CountKey = "n";

    public static bool IsPlaying { get; private set; }
    public static bool PlaysBots { get; private set; }
    public static string Code { get; private set; }
    public static bool InLobby { get; private set; }
    public static bool IsHost { get; private set; }
    public static Meta Lobby { get; private set; }
    public static event Action Changed;

    public static string LastError { get; private set; }

    [Serializable]
    public class Meta
    {
        public int v = 1;
        public int open = 1;
        public int started;
        public int over;
        public string hostId = "";
        public string hostName = "";
        public string rules = "";
        public int bots;
        public int seed;
        public int year = 1;
        public string whoseTurn = "";
        public string whoseId = "";
        public string phase = "";
        public int actionCount;
        public string[] peopleIds = Array.Empty<string>();
        public string[] peopleNames = Array.Empty<string>();
        public string[] seats = Array.Empty<string>();
    }

    public class Summary
    {
        public string Code;
        public string Line;
        public bool YourTurn;
        public bool Open;
        public bool Over;
    }

    static PlayFab.GroupsModels.EntityKey _group;
    static float _uploadAt;
    static bool _uploadQueued;
    static GameManager _gm;

    public static async Task HostAsync()
    {
        await EnsureLogin();
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var code = NewCode();
            try
            {
                await CreateShared(code);
                try
                {
                    var g = await Call<CreateGroupResponse>((ok, fail) => PlayFabGroupsAPI.CreateGroup(
                        new CreateGroupRequest { GroupName = GroupPrefix + code }, ok, fail));
                    _group = g.Group;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("PlayFab Groups create failed (join-by-code needs Groups enabled): " + e.Message);
                    _group = null;
                }
                var meta = new Meta
                {
                    hostId = PlayFabLogin.PlayFabId,
                    hostName = PlayFabLogin.DisplayName,
                    rules = MatchSession.Options.Encode(),
                    peopleIds = new[] { PlayFabLogin.PlayFabId },
                    peopleNames = new[] { PlayFabLogin.DisplayName },
                };
                await WriteMeta(code, meta, publicMeta: true);
                await Remember(code);
                EnterLobby(code, meta, host: true);
                return;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                if (attempt == 7) throw;
            }
        }
    }

    public static async Task JoinAsync(string raw)
    {
        await EnsureLogin();
        var code = (raw ?? "").Trim().ToUpperInvariant();
        if (code.Length < 4) throw new Exception("Enter the match code.");
        var data = await GetData(code);
        var meta = ReadMeta(data) ?? throw new Exception("No match with that code.");
        if (meta.started == 1) throw new Exception("That match already started. Open it from Your games.");
        if (meta.open != 1) throw new Exception("That match isn't accepting players.");
        if (IndexOf(meta.peopleIds, PlayFabLogin.PlayFabId) >= 0)
        {
            EnterLobby(code, meta, host: meta.hostId == PlayFabLogin.PlayFabId);
            return;
        }
        try
        {
            var g = await Call<GetGroupResponse>((ok, fail) => PlayFabGroupsAPI.GetGroup(
                new GetGroupRequest { GroupName = GroupPrefix + code }, ok, fail));
            _group = g.Group;
            await Call<ApplyToGroupResponse>((ok, fail) => PlayFabGroupsAPI.ApplyToGroup(
                new ApplyToGroupRequest { Group = _group }, ok, fail));
        }
        catch (Exception e)
        {
            throw new Exception("Couldn't apply to join. The host needs PlayFab Groups enabled. " + e.Message);
        }
        await Remember(code);
        EnterLobby(code, meta, host: false);
    }

    public static async Task RefreshLobbyAsync()
    {
        if (!InLobby || string.IsNullOrEmpty(Code)) return;
        var data = await GetData(Code);
        Lobby = ReadMeta(data) ?? Lobby;
        if (IsHost) await AdmitAsync();
        var latest = await GetData(Code);
        Lobby = ReadMeta(latest) ?? Lobby;
        if (!IsHost && IndexOf(Lobby.peopleIds, PlayFabLogin.PlayFabId) < 0 && _group != null)
        {
            // Still waiting for the host to admit this device.
        }
        Changed?.Invoke();
    }

    public static async Task AdmitAsync()
    {
        if (!IsHost || _group == null) return;
        ListGroupApplicationsResponse apps;
        try
        {
            apps = await Call<ListGroupApplicationsResponse>((ok, fail) => PlayFabGroupsAPI.ListGroupApplications(
                new ListGroupApplicationsRequest { Group = _group }, ok, fail));
        }
        catch { return; }
        if (apps.Applications == null || apps.Applications.Count == 0) return;
        var ids = new List<string>(Lobby.peopleIds ?? Array.Empty<string>());
        var names = new List<string>(Lobby.peopleNames ?? Array.Empty<string>());
        foreach (var app in apps.Applications)
        {
            if (ids.Count + Lobby.bots >= MaxPlayers) break;
            var title = app.Entity?.Key;
            var master = MasterId(app.Entity);
            if (title == null || string.IsNullOrEmpty(master)) continue;
            try
            {
                await Call<PlayFab.GroupsModels.EmptyResponse>((ok, fail) => PlayFabGroupsAPI.AcceptGroupApplication(
                    new AcceptGroupApplicationRequest { Group = _group, Entity = title }, ok, fail));
            }
            catch (Exception e) { Debug.LogWarning("Admit failed: " + e.Message); continue; }
            try
            {
                await Call<AddSharedGroupMembersResult>((ok, fail) => PlayFabClientAPI.AddSharedGroupMembers(
                    new AddSharedGroupMembersRequest { SharedGroupId = Code, PlayFabIds = new List<string> { master } }, ok, fail));
            }
            catch (Exception e) { Debug.LogWarning("Shared group add failed: " + e.Message); }
            if (IndexOf(ids.ToArray(), master) < 0)
            {
                ids.Add(master);
                names.Add(await DisplayNameOf(master));
            }
        }
        Lobby.peopleIds = ids.ToArray();
        Lobby.peopleNames = names.ToArray();
        await WriteMeta(Code, Lobby, publicMeta: true);
        Changed?.Invoke();
    }

    public static async Task SetBotsAsync(int count)
    {
        if (!IsHost || Lobby == null) return;
        count = Mathf.Clamp(count, 0, MaxPlayers - (Lobby.peopleIds?.Length ?? 1));
        Lobby.bots = count;
        await WriteMeta(Code, Lobby, publicMeta: true);
        Changed?.Invoke();
    }

    public static async Task LeaveLobbyAsync()
    {
        InLobby = false;
        IsHost = false;
        Code = null;
        Lobby = null;
        _group = null;
        Changed?.Invoke();
        await Task.CompletedTask;
    }

    public static async Task StartAsync()
    {
        if (!IsHost || Lobby == null) throw new Exception("Only the host can start.");
        var humans = Lobby.peopleIds?.Length ?? 0;
        var n = humans + Lobby.bots;
        if (n < MinPlayers) throw new Exception("Need at least two players (humans or bots).");
        if (n > MaxPlayers) throw new Exception("Too many players.");
        await AdmitAsync();
        var names = new List<string>();
        var bots = new List<bool>();
        var ids = new List<string>();
        for (var i = 0; i < (Lobby.peopleIds?.Length ?? 0); i++)
        {
            names.Add(i < Lobby.peopleNames.Length && !string.IsNullOrEmpty(Lobby.peopleNames[i])
                ? Lobby.peopleNames[i] : "Commander");
            bots.Add(false);
            ids.Add(Lobby.peopleIds[i]);
        }
        for (var b = 1; b <= Lobby.bots; b++)
        {
            names.Add("Bot " + b);
            bots.Add(true);
            ids.Add("bot:" + b);
        }
        var seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        Lobby.started = 1;
        Lobby.open = 0;
        Lobby.seed = seed;
        Lobby.rules = MatchSession.Options.Encode();
        Lobby.seats = new string[names.Count];
        var colors = new[] { "Red", "Blue", "Green", "Yellow", "Purple" };
        for (var i = 0; i < names.Count; i++)
            Lobby.seats[i] = names[i] + "|" + colors[i % colors.Length] + "|" +
                             (bots[i] ? "Standard BOT" : "Human") + "|" + ids[i];
        var file = new MatchSave.File
        {
            Kind = MatchKind.Correspondence.ToString(),
            Seed = seed,
            Options = Lobby.rules,
            Year = 1,
            WhoseTurn = names[0],
            Phase = "Setup",
            Seats = Lobby.seats,
            Actions = Array.Empty<string>(),
            Code = Code,
        };
        await UploadAsync(Code, file, Lobby, expectedCount: 0);
        Arm(file);
    }

    public static async Task<List<Summary>> ListAsync()
    {
        await EnsureLogin();
        var list = new List<Summary>();
        ListMembershipResponse membership = null;
        try
        {
            membership = await Call<ListMembershipResponse>((ok, fail) => PlayFabGroupsAPI.ListMembership(
                new ListMembershipRequest(), ok, fail));
        }
        catch (Exception e) { Debug.LogWarning("ListMembership: " + e.Message); }
        var codes = new HashSet<string>();
        if (membership?.Groups != null)
            foreach (var g in membership.Groups)
            {
                var name = g.GroupName ?? "";
                if (name.StartsWith(GroupPrefix) && name.Length > GroupPrefix.Length)
                    codes.Add(name.Substring(GroupPrefix.Length));
            }
        foreach (var extra in await Inbox()) codes.Add(extra);
        foreach (var code in codes)
        {
            try
            {
                var meta = ReadMeta(await GetData(code));
                if (meta == null) continue;
                list.Add(ToSummary(code, meta));
            }
            catch { /* stale group */ }
        }
        return list;
    }

    public static async Task OpenAsync(string code)
    {
        await EnsureLogin();
        var data = await GetData(code);
        var meta = ReadMeta(data) ?? throw new Exception("Couldn't load that match.");
        if (meta.started != 1)
        {
            EnterLobby(code, meta, host: meta.hostId == PlayFabLogin.PlayFabId);
            if (_group == null)
            {
                try
                {
                    var g = await Call<GetGroupResponse>((ok, fail) => PlayFabGroupsAPI.GetGroup(
                        new GetGroupRequest { GroupName = GroupPrefix + code }, ok, fail));
                    _group = g.Group;
                }
                catch { /* lobby still works from shared group */ }
            }
            return;
        }
        var file = ReadFile(data, meta);
        Arm(file);
    }

    public static void Arm(MatchSave.File file)
    {
        InLobby = false;
        var lobby = UnityEngine.Object.FindAnyObjectByType<UIPlayerCreationManager>(FindObjectsInactive.Include);
        if (lobby == null || !lobby.ApplySavedSeats(file.Seats))
            throw new Exception("Couldn't restore the seats.");
        MatchSave.ArmResume(file);
        if (!string.IsNullOrEmpty(file.Code)) Code = file.Code;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
    }

    public static void Bind(GameManager gm)
    {
        _gm = gm;
        IsPlaying = true;
        PlaysBots = false;
        var file = MatchSave.Current;
        if (file != null && !string.IsNullOrEmpty(file.Code)) Code = file.Code;
        SyncWhoPlaysBots(gm);
        var go = gm.gameObject;
        if (go.GetComponent<Driver>() == null) go.AddComponent<Driver>();
    }

    public static void Unbind()
    {
        IsPlaying = false;
        PlaysBots = false;
        _gm = null;
        _uploadQueued = false;
    }

    public static void NoteSaved(GameManager gm)
    {
        if (!IsPlaying || gm == null || MatchSave.Current == null) return;
        SyncWhoPlaysBots(gm);
        _uploadQueued = true;
        _uploadAt = Time.unscaledTime + 0.6f;
    }

    public static async Task FlushAsync(GameManager gm)
    {
        if (!IsPlaying || gm == null) return;
        _uploadQueued = false;
        var file = MatchSave.Current;
        if (file == null || string.IsNullOrEmpty(Code)) return;
        var data = await GetData(Code);
        var remote = ReadMeta(data);
        var localCount = file.Actions?.Length ?? 0;
        if (remote != null && remote.actionCount > localCount)
            throw new Exception("The cloud copy is ahead. Leave and open the match again.");
        var meta = remote ?? new Meta();
        FillMetaFromGame(meta, gm, file);
        await UploadAsync(Code, file, meta, expectedCount: remote?.actionCount ?? 0);
    }

    public static async Task FinishAsync(GameManager gm)
    {
        if (MatchSave.Current == null) return;
        try
        {
            var data = await GetData(Code);
            var meta = ReadMeta(data) ?? new Meta();
            FillMetaFromGame(meta, gm, MatchSave.Current);
            meta.over = 1;
            await UploadAsync(Code, MatchSave.Current, meta, expectedCount: meta.actionCount);
        }
        catch (Exception e) { Debug.LogWarning("Couldn't upload finished match: " + e.Message); }
    }

    static void SyncWhoPlaysBots(GameManager gm)
    {
        if (gm == null) { PlaysBots = false; return; }
        var p = gm.InputPlayer ?? gm.CurrentPlayer;
        PlaysBots = p != null && p.IsBot();
    }

    static void FillMetaFromGame(Meta meta, GameManager gm, MatchSave.File file)
    {
        var input = gm.InputPlayer ?? gm.CurrentPlayer;
        meta.started = 1;
        meta.open = 0;
        meta.year = gm.Year;
        meta.whoseTurn = input != null ? input.Name : "";
        var seatId = gm.GetPlayerId(input);
        meta.whoseId = input == null ? "" : input.IsBot() ? "bot"
            : (file.Seats != null && seatId >= 0 && seatId < file.Seats.Length ? MatchSave.SeatPlayFab(file.Seats[seatId]) : "");
        meta.phase = gm.IsDrafting ? "Setup" : gm.IsBidding ? "Bidding" : (gm.CurrentPhase?.Name ?? "");
        meta.actionCount = file.Actions?.Length ?? 0;
        meta.seats = file.Seats;
        meta.seed = file.Seed;
        meta.rules = file.Options;
        if (gm.GameState == GameState.Over) meta.over = 1;
    }

    static void EnterLobby(string code, Meta meta, bool host)
    {
        Code = code;
        Lobby = meta;
        InLobby = true;
        IsHost = host;
        Changed?.Invoke();
    }

    static Summary ToSummary(string code, Meta meta)
    {
        var mine = PlayFabLogin.PlayFabId;
        var yourTurn = meta.started == 1 && meta.over == 0 &&
                       (meta.whoseId == mine || (meta.whoseId == "bot" && meta.hostId == mine));
        var line = meta.over == 1 ? "Finished"
            : meta.started != 1 ? "Waiting for players"
            : yourTurn ? "Your turn · Year " + meta.year
            : "Waiting for " + (string.IsNullOrEmpty(meta.whoseTurn) ? "the other player" : meta.whoseTurn);
        return new Summary { Code = code, Line = code + "  ·  " + line, YourTurn = yourTurn, Open = meta.open == 1 && meta.started == 0, Over = meta.over == 1 };
    }

    static async Task UploadAsync(string code, MatchSave.File file, Meta meta, int expectedCount)
    {
        meta.actionCount = file.Actions?.Length ?? 0;
        if (expectedCount > meta.actionCount)
            throw new Exception("The cloud copy is ahead.");
        var zipped = Zip(JsonUtility.ToJson(file));
        var n = Math.Max(1, (zipped.Length + Chunk - 1) / Chunk);
        var payload = new Dictionary<string, string> { { CountKey, n.ToString() } };
        for (var i = 0; i < n; i++)
        {
            var start = i * Chunk;
            var len = Math.Min(Chunk, zipped.Length - start);
            payload["d" + i] = zipped.Substring(start, len);
        }
        await Call<UpdateSharedGroupDataResult>((ok, fail) => PlayFabClientAPI.UpdateSharedGroupData(
            new UpdateSharedGroupDataRequest
            {
                SharedGroupId = code,
                Data = payload,
                Permission = UserDataPermission.Private,
            }, ok, fail));
        await WriteMeta(code, meta, publicMeta: true);
    }

    static MatchSave.File ReadFile(GetSharedGroupDataResult data, Meta meta)
    {
        if (!data.Data.TryGetValue(CountKey, out var nRec) || !int.TryParse(nRec.Value, out var n) || n <= 0)
            return new MatchSave.File
            {
                Kind = MatchKind.Correspondence.ToString(),
                Seed = meta.seed,
                Options = meta.rules,
                Seats = meta.seats,
                Year = meta.year,
                WhoseTurn = meta.whoseTurn,
                Phase = meta.phase,
                Actions = Array.Empty<string>(),
            };
        var sb = new StringBuilder();
        for (var i = 0; i < n; i++)
        {
            if (!data.Data.TryGetValue("d" + i, out var rec)) throw new Exception("Match data is incomplete.");
            sb.Append(rec.Value);
        }
        var file = JsonUtility.FromJson<MatchSave.File>(Unzip(sb.ToString()));
        if (file.Seats == null || file.Seats.Length == 0) file.Seats = meta.seats;
        return file;
    }

    static async Task WriteMeta(string code, Meta meta, bool publicMeta)
    {
        await Call<UpdateSharedGroupDataResult>((ok, fail) => PlayFabClientAPI.UpdateSharedGroupData(
            new UpdateSharedGroupDataRequest
            {
                SharedGroupId = code,
                Data = new Dictionary<string, string> { { MetaKey, JsonUtility.ToJson(meta) } },
                Permission = publicMeta ? UserDataPermission.Public : UserDataPermission.Private,
            }, ok, fail));
    }

    static async Task<GetSharedGroupDataResult> GetData(string code) =>
        await Call<GetSharedGroupDataResult>((ok, fail) => PlayFabClientAPI.GetSharedGroupData(
            new GetSharedGroupDataRequest { SharedGroupId = code, GetMembers = true }, ok, fail));

    static Meta ReadMeta(GetSharedGroupDataResult data)
    {
        if (data?.Data == null || !data.Data.TryGetValue(MetaKey, out var rec) || string.IsNullOrEmpty(rec.Value))
            return null;
        return JsonUtility.FromJson<Meta>(rec.Value);
    }

    const string InboxKey = "ie.matches";

    static async Task Remember(string code)
    {
        if (string.IsNullOrEmpty(code)) return;
        var codes = new List<string>(await Inbox());
        if (!codes.Contains(code)) codes.Add(code);
        await Call<UpdateUserDataResult>((ok, fail) => PlayFabClientAPI.UpdateUserData(
            new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string> { { InboxKey, string.Join(",", codes) } },
                Permission = UserDataPermission.Private,
            }, ok, fail));
    }

    static async Task<string[]> Inbox()
    {
        try
        {
            var data = await Call<GetUserDataResult>((ok, fail) => PlayFabClientAPI.GetUserData(
                new GetUserDataRequest { Keys = new List<string> { InboxKey } }, ok, fail));
            if (data.Data != null && data.Data.TryGetValue(InboxKey, out var rec) && !string.IsNullOrEmpty(rec.Value))
                return rec.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }
        catch { /* none yet */ }
        return Array.Empty<string>();
    }

    static async Task CreateShared(string code) =>
        await Call<CreateSharedGroupResult>((ok, fail) => PlayFabClientAPI.CreateSharedGroup(
            new CreateSharedGroupRequest { SharedGroupId = code }, ok, fail));

    static async Task EnsureLogin()
    {
        var tcs = new TaskCompletionSource<bool>();
        PlayFabLogin.EnsureLoggedIn((ok, err) =>
        {
            if (ok) tcs.TrySetResult(true);
            else tcs.TrySetException(new Exception(err ?? "PlayFab sign-in failed."));
        });
        await tcs.Task;
        await PlayFabLogin.PushDisplayName();
    }

    static string NewCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var c = new char[6];
        for (var i = 0; i < c.Length; i++) c[i] = alphabet[UnityEngine.Random.Range(0, alphabet.Length)];
        return new string(c);
    }

    static string MasterId(EntityWithLineage e)
    {
        if (e?.Lineage != null && e.Lineage.TryGetValue("master_player_account", out var m) && m != null)
            return m.Id;
        return e?.Key?.Id;
    }

    static async Task<string> DisplayNameOf(string playFabId)
    {
        try
        {
            var profile = await Call<GetPlayerProfileResult>((ok, fail) => PlayFabClientAPI.GetPlayerProfile(
                new GetPlayerProfileRequest
                {
                    PlayFabId = playFabId,
                    ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true },
                }, ok, fail));
            var name = profile.PlayerProfile?.DisplayName;
            if (!string.IsNullOrEmpty(name)) return name;
        }
        catch (Exception e) { Debug.LogWarning("No display name for " + playFabId + ": " + e.Message); }
        return "Commander";
    }

    static int IndexOf(string[] list, string id)
    {
        if (list == null || id == null) return -1;
        for (var i = 0; i < list.Length; i++) if (list[i] == id) return i;
        return -1;
    }

    static string Zip(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? "");
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
            gz.Write(bytes, 0, bytes.Length);
        return Convert.ToBase64String(ms.ToArray());
    }

    static string Unzip(string b64)
    {
        var bytes = Convert.FromBase64String(b64 ?? "");
        using var input = new MemoryStream(bytes);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var outMs = new MemoryStream();
        gz.CopyTo(outMs);
        return Encoding.UTF8.GetString(outMs.ToArray());
    }

    static Task<T> Call<T>(Action<Action<T>, Action<PlayFabError>> start)
    {
        var tcs = new TaskCompletionSource<T>();
        start(r => tcs.TrySetResult(r), err => tcs.TrySetException(new Exception(err.ErrorMessage ?? err.Error.ToString())));
        return tcs.Task;
    }

    class Driver : MonoBehaviour
    {
        void Update()
        {
            if (!_uploadQueued || Time.unscaledTime < _uploadAt) return;
            _uploadQueued = false;
            var gm = _gm;
            if (gm == null) return;
            _ = FlushSafe(gm);
        }

        static async Task FlushSafe(GameManager gm)
        {
            try { await FlushAsync(gm); }
            catch (Exception e) { Debug.LogWarning("Cloud save failed: " + e.Message); }
        }
    }
}
