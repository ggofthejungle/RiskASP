using Unity.Services.Multiplayer;

// The room service wants a snapshot it can hand to the next host. In lockstep every device already holds
// the whole game and the action log, so the snapshot is only the number of entries, for the logs.
public sealed class LockstepMigrationData : IMigrationDataHandler
{
    public static int AppliedEntries;

    public byte[] Generate() => System.BitConverter.GetBytes(AppliedEntries);

    public void Apply(byte[] migrationData)
    {
        if (migrationData is { Length: >= 4 })
            UnityEngine.Debug.Log($"Online: the old host had sent {System.BitConverter.ToInt32(migrationData, 0)} entries; " +
                                  $"this device has applied {AppliedEntries}.");
    }
}
