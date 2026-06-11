// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using System.Collections.Generic;

namespace Game.Logic
{
    /// <summary>
    /// Identifier for country tiers based on PPP (Purchasing Power Parity).
    /// </summary>
    [MetaSerializable]
    public class CountryTierId : StringId<CountryTierId> { }

    /// <summary>
    /// Defines a country tier with its associated country codes.
    /// </summary>
    [MetaSerializable]
    public class CountryTierInfo : IGameConfigData<CountryTierId>
    {
        [MetaMember(1)] public CountryTierId            TierId          { get; private set; }
        [MetaMember(2)] public string                   DisplayName     { get; private set; }
        [MetaMember(3)] public int                      TierNumber      { get; private set; }
        [MetaMember(4)] public OrderedSet<string>       CountryCodes    { get; private set; }

        public CountryTierId ConfigKey => TierId;

        public CountryTierInfo() { }
        public CountryTierInfo(CountryTierId tierId, string displayName, int tierNumber, IEnumerable<string> countryCodes)
        {
            TierId = tierId;
            DisplayName = displayName;
            TierNumber = tierNumber;
            CountryCodes = new OrderedSet<string>(countryCodes);
        }

        /// <summary>
        /// Checks if the given country code belongs to this tier.
        /// </summary>
        public bool ContainsCountry(string countryCode)
        {
            return CountryCodes.Contains(countryCode?.ToUpperInvariant());
        }
    }

    /// <summary>
    /// Static content for country tier definitions.
    /// </summary>
    public static class CountryTierContent
    {
        /// <summary>
        /// High PPP countries (Tier 1) - typically highest pricing.
        /// </summary>
        static readonly string[] Tier1Countries = new[]
        {
            "US", "CA", "GB", "DE", "FR", "AU", "JP", "CH", "NO", "SE",
            "DK", "NL", "AT", "BE", "IE", "FI", "NZ", "SG"
        };

        /// <summary>
        /// Medium PPP countries (Tier 2) - mid-range pricing.
        /// </summary>
        static readonly string[] Tier2Countries = new[]
        {
            "ES", "IT", "KR", "TW", "PT", "CZ", "PL", "GR", "HU", "SA",
            "AE", "IL", "CL", "MY", "MX"
        };

        // Note: Tier 3 (Low PPP) is the default for all other countries

        /// <summary>
        /// Creates the country tiers library.
        /// </summary>
        public static GameConfigLibrary<CountryTierId, CountryTierInfo> CreateCountryTiersLibrary()
        {
            CountryTierInfo[] tiers = new[]
            {
                new CountryTierInfo(
                    CountryTierId.FromString("Tier1"),
                    "High PPP Countries",
                    tierNumber: 1,
                    Tier1Countries),
                new CountryTierInfo(
                    CountryTierId.FromString("Tier2"),
                    "Medium PPP Countries",
                    tierNumber: 2,
                    Tier2Countries),
                new CountryTierInfo(
                    CountryTierId.FromString("Tier3"),
                    "Low PPP Countries",
                    tierNumber: 3,
                    System.Array.Empty<string>()), // Default tier - no explicit country list
            };

            return GameConfigLibrary<CountryTierId, CountryTierInfo>.CreateSolo(tiers);
        }

        /// <summary>
        /// Gets the tier number for a given country code (1, 2, or 3).
        /// Returns 3 (Low PPP) for unknown or null countries.
        /// </summary>
        public static int GetTierForCountry(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode))
                return 3;

            string upperCode = countryCode.ToUpperInvariant();

            foreach (string code in Tier1Countries)
            {
                if (code == upperCode)
                    return 1;
            }

            foreach (string code in Tier2Countries)
            {
                if (code == upperCode)
                    return 2;
            }

            return 3; // Default to Tier 3 (Low PPP)
        }
    }
}
