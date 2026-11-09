using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

// Device login on the Invade Earth PlayFab title (set in PlayFabSharedSettings).
// A random id is kept in PlayerPrefs, so the same install keeps the same account.
// Google / Steam / Apple sign-in can be linked to this account later.
public static class PlayFabLogin
{
    static string DeviceKey => OnlineRoom.LocalProfile is { } p ? "ie.playfab.device." + p : "ie.playfab.device";

    public static bool LoggedIn => PlayFabClientAPI.IsClientLoggedIn();
    public static bool InProgress { get; private set; }
    public static string PlayFabId { get; private set; }
    public static string EntityId { get; private set; }
    public static string EntityType { get; private set; }
    public static bool NewlyCreated { get; private set; }
    public static string LastError { get; private set; }
    public static string Username { get; private set; }
    public static string DisplayName
    {
        get => OnlineRoom.DisplayName;
        set => OnlineRoom.DisplayName = value;
    }

    static readonly List<Action<bool, string>> Waiting = new();

    public static void EnsureLoggedIn(Action<bool, string> done = null)
    {
        if (LoggedIn)
        {
            done?.Invoke(true, null);
            return;
        }
        if (done != null) Waiting.Add(done);
        if (InProgress) return;

        InProgress = true;
        LastError = null;
        var request = new LoginWithCustomIDRequest
        {
            CustomId = DeviceId(),
            CreateAccount = true,
        };
        PlayFabClientAPI.LoginWithCustomID(request, result =>
        {
            PlayFabId = result.PlayFabId;
            EntityId = result.EntityToken?.Entity?.Id;
            EntityType = result.EntityToken?.Entity?.Type;
            NewlyCreated = result.NewlyCreated;
            Debug.Log($"PlayFab: signed in as {PlayFabId}{(NewlyCreated ? " (new account)" : "")}.");
            RefreshAccount((_, __) => { });
            Finish(true, null);
        }, error =>
        {
            LastError = error.GenerateErrorReport();
            Debug.LogWarning("PlayFab login failed: " + LastError);
            Finish(false, error.ErrorMessage);
        });
    }

    // A second account on this machine (correspondence play-test). Does not replace the saved device id.
    public static void SignInCustom(string customId, Action<bool, string> done)
    {
        if (string.IsNullOrEmpty(customId)) { done?.Invoke(false, "Missing id."); return; }
        BeginSwitch();
        if (done != null) Waiting.Add(done);
        PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest { CustomId = customId, CreateAccount = true },
            result =>
            {
                PlayFabId = result.PlayFabId;
                EntityId = result.EntityToken?.Entity?.Id;
                EntityType = result.EntityToken?.Entity?.Type;
                NewlyCreated = result.NewlyCreated;
                Debug.Log($"PlayFab: signed in as {PlayFabId}{(NewlyCreated ? " (new account)" : "")}.");
                RefreshAccount((_, __) => { });
                Finish(true, null);
            },
            error =>
            {
                LastError = error.GenerateErrorReport();
                Debug.LogWarning("PlayFab login failed: " + LastError);
                Finish(false, error.ErrorMessage);
            });
    }

    // Back to this install's saved device account.
    public static void SignInThisDevice(Action<bool, string> done)
    {
        BeginSwitch();
        EnsureLoggedIn(done);
    }

    static void BeginSwitch()
    {
        PlayFabClientAPI.ForgetAllCredentials();
        PlayFabId = null;
        EntityId = null;
        EntityType = null;
        Username = null;
        InProgress = false;
        Waiting.Clear();
    }

    static void Finish(bool ok, string message)
    {
        InProgress = false;
        var callbacks = Waiting.ToArray();
        Waiting.Clear();
        foreach (var callback in callbacks) callback(ok, message);
    }

    public static void RefreshAccount(Action<bool, string> done)
    {
        if (!LoggedIn) { done?.Invoke(false, "Not signed in."); return; }
        PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest(), result =>
        {
            Username = result.AccountInfo?.Username;
            var titleName = result.AccountInfo?.TitleInfo?.DisplayName;
            if (!string.IsNullOrEmpty(titleName)) DisplayName = titleName;
            done?.Invoke(true, null);
        }, error => done?.Invoke(false, error.ErrorMessage));
    }

    public static System.Threading.Tasks.Task PushDisplayName()
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        var name = DisplayName;
        if (!LoggedIn || name == null || name.Length < 3)
        {
            tcs.TrySetResult(false);
            return tcs.Task;
        }
        PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest { DisplayName = name.Length > 25 ? name.Substring(0, 25) : name },
            _ => tcs.TrySetResult(true),
            error =>
            {
                Debug.LogWarning("Display name not saved: " + error.ErrorMessage);
                tcs.TrySetResult(false);
            });
        return tcs.Task;
    }

    public static void RegisterUsername(string username, string email, string password, Action<bool, string> done)
    {
        if (!LoggedIn) { done?.Invoke(false, "Sign in first."); return; }
        PlayFabClientAPI.AddUsernamePassword(new AddUsernamePasswordRequest
        {
            Username = username,
            Email = email,
            Password = password,
        }, result =>
        {
            Username = result.Username;
            done?.Invoke(true, null);
        }, error => done?.Invoke(false, error.ErrorMessage));
    }

    public static void SignInWithUsername(string username, string password, Action<bool, string> done)
    {
        PlayFabClientAPI.ForgetAllCredentials();
        PlayFabId = null;
        PlayFabClientAPI.LoginWithPlayFab(new LoginWithPlayFabRequest { Username = username, Password = password }, result =>
        {
            PlayFabId = result.PlayFabId;
            EntityId = result.EntityToken?.Entity?.Id;
            EntityType = result.EntityToken?.Entity?.Type;
            NewlyCreated = result.NewlyCreated;
            Username = username;
            PlayFabClientAPI.LinkCustomID(new LinkCustomIDRequest { CustomId = DeviceId(), ForceLink = false },
                _ =>
                {
                    RefreshAccount((_, __) => { });
                    done?.Invoke(true, null);
                },
                error =>
                {
                    Debug.LogWarning("Couldn't link this device: " + error.ErrorMessage);
                    RefreshAccount((_, __) => { });
                    done?.Invoke(true, null);
                });
        }, error => done?.Invoke(false, error.ErrorMessage));
    }

    static string DeviceId()
    {
        var id = PlayerPrefs.GetString(DeviceKey, "");
        if (id.Length > 0) return id;
        id = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(DeviceKey, id);
        PlayerPrefs.Save();
        return id;
    }
}
