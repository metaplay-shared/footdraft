// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Config;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// Global game configuration values (singleton settings, not item libraries). These are the designer- and
    /// LiveOps-tunable knobs for the economy and progression: match rewards, the manager-level XP curve, card
    /// upgrade costs, and starting balances. Being code-only config, they can be hot-tuned and A/B-tested.
    /// </summary>
    [MetaSerializable]
    public class GlobalConfig : GameConfigKeyValue<GlobalConfig>
    {
        // --- Starting balances (granted once on account creation) ---
        // 500 Coins ≈ two starter-tier signings on day one, so the transfer market is immediately tangible.
        [MetaMember(1)] public int StartingCoins  { get; private set; } = 500;
        [MetaMember(2)] public int StartingGems   { get; private set; } = 50;
        [MetaMember(3)] public int StartingShards { get; private set; } = 12;

        // --- Per-match rewards (win / loss) ---
        [MetaMember(10)] public int MatchWinCoins   { get; private set; } = 100;
        [MetaMember(11)] public int MatchLossCoins  { get; private set; } = 30;
        [MetaMember(12)] public int MatchWinXp      { get; private set; } = 50;
        [MetaMember(13)] public int MatchLossXp     { get; private set; } = 20;
        [MetaMember(14)] public int MatchWinShards  { get; private set; } = 2;
        [MetaMember(15)] public int MatchLossShards { get; private set; } = 1;

        // --- Manager-level XP curve ---
        [MetaMember(20)] public int MaxManagerLevel { get; private set; } = 50;
        // XP required to advance FROM level n TO level n+1 = BaseXpPerLevel + (n-1) * XpPerLevelStep.
        [MetaMember(21)] public int BaseXpPerLevel  { get; private set; } = 100;
        [MetaMember(22)] public int XpPerLevelStep  { get; private set; } = 50;

        // --- Card upgrades ---
        [MetaMember(30)] public int MaxCardUpgradeLevel    { get; private set; } = 10;
        [MetaMember(31)] public int CardUpgradeBaseCoins   { get; private set; } = 150;
        [MetaMember(32)] public int CardUpgradeCoinsStep   { get; private set; } = 150;
        [MetaMember(33)] public int CardUpgradeBaseShards  { get; private set; } = 3;
        [MetaMember(34)] public int CardUpgradeShardsStep  { get; private set; } = 2;
        // Each upgrade level adds this many rating points to a card's effective rating (→ bigger dice at thresholds).
        [MetaMember(35)] public int RatingPerUpgradeLevel  { get; private set; } = 2;

        // --- Bot opponent difficulty scaling ---
        // A bot's squad strength is scaled toward the human's, from BotBaseDifficultyPct at manager level 1
        // up to 100% at BotFullDifficultyLevel — so early matches are winnable and later ones competitive.
        [MetaMember(40)] public int BotBaseDifficultyPct  { get; private set; } = 80;
        [MetaMember(41)] public int BotFullDifficultyLevel { get; private set; } = 8;

        // --- Match Tickets (gate matchmade entry; friendlies are free) ---
        [MetaMember(50)] public int MaxMatchTickets    { get; private set; } = 10;
        [MetaMember(51)] public int TicketRegenMinutes { get; private set; } = 15;
        [MetaMember(52)] public int TicketRefillGemCost { get; private set; } = 30;

        // --- Twice-daily Cup ---
        [MetaMember(60)] public int CupWindowHours       { get; private set; } = 12;  // contiguous windows → 2 Cups/day
        [MetaMember(61)] public int CupTokensPerWin      { get; private set; } = 10;
        [MetaMember(62)] public int CupTokensPerLoss     { get; private set; } = 3;
        [MetaMember(63)] public int CupTokensPerRoundWon { get; private set; } = 2;
        // Cup milestone reward track (cumulative token thresholds within a Cup).
        [MetaMember(64)] public CupMilestone[] CupMilestones { get; private set; } = new CupMilestone[]
        {
            new CupMilestone(tokens: 10,  coins: 150, gems: 0,  shards: 3),
            new CupMilestone(tokens: 25,  coins: 300, gems: 5,  shards: 6),
            new CupMilestone(tokens: 50,  coins: 600, gems: 10, shards: 12),
            new CupMilestone(tokens: 90,  coins: 1000, gems: 20, shards: 20),
            new CupMilestone(tokens: 150, coins: 2000, gems: 40, shards: 40),
        };

        // --- Clubs & Club League ---
        [MetaMember(70)] public int MinManagerLevelForClubs { get; private set; } = 5;
        [MetaMember(71)] public int ClubLeagueWindowDays    { get; private set; } = 7;
        [MetaMember(72)] public int ClubPointsPerWin        { get; private set; } = 10;
        [MetaMember(73)] public int ClubPointsPerLoss       { get; private set; } = 3;
        [MetaMember(74)] public int ClubPointsPerRoundWon   { get; private set; } = 2;
        [MetaMember(75)] public int ClubStandingsTopN       { get; private set; } = 5;

        // --- Season, Season Pass & ranked ladder ---
        [MetaMember(80)] public int SeasonWindowDays   { get; private set; } = 30;
        [MetaMember(81)] public int PassXpPerWin       { get; private set; } = 40;
        [MetaMember(82)] public int PassXpPerLoss      { get; private set; } = 15;
        [MetaMember(83)] public int PassXpPerRoundWon  { get; private set; } = 5;
        [MetaMember(84)] public int PassXpPerTier      { get; private set; } = 100;
        [MetaMember(85)] public int PremiumPassGemCost { get; private set; } = 80;

        [MetaMember(86)] public PassReward[] PassFreeRewards { get; private set; } = new PassReward[]
        {
            new PassReward(coins: 100, gems: 0, shards: 2),
            new PassReward(coins: 150, gems: 0, shards: 3),
            new PassReward(coins: 0,   gems: 5, shards: 0),
            new PassReward(coins: 200, gems: 0, shards: 4),
            new PassReward(coins: 250, gems: 0, shards: 5),
            new PassReward(coins: 0,   gems: 8, shards: 0),
            new PassReward(coins: 300, gems: 0, shards: 6),
            new PassReward(coins: 350, gems: 0, shards: 8),
            new PassReward(coins: 0,   gems: 12, shards: 0),
            new PassReward(coins: 500, gems: 15, shards: 12),
        };
        [MetaMember(87)] public PassReward[] PassPremiumRewards { get; private set; } = new PassReward[]
        {
            new PassReward(coins: 250, gems: 10, shards: 5),
            new PassReward(coins: 300, gems: 10, shards: 6),
            new PassReward(coins: 0,   gems: 25, shards: 0),
            new PassReward(coins: 400, gems: 15, shards: 8),
            new PassReward(coins: 500, gems: 15, shards: 10),
            new PassReward(coins: 0,   gems: 35, shards: 0),
            new PassReward(coins: 600, gems: 20, shards: 12),
            new PassReward(coins: 800, gems: 25, shards: 16),
            new PassReward(coins: 0,   gems: 50, shards: 0),
            new PassReward(coins: 1500, gems: 80, shards: 40),
        };

        // Gem packs (IAP-simulated). PriceLabel is display only; no real money in the demo.
        [MetaMember(88)] public ShopProduct[] ShopProducts { get; private set; } = new ShopProduct[]
        {
            new ShopProduct("gems_s",  80,   "€1.99"),
            new ShopProduct("gems_m",  250,  "€4.99"),
            new ShopProduct("gems_l",  650,  "€9.99"),
            new ShopProduct("gems_xl", 1400, "€19.99"),
        };

        [MetaMember(90)] public int RankWinPoints   { get; private set; } = 30;
        [MetaMember(91)] public int RankLossPoints  { get; private set; } = 12; // subtracted, floored at 0
        [MetaMember(92)] public int RankRoundBonus  { get; private set; } = 3;
        [MetaMember(93)] public RankDivision[] RankDivisions { get; private set; } = new RankDivision[]
        {
            new RankDivision("Bronze",   0,    "🥉"),
            new RankDivision("Silver",   100,  "🥈"),
            new RankDivision("Gold",     250,  "🥇"),
            new RankDivision("Platinum", 500,  "💠"),
            new RankDivision("Diamond",  900,  "💎"),
            new RankDivision("Champion", 1500, "👑"),
        };

        // --- Weekly marquee Bracket Cup ---
        [MetaMember(100)] public int BracketWindowDays              { get; private set; } = 7;
        [MetaMember(101)] public int BracketOpponentBaseStrength    { get; private set; } = 34;
        [MetaMember(102)] public int BracketOpponentStrengthPerRound { get; private set; } = 4;
        // Reward granted for winning each round (index 0 = Round of 16 win … 3 = Final win / champion).
        [MetaMember(103)] public BracketRoundReward[] BracketRoundRewards { get; private set; } = new BracketRoundReward[]
        {
            new BracketRoundReward(coins: 300,  gems: 5,  shards: 5),
            new BracketRoundReward(coins: 600,  gems: 10, shards: 10),
            new BracketRoundReward(coins: 1200, gems: 20, shards: 20),
            new BracketRoundReward(coins: 2500, gems: 50, shards: 40),
        };

        // --- Daily-login streak (WS4 retention) ---
        [MetaMember(110)] public int DailyStreakBaseCoins    { get; private set; } = 50;
        [MetaMember(111)] public int DailyStreakBonusPerDay  { get; private set; } = 15;
        [MetaMember(112)] public int DailyStreakMaxBonusDays { get; private set; } = 7;

        // --- Interconnected meta economy ---
        /// <summary> Gems to swap one unclaimed daily quest for the next unused one. </summary>
        [MetaMember(113)] public int QuestRerollGemCost { get; private set; } = 5;
        // Coin packs: Gems → Coins (transfer money). Rate improves with size (25 → ~36 coins/gem).
        [MetaMember(114)] public CoinProduct[] CoinProducts { get; private set; } = new CoinProduct[]
        {
            new CoinProduct("coins_s", 500,  20),
            new CoinProduct("coins_m", 1500, 50),
            new CoinProduct("coins_l", 4000, 110),
        };
        /// <summary> How many daily quests are active at once (the first N daily-scope quests in config order). </summary>
        [MetaMember(115)] public int DailyQuestSlots { get; private set; } = 3;

        public GlobalConfig() { }

        /// <summary> Coins rewarded for a login at the given streak length (day 1 = base; grows per day, capped). </summary>
        public long DailyStreakReward(int streak)
        {
            int bonusDays = streak - 1;
            if (bonusDays < 0) bonusDays = 0;
            if (bonusDays > DailyStreakMaxBonusDays) bonusDays = DailyStreakMaxBonusDays;
            return DailyStreakBaseCoins + (long)bonusDays * DailyStreakBonusPerDay;
        }

        /// <summary> Number of Season Pass tiers fully earned at the given pass XP (capped at the track length). </summary>
        public int PassTier(long passXp)
        {
            if (PassXpPerTier <= 0)
                return 0;
            long tier = passXp / PassXpPerTier;
            int max = PassFreeRewards.Length;
            return tier > max ? max : (int)tier;
        }

        /// <summary> Index into <see cref="RankDivisions"/> for the given Season Rank Points. </summary>
        public int DivisionIndex(int points)
        {
            int idx = 0;
            for (int i = 0; i < RankDivisions.Length; i++)
                if (points >= RankDivisions[i].MinPoints)
                    idx = i;
            return idx;
        }

        /// <summary> Target bot difficulty (percent of the human's squad strength) at a given manager level. </summary>
        public int BotDifficultyPct(int managerLevel)
        {
            if (managerLevel >= BotFullDifficultyLevel)
                return 100;
            if (BotFullDifficultyLevel <= 1)
                return 100;
            return BotBaseDifficultyPct + (100 - BotBaseDifficultyPct) * (managerLevel - 1) / (BotFullDifficultyLevel - 1);
        }

        /// <summary> XP required to advance from <paramref name="currentLevel"/> to the next level. </summary>
        public long XpToReachNextLevel(int currentLevel) => BaseXpPerLevel + (long)(currentLevel - 1) * XpPerLevelStep;

        /// <summary> Coin cost to take a card from <paramref name="currentLevel"/> to the next. </summary>
        public long CardUpgradeCoinCost(int currentLevel) => CardUpgradeBaseCoins + (long)currentLevel * CardUpgradeCoinsStep;

        /// <summary> Shard cost to take a card from <paramref name="currentLevel"/> to the next. </summary>
        public long CardUpgradeShardCost(int currentLevel) => CardUpgradeBaseShards + (long)currentLevel * CardUpgradeShardsStep;
    }
}
