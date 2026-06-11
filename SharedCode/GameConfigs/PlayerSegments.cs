// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Math;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using System.Collections.Generic;
using static Metaplay.Core.Player.PlayerPropertyConstant;

namespace Game.Logic
{
    #region Player Segment Info

    /// <summary>
    /// Game-specific player segment info class.
    /// </summary>
    [MetaSerializableDerived(1)]
    public class PlayerSegmentInfo : PlayerSegmentInfoBase
    {
        public PlayerSegmentInfo() { }
        public PlayerSegmentInfo(PlayerSegmentId segmentId, PlayerCondition playerCondition, string displayName, string description)
            : base(segmentId, playerCondition, displayName, description)
        {
        }
    }

    #endregion

    #region Player Property IDs

    /// <summary>
    /// Property for time since player's last login.
    /// Used for activity-based segmentation.
    /// </summary>
    [MetaSerializableDerived(1001)]
    public class PlayerPropertyTimeSinceLastLogin : TypedPlayerPropertyId<MetaDuration>
    {
        public override string DisplayName => "Time since last login";

        public override MetaDuration GetTypedValueForPlayer(IPlayerModelBase player)
        {
            return player.CurrentTime - player.Stats.LastLoginAt;
        }
    }

    /// <summary>
    /// Property for player's total IAP spend in USD.
    /// Uses the SDK's built-in TotalIapSpend tracking from InAppPurchaseHistory.
    /// </summary>
    [MetaSerializableDerived(1002)]
    public class PlayerPropertyTotalIapSpendUsd : TypedPlayerPropertyId<F64>
    {
        public override string DisplayName => "Total IAP spend (USD)";

        public override F64 GetTypedValueForPlayer(IPlayerModelBase player)
        {
            // Uses SDK's built-in IAP tracking from InAppPurchaseHistory
            return player.TotalIapSpend;
        }
    }

    /// <summary>
    /// Property for player's country tier based on PPP.
    /// Used for country-based segmentation.
    /// </summary>
    [MetaSerializableDerived(1003)]
    public class PlayerPropertyCountryTier : TypedPlayerPropertyId<int>
    {
        public override string DisplayName => "Country tier (PPP)";

        public override int GetTypedValueForPlayer(IPlayerModelBase player)
        {
            string countryCode = player.LastKnownLocation?.Country.IsoCode;
            return CountryTierContent.GetTierForCountry(countryCode);
        }
    }

    /// <summary>
    /// Property for player's last known country code.
    /// Used for specific country targeting.
    /// </summary>
    [MetaSerializableDerived(1004)]
    public class PlayerPropertyLastKnownCountry : TypedPlayerPropertyId<string>
    {
        public override string DisplayName => "Last known country";

        public override string GetTypedValueForPlayer(IPlayerModelBase player)
        {
            return player.LastKnownLocation?.Country.IsoCode;
        }
    }

    #endregion

    #region Segment Content

    /// <summary>
    /// Static content for player segment definitions.
    /// </summary>
    public static class PlayerSegmentContent
    {
        /// <summary>
        /// Creates the player segments library with all activity, spending, and country tier segments.
        /// </summary>
        public static GameConfigLibrary<PlayerSegmentId, PlayerSegmentInfo> CreatePlayerSegmentsLibrary()
        {
            List<PlayerSegmentInfo> segments = new List<PlayerSegmentInfo>();

            // Activity-based segments (login recency)
            segments.AddRange(CreateActivitySegments());

            // Spending-based segments (total IAP USD)
            segments.AddRange(CreateSpendingSegments());

            // Country tier segments (PPP)
            segments.AddRange(CreateCountryTierSegments());

            return GameConfigLibrary<PlayerSegmentId, PlayerSegmentInfo>.CreateSolo(segments);
        }

        /// <summary>
        /// Creates activity-based segments based on login recency.
        /// </summary>
        static IEnumerable<PlayerSegmentInfo> CreateActivitySegments()
        {
            // Active Players: Last login < 24 hours
            yield return new PlayerSegmentInfo(
                PlayerSegmentId.FromString("ActivePlayers"),
                new PlayerSegmentBasicCondition(
                    propertyRequirements: new List<PlayerPropertyRequirement>
                    {
                        new PlayerPropertyRequirement(
                            new PlayerPropertyTimeSinceLastLogin(),
                            min: null,
                            max: new MetaDurationConstant(MetaDuration.FromHours(24)))
                    },
                    requireAnySegment: null,
                    requireAllSegments: null),
                displayName: "Active Players",
                description: "Players who logged in within the last 24 hours");

            // At-Risk Players: Last login between 3-7 days
            yield return new PlayerSegmentInfo(
                PlayerSegmentId.FromString("AtRiskPlayers"),
                new PlayerSegmentBasicCondition(
                    propertyRequirements: new List<PlayerPropertyRequirement>
                    {
                        new PlayerPropertyRequirement(
                            new PlayerPropertyTimeSinceLastLogin(),
                            min: new MetaDurationConstant(MetaDuration.FromDays(3)),
                            max: new MetaDurationConstant(MetaDuration.FromDays(7)))
                    },
                    requireAnySegment: null,
                    requireAllSegments: null),
                displayName: "At-Risk Players",
                description: "Players who haven't logged in for 3-7 days");

            // Dormant Players: Last login between 7-30 days
            yield return new PlayerSegmentInfo(
                PlayerSegmentId.FromString("DormantPlayers"),
                new PlayerSegmentBasicCondition(
                    propertyRequirements: new List<PlayerPropertyRequirement>
                    {
                        new PlayerPropertyRequirement(
                            new PlayerPropertyTimeSinceLastLogin(),
                            min: new MetaDurationConstant(MetaDuration.FromDays(7)),
                            max: new MetaDurationConstant(MetaDuration.FromDays(30)))
                    },
                    requireAnySegment: null,
                    requireAllSegments: null),
                displayName: "Dormant Players",
                description: "Players who haven't logged in for 7-30 days");

            // Churned Players: Last login > 30 days
            yield return new PlayerSegmentInfo(
                PlayerSegmentId.FromString("ChurnedPlayers"),
                new PlayerSegmentBasicCondition(
                    propertyRequirements: new List<PlayerPropertyRequirement>
                    {
                        new PlayerPropertyRequirement(
                            new PlayerPropertyTimeSinceLastLogin(),
                            min: new MetaDurationConstant(MetaDuration.FromDays(30)),
                            max: null)
                    },
                    requireAnySegment: null,
                    requireAllSegments: null),
                displayName: "Churned Players",
                description: "Players who haven't logged in for more than 30 days");
        }

