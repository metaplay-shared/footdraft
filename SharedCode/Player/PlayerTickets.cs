// FOOTDRAFT — regenerating Match Tickets that gate matchmaking entry.

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// Match Tickets: a regenerating soft resource spent to enter matchmade (ranked/Cup) matches. One ticket
    /// regenerates every <see cref="GlobalConfig.TicketRegenMinutes"/> up to <see cref="GlobalConfig.MaxMatchTickets"/>;
    /// when full, the regen clock parks at "now" so it only starts counting after the next spend. Friendlies are
    /// free, so they don't touch this. Gems can refill instantly (a convenience sink).
    /// </summary>
    [MetaSerializable]
    public class PlayerTickets
    {
        [MetaMember(1)] public int      Count       { get; set; }
        [MetaMember(2)] public MetaTime LastRegenAt { get; set; }

        public PlayerTickets() { }

        /// <summary> Pure projection: tickets available at <paramref name="now"/> (does not mutate). </summary>
        public int Available(MetaTime now, GlobalConfig global)
        {
            if (Count >= global.MaxMatchTickets)
                return Count;
            long regenMs = (long)global.TicketRegenMinutes * 60_000L;
            if (regenMs <= 0)
                return Count;
            long elapsed = (now - LastRegenAt).Milliseconds;
            int regened = elapsed > 0 ? (int)(elapsed / regenMs) : 0;
            int available = Count + regened;
            return available > global.MaxMatchTickets ? global.MaxMatchTickets : available;
        }

        /// <summary> Milliseconds until the next ticket regenerates (0 if full or regen disabled). </summary>
        public long MillisToNextRegen(MetaTime now, GlobalConfig global)
        {
            if (Count >= global.MaxMatchTickets)
                return 0;
            long regenMs = (long)global.TicketRegenMinutes * 60_000L;
            if (regenMs <= 0)
                return 0;
            long elapsed = (now - LastRegenAt).Milliseconds;
            long intoCurrent = elapsed % regenMs;
            return intoCurrent < 0 ? regenMs : regenMs - intoCurrent;
        }

        /// <summary> Mutating: bank any regenerated tickets and advance the regen clock. </summary>
        public void Refresh(MetaTime now, GlobalConfig global)
        {
            int max = global.MaxMatchTickets;
            if (Count >= max)
            {
                LastRegenAt = now;
                return;
            }
            long regenMs = (long)global.TicketRegenMinutes * 60_000L;
            if (regenMs <= 0)
                return;
            long elapsed = (now - LastRegenAt).Milliseconds;
            if (elapsed < regenMs)
                return;
            int regened = (int)(elapsed / regenMs);
            Count += regened;
            if (Count >= max)
            {
                Count = max;
                LastRegenAt = now;
            }
            else
            {
                LastRegenAt = LastRegenAt + MetaDuration.FromMilliseconds((long)regened * regenMs);
            }
        }

        /// <summary> Refresh then spend one ticket if available. Returns true on success. </summary>
        public bool TrySpend(MetaTime now, GlobalConfig global)
        {
            Refresh(now, global);
            if (Count <= 0)
                return false;
            bool wasFull = Count >= global.MaxMatchTickets;
            Count--;
            // Start the regen clock when dropping below the cap.
            if (wasFull)
                LastRegenAt = now;
            return true;
        }

        /// <summary> Refill to the cap (e.g. a gem purchase). </summary>
        public void Refill(MetaTime now, GlobalConfig global)
        {
            Count = global.MaxMatchTickets;
            LastRegenAt = now;
        }
    }
}
