// FOOTDRAFT — placeholder model for the singleton Form (live-form) registry entity.

using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.MultiplayerEntity;

namespace Game.Logic
{
    /// <summary>
    /// Trivial model for the singleton <c>FormActor</c>. The live-form overrides live in the actor's memory
    /// (settable at runtime by an operator, no redeploy); this model only exists to reuse the multiplayer-entity
    /// actor base.
    /// </summary>
    [MetaSerializableDerived(103)]
    [SupportedSchemaVersions(1, 1)]
    public class FormModel : MultiplayerModelBase<FormModel>
    {
        public override int TicksPerSecond => 1;
        public override void OnTick() { }
        public override void OnFastForwardTime(MetaDuration elapsedTime) { }
        public override string GetDisplayNameForDashboard() => "Live Form Registry";
    }

    [MetaSerializable]
    public abstract class FormAction : ModelAction<FormModel>
    {
    }
}
