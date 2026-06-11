// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Player;

namespace Game.Logic
{
    /// <summary>
    /// Custom config parser provider for game-specific player property types.
    /// </summary>
    public class GameConfigParsers : ConfigParserProvider
    {
        public override void RegisterParsers(ConfigParser parser)
        {
            parser.RegisterCustomParseFunc<PlayerPropertyId>(ParsePlayerPropertyId);
            parser.RegisterCustomParseFunc<FormationId>(ParseFormationId);
        }

        /// <summary>
        /// Formation ids like "4-3-3" start with a digit, which the SDK's default StringId config token
        /// (identifier-style: must start with a letter/underscore) rejects. Same charset, digit start allowed.
        /// </summary>
        static readonly ConfigLexer.CustomTokenSpec FormationIdToken = new ConfigLexer.CustomTokenSpec(@"[a-zA-Z0-9_][a-zA-Z0-9_\-.]*", name: "FormationId");

        static FormationId ParseFormationId(ConfigParser parser, ConfigLexer lexer)
            => FormationId.FromString(lexer.ParseCustomToken(FormationIdToken));

        /// <summary>
        /// Parses player property identifiers from config.
        /// </summary>
        static PlayerPropertyId ParsePlayerPropertyId(ConfigParser parser, ConfigLexer lexer)
        {
            // Try SDK-provided properties first
            if (ConfigParser.TryParseCorePlayerPropertyId(lexer, out PlayerPropertyId propertyId))
                return propertyId;

            // Parse game-specific properties
            string type = lexer.ParseIdentifier();

            switch (type)
            {
                case "TimeSinceLastLogin":
                    return new PlayerPropertyTimeSinceLastLogin();

                case "TotalIapSpendUsd":
                    return new PlayerPropertyTotalIapSpendUsd();

                case "CountryTier":
                    return new PlayerPropertyCountryTier();

                case "LastKnownCountry":
                    return new PlayerPropertyLastKnownCountry();
            }

            throw new ParseError($"Unknown PlayerPropertyId: {type}");
        }
    }
}
