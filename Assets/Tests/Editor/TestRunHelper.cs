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
    private static readonly TestRunnerApi Api;

    static TestRunHelper()
    {
        Api = ScriptableObject.CreateInstance<TestRunnerApi>();
        Api.RegisterCallbacks(new ResultWriter());
    }

    [MenuItem("Invade Earth/Run Hot-Seat Tests")]
    public static void RunPlayModeTests()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(ResultsPath, "QUEUED\n");
        Api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.PlayMode,
            assemblyNames = new[] { "InvadeEarth.Tests.PlayMode" }
        }));
    }

    private class ResultWriter : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) => File.WriteAllText(ResultsPath, "STARTED\n");
        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren) return;
            var line = new StringBuilder($"{result.TestStatus.ToString().ToUpper()} {result.Name}");
            if (!string.IsNullOrEmpty(result.Message)) line.Append(" :: ").Append(result.Message.Trim().Replace("\n", " | "));
            File.AppendAllText(ResultsPath, line + "\n");
        }

        public void RunFinished(ITestResultAdaptor result) =>
            File.AppendAllText(ResultsPath, $"FINISHED passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}\n");
    }
}
