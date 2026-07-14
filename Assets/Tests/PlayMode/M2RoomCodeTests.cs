using M2.Network;
using NUnit.Framework;

namespace M2.Tests.PlayMode
{
    public class M2RoomCodeTests
    {
        [Test]
        public void Generate_Uses_The_M2_Prefix_And_Four_Uppercase_Alphanumeric_Characters()
        {
            string roomCode = M2RoomCode.Generate();

            StringAssert.IsMatch("^M2-[A-Z0-9]{4}$", roomCode);
        }

        [TestCase("m2-1l4g", "M2-1L4G")]
        [TestCase(" M2-7X4K ", "M2-7X4K")]
        public void TryNormalize_Accepts_The_M2_Code_Without_Case_Sensitivity(string input, string expected)
        {
            bool valid = M2RoomCode.TryNormalize(input, out string roomCode);

            Assert.IsTrue(valid);
            Assert.AreEqual(expected, roomCode);
        }

        [TestCase("")]
        [TestCase("7X4K")]
        [TestCase("M2-7X4")]
        [TestCase("M2-7X4K5")]
        [TestCase("M2-7X*K")]
        public void TryNormalize_Rejects_Anything_Outside_The_M2_Code_Format(string input)
        {
            bool valid = M2RoomCode.TryNormalize(input, out string roomCode);

            Assert.IsFalse(valid);
            Assert.AreEqual(string.Empty, roomCode);
        }
    }
}
