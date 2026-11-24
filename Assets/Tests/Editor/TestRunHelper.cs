using System.Linq;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Adds "Invade Earth/Run Hot-Seat Tests" and writes results to Logs/test-results.txt
// (registered on every domain load, so it survives the reload when Play Mode tests start).
[InitializeOnLoad]
public static class TestRunHelper
{
    public const string ResultsPath = "Logs/test-results.txt";
    public const string FilterPath = "Logs/test-filter.txt";
    // Survives the domain reload that Play Mode triggers. DisallowAutoRefresh does not,
    // so every load during a run has to call it again or the editor locks on the seat asset.
    const string RefreshHeld = "InvadeEarth.Tests.RefreshHeld";
    static bool _heldThisDomain;
    private static readonly TestRunnerApi Api;

    static TestRunHelper()
    {
        Api = ScriptableObject.CreateInstance<TestRunnerApi>();
        Api.RegisterCallbacks(new ResultWriter());
        EditorApplication.playModeStateChanged += OnPlayMode;
        // One hold per domain. The Play Mode reload clears the native counter, so the first
        // test in that domain takes it again. Don't drop the flag here: this constructor
        // also runs at the reload, and Play Mode is not always visible yet.
        if (EditorPrefs.GetBool(RefreshHeld, false))
            HoldThisDomain();
    }

    static void HoldThisDomain()
    {
        if (_heldThisDomain) return;
        _heldThisDomain = true;
        AssetDatabase.DisallowAutoRefresh();
    }

    // Play Mode tests edit StartingPlayersConfiguration while a scene is running. With auto-refresh on,
    // Unity then tries to reimport that asset on the main thread and waits for Play Mode to end.
    // Play Mode is waiting for the reimport, so the editor locks up and never throws.
    // A try/catch cannot catch that: LoadAssetAtPath does not return.
    static void HoldAssetRefresh()
    {
        EditorPrefs.SetBool(RefreshHeld, true);
    }

    static void ReleaseAssetRefresh()
    {
        if (!EditorPrefs.GetBool(RefreshHeld, false)) return;
        EditorPrefs.SetBool(RefreshHeld, false);
        AssetDatabase.AllowAutoRefresh();
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
            ReleaseAssetRefresh();
    }

    [MenuItem("Invade Earth/Run Hot-Seat Tests")]
    public static void RunPlayModeTests()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(ResultsPath, "QUEUED\n");
        TurnOffErrorPause();
        HoldAssetRefresh();
        var filter = new Filter
        {
            testMode = TestMode.PlayMode,
            assemblyNames = new[] { "InvadeEarth.Tests.PlayMode" }
        };
        // Optional: Logs/test-filter.txt with one test class name per line runs only those classes (used once).
        if (File.Exists(FilterPath))
        {
            var classes = File.ReadAllLines(FilterPath)
                .Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToArray();
            File.Delete(FilterPath);
            if (classes.Length > 0)
                filter.groupNames = new[] { "^(" + string.Join("|", classes) + ")\\." };
        }
        Api.Execute(new ExecutionSettings(filter));
    }

    // Several tests log an error on purpose (and expect it). With the Console's "Error Pause" on, Unity pauses
    // Play Mode at that error and the run hangs. Switch it off for test runs.
    private static void TurnOffErrorPause()
    {
        try
        {
            const int errorPause = 1 << 2; // UnityEditor.ConsoleWindow.ConsoleFlags.ErrorPause
            var logEntries = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");
            var setFlag = logEntries?.GetMethod("SetConsoleFlag",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            setFlag?.Invoke(null, new object[] { errorPause, false });
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Couldn't turn off Error Pause: " + e.Message);
        }
    }

    private class ResultWriter : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) => File.WriteAllText(ResultsPath, "STARTED\n");
        // Written before each test so a freeze shows which test was running (it's replaced by the result).
        public void TestStarted(ITestAdaptor test)
        {
            // The test is about to edit StartingPlayersConfiguration in Play Mode. If that asset
            // is still marked dirty, the next LoadAssetAtPath reimports it and the editor locks.
            if (!test.HasChildren)
            {
                HoldThisDomain();
                ForgetSeatEdits();
                File.AppendAllText(ResultsPath, $"RUNNING {test.Name}\n");
            }
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren) return;
            var lines = File.ReadAllLines(ResultsPath).Where(l => l != $"RUNNING {result.Name}");
            File.WriteAllLines(ResultsPath, lines);
            var line = new StringBuilder($"{result.TestStatus.ToString().ToUpper()} {result.Name}");
            if (!string.IsNullOrEmpty(result.Message)) line.Append(" :: ").Append(result.Message.Trim().Replace("\n", " | "));
            File.AppendAllText(ResultsPath, line + "\n");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            File.AppendAllText(ResultsPath, $"FINISHED passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}\n");
            ForgetSeatEdits();
            if (!EditorApplication.isPlaying)
                ReleaseAssetRefresh();
        }

        // In-memory seat lists are what the tests want. Clearing the dirty flag stops Unity
        // from reimporting the asset while Play Mode is still running.
        static void ForgetSeatEdits()
        {
            foreach (var obj in Resources.FindObjectsOfTypeAll<ScriptableObject>())
                if (obj != null && obj.GetType().Name == "StartingPlayersConfiguration")
                    EditorUtility.ClearDirty(obj);
        }
    }
}
