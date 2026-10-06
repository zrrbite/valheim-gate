using System.Globalization;

namespace ICanShowYouTheWorld.RunMode
{
    /// <summary>
    /// The charred one, left standing after the saga is won (owner, 2026-10-06: "keep him standing for his
    /// last line. Someone might find him"). When a run ends at Fader, the speakers are dismissed - so his
    /// line for after Fader was never heard. Now his spot is saved on the character with its world, and
    /// while no run is live he waits there, without a map pin, until somebody goes back and hears it.
    /// </summary>
    public sealed class Afterword
    {
        public string World;
        public float X, Y, Z;

        /// <summary>"x|y|z|world": the world last, so its name may hold anything.</summary>
        public string Encode() =>
            string.Join("|",
                X.ToString("R", CultureInfo.InvariantCulture),
                Y.ToString("R", CultureInfo.InvariantCulture),
                Z.ToString("R", CultureInfo.InvariantCulture),
                World ?? string.Empty);

        /// <summary>Null for nothing, or anything unreadable: no afterword rather than an error.</summary>
        public static Afterword Decode(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var parts = text.Split(new[] { '|' }, 4);
            if (parts.Length != 4 || parts[3].Length == 0) return null;

            float x, y, z;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                return null;

            return new Afterword { X = x, Y = y, Z = z, World = parts[3] };
        }

        /// <summary>He stands in his own world, while no run is live.</summary>
        public static bool Stands(Afterword afterword, string world, bool runActive) =>
            afterword != null && !runActive && world != null && afterword.World == world;

        /// <summary>Saved when a run is won with Fader down and he was met (his spot is known).</summary>
        public static bool ShouldSave(bool won, bool faderDown, bool metHim) => won && faderDown && metHim;
    }
}
