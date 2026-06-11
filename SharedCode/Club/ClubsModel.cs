// FOOTDRAFT — placeholder model for the singleton Clubs registry entity.

using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.MultiplayerEntity;

namespace Game.Logic
{
    /// <summary>
    /// Trivial model for the singleton <c>ClubsActor</c>. The actual club registry + Club League standings live
    /// in the actor's memory (clients don't observe this entity); this model only exists to reuse the
    /// multiplayer-entity actor base. (In production the Guild + Leagues frameworks persist this state.)
    /// </summary>
    [MetaSerializableDerived(102)]
    [SupportedSchemaVersions(1, 1)]
    public class ClubsModel : MultiplayerModelBase<ClubsModel>
    {
        public override int TicksPerSecond => 1;
        public override void OnTick() { }
        public override void OnFastForwardTime(MetaDuration elapsedTime) { }
        public override string GetDisplayNameForDashboard() => "Clubs Registry";
    }

    /// <summary> Action base for <see cref="ClubsModel"/> (no concrete actions; state is server-actor memory). </summary>
    [MetaSerializable]
    public abstract class ClubsAction : ModelAction<ClubsModel>
    {
    }
}
