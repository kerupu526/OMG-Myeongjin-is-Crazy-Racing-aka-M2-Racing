namespace M2.Items
{
    // Compact wire representation of an item. ItemDefinition is a class, so it can't be
    // replicated over Netcode directly; instead we send this single byte and each peer
    // looks up the full definition locally via ItemCatalog.CreateFromId. The numeric
    // layout is (int)type * 2 + tier + 1, so ItemCatalog.IdFor can compute it arithmetically
    // (None = 0 = "empty slot / collected").
    public enum NetItemId : byte
    {
        None = 0,
        AccelBase = 1,
        AccelDerived = 2,
        AttackBase = 3,
        AttackDerived = 4,
        DefenseBase = 5,
        DefenseDerived = 6,
    }
}
