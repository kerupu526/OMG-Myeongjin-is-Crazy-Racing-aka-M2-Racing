using UnityEngine.InputSystem;

namespace M2.Tests.PlayMode
{
    /// <summary>
    /// InputTestFixture enables an experimental, package-internal cache consistency diagnostic.
    /// In the current Unity 6 editor it can log a false-positive error while a PlayMode test
    /// moves between frames, which makes otherwise valid input tests fail through LogAssert.
    /// Keep input-value caching enabled and turn off only that diagnostic check.
    /// </summary>
    public abstract class StableInputTestFixture : InputTestFixture
    {
        const string ParanoidReadValueCachingChecks = "PARANOID_READ_VALUE_CACHING_CHECKS";

        public override void Setup()
        {
            base.Setup();
            InputSystem.settings.SetInternalFeatureFlag(ParanoidReadValueCachingChecks, false);
        }
    }
}
