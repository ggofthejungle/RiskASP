using NUnit.Framework;

// Shared helpers for PlayMode tests.
public static class TestGame
{
    // After bidding, every chooser (highest bid first) takes the earliest free turn position,
    // so the highest bidder plays first, like before turn-order choosing existed.
    public static void ChooseEarliest(GameManager gm)
    {
        for (int i = 0; i < 10 && gm.IsChoosingOrder; i++)
            Assert.IsTrue(gm.ChooseTurnPosition(gm.AvailableTurnPositions[0]));
        Assert.IsFalse(gm.IsChoosingOrder, "Turn order should be chosen");
    }
}
