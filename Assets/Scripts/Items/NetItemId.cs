namespace M2.Items
{
    // Stable wire IDs. Values are grouped by type but intentionally not derived from tier: the
    // final roster has several items at the same tier, and these bytes must remain compatible
    // between host/client and across future balance-only changes.
    public enum NetItemId : byte
    {
        None = 0,
        Gasoline = 1,
        SuperGasoline = 2,
        HappyBirthdayToYou = 3,
        JaeSeokGasoline = 4,

        Bomb = 16,
        C4 = 17,
        Dynamite = 18,
        StickGrenade = 19,
        AtomicBomb = 20,
        LoveLetter = 21,

        Shield = 32,
        SpikedShield = 33,
        GoldenShield = 34,
    }
}
