// FOOTDRAFT — the manager's "club": every player they've ever drafted into a knockout XI ("scouted"), with
// how many times. Powers the My Club collection gallery + the all-time best XI showcase. Cosmetic/collection
// value only — it never changes a draft or a match (the draft core stays fair).

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// A manager's scouted-player collection: a player id (legend or World-Cup squad id) → how many times it
    /// has been drafted into a locked knockout XI. Grows as the manager plays Draft Cup / World Cup runs.
    /// </summary>
    [MetaSerializable]
    public class PlayerCollection
    {
        /// <summary> Player id → times drafted into a locked XI. </summary>
        [MetaMember(1)] public MetaDictionary<string, int> Drafted { get; private set; } = new MetaDictionary<string, int>();

        /// <summary> Total drafts recorded (sum of all counts) — the "scouting" lifetime number. </summary>
        [MetaMember(2)] public int TotalDrafted { get; set; }

        public PlayerCollection() { }

        /// <summary> Distinct players scouted (collection size). </summary>
        public int UniqueScouted => Drafted.Count;

        /// <summary> Records one drafted appearance of a player id. </summary>
        public void Record(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
                return;
            Drafted[playerId] = (Drafted.TryGetValue(playerId, out int n) ? n : 0) + 1;
            TotalDrafted++;
        }

        public int TimesDrafted(string playerId) => Drafted.TryGetValue(playerId, out int n) ? n : 0;
    }
}
