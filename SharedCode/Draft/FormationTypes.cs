// FOOTDRAFT — formations: an ordered list of 11 position slots the draft must fill.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using System.Collections.Generic;

namespace Game.Logic
{
    /// <summary> Identifier for a formation, e.g. "4-3-3". </summary>
    [MetaSerializable]
    public class FormationId : StringId<FormationId> { }

    /// <summary>
    /// A formation: exactly eleven ordered <see cref="Slots"/>, each constraining which <see cref="Position"/>
    /// can be drafted there. Slot 0 is always the goalkeeper. The DEF/MID/FWD counts are what the original
    /// 38-0 "formation balance" reward keys off (too few defenders / no GK hurts you).
    /// </summary>
    [MetaSerializable]
    public class FormationInfo : IGameConfigData<FormationId>
    {
        [MetaMember(1)] public FormationId    Id          { get; private set; }
        [MetaMember(2)] public string         DisplayName { get; private set; }
        [MetaMember(3)] public List<Position> Slots       { get; private set; } = new List<Position>();

        public FormationId ConfigKey => Id;

        public FormationInfo() { }
        public FormationInfo(string id, string displayName, List<Position> slots)
        {
            Id          = FormationId.FromString(id);
            DisplayName = displayName;
            Slots       = slots;
        }

        public int CountOf(Position position)
        {
            int n = 0;
            foreach (Position slot in Slots)
                if (slot == position)
                    n++;
            return n;
        }
    }

    public static class FormationContent
    {
        // Slot builder: GK + given DEF/MID/FWD counts (sums must be 11).
        static List<Position> Lineup(int def, int mid, int fwd)
        {
            List<Position> slots = new List<Position>(11) { Position.GK };
            for (int i = 0; i < def; i++) slots.Add(Position.DEF);
            for (int i = 0; i < mid; i++) slots.Add(Position.MID);
            for (int i = 0; i < fwd; i++) slots.Add(Position.FWD);
            return slots;
        }

        public static readonly FormationInfo[] Formations =
        {
            new FormationInfo("4-3-3",   "4-3-3",   Lineup(4, 3, 3)),
            new FormationInfo("4-4-2",   "4-4-2",   Lineup(4, 4, 2)),
            new FormationInfo("4-2-3-1", "4-2-3-1", Lineup(4, 5, 1)), // 2 holding + 3 attacking mids modelled as 5 MID
            new FormationInfo("3-5-2",   "3-5-2",   Lineup(3, 5, 2)),
            new FormationInfo("5-3-2",   "5-3-2",   Lineup(5, 3, 2)),
        };

        public static GameConfigLibrary<FormationId, FormationInfo> CreateLibrary()
            => GameConfigLibrary<FormationId, FormationInfo>.CreateSolo(Formations);
    }
}
