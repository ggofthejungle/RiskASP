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
            Finish(true, null);
        }, error =>
        {
            LastError = error.GenerateErrorReport();
            Debug.LogWarning("PlayFab login failed: " + LastError);
            Finish(false, error.ErrorMessage);
        });
    }

    static void Finish(bool ok, string message)
    {
        InProgress = false;
        var callbacks = Waiting.ToArray();
        Waiting.Clear();
        foreach (var callback in callbacks) callback(ok, message);
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
