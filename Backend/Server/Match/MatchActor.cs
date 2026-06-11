// FOOTDRAFT — match server actor.

using Game.Logic;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Client;
using Metaplay.Core.MultiplayerEntity;
using Metaplay.Server.MultiplayerEntity;
using Metaplay.Server.MultiplayerEntity.InternalMessages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Game.Server
{
    /// <summary>
    /// Setup parameters for creating a new match. Carries the participating players (id -> name) and which of
    /// them are bots. (Team selection is v0-assigned round-robin in the actor; lobby-driven team picking is a
    /// follow-up.)
    /// </summary>
    [MetaSerializableDerived(1)]
    public class MatchSetupParams : IMultiplayerEntitySetupParams
    {
        public MetaDictionary<EntityId, string> Players;
        public List<EntityId>                   BotIds;

        [MetaDeserializationConstructor]
        public MatchSetupParams(MetaDictionary<EntityId, string> players, List<EntityId> botIds)
        {
            Players = players;
            BotIds = botIds;
        }
    }

    [EntityConfig]
    public class MatchConfig : EphemeralEntityConfig
    {
        public override EntityKind        EntityKind           => EntityKindGame.Match;
        public override Type              EntityActorType      => typeof(MatchActor);
        // Matches scale with the number of concurrent games, so place them on the logic NodeSet and shard them.
        public override NodeSetPlacement  NodeSetPlacement     => NodeSetPlacement.Logic;
        public override IShardingStrategy ShardingStrategy     => ShardingStrategies.CreateStaticSharded();
        public override TimeSpan          ShardShutdownTimeout => TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Server actor for a single head-to-head dice match. Ticks the <see cref="MatchModel"/> forward (briefing
    /// delay, best-of-five round rolls, win resolution) and validates client-originated actions.
    /// </summary>
    public class MatchActor : EphemeralMultiplayerEntityActorBase<MatchModel, MatchAction>, IMatchModelServerListener
    {
        protected override void OnSwitchedToModel(MatchModel model)
        {
            // Receive server-authoritative match outcomes (e.g. to grant end-of-match rewards). Set only on the
            // server; clients keep the empty server listener.
            model.ServerListener = this;
        }

        static readonly string[] BotNames = { "Sunday League XI", "AI United", "Bot City", "Algorithm FC", "The Underdogs" };

        protected override async Task SetUpModelAsync(MatchModel model, IMultiplayerEntitySetupParams setupParams)
        {
            MatchSetupParams matchSetup = (MatchSetupParams)setupParams;
            GlobalConfig     global     = ((SharedGameConfig)_baselineGameConfig.SharedConfig).Global;

            // Resolve the human XIs from their authoritative models (drafted-squad line ratings). Track their
            // average overall + level to scale the bot opponent so early matches stay winnable.
            Dictionary<EntityId, (LineRatings Ratings, string Crest)> humans = new Dictionary<EntityId, (LineRatings, string)>();
            int humanOverallSum = 0, humanCount = 0, minHumanLevel = int.MaxValue;
            foreach ((EntityId playerId, string playerName) in matchSetup.Players)
            {
                if (matchSetup.BotIds.Contains(playerId))
                    continue;
                (LineRatings ratings, string crest, int level) = await FetchPlayerRatingsAsync(playerId);
                humans[playerId] = (ratings, crest);
                humanOverallSum += OverallOf(ratings);
                humanCount++;
                minHumanLevel = System.Math.Min(minHumanLevel, level);
            }

            int refOverall = humanCount > 0 ? humanOverallSum / humanCount : 75;
            int refLevel   = humanCount > 0 ? minHumanLevel : 1;

            int botIndex = 0;
            foreach ((EntityId playerId, string playerName) in matchSetup.Players)
            {
                bool        isBot = matchSetup.BotIds.Contains(playerId);
                LineRatings ratings;
                string      crest, name;
                if (isBot)
                {
                    int target = refOverall * global.BotDifficultyPct(refLevel) / 100;
                    ratings = BotRatings(target);
                    crest   = "🤖";
                    name    = BotNames[botIndex++ % BotNames.Length];
                }
                else
                {
                    (LineRatings r, string c) = humans[playerId];
                    ratings = r;
                    crest   = c;
                    name    = playerName;
                }

                model.Squads[playerId] = new MatchSquadState(name, crest, isBot, ratings);
            }

            // Start in the pre-match briefing phase; OnTick kicks off (and builds the timeline) after the delay.
            model.Phase = MatchPhase.Starting;
        }

        /// <summary> Weighted single-number overall of a line-rating set (mirrors MatchSquadState.Overall). </summary>
        static int OverallOf(LineRatings r)
            => (r.Attack * 3 + r.Midfield * 3 + r.Defence * 3 + r.Goalkeeping) / 10 + (r.Chemistry > 0 ? r.Chemistry / 4 : 0);

        /// <summary> A bot XI whose lines sit around a target overall (clamped to a sane band). </summary>
        static LineRatings BotRatings(int targetOverall)
        {
            int t = System.Math.Clamp(targetOverall, 55, 95);
            return new LineRatings { Attack = t, Midfield = t, Defence = t, Goalkeeping = System.Math.Max(40, t - 2), Chemistry = 4 };
        }

        /// <summary> Fetches a human player's drafted-XI line ratings + crest + level, with a baseline fallback. </summary>
        async Task<(LineRatings Ratings, string Crest, int Level)> FetchPlayerRatingsAsync(EntityId playerId)
        {
            try
            {
                PlayerGetSquadResponse response = await EntityAskAsync<PlayerGetSquadResponse>(playerId, new PlayerGetSquadRequest());
                if (response?.Ratings != null)
                    return (response.Ratings, string.IsNullOrEmpty(response.Crest) ? "⚽" : response.Crest, response.ManagerLevel);
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to fetch ratings for player {PlayerId}; using a baseline XI. Error: {Error}", playerId, ex);
            }
            return (new LineRatings { Attack = 72, Midfield = 72, Defence = 72, Goalkeeping = 70 }, "⚽", 1);
        }

        void IMatchModelServerListener.MatchEnded(EntityId winner)
        {
            // Grant end-of-match rewards to each human participant. Bots have no PlayerActor.
            // (roundsWon carries goals scored — it scales the reward bonus, same as before.)
            foreach ((EntityId playerId, MatchSquadState squad) in Model.Squads)
            {
                if (squad.IsBot)
                    continue;
                CastMessage(playerId, new GrantMatchRewardsMessage(won: playerId == winner, roundsWon: squad.Goals));
            }
        }

        protected override bool ValidateClientOriginatingAction(ClientPeerState client, MatchAction action)
        {
            if (action is MatchClientAction clientAction)
                return clientAction.ValidateOnServer(client.PlayerId);
            return true;
        }

        protected override Task<InternalEntitySubscribeResponseBase> OnClientSessionStart(
            EntityId sessionId,
            EntityId playerId,
            InternalEntitySubscribeRequestBase requestBase,
            List<AssociatedEntityRefBase> associatedEntities)
        {
            // Only players that are part of this match may observe it.
            if (!Model.Squads.ContainsKey(playerId))
                throw new InternalEntitySubscribeRefusedBase.Builtins.NotAParticipant();

            return base.OnClientSessionStart(sessionId, playerId, requestBase, associatedEntities);
        }
    }
}
