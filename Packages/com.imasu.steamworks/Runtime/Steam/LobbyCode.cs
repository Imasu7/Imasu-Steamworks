using System;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Short human-friendly lobby codes ("HG6FY14"). The code is stored as
    /// lobby data; joiners search for it with a matchmaking string filter.
    /// Lookalike glyphs (O/0, I/1, L) are excluded so codes survive being
    /// read aloud or typed by hand.
    /// </summary>
    public static class LobbyCode
    {
        public const int Length = 7;

        /// <summary>Unambiguous uppercase alphabet (no I, O, 0, 1).</summary>
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        static readonly Random rng = new Random();

        public static string Generate()
        {
            char[] chars = new char[Length];
            lock (rng)
            {
                for (int i = 0; i < Length; i++)
                    chars[i] = Alphabet[rng.Next(Alphabet.Length)];
            }
            return new string(chars);
        }

        /// <summary>Normalizes user input: trim + uppercase + drop separators.</summary>
        public static string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            char[] chars = input.Trim().ToUpperInvariant().ToCharArray();
            int n = 0;
            foreach (char c in chars)
                if (!char.IsWhiteSpace(c) && c != '-' && c != '_')
                    chars[n++] = c;
            return new string(chars, 0, n);
        }

        /// <summary>True when the normalized input looks like a full Steam lobby ID rather than a short code.</summary>
        public static bool LooksLikeRawLobbyId(string normalized)
        {
            return normalized.Length > Length && ulong.TryParse(normalized, out _);
        }

        /// <summary>True when the normalized input is a valid short code (exactly <see cref="Length"/> alphabet chars).</summary>
        public static bool LooksLikeCode(string normalized)
        {
            if (string.IsNullOrEmpty(normalized) || normalized.Length != Length) return false;
            foreach (char c in normalized)
                if (Alphabet.IndexOf(c) < 0)
                    return false;
            return true;
        }
    }
}
