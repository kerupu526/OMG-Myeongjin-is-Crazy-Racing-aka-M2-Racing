using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace M2.Tests.PlayMode
{
    /// <summary>
    /// Provides deterministic synthetic input devices without replacing the editor's global
    /// Input System state. InputTestFixture.SaveAndReset can leave the active UI input module
    /// with a stale state buffer in Unity 6 PlayMode tests, which then produces a false error
    /// from InputActionState.OnBeforeInitialUpdate.
    /// </summary>
    public abstract class StableInputTestFixture
    {
        readonly List<InputDevice> testDevices = new List<InputDevice>();

        [SetUp]
        public virtual void Setup()
        {
        }

        [TearDown]
        public virtual void TearDown()
        {
            for (int i = testDevices.Count - 1; i >= 0; i--)
            {
                InputDevice device = testDevices[i];
                if (device != null && device.added)
                    InputSystem.RemoveDevice(device);
            }

            testDevices.Clear();
        }

        protected Keyboard AddTestKeyboard()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            testDevices.Add(keyboard);
            return keyboard;
        }

        protected void Press(ButtonControl button)
        {
            // Keyboard keys are bitfield controls, so QueueDeltaStateEvent cannot address
            // them directly. Build the delta event the same way InputTestFixture does.
            using (DeltaStateEvent.From(button, out var eventPtr))
            {
                eventPtr.time = InputState.currentTime;
                button.WriteValueIntoEvent(1f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
        }
    }
}