        /// <summary>
        /// Creates spending-based segments based on total IAP spend in USD.
        /// </summary>
        static IEnumerable<PlayerSegmentInfo> CreateSpendingSegments()
        {
            // Non-Spenders: $0
            yield return new PlayerSegmentInfo(
                PlayerSegmentId.FromString("NonSpenders"),
                new PlayerSegmentBasicCondition(
                    propertyRequirements: new List<PlayerPropertyRequirement>
                    {
                        new PlayerPropertyRequirement(
                            new PlayerPropertyTotalIapSpendUsd(),
                            min: null,
                            max: new F64Constant(F64.Zero))
                    },
                    requireAnySegment: null,
                    requireAllSegments: null),
                displayName: "Non-Spenders",
                description: "Players who have never made an IAP purchase");

            // Minnows: $0.01 - $9.99
            yield return new PlayerSegmentInfo(
                PlayerSegmentId.FromString("Minnows"),
                new PlayerSegmentBasicCondition(
                    propertyRequirements: new List<PlayerPropertyRequirement>
                    {
                        new PlayerPropertyRequirement(
                            new PlayerPropertyTotalIapSpendUsd(),
                            min: new F64Constant(F64.FromDouble(0.01)),
                            max: new F64Constant(F64.FromDouble(9.99)))
                    },
                    requireAnySegment: null,
                    requireAllSegments: null),
                displayName: "Minnows",
                description: "Players who have spent between $0.01 and $9.99");

            // Dolphins: $10 - $99.99
            yield return new PlayerSegmentInfo(
                PlayerSegmentId.FromString("Dolphins"),
                new PlayerSegmentBasicCondition(
                    propertyRequirements: new List<PlayerPropertyRequirement>
                    {
                        new PlayerPropertyRequirement(
                            new PlayerPropertyTotalIapSpendUsd(),
                            min: new F64Constant(F64.FromDouble(10.0)),
                            max: new F64Constant(F64.FromDouble(99.99)))
                    },
                    requireAnySegment: null,
                    requireAllSegments: null),
                displayName: "Dolphins",
                description: "Players who have spent between $10 and $99.99");

            // Whales: $100+
            yield return new PlayerSegmentInfo(
                PlayerSegmentId.FromString("Whales"),
                new PlayerSegmentBasicCondition(
                    propertyRequirements: new List<PlayerPropertyRequirement>
                    {
                        new PlayerPropertyRequirement(
                            new PlayerPropertyTotalIapSpendUsd(),
                            min: new F64Constant(F64.FromDouble(100.0)),
                            max: null)
                    },
                    requireAnySegment: null,
                    requireAllSegments: null),
                displayName: "Whales",
                description: "Players who have spent $100 or more");
        }

        /// <summary>
        /// Creates country tier segments based on PPP (Purchasing Power Parity).
        /// </summary>
        static IEnumerable<PlayerSegmentInfo> CreateCountryTierSegments()
        {
            // Tier 1: High PPP countries
            yield return new PlayerSegmentInfo(
                PlayerSegmentId.FromString("CountryTier1"),
                new PlayerSegmentBasicCondition(
                    propertyRequirements: new List<PlayerPropertyRequirement>
                    {
                        new PlayerPropertyRequirement(
                            new PlayerPropertyCountryTier(),
                            min: new LongConstant(1),
                            max: new LongConstant(1))
                    },
                    requireAnySegment: null,
                    requireAllSegments: null),
                displayName: "High PPP Countries",
                description: "Players from high purchasing power countries (US, CA, GB, DE, FR, AU, JP, etc.)");

            // Tier 2: Medium PPP countries
            yield return new PlayerSegmentInfo(
                PlayerSegmentId.FromString("CountryTier2"),
                new PlayerSegmentBasicCondition(
                    propertyRequirements: new List<PlayerPropertyRequirement>
                    {
                        new PlayerPropertyRequirement(
                            new PlayerPropertyCountryTier(),
                            min: new LongConstant(2),
                            max: new LongConstant(2))
                    },
                    requireAnySegment: null,
                    requireAllSegments: null),
                displayName: "Medium PPP Countries",
                description: "Players from medium purchasing power countries (ES, IT, KR, TW, PT, etc.)");

            // Tier 3: Low PPP countries
            yield return new PlayerSegmentInfo(
                PlayerSegmentId.FromString("CountryTier3"),
                new PlayerSegmentBasicCondition(
                    propertyRequirements: new List<PlayerPropertyRequirement>
                    {
                        new PlayerPropertyRequirement(
                            new PlayerPropertyCountryTier(),
                            min: new LongConstant(3),
                            max: new LongConstant(3))
                    },
                    requireAnySegment: null,
                    requireAllSegments: null),
                displayName: "Low PPP Countries",
                description: "Players from lower purchasing power countries (default tier)");
        }
    }

    #endregion
}
