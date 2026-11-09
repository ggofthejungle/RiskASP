using System.IO;
using NUnit.Framework;
using UI;

// SAVE-1: the local save file round-trips. Resume of a live match is probed in Play Mode, not here.
public class MatchSaveTests
{
    string _folder;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "ie-save-tests-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        MatchSave.FolderOverride = _folder;
        MatchSave.Delete();
    }

    [TearDown]
    public void TearDown()
    {
        MatchSave.Delete();
        MatchSave.FolderOverride = null;
        MatchSave.ClearPending();
        try { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); } catch { /* leftover */ }
    }

    [Test]
    public void EmptyFolder_HasNoSave()
    {
        Assert.IsFalse(MatchSave.HasSave);
        Assert.AreEqual("", MatchSave.ContinueSummary);
    }

    [Test]
    public void File_RoundTripsSeatsAndActions()
    {
        var original = new MatchSave.File
        {
            Kind = MatchKind.SinglePlayer.ToString(),
            Seed = 21,
            Options = MatchOptions.Plain.Encode(),
            Year = 2,
            WhoseTurn = "You",
            Phase = "Attack",
            Seats = new[] { "You|Red|Human", "Bot|Blue|Standard BOT" },
            Actions = new[] { "{\"Type\":\"Bid\",\"PlayerId\":0,\"Amount\":1}" },
        };
        var json = UnityEngine.JsonUtility.ToJson(original);
        var copy = UnityEngine.JsonUtility.FromJson<MatchSave.File>(json);
        Assert.AreEqual(21, copy.Seed);
        Assert.AreEqual("You", copy.WhoseTurn);
        Assert.AreEqual(2, copy.Seats.Length);
        Assert.AreEqual("You", MatchSave.SeatName(copy.Seats[0]));
        Assert.AreEqual("Red", MatchSave.SeatColor(copy.Seats[0]));
        Assert.AreEqual("Human", MatchSave.SeatConfig(copy.Seats[0]));
        Assert.AreEqual("Standard BOT", MatchSave.SeatConfig(copy.Seats[1]));
        StringAssert.Contains("Year 2", copy.Summary);
        StringAssert.Contains("You", copy.Summary);
        Assert.AreEqual(1, copy.Actions.Length);
    }
}
