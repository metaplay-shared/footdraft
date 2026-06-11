// FOOTDRAFT — WS3: transfer-window swap validation, the daily window schedule, and the wallet-coin cost curves.

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class TransferTests
    {
        static FormationInfo F433() => Array.Find(FormationContent.Formations, x => x.Id.Value == "4-3-3");

        static LegendPlayer Legend(string id, string name, Position pos) =>
            new LegendPlayer(id, name, pos, ovr: 80, nation: "Nation", club: "Club", era: Era.E2010s, season: "2015/16");

        // A 4-3-3 roster: slot 0 = GK, 1..4 = DEF, 5..7 = MID, 8..10 = FWD.
        static Dictionary<int, string> Roster() => new Dictionary<int, string>
        {
            { 1, "old_def" }, // a DEF we'll try to swap out
            { 8, "old_fwd" },
        };

        [Test]
        public void ValidSwapReturnsTheFreedSlot()
        {
            HashSet<string> taken = new HashSet<string> { "OldDef", "OldFwd" };
            LegendPlayer drop = Legend("old_def", "OldDef", Position.DEF);
            LegendPlayer add  = Legend("new_def", "NewDef", Position.DEF);

            string err = LeagueTransferEngine.ValidateSwap(F433(), Roster(), taken, drop, add, out int slot);

            Assert.That(err, Is.Empty);
            Assert.That(slot, Is.EqualTo(1), "the DEF slot the dropped player occupied");
        }

        [Test]
        public void RejectsPositionMismatch()
        {
            // Trying to put a forward into the dropped defender's slot.
            string err = LeagueTransferEngine.ValidateSwap(F433(), Roster(), new HashSet<string> { "OldDef" },
                Legend("old_def", "OldDef", Position.DEF), Legend("a_fwd", "AFwd", Position.FWD), out _);
            Assert.That(err, Does.Contain("DEF"));
        }

        [Test]
        public void RejectsAddingAPlayerAlreadyOnAnotherTeam()
        {
            HashSet<string> taken = new HashSet<string> { "OldDef", "RivalDef" };
            string err = LeagueTransferEngine.ValidateSwap(F433(), Roster(), taken,
                Legend("old_def", "OldDef", Position.DEF), Legend("rival_def", "RivalDef", Position.DEF), out _);
            Assert.That(err, Does.Contain("already on another team"));
        }

        [Test]
        public void RejectsDroppingAPlayerNotInTheXi()
        {
            string err = LeagueTransferEngine.ValidateSwap(F433(), Roster(), new HashSet<string> { "Ghost" },
                Legend("ghost", "Ghost", Position.DEF), Legend("new_def", "NewDef", Position.DEF), out _);
            Assert.That(err, Does.Contain("isn't in your XI"));
        }

        [Test]
        public void TransferCostScalesWithOvrAndFloorsAtBase()
        {
            // Defaults: base 60, +15/OVR above pivot 70.
            LeagueDefinition def = LeagueDefinitionContent.Default;
            Assert.That(def.TransferCostFor(70), Is.EqualTo(60),  "pivot OVR costs the base");
            Assert.That(def.TransferCostFor(64), Is.EqualTo(60),  "below pivot floors at the base");
            Assert.That(def.TransferCostFor(75), Is.EqualTo(135));
            Assert.That(def.TransferCostFor(80), Is.EqualTo(210));
            Assert.That(def.TransferCostFor(90), Is.EqualTo(360));
            Assert.That(def.TransferCostFor(93), Is.EqualTo(405));
        }

        [Test]
        public void MarqueeGemCostScalesAboveTheEligibilityFloor()
        {
            // Defaults: min OVR 85, 25 gems base, +5/OVR above.
            LeagueDefinition def = LeagueDefinitionContent.Default;
            Assert.That(def.MarqueeGemCostFor(85), Is.EqualTo(25));
            Assert.That(def.MarqueeGemCostFor(88), Is.EqualTo(40));
            Assert.That(def.MarqueeGemCostFor(93), Is.EqualTo(65));
        }

        [Test]
        public void DailyWindowScheduleOpensWithinTheConfiguredHours()
        {
            // The DEFAULT window is always-open (the in-season metagame); a configured short window
            // (e.g. a 17:00–19:00 "deadline day") still gates correctly.
            LeagueDefinition def = LeagueDefinitionContent.Default;
            typeof(LeagueDefinition).GetProperty(nameof(LeagueDefinition.TransferWindowStartHourUtc))!.SetValue(def, 17);
            typeof(LeagueDefinition).GetProperty(nameof(LeagueDefinition.TransferWindowDurationHours))!.SetValue(def, 2);
            try
            {
                Assert.That(def.IsTransferWindowHour(17), Is.True);
                Assert.That(def.IsTransferWindowHour(18), Is.True);
                Assert.That(def.IsTransferWindowHour(19), Is.False, "closes when the matchday sims");
                Assert.That(def.IsTransferWindowHour(9), Is.False);
            }
            finally
            {
                // Default is a shared static — restore the always-open default.
                typeof(LeagueDefinition).GetProperty(nameof(LeagueDefinition.TransferWindowStartHourUtc))!.SetValue(def, 0);
                typeof(LeagueDefinition).GetProperty(nameof(LeagueDefinition.TransferWindowDurationHours))!.SetValue(def, 24);
            }
        }
    }
}
