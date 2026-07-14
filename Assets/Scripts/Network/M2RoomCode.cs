using System;
using System.Security.Cryptography;

namespace M2.Network
{
    /// <summary>
    /// Creates the player-facing identifier for an M2 private race.  This identifier is also the
    /// stable Lobby session ID, so a guest can join the same private session without exposing the
    /// Unity-generated Relay/Lobby join code in the UI.
    /// </summary>
    public static class M2RoomCode
    {
        public const string Prefix = "M2-";
        public const int SuffixLength = 4;

        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public static string Generate()
        {
            byte[] randomBytes = new byte[SuffixLength];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(randomBytes);
            }

            char[] suffix = new char[SuffixLength];
            for (int i = 0; i < suffix.Length; i++)
            {
                suffix[i] = Alphabet[randomBytes[i] % Alphabet.Length];
            }

            return Prefix + new string(suffix);
        }

        public static bool TryNormalize(string input, out string roomCode)
        {
            roomCode = string.Empty;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string normalized = input.Trim().ToUpperInvariant();
            if (!normalized.StartsWith(Prefix, StringComparison.Ordinal)) return false;

            string suffix = normalized.Substring(Prefix.Length);
            if (suffix.Length != SuffixLength) return false;

            for (int i = 0; i < suffix.Length; i++)
            {
                char character = suffix[i];
                bool isUppercaseLetter = character >= 'A' && character <= 'Z';
                bool isDigit = character >= '0' && character <= '9';
                if (!isUppercaseLetter && !isDigit) return false;
            }

            roomCode = Prefix + suffix;
            return true;
        }
    }
}
