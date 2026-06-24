// FOOTDRAFT — "pin draft" House Rule: a per-league filter on the spin pool, so a commissioner can run a themed
// draft (e.g. "2010s only" or "elite clubs only"). Encoded as one compact string so it threads through the
// create flow exactly like CapBands. Shared so the client (rules summary) + server (spin filter) agree.

using System.Collections.Generic;

namespace Game.Logic
{
    public static class LeaguePin
    {
        /// <summary>
        /// Parse a pin string ("era:E2010s,elite:1") into its parts. <paramref name="era"/> is an <see cref="Era"/>
        /// enum name ("" = any era); <paramref name="elite"/> restricts to top-tier club-seasons. Blank = no pin.
        /// </summary>
        public static (string Era, bool Elite) Parse(string pin)
        {
            string era = "";
            bool elite = false;
            if (!string.IsNullOrWhiteSpace(pin))
            {
                foreach (string part in pin.Split(','))
                {
                    string[] kv = part.Split(':');
                    if (kv.Length != 2) continue;
                    string k = kv[0].Trim().ToLowerInvariant();
                    string v = kv[1].Trim();
                    if (k == "era" && !string.IsNullOrEmpty(v)) era = v;
                    else if (k == "elite" && (v == "1" || v.ToLowerInvariant() == "true")) elite = true;
                }
            }
            return (era, elite);
        }

        public static bool IsSet(string pin)
        {
            (string era, bool elite) = Parse(pin);
            return !string.IsNullOrEmpty(era) || elite;
        }

        /// <summary> Human-readable summary for the rules line ("90s · elite clubs"), or "" if no pin. </summary>
        public static string Describe(string pin)
        {
            (string era, bool elite) = Parse(pin);
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(era)) parts.Add(EraLabel(era) + " only");
            if (elite) parts.Add("elite clubs");
            return string.Join(" · ", parts);
        }

        public static string EraLabel(string era) => era switch
        {
            "E1980s" => "80s",
            "E1990s" => "90s",
            "E2000s" => "00s",
            "E2010s" => "10s",
            "E2020s" => "20s",
            _        => era,
        };
    }
}
