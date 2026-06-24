// FOOTDRAFT — claimed-objective state. Objectives are career milestones (defined in code, computed from the
// live model) that pay out a reward once when claimed. This holds only the "already claimed" set; progress is
// derived on the fly (see Objectives), so the model stays tiny and the track can be re-tuned without migration.

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    [MetaSerializable]
    public class PlayerObjectives
    {
        /// <summary> Ids of objectives whose reward has been claimed. </summary>
        [MetaMember(1)] public OrderedSet<string> Claimed { get; private set; } = new OrderedSet<string>();

        public PlayerObjectives() { }

        public bool IsClaimed(string id) => Claimed.Contains(id);
    }
}
