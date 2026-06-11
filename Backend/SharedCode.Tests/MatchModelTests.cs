// FOOTDRAFT — match model unit tests: kick-off, goal reveal over the clock, decisive full time, determinism.

using Metaplay.Core;
using Metaplay.Core.MultiplayerEntity;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    /// <summary>
    /// Tests the deterministic per-tick logic of <see cref="MatchModel"/>: the briefing→kick-off transition,
    /// the precomputed goal timeline, goals revealing as the clock runs, a decisive full-time result, and
    /// replay determinism + rating sensitivity.
    /// </summary>
    [TestFixture]
    public class MatchModelTests
    {
        static EntityId NewManager() => EntityId.CreateRandom(EntityKindCore.Player);

        static LineRatings R(int att, int mid, int def, int gk, int chem = 0)
            => new LineRatings { Attack = att, Midfield = mid, Defence = def, Goalkeeping = gk, Chemistry = chem };
        static LineRatings Strong() => R(92, 90, 90, 88, 12);
        static LineRatings Weak()   => R(68, 66, 67, 65);

        static MatchModel CreateMatch(MetaTime epoch, params (EntityId id, bool isBot, LineRatings r)[] squads)
        {
            MatchModel model = new MatchModel();
            ((IMultiplayerModel)model).ResetTime(epoch);
            foreach ((EntityId id, bool isBot, LineRatings r) in squads)
                model.Squads[id] = new MatchSquadState(id.ToString(), "⚽", isBot, r);
            return model; // starts in MatchPhase.Starting; ticking drives kick-off + playout
        }

        static void Tick(MatchModel model, int numTicks)
        {
            for (int i = 0; i < numTicks; i++)
                model.Tick(checksumCtx: null);
        }

        static void PlayToEnd(MatchModel model, int maxTicks = 800)
        {
            int i = 0;
            while (model.Phase != MatchPhase.Ended && i++ < maxTicks)
                model.Tick(checksumCtx: null);
        }

        [Test]
        public void BriefingHoldsThenKicksOffAndBuildsTimeline()
        {
            EntityId a = NewManager(), b = NewManager();
            MatchModel m = CreateMatch(MetaTime.Epoch, (a, false, Strong()), (b, false, Weak()));

            Tick(m, MatchModel.StartDelayTicks - 1);
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.Starting));
            Assert.That(m.Goals, Is.Empty);

            Tick(m, 1); // reaches the start delay → kick off + build timeline
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.Active));
            Assert.That(m.TimelineBuilt, Is.True);
            Assert.That(m.Goals.Count, Is.GreaterThanOrEqualTo(1)); // decisive: ≥1 goal (draw → stoppage clincher)
        }

        [Test]
        public void GoalsRevealAsTheClockRunsAndTallyMatchesTimeline()
        {
            EntityId a = NewManager(), b = NewManager();
            MatchModel m = CreateMatch(MetaTime.Epoch, (a, false, Strong()), (b, false, Weak()));

            PlayToEnd(m);
            Assert.That(m.Phase, Is.EqualTo(MatchPhase.Ended));
            Assert.That(m.RevealedGoals, Is.EqualTo(m.Goals.Count));

            // Final tally equals the timeline.
            int aGoals = 0, bGoals = 0;
            foreach (MatchGoal g in m.Goals)
            {
                if (g.Scorer == a) aGoals++;
                else if (g.Scorer == b) bGoals++;
            }
            Assert.That(m.GoalsOf(a), Is.EqualTo(aGoals));
            Assert.That(m.GoalsOf(b), Is.EqualTo(bGoals));
        }

        [Test]
        public void FullTimeIsDecisiveAndWinnerIsAParticipant()
        {
            EntityId a = NewManager(), b = NewManager();
            // Use a seed that the sim draws as a draw, to exercise the stoppage-time clincher path too.
            for (int s = 0; s < 20; s++)
            {
                MatchModel m = CreateMatch(MetaTime.Epoch + MetaDuration.FromSeconds(s), (a, false, R(82, 82, 82, 80)), (b, false, R(82, 82, 82, 80)));
                PlayToEnd(m);
                Assert.That(m.Phase, Is.EqualTo(MatchPhase.Ended));
                Assert.That(m.Winner == a || m.Winner == b, Is.True, "winner must be a participant");
                Assert.That(m.GoalsOf(m.Winner), Is.GreaterThanOrEqualTo(m.GoalsOf(m.Winner == a ? b : a)));
            }
        }

        [Test]
        public void SameEpochAndRatingsProduceIdenticalMatch()
        {
            EntityId a = NewManager(), b = NewManager();
            MatchModel first  = CreateMatch(MetaTime.Epoch, (a, false, Strong()), (b, false, Weak()));
            MatchModel second = CreateMatch(MetaTime.Epoch, (a, false, Strong()), (b, false, Weak()));
            PlayToEnd(first);
            PlayToEnd(second);

            Assert.That(second.Winner, Is.EqualTo(first.Winner));
            Assert.That(second.Goals.Count, Is.EqualTo(first.Goals.Count));
            for (int i = 0; i < first.Goals.Count; i++)
            {
                Assert.That(second.Goals[i].Minute, Is.EqualTo(first.Goals[i].Minute));
                Assert.That(second.Goals[i].Scorer, Is.EqualTo(first.Goals[i].Scorer));
            }
        }

        [Test]
        public void StrongerXiWinsTheLargeMajority()
        {
            EntityId strong = NewManager(), weak = NewManager();
            int strongWins = 0;
            const int samples = 50;
            for (int i = 0; i < samples; i++)
            {
                MatchModel m = CreateMatch(MetaTime.Epoch + MetaDuration.FromSeconds(i), (strong, false, Strong()), (weak, false, Weak()));
                PlayToEnd(m);
                if (m.Winner == strong)
                    strongWins++;
            }
            Assert.That(strongWins, Is.GreaterThanOrEqualTo(40), $"stronger XI won {strongWins}/{samples}; expected a large majority");
        }
    }
}
