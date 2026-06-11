// FOOTDRAFT — real-world "form sync" shared types.

using Metaplay.Core.Model;
using System.Collections.Generic;

namespace Game.Logic
{
    /// <summary>
    /// One live "in-form" override: a player (by name) gets their match die bumped by <see cref="TierDelta"/>
    /// tiers (d6→d8→d10; negative nerfs). Set live by an operator when the real WC2026 player is in form — the
    /// change propagates to live matches without a redeploy (see <c>FormActor</c>).
    /// </summary>
    [MetaSerializable]
    public class FormEntry
    {
        [MetaMember(1)] public string Name      { get; private set; }
        [MetaMember(2)] public int    TierDelta { get; private set; }

        public FormEntry() { }
        public FormEntry(string name, int tierDelta)
        {
            Name      = name;
            TierDelta = tierDelta;
        }
    }

    /// <summary> A snapshot of the current live-form overrides, for display in the operator/in-form panel. </summary>
    [MetaSerializable]
    public class FormSnapshot
    {
        [MetaMember(1)] public List<FormEntry> Entries { get; set; } = new List<FormEntry>();

        public FormSnapshot() { }
    }

    /// <summary> Helpers for applying a form tier-bump to a die size. </summary>
    public static class FormUtil
    {
        static readonly int[] DieTiers = { 6, 8, 10 };

        /// <summary> Bumps a die size up/down by <paramref name="tierDelta"/> tiers, clamped to the d6..d10 range. </summary>
        public static int BumpDieTier(int sides, int tierDelta)
        {
            int idx = -1;
            for (int i = 0; i < DieTiers.Length; i++)
                if (DieTiers[i] == sides) { idx = i; break; }
            if (idx < 0)
                return sides; // unknown die size — leave as-is
            int next = idx + tierDelta;
            if (next < 0) next = 0;
            if (next > DieTiers.Length - 1) next = DieTiers.Length - 1;
            return DieTiers[next];
        }
    }
}
