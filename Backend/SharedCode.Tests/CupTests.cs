// FOOTDRAFT — Phase 3 tickets & Cup unit tests.

using Metaplay.Core;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    /// <summary>
    /// Unit tests for the Phase 3 meta: regenerating Match Tickets (regen-to-cap, spend, refill) and the
    /// twice-daily Cup schedule (window rollover) plus the milestone track ordering.
    /// </summary>
    [TestFixture]
    public class CupTests
    {
        static MetaTime At(long ms) => MetaTime.FromMillisecondsSinceEpoch(ms);

        [Test]
        public void TicketsRegenOverTimeUpToCap()
        {
            GlobalConfig g = new GlobalConfig();
            long regenMs = (long)g.TicketRegenMinutes * 60_000L;
            PlayerTickets t = new PlayerTickets { Count = 0, LastRegenAt = At(0) };

            Assert.That(t.Available(At(0), g), Is.EqualTo(0));
            Assert.That(t.Available(At(regenMs), g), Is.EqualTo(1));
            Assert.That(t.Available(At(regenMs * 3), g), Is.EqualTo(3));
            Assert.That(t.Available(At(regenMs * 1000), g), Is.EqualTo(g.MaxMatchTickets), "regen is capped");
        }

        [Test]
        public void TicketSpendAndDrain()
        {
            GlobalConfig g = new GlobalConfig();
            PlayerTickets t = new PlayerTickets();
            t.Refill(At(0), g);
            Assert.That(t.Count, Is.EqualTo(g.MaxMatchTickets));

            Assert.That(t.TrySpend(At(0), g), Is.True);
            Assert.That(t.Count, Is.EqualTo(g.MaxMatchTickets - 1));

            PlayerTickets empty = new PlayerTickets { Count = 1, LastRegenAt = At(0) };
            Assert.That(empty.TrySpend(At(0), g), Is.True);
            Assert.That(empty.TrySpend(At(0), g), Is.False, "cannot spend when empty");
        }

        [Test]
        public void RefreshBanksRegeneratedTickets()
        {
            GlobalConfig g = new GlobalConfig();
            long regenMs = (long)g.TicketRegenMinutes * 60_000L;
            PlayerTickets t = new PlayerTickets { Count = 2, LastRegenAt = At(0) };

            t.Refresh(At(regenMs * 2 + 5), g);
            Assert.That(t.Count, Is.EqualTo(4));
        }

        [Test]
        public void CupRollsOverEachWindow()
        {
            GlobalConfig g = new GlobalConfig();
            long windowMs = (long)g.CupWindowHours * 60L * 60L * 1000L;

            long id0 = CupSchedule.CurrentCupId(At(windowMs * 5 + 10), g);
            long id1 = CupSchedule.CurrentCupId(At(windowMs * 6 + 10), g);
            Assert.That(id1, Is.EqualTo(id0 + 1));
            Assert.That(CupSchedule.CupEndsAt(id0, g).MillisecondsSinceEpoch, Is.EqualTo(windowMs * 6));
        }

        [Test]
        public void CupMilestonesAscendInTokens()
        {
            GlobalConfig g = new GlobalConfig();
            Assert.That(g.CupMilestones.Length, Is.GreaterThan(0));
            for (int i = 1; i < g.CupMilestones.Length; i++)
                Assert.That(g.CupMilestones[i].Tokens, Is.GreaterThan(g.CupMilestones[i - 1].Tokens));
        }
    }
}
