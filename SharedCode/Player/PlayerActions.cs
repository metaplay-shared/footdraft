// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System.Collections.Generic;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Player;

namespace Game.Logic
{
    /// <summary>
    /// Game-specific player action class, which attaches all game-specific actions to <see cref="PlayerModel"/>.
    /// </summary>
    public abstract class PlayerAction : PlayerActionCore<PlayerModel>
    {
    }

    /// <summary>
    /// Game-specific <see cref="PlayerUnsynchronizedServerActionCore{TModel}"/>
    /// </summary>
    public abstract class PlayerUnsynchronizedServerAction : PlayerUnsynchronizedServerActionCore<PlayerModel>
    {
    }

    /// <summary>
    /// Game-specific <see cref="PlayerSynchronizedServerActionCore{TModel}"/>
    /// </summary>
    public abstract class PlayerSynchronizedServerAction : PlayerSynchronizedServerActionCore<PlayerModel>
    {
    }

    /// <summary>
    /// Registry for game-specific ActionCodes, used by the individual PlayerAction classes. Action codes are
    /// global across the game; match/lobby actions live in <see cref="MatchActionCodes"/>.
    /// </summary>
    public static class ActionCodes
    {
        public const int PlayerFindMatch         = 5010;
        public const int PlayerLeaveMatch        = 5011;
        public const int PlayerStartMatchNow     = 5012;
        public const int PlayerSelectTeam        = 5013;
        public const int PlayerUpgradeCard       = 5014;
        public const int PlayerGrantMatchRewards = 5015;
        public const int PlayerGrantCurrencyDebug = 5016;
        public const int PlayerCreateFriendly    = 5017;
        public const int PlayerJoinFriendly      = 5018;
        public const int PlayerCancelFriendly    = 5019;
        public const int PlayerClaimCupMilestone = 5020;
        public const int PlayerRefillTickets     = 5021;
        public const int PlayerGrantTicketsDebug = 5022;
        public const int PlayerJoinClub          = 5023;
        public const int PlayerLeaveClub         = 5024;
        public const int PlayerRefreshClub       = 5025;
        public const int PlayerSetClubMembership = 5026;
        public const int PlayerSetClubSnapshot   = 5027;
        public const int PlayerClaimPassReward   = 5028;
        public const int PlayerBuyPremiumPass    = 5029;
        public const int PlayerSimPurchase       = 5030;
        public const int PlayerApplyFormDebug    = 5031;
        public const int PlayerClearFormDebug    = 5032;
        public const int PlayerRefreshForm       = 5033;
        public const int PlayerSetFormSnapshot   = 5034;
        public const int PlayerEnterBracket      = 5035;
        public const int PlayerPlayBracketRound  = 5036;
        public const int PlayerBuyCosmetic       = 5037;
        public const int PlayerEquipCosmetic     = 5038;
        // FOOTDRAFT spin-draft (P1).
        public const int PlayerChooseFormation   = 5039;
        public const int PlayerSpinForSlot       = 5040;
        public const int PlayerSetSpinOffer      = 5041;
        public const int PlayerPickFromOffer     = 5042;
        public const int PlayerResetDraft        = 5043;
        // FOOTDRAFT season league (P4).
        public const int PlayerCreateLeague      = 5044;
        public const int PlayerJoinLeague        = 5045;
        public const int PlayerLeaveLeague       = 5046;
        public const int PlayerStartLeagueSeason = 5047;
        public const int PlayerPlayLeagueFixture = 5048;
        public const int PlayerRefreshLeague     = 5049;
        public const int PlayerSetLeagueState    = 5050;
        public const int PlayerSetLeagueFormation = 5051;
        public const int PlayerLeagueDraftPick    = 5052;
        public const int PlayerLeagueAutoPick     = 5053;
        public const int PlayerSimulateLeague     = 5054;
        public const int PlayerLeagueSpin         = 5055;
        // FOOTDRAFT dashboard admin action (WS5c): grant currency from the LiveOps Dashboard (works in all envs).
        public const int PlayerAdminGrantCurrency = 5056;
        // FOOTDRAFT retention (WS4): server-issued daily-login streak update.
        public const int PlayerUpdateLoginStreak  = 5057;
        // FOOTDRAFT retention (WS4): claim a completed daily quest's reward.
        public const int PlayerClaimQuest         = 5058;
        // FOOTDRAFT transfer window (WS3): drop one drafted legend for another during an open window.
        public const int PlayerLeagueTransferSwap = 5059;
        // FOOTDRAFT single player: create a solo season (no lobby; CPU teams fill the league).
        public const int PlayerCreateSoloLeague   = 5060;
        // FOOTDRAFT wallet economy: transfers/spins charge the wallet up-front; the server refunds if the league rejects.
        public const int PlayerRefundLeagueCharge = 5061;
        public const int PlayerRecordTransferMade = 5062;
        // FOOTDRAFT quests: swap one unclaimed daily quest for the next unused one (costs Gems).
        public const int PlayerRerollQuest        = 5063;
        // FOOTDRAFT shop: exchange Gems for a Coins pack.
        public const int PlayerBuyCoinPack        = 5064;
        // FOOTDRAFT Draft Cup (FUT-Draft-style paid mode): enter (pay + reset draft) + play a knockout round.
        public const int PlayerEnterDraftCup      = 5065;
        public const int PlayerPlayDraftCupRound  = 5066;
        // FOOTDRAFT World Cup 2026 (draft a one-off XI from real WC squads → knockout vs real nations).
        public const int PlayerEnterWorldCup      = 5067;
        public const int PlayerPlayWorldCupRound  = 5068;
        // FOOTDRAFT Scout Packs (FUT-style pack opening: currency + cosmetic/star pulls).
        public const int PlayerOpenPack           = 5069;
        // FOOTDRAFT Objectives (claimable career-milestone reward track).
        public const int PlayerClaimObjective     = 5070;
        // FOOTDRAFT Featured Offers (value bundles; sim-IAP).
        public const int PlayerBuyBundle          = 5071;
        // FOOTDRAFT World Cup leaderboard (refresh the cached board; server-set cache).
        public const int PlayerRefreshWorldCupLeaderboard = 5072;
        public const int PlayerSetWcLeaderboard           = 5073;
        // FOOTDRAFT P2P trades (propose a player+cash trade; respond/cancel).
        public const int PlayerLeagueProposeTrade = 5074;
        public const int PlayerLeagueRespondTrade = 5075;
        // FOOTDRAFT Scout-Pack draft boosts: server-issued elite-spin consumption.
        public const int PlayerConsumeEliteSpin   = 5076;
    }

    /// <summary> A club name: 2–20 characters, letters/digits/space, trimmed. </summary>
    public static class ClubName
    {
        public static bool IsValid(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            string trimmed = name.Trim();
            if (trimmed.Length < 2 || trimmed.Length > 20)
                return false;
            foreach (char c in trimmed)
            {
                bool ok = char.IsLetterOrDigit(c) || c == ' ';
                if (!ok)
                    return false;
            }
            return true;
        }
    }

    /// <summary> A private-match "room code": 4–8 uppercase alphanumerics, shared between two friends. </summary>
    public static class FriendlyCode
    {
        public static bool IsValid(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length < 4 || code.Length > 8)
                return false;
            foreach (char c in code)
            {
                bool ok = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
                if (!ok)
                    return false;
            }
            return true;
        }
    }

    /// <summary> Game-specific results returned from player actions. </summary>
    public static class PlayerActionResults
    {
        public static readonly MetaActionResult UnknownTeam         = new MetaActionResult(nameof(UnknownTeam));
        public static readonly MetaActionResult UnknownCard         = new MetaActionResult(nameof(UnknownCard));
        public static readonly MetaActionResult CardAtMaxLevel      = new MetaActionResult(nameof(CardAtMaxLevel));
        public static readonly MetaActionResult NotEnoughCurrency   = new MetaActionResult(nameof(NotEnoughCurrency));
        public static readonly MetaActionResult InvalidAmount       = new MetaActionResult(nameof(InvalidAmount));
        public static readonly MetaActionResult InvalidFriendlyCode = new MetaActionResult(nameof(InvalidFriendlyCode));
        public static readonly MetaActionResult AlreadyInMatch      = new MetaActionResult(nameof(AlreadyInMatch));
        public static readonly MetaActionResult NoTicketsLeft       = new MetaActionResult(nameof(NoTicketsLeft));
        public static readonly MetaActionResult NoMilestoneToClaim  = new MetaActionResult(nameof(NoMilestoneToClaim));
        public static readonly MetaActionResult MilestoneLocked     = new MetaActionResult(nameof(MilestoneLocked));
        public static readonly MetaActionResult TicketsAlreadyFull  = new MetaActionResult(nameof(TicketsAlreadyFull));
        public static readonly MetaActionResult InvalidClubName     = new MetaActionResult(nameof(InvalidClubName));
        public static readonly MetaActionResult ClubLevelLocked     = new MetaActionResult(nameof(ClubLevelLocked));
        public static readonly MetaActionResult NotInClub           = new MetaActionResult(nameof(NotInClub));
        public static readonly MetaActionResult NoPassRewardToClaim = new MetaActionResult(nameof(NoPassRewardToClaim));
        public static readonly MetaActionResult PassTierNotReached  = new MetaActionResult(nameof(PassTierNotReached));
        public static readonly MetaActionResult PremiumNotOwned     = new MetaActionResult(nameof(PremiumNotOwned));
        public static readonly MetaActionResult PremiumAlreadyOwned = new MetaActionResult(nameof(PremiumAlreadyOwned));
        public static readonly MetaActionResult UnknownProduct      = new MetaActionResult(nameof(UnknownProduct));
        public static readonly MetaActionResult BracketInProgress   = new MetaActionResult(nameof(BracketInProgress));
        public static readonly MetaActionResult BracketNotActive    = new MetaActionResult(nameof(BracketNotActive));
        public static readonly MetaActionResult DraftCupBusy        = new MetaActionResult(nameof(DraftCupBusy));
        public static readonly MetaActionResult DraftCupNotActive   = new MetaActionResult(nameof(DraftCupNotActive));
        public static readonly MetaActionResult WorldCupBusy        = new MetaActionResult(nameof(WorldCupBusy));
        public static readonly MetaActionResult WorldCupNotActive   = new MetaActionResult(nameof(WorldCupNotActive));
        public static readonly MetaActionResult WorldCupUnavailable = new MetaActionResult(nameof(WorldCupUnavailable));
        public static readonly MetaActionResult PackAlreadyClaimedToday = new MetaActionResult(nameof(PackAlreadyClaimedToday));
        public static readonly MetaActionResult UnknownObjective        = new MetaActionResult(nameof(UnknownObjective));
        public static readonly MetaActionResult ObjectiveNotComplete    = new MetaActionResult(nameof(ObjectiveNotComplete));
        public static readonly MetaActionResult ObjectiveAlreadyClaimed = new MetaActionResult(nameof(ObjectiveAlreadyClaimed));
        public static readonly MetaActionResult BundleAlreadyOwned      = new MetaActionResult(nameof(BundleAlreadyOwned));
        public static readonly MetaActionResult DraftNotComplete    = new MetaActionResult(nameof(DraftNotComplete));
        public static readonly MetaActionResult UnknownCosmetic     = new MetaActionResult(nameof(UnknownCosmetic));
        public static readonly MetaActionResult CosmeticNotOwned    = new MetaActionResult(nameof(CosmeticNotOwned));
        public static readonly MetaActionResult CosmeticAlreadyOwned = new MetaActionResult(nameof(CosmeticAlreadyOwned));
        // FOOTDRAFT daily quests (WS4).
        public static readonly MetaActionResult UnknownQuest        = new MetaActionResult(nameof(UnknownQuest));
        public static readonly MetaActionResult QuestNotComplete    = new MetaActionResult(nameof(QuestNotComplete));
        public static readonly MetaActionResult QuestAlreadyClaimed = new MetaActionResult(nameof(QuestAlreadyClaimed));
        // FOOTDRAFT spin-draft (P1).
        public static readonly MetaActionResult UnknownFormation    = new MetaActionResult(nameof(UnknownFormation));
        public static readonly MetaActionResult NoFormationChosen   = new MetaActionResult(nameof(NoFormationChosen));
        public static readonly MetaActionResult SlotOutOfRange      = new MetaActionResult(nameof(SlotOutOfRange));
        public static readonly MetaActionResult SlotAlreadyFilled   = new MetaActionResult(nameof(SlotAlreadyFilled));
        public static readonly MetaActionResult OfferAlreadyPending = new MetaActionResult(nameof(OfferAlreadyPending));
        public static readonly MetaActionResult NoSpinPending       = new MetaActionResult(nameof(NoSpinPending));
        public static readonly MetaActionResult NoRerollsLeft       = new MetaActionResult(nameof(NoRerollsLeft));
        public static readonly MetaActionResult LegendNotInOffer    = new MetaActionResult(nameof(LegendNotInOffer));
        public static readonly MetaActionResult WrongPositionForSlot = new MetaActionResult(nameof(WrongPositionForSlot));
        public static readonly MetaActionResult LegendAlreadyDrafted = new MetaActionResult(nameof(LegendAlreadyDrafted));
        public static readonly MetaActionResult DraftLocked         = new MetaActionResult(nameof(DraftLocked));
        // FOOTDRAFT season league (P4).
        public static readonly MetaActionResult InvalidLeagueCode   = new MetaActionResult(nameof(InvalidLeagueCode));
        public static readonly MetaActionResult AlreadyInLeague     = new MetaActionResult(nameof(AlreadyInLeague));
        public static readonly MetaActionResult NotInLeague         = new MetaActionResult(nameof(NotInLeague));
        // FOOTDRAFT wallet transfer economy.
        public static readonly MetaActionResult UnknownPlayer       = new MetaActionResult(nameof(UnknownPlayer));
        public static readonly MetaActionResult NotMarqueeEligible  = new MetaActionResult(nameof(NotMarqueeEligible));
        public static readonly MetaActionResult NoRerollAvailable   = new MetaActionResult(nameof(NoRerollAvailable));
    }

    /// <summary>
    /// Requests that the player be added to the matchmaking queue. The actual queueing is performed by the
    /// server-side PlayerActor (which talks to the lobby entity) via the server listener.
    /// </summary>
    [ModelAction(ActionCodes.PlayerFindMatch)]
    public class PlayerFindMatch : PlayerAction
    {
        public PlayerFindMatch() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            // Matchmade entry costs one Match Ticket (friendlies are free).
            GlobalConfig global = player.GameConfig.Global;
            if (player.Tickets.Available(player.CurrentTime, global) <= 0)
                return PlayerActionResults.NoTicketsLeft;

            if (commit)
            {
                if (!player.Tickets.TrySpend(player.CurrentTime, global))
                    return PlayerActionResults.NoTicketsLeft;
                player.ServerListener.FindMatch();
            }

            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Requests that the player leave their current match and return to the lobby.
    /// </summary>
    [ModelAction(ActionCodes.PlayerLeaveMatch)]
    public class PlayerLeaveMatch : PlayerAction
    {
        public PlayerLeaveMatch() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
                player.ServerListener.LeaveMatch();

            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Requests that the lobby start a match immediately (regardless of the queued player count), padding
    /// with bots as needed. Only meaningful while the player is queued.
    /// </summary>
    [ModelAction(ActionCodes.PlayerStartMatchNow)]
    public class PlayerStartMatchNow : PlayerAction
    {
        public PlayerStartMatchNow() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
                player.ServerListener.StartMatchNow();

            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Sets the national team the manager fields. Validated against the configured teams. The selected team's
    /// squad (with the manager's card upgrades applied) is what the server brings into the next match.
    /// </summary>
    [ModelAction(ActionCodes.PlayerSelectTeam)]
    public class PlayerSelectTeam : PlayerAction
    {
        public string NewTeamId { get; private set; }

        PlayerSelectTeam() { }
        public PlayerSelectTeam(string newTeamId) { NewTeamId = newTeamId; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.GameConfig.Teams.ContainsKey(TeamId.FromString(NewTeamId)))
                return PlayerActionResults.UnknownTeam;

            if (commit)
                player.SelectedTeamId = NewTeamId;

            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Upgrades one squad card (a team slot), spending Coins + Shards looked up from config. Transactional: the
    /// cost is deducted and the upgrade applied in the same step, and the costs come from the server-authoritative
    /// config (never from client parameters).
    /// </summary>
    [ModelAction(ActionCodes.PlayerUpgradeCard)]
    public class PlayerUpgradeCard : PlayerAction
    {
        public string CardTeamId { get; private set; }
        public int    Slot       { get; private set; }

        PlayerUpgradeCard() { }
        public PlayerUpgradeCard(string cardTeamId, int slot)
        {
            CardTeamId = cardTeamId;
            Slot       = slot;
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            GlobalConfig global = player.GameConfig.Global;

            if (!player.GameConfig.Teams.TryGetValue(TeamId.FromString(CardTeamId), out TeamInfo team))
                return PlayerActionResults.UnknownTeam;
            if (Slot < 0 || Slot >= team.Squad.Count)
                return PlayerActionResults.UnknownCard;

            string cardKey      = CardKeys.For(CardTeamId, Slot);
            int    currentLevel = player.SquadBook.UpgradeLevelOf(cardKey);
            if (currentLevel >= global.MaxCardUpgradeLevel)
                return PlayerActionResults.CardAtMaxLevel;

            long coinCost  = global.CardUpgradeCoinCost(currentLevel);
            long shardCost = global.CardUpgradeShardCost(currentLevel);
            if (!player.Wallet.CanAfford(CurrencyType.Coins, coinCost) ||
                !player.Wallet.CanAfford(CurrencyType.Shards, shardCost))
                return PlayerActionResults.NotEnoughCurrency;

            if (commit)
            {
                player.Wallet.TrySpend(CurrencyType.Coins, coinCost);
                player.Wallet.TrySpend(CurrencyType.Shards, shardCost);
                player.SquadBook.SetUpgradeLevel(cardKey, currentLevel + 1);
                player.ClientListener.CardUpgraded(cardKey, currentLevel + 1);
            }

            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Server-issued reward grant for a finished match. Enqueued by the PlayerActor when the match actor reports
    /// the result, so it works whether or not the player is currently connected. The reward amounts come from
    /// config — the action only carries whether the manager won. Synchronized so it can safely mutate the
    /// checksummed wallet/progression state.
    /// </summary>
    [ModelAction(ActionCodes.PlayerGrantMatchRewards)]
    public class PlayerGrantMatchRewards : PlayerSynchronizedServerAction
    {
        public bool Won       { get; private set; }
        public int  RoundsWon { get; private set; }

        PlayerGrantMatchRewards() { }
        public PlayerGrantMatchRewards(bool won, int roundsWon)
        {
            Won       = won;
            RoundsWon = roundsWon;
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
            {
                GlobalConfig global = player.GameConfig.Global;
                int coins  = (Won ? global.MatchWinCoins : global.MatchLossCoins) * LiveOpsBoosts.CoinMultiplier(player);
                int xp     = Won ? global.MatchWinXp     : global.MatchLossXp;
                int shards = Won ? global.MatchWinShards : global.MatchLossShards;

                player.Wallet.Earn(CurrencyType.Coins, coins);
                player.Wallet.Earn(CurrencyType.Shards, shards);
                player.GrantXp(xp, global);
                player.RecordMatchResult(Won);

                // The match also feeds the live Cup: base tokens for the result + a per-round-won bonus.
                int cupTokens = (Won ? global.CupTokensPerWin : global.CupTokensPerLoss) + RoundsWon * global.CupTokensPerRoundWon;
                player.GrantCupTokens(cupTokens, player.CurrentTime);

                // ...and the player's Club League contribution (the PlayerActor reports it to the ClubsActor).
                int clubPoints = (Won ? global.ClubPointsPerWin : global.ClubPointsPerLoss) + RoundsWon * global.ClubPointsPerRoundWon;
                player.AccrueClubPoints(clubPoints, player.CurrentTime);

                // ...and Season Pass XP + ranked-ladder points.
                int passXp = (Won ? global.PassXpPerWin : global.PassXpPerLoss) + RoundsWon * global.PassXpPerRoundWon;
                player.GrantPassXp(passXp, player.CurrentTime);
                int rankDelta = Won ? (global.RankWinPoints + RoundsWon * global.RankRoundBonus) : -global.RankLossPoints;
                player.GrantSeasonRankPoints(rankDelta, player.CurrentTime);

                // ...and quest progress (WS4): every match counts; wins also count toward win quests. For league
                // fixtures RoundsWon carries the GOALS the player's XI scored (see LeagueActor.GrantFixtureRewards),
                // so it doubles as the GoalsScored metric. Daily quests roll per UTC day; season quests accumulate
                // over the league season (keyed to the league code).
                string leagueCode = player.League.Code;
                QuestEngine.Advance(player.Quests, player.GameConfig.Quests.Values, QuestMetric.MatchesPlayed, 1, player.CurrentTime, leagueCode);
                if (Won)
                    QuestEngine.Advance(player.Quests, player.GameConfig.Quests.Values, QuestMetric.MatchesWon, 1, player.CurrentTime, leagueCode);
                QuestEngine.Advance(player.Quests, player.GameConfig.Quests.Values, QuestMetric.GoalsScored, RoundsWon, player.CurrentTime, leagueCode);

                player.ClientListener.MatchRewardsGranted(Won, coins, xp, shards, cupTokens);
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> Open a private 1v1 room under a shared code, so a friend can join with the same code. </summary>
    [ModelAction(ActionCodes.PlayerCreateFriendly)]
    public class PlayerCreateFriendly : PlayerAction
    {
        public string Code { get; private set; }

        PlayerCreateFriendly() { }
        public PlayerCreateFriendly(string code) { Code = code; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!FriendlyCode.IsValid(Code))
                return PlayerActionResults.InvalidFriendlyCode;
            if (player.CurrentMatch != EntityId.None)
                return PlayerActionResults.AlreadyInMatch;

            if (commit)
                player.ServerListener.CreateFriendly(Code);
            return MetaActionResult.Success;
        }
    }

    /// <summary> Join a friend's private room by its shared code (forms the 1v1 match when both are present). </summary>
    [ModelAction(ActionCodes.PlayerJoinFriendly)]
    public class PlayerJoinFriendly : PlayerAction
    {
        public string Code { get; private set; }

        PlayerJoinFriendly() { }
        public PlayerJoinFriendly(string code) { Code = code; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!FriendlyCode.IsValid(Code))
                return PlayerActionResults.InvalidFriendlyCode;
            if (player.CurrentMatch != EntityId.None)
                return PlayerActionResults.AlreadyInMatch;

            if (commit)
                player.ServerListener.JoinFriendly(Code);
            return MetaActionResult.Success;
        }
    }

    /// <summary> Close any private room this player created (e.g. they cancelled while waiting). </summary>
    [ModelAction(ActionCodes.PlayerCancelFriendly)]
    public class PlayerCancelFriendly : PlayerAction
    {
        public PlayerCancelFriendly() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
                player.ServerListener.CancelFriendly();
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Claims the next unclaimed Cup milestone if its token threshold is reached, granting its reward. Costs
    /// nothing — it's the payout for Cup Tokens already earned.
    /// </summary>
    [ModelAction(ActionCodes.PlayerClaimCupMilestone)]
    public class PlayerClaimCupMilestone : PlayerAction
    {
        public PlayerClaimCupMilestone() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            GlobalConfig global = player.GameConfig.Global;
            long cupId   = CupSchedule.CurrentCupId(player.CurrentTime, global);
            bool current = player.Cup.CupId == cupId;
            int  claimed = current ? player.Cup.ClaimedMilestones : 0;
            int  tokens  = current ? player.Cup.Tokens : 0;

            if (claimed >= global.CupMilestones.Length)
                return PlayerActionResults.NoMilestoneToClaim;
            if (tokens < global.CupMilestones[claimed].Tokens)
                return PlayerActionResults.MilestoneLocked;

            if (commit)
            {
                player.SyncCup(player.CurrentTime);
                if (player.Cup.ClaimedMilestones >= global.CupMilestones.Length)
                    return PlayerActionResults.NoMilestoneToClaim;
                CupMilestone milestone = global.CupMilestones[player.Cup.ClaimedMilestones];
                if (player.Cup.Tokens < milestone.Tokens)
                    return PlayerActionResults.MilestoneLocked;

                player.Wallet.Earn(CurrencyType.Coins, milestone.Coins);
                player.Wallet.Earn(CurrencyType.Gems, milestone.Gems);
                player.Wallet.Earn(CurrencyType.Shards, milestone.Shards);
                player.Cup.ClaimedMilestones++;
                player.ClientListener.CupMilestoneClaimed(player.Cup.ClaimedMilestones);
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> Refill Match Tickets to full for Gems (a convenience sink). </summary>
    [ModelAction(ActionCodes.PlayerRefillTickets)]
    public class PlayerRefillTickets : PlayerAction
    {
        public PlayerRefillTickets() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            GlobalConfig global = player.GameConfig.Global;
            if (player.Tickets.Available(player.CurrentTime, global) >= global.MaxMatchTickets)
                return PlayerActionResults.TicketsAlreadyFull;
            if (!player.Wallet.CanAfford(CurrencyType.Gems, global.TicketRefillGemCost))
                return PlayerActionResults.NotEnoughCurrency;

            if (commit)
            {
                player.Wallet.TrySpend(CurrencyType.Gems, global.TicketRefillGemCost);
                player.Tickets.Refill(player.CurrentTime, global);
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> Development-only: refill Match Tickets for local testing. </summary>
    [ModelAction(ActionCodes.PlayerGrantTicketsDebug)]
    [DevelopmentOnlyAction]
    public class PlayerGrantTicketsDebug : PlayerAction
    {
        public PlayerGrantTicketsDebug() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
                player.Tickets.Refill(player.CurrentTime, player.GameConfig.Global);
            return MetaActionResult.Success;
        }
    }

    /// <summary> Join (or create, if new) the club with the given name. Unlocks at a configured manager level. </summary>
    [ModelAction(ActionCodes.PlayerJoinClub)]
    public class PlayerJoinClub : PlayerAction
    {
        public string Name { get; private set; }

        PlayerJoinClub() { }
        public PlayerJoinClub(string name) { Name = name; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (player.PlayerLevel < player.GameConfig.Global.MinManagerLevelForClubs)
                return PlayerActionResults.ClubLevelLocked;
            if (!ClubName.IsValid(Name))
                return PlayerActionResults.InvalidClubName;

            if (commit)
                player.ServerListener.JoinClub(Name.Trim());
            return MetaActionResult.Success;
        }
    }

    /// <summary> Leave the current club. </summary>
    [ModelAction(ActionCodes.PlayerLeaveClub)]
    public class PlayerLeaveClub : PlayerAction
    {
        public PlayerLeaveClub() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.Club.InClub)
                return PlayerActionResults.NotInClub;
            if (commit)
                player.ServerListener.LeaveClub();
            return MetaActionResult.Success;
        }
    }

    /// <summary> Refresh the cached Club League standings (the server fetches them from the ClubsActor). </summary>
    [ModelAction(ActionCodes.PlayerRefreshClub)]
    public class PlayerRefreshClub : PlayerAction
    {
        public PlayerRefreshClub() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
                player.ServerListener.RefreshClub();
            return MetaActionResult.Success;
        }
    }

    /// <summary> Server-set: records the player's club membership after the ClubsActor confirms join/leave. </summary>
    [ModelAction(ActionCodes.PlayerSetClubMembership)]
    public class PlayerSetClubMembership : PlayerSynchronizedServerAction
    {
        public string Name { get; private set; }

        PlayerSetClubMembership() { }
        public PlayerSetClubMembership(string name) { Name = name; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
            {
                player.Club.Name = Name ?? "";
                if (string.IsNullOrEmpty(player.Club.Name))
                {
                    player.Club.ContribThisWeek = 0;
                    player.Club.Snapshot = new ClubSnapshot();
                }
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> Server-set: stores a refreshed Club League standings snapshot for display. </summary>
    [ModelAction(ActionCodes.PlayerSetClubSnapshot)]
    public class PlayerSetClubSnapshot : PlayerSynchronizedServerAction
    {
        public ClubSnapshot Snapshot { get; private set; }

        PlayerSetClubSnapshot() { }
        public PlayerSetClubSnapshot(ClubSnapshot snapshot) { Snapshot = snapshot; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit && Snapshot != null)
                player.Club.Snapshot = Snapshot;
            return MetaActionResult.Success;
        }
    }

    /// <summary> Claims the next reached tier on the free or premium Season Pass track. </summary>
    [ModelAction(ActionCodes.PlayerClaimPassReward)]
    public class PlayerClaimPassReward : PlayerAction
    {
        public bool Premium { get; private set; }

        PlayerClaimPassReward() { }
        public PlayerClaimPassReward(bool premium) { Premium = premium; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            GlobalConfig global   = player.GameConfig.Global;
            long         seasonId = SeasonSchedule.CurrentSeasonId(player.CurrentTime, global);
            bool         current  = player.Pass.SeasonId == seasonId;
            PassReward[] track    = Premium ? global.PassPremiumRewards : global.PassFreeRewards;
            int          tier     = current ? global.PassTier(player.Pass.Xp) : 0;
            int          claimed  = current ? (Premium ? player.Pass.ClaimedPremium : player.Pass.ClaimedFree) : 0;

            if (Premium && !(current && player.Pass.PremiumOwned))
                return PlayerActionResults.PremiumNotOwned;
            if (claimed >= track.Length)
                return PlayerActionResults.NoPassRewardToClaim;
            if (claimed >= tier)
                return PlayerActionResults.PassTierNotReached;

            if (commit)
            {
                player.SyncSeason(player.CurrentTime);
                int c = Premium ? player.Pass.ClaimedPremium : player.Pass.ClaimedFree;
                int t = global.PassTier(player.Pass.Xp);
                if (Premium && !player.Pass.PremiumOwned)
                    return PlayerActionResults.PremiumNotOwned;
                if (c >= track.Length)
                    return PlayerActionResults.NoPassRewardToClaim;
                if (c >= t)
                    return PlayerActionResults.PassTierNotReached;

                PassReward reward = track[c];
                player.Wallet.Earn(CurrencyType.Coins, reward.Coins);
                player.Wallet.Earn(CurrencyType.Gems, reward.Gems);
                player.Wallet.Earn(CurrencyType.Shards, reward.Shards);
                if (Premium)
                    player.Pass.ClaimedPremium++;
                else
                    player.Pass.ClaimedFree++;
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> Unlocks the premium Season Pass track for the current season, spending Gems. </summary>
    [ModelAction(ActionCodes.PlayerBuyPremiumPass)]
    public class PlayerBuyPremiumPass : PlayerAction
    {
        public PlayerBuyPremiumPass() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            GlobalConfig global   = player.GameConfig.Global;
            long         seasonId = SeasonSchedule.CurrentSeasonId(player.CurrentTime, global);
            bool         owned    = player.Pass.SeasonId == seasonId && player.Pass.PremiumOwned;

            if (owned)
                return PlayerActionResults.PremiumAlreadyOwned;
            if (!player.Wallet.CanAfford(CurrencyType.Gems, global.PremiumPassGemCost))
                return PlayerActionResults.NotEnoughCurrency;

            if (commit)
            {
                player.SyncSeason(player.CurrentTime);
                if (player.Pass.PremiumOwned)
                    return PlayerActionResults.PremiumAlreadyOwned;
                player.Wallet.TrySpend(CurrencyType.Gems, global.PremiumPassGemCost);
                player.Pass.PremiumOwned = true;
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Simulated in-app purchase of a gem pack: grants the pack's Gems. In a production build this would route
    /// through Metaplay's In-App Purchase validation against the store receipt (see the design doc handoff);
    /// here it is granted server-side for the demo (no real money).
    /// </summary>
    [ModelAction(ActionCodes.PlayerSimPurchase)]
    public class PlayerSimPurchase : PlayerAction
    {
        public string ProductId { get; private set; }

        PlayerSimPurchase() { }
        public PlayerSimPurchase(string productId) { ProductId = productId; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            ShopProduct product = null;
            foreach (ShopProduct candidate in player.GameConfig.Global.ShopProducts)
            {
                if (candidate.Id == ProductId)
                {
                    product = candidate;
                    break;
                }
            }
            if (product == null)
                return PlayerActionResults.UnknownProduct;

            if (commit)
                player.Wallet.Earn(CurrencyType.Gems, product.Gems);
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Exchange Gems for a pack of Coins — the hard→soft currency bridge that lets a spender top up transfer
    /// money. Pure local transaction: spend and grant happen atomically in one Execute.
    /// </summary>
    [ModelAction(ActionCodes.PlayerBuyCoinPack)]
    public class PlayerBuyCoinPack : PlayerAction
    {
        public string ProductId { get; private set; }

        PlayerBuyCoinPack() { }
        public PlayerBuyCoinPack(string productId) { ProductId = productId; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            CoinProduct product = null;
            foreach (CoinProduct candidate in player.GameConfig.Global.CoinProducts)
            {
                if (candidate.Id == ProductId)
                {
                    product = candidate;
                    break;
                }
            }
            if (product == null)
                return PlayerActionResults.UnknownProduct;
            if (!player.Wallet.CanAfford(CurrencyType.Gems, product.GemCost))
                return PlayerActionResults.NotEnoughCurrency;

            if (commit)
            {
                player.Wallet.TrySpend(CurrencyType.Gems, product.GemCost);
                player.Wallet.Earn(CurrencyType.Coins, product.Coins);
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Operator/dev: set a player's live "form" die-tier override (0 clears). In production this is an
    /// authenticated dashboard admin action / game-config hot-update; here it's a development action so the
    /// "real-world form sync" demo is drivable locally. The effect propagates to newly-formed matches.
    /// </summary>
    [ModelAction(ActionCodes.PlayerApplyFormDebug)]
    [DevelopmentOnlyAction]
    public class PlayerApplyFormDebug : PlayerAction
    {
        public string PlayerName { get; private set; }
        public int    TierDelta  { get; private set; }

        PlayerApplyFormDebug() { }
        public PlayerApplyFormDebug(string playerName, int tierDelta)
        {
            PlayerName = playerName;
            TierDelta  = tierDelta;
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
                player.ServerListener.ApplyForm(PlayerName, TierDelta);
            return MetaActionResult.Success;
        }
    }

    /// <summary> Operator/dev: clear all live-form overrides. </summary>
    [ModelAction(ActionCodes.PlayerClearFormDebug)]
    [DevelopmentOnlyAction]
    public class PlayerClearFormDebug : PlayerAction
    {
        public PlayerClearFormDebug() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
                player.ServerListener.ClearForm();
            return MetaActionResult.Success;
        }
    }

    /// <summary> Refresh the cached live-form snapshot (the server fetches it from the FormActor). </summary>
    [ModelAction(ActionCodes.PlayerRefreshForm)]
    public class PlayerRefreshForm : PlayerAction
    {
        public PlayerRefreshForm() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
                player.ServerListener.RefreshForm();
            return MetaActionResult.Success;
        }
    }

    /// <summary> Server-set: stores a refreshed live-form snapshot for display. </summary>
    [ModelAction(ActionCodes.PlayerSetFormSnapshot)]
    public class PlayerSetFormSnapshot : PlayerSynchronizedServerAction
    {
        public FormSnapshot Snapshot { get; private set; }

        PlayerSetFormSnapshot() { }
        public PlayerSetFormSnapshot(FormSnapshot snapshot) { Snapshot = snapshot; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit && Snapshot != null)
                player.FormView = Snapshot;
            return MetaActionResult.Success;
        }
    }

    /// <summary> Enter this week's marquee Bracket Cup (starts a fresh run; free, unlimited re-entries). </summary>
    [ModelAction(ActionCodes.PlayerEnterBracket)]
    public class PlayerEnterBracket : PlayerAction
    {
        public PlayerEnterBracket() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            long week   = BracketCup.CurrentWeekId(player.CurrentTime, player.GameConfig.Global);
            bool active = player.Bracket.WeekId == week && player.Bracket.State == BracketState.Active;
            if (active)
                return PlayerActionResults.BracketInProgress;

            if (commit)
            {
                player.SyncBracket(player.CurrentTime);
                player.Bracket.WeekId     = week;
                player.Bracket.State      = BracketState.Active;
                player.Bracket.RoundIndex = 0;
                player.Bracket.RunCount++;
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Plays the current bracket round: a strength-weighted, server-authoritative deterministic dice duel vs a
    /// (tougher each round) bot. On a win, grants the round reward and advances (or crowns champion); on a loss,
    /// the run ends.
    /// </summary>
    [ModelAction(ActionCodes.PlayerPlayBracketRound)]
    public class PlayerPlayBracketRound : PlayerAction
    {
        public PlayerPlayBracketRound() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            GlobalConfig global = player.GameConfig.Global;
            long week   = BracketCup.CurrentWeekId(player.CurrentTime, global);
            bool active = player.Bracket.WeekId == week && player.Bracket.State == BracketState.Active;
            if (!active)
                return PlayerActionResults.BracketNotActive;

            if (commit)
            {
                player.SyncBracket(player.CurrentTime);
                if (player.Bracket.State != BracketState.Active)
                    return PlayerActionResults.BracketNotActive;

                int   round    = player.Bracket.RoundIndex;
                int   strength = player.SelectedSquadStrength();
                ulong seed     = (ulong)(player.Bracket.WeekId + 1) * 0x9E3779B97F4A7C15ul
                               ^ (ulong)(round + 1) * 0xC2B2AE3D27D4EB4Ful
                               ^ (ulong)(player.Bracket.RunCount + 1) * 0xD1B54A32D192ED03ul;

                if (BracketCup.ResolveRound(strength, round, seed, global))
                {
                    if (round >= 0 && round < global.BracketRoundRewards.Length)
                    {
                        BracketRoundReward reward = global.BracketRoundRewards[round];
                        player.Wallet.Earn(CurrencyType.Coins, reward.Coins);
                        player.Wallet.Earn(CurrencyType.Gems, reward.Gems);
                        player.Wallet.Earn(CurrencyType.Shards, reward.Shards);
                    }
                    if (round > player.Bracket.BestRoundReached)
                        player.Bracket.BestRoundReached = round;
                    if (round > player.Honours.BracketBestRound)
                        player.Honours.BracketBestRound = round;
                    if (round + 1 >= BracketCup.RoundsTotal)
                    {
                        player.Bracket.State = BracketState.Champion;
                        player.Honours.BracketTitles++;
                    }
                    else
                        player.Bracket.RoundIndex = round + 1;
                }
                else
                {
                    player.Bracket.State = BracketState.Eliminated;
                }
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Enter the Draft Cup: pay the entry fee (Coins standard / Gems premium), reset the draft and begin
    /// drafting a one-off XI. Premium boosts the reward ladder. One run at a time.
    /// </summary>
    [ModelAction(ActionCodes.PlayerEnterDraftCup)]
    public class PlayerEnterDraftCup : PlayerAction
    {
        public bool Premium { get; private set; }

        PlayerEnterDraftCup() { }
        public PlayerEnterDraftCup(bool premium = false) { Premium = premium; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            DraftCupRun cup = player.DraftCup;
            if (cup.State == DraftCupState.Drafting || cup.State == DraftCupState.Active)
                return PlayerActionResults.DraftCupBusy;

            GlobalConfig global = player.GameConfig.Global;
            CurrencyType currency = Premium ? CurrencyType.Gems : CurrencyType.Coins;
            long cost = Premium ? global.DraftCupEntryGems : global.DraftCupEntryCoins;
            if (!player.Wallet.CanAfford(currency, cost))
                return PlayerActionResults.NotEnoughCurrency;

            if (commit)
            {
                player.Wallet.TrySpend(currency, cost);
                player.Draft.Reset();
                cup.State            = DraftCupState.Drafting;
                cup.RoundIndex       = 0;
                cup.BestRoundReached = -1;
                cup.Premium          = Premium;
                cup.RunCount++;
                cup.LastMyGoals      = 0;
                cup.LastOppGoals     = 0;
                cup.LastScorers      = "";
                player.Honours.DraftCupRuns++;
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Play the current Draft Cup round: resolves the drafted XI vs an escalating CPU side via the deterministic
    /// match sim, grants the (premium-boosted) round reward and advances / crowns champion on a win, or eliminates
    /// on a loss. The first call once the XI is complete starts the knockout (Drafting → Active).
    /// </summary>
    [ModelAction(ActionCodes.PlayerPlayDraftCupRound)]
    public class PlayerPlayDraftCupRound : PlayerAction
    {
        public PlayerPlayDraftCupRound() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            DraftCupRun cup = player.DraftCup;
            SharedGameConfig config = player.GameConfig;
            GlobalConfig global = config.Global;

            bool draftComplete = player.Draft.HasFormation
                && config.Formations.TryGetValue(player.Draft.Formation, out FormationInfo formation)
                && player.Draft.IsComplete(formation);

            if (cup.State == DraftCupState.Drafting)
            {
                if (!draftComplete)
                    return PlayerActionResults.DraftNotComplete;
            }
            else if (cup.State != DraftCupState.Active)
            {
                return PlayerActionResults.DraftCupNotActive;
            }

            if (commit)
            {
                if (cup.State == DraftCupState.Drafting)
                {
                    cup.State      = DraftCupState.Active;
                    cup.RoundIndex = 0;
                    player.RecordDraftedXiForPlay(); // scout the XI into My Club + track best-XI overall
                }

                int round = cup.RoundIndex;
                LineRatings you = player.ResolveDraftRatings();
                LineRatings opp = DraftCup.OpponentLines(round, global);
                ulong seed = DraftCup.SeedFor(cup.RunCount, round);
                MatchResult result = MatchSim.Resolve(you, opp, seed);
                bool win = DraftCup.IsWin(result, you, opp);

                cup.LastMyGoals  = result.HomeGoals;
                cup.LastOppGoals = result.AwayGoals;
                cup.LastScorers  = DraftCup.ScorersLine(result, MyDraftSquad(player), seed);
                bool dcChampion  = win && (round + 1 >= DraftCup.RoundsTotal);
                cup.LastReport   = MatchReport.Knockout($"a {global.DraftCupOpponentBaseOvr + round * global.DraftCupOpponentOvrPerRound}-rated side",
                                       result.HomeGoals, result.AwayGoals, cup.LastScorers, DraftCup.RoundName(round), win, dcChampion, seed);

                if (win)
                {
                    if (round >= 0 && round < global.DraftCupRoundRewards.Length)
                    {
                        BracketRoundReward reward = global.DraftCupRoundRewards[round];
                        int pct = cup.Premium ? 100 + global.DraftCupPremiumBonusPct : 100;
                        player.Wallet.Earn(CurrencyType.Coins,  (long)reward.Coins  * pct / 100);
                        player.Wallet.Earn(CurrencyType.Gems,   (long)reward.Gems   * pct / 100);
                        player.Wallet.Earn(CurrencyType.Shards, (long)reward.Shards * pct / 100);
                    }
                    if (round > cup.BestRoundReached)
                        cup.BestRoundReached = round;
                    if (round > player.Honours.DraftCupBestRound)
                        player.Honours.DraftCupBestRound = round;
                    if (round + 1 >= DraftCup.RoundsTotal)
                    {
                        cup.State = DraftCupState.Champion;
                        player.Honours.DraftCupTitles++;
                        player.Cosmetics.Owned.Add("avatar_cup_king"); // prestige flair (idempotent)
                    }
                    else
                        cup.RoundIndex = round + 1;
                }
                else
                {
                    cup.State = DraftCupState.Eliminated;
                }
            }
            return MetaActionResult.Success;
        }

        static List<(string Name, Position Pos, int Ovr)> MyDraftSquad(PlayerModel player)
        {
            List<(string Name, Position Pos, int Ovr)> squad = new List<(string Name, Position Pos, int Ovr)>();
            foreach ((int _, LegendId id) in player.Draft.Picks)
            {
                LegendPlayer p = player.ResolveDraftPlayer(id);
                if (p != null)
                    squad.Add((p.Name, p.Position, p.Ovr));
            }
            return squad;
        }
    }

    /// <summary>
    /// Enter the World Cup 2026: pay the entry fee (Coins standard / Gems premium), reset the draft and begin
    /// drafting a one-off XI from the real WC national-team squads. Premium boosts the reward ladder. One run at
    /// a time, and mutually exclusive with a Draft Cup run (both use the player's single drafted XI).
    /// </summary>
    [ModelAction(ActionCodes.PlayerEnterWorldCup)]
    public class PlayerEnterWorldCup : PlayerAction
    {
        public bool Premium { get; private set; }

        PlayerEnterWorldCup() { }
        public PlayerEnterWorldCup(bool premium = false) { Premium = premium; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            WorldCupRun cup = player.WorldCup;
            if (cup.State == WorldCupState.Drafting || cup.State == WorldCupState.Active)
                return PlayerActionResults.WorldCupBusy;
            if (player.DraftCup.State == DraftCupState.Drafting || player.DraftCup.State == DraftCupState.Active)
                return PlayerActionResults.DraftCupBusy;
            if (player.GameConfig.WorldCupPlayers.Count == 0)
                return PlayerActionResults.WorldCupUnavailable; // no squad data shipped/published yet

            GlobalConfig global   = player.GameConfig.Global;
            CurrencyType currency = Premium ? CurrencyType.Gems : CurrencyType.Coins;
            long         cost     = Premium ? global.WorldCupEntryGems : global.WorldCupEntryCoins;
            if (!player.Wallet.CanAfford(currency, cost))
                return PlayerActionResults.NotEnoughCurrency;

            if (commit)
            {
                player.Wallet.TrySpend(currency, cost);
                player.Draft.Reset();
                cup.State            = WorldCupState.Drafting;
                cup.RoundIndex       = 0;
                cup.BestRoundReached = -1;
                cup.Premium          = Premium;
                cup.RunCount++;
                cup.LastMyGoals      = 0;
                cup.LastOppGoals     = 0;
                cup.LastScorers      = "";
                cup.LastOpponent     = "";
                player.Honours.WorldCupRuns++;
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Play the current World Cup round: resolves the drafted XI vs a real nation (escalating strength, weakest
    /// in the opening round → strongest in the final) via the deterministic match sim, grants the (premium-
    /// boosted) round reward and advances / crowns champion on a win, or eliminates on a loss. The first call
    /// once the XI is complete starts the knockout (Drafting → Active).
    /// </summary>
    [ModelAction(ActionCodes.PlayerPlayWorldCupRound)]
    public class PlayerPlayWorldCupRound : PlayerAction
    {
        public PlayerPlayWorldCupRound() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            WorldCupRun      cup    = player.WorldCup;
            SharedGameConfig config = player.GameConfig;
            GlobalConfig     global = config.Global;

            bool draftComplete = player.Draft.HasFormation
                && config.Formations.TryGetValue(player.Draft.Formation, out FormationInfo formation)
                && player.Draft.IsComplete(formation);

            if (cup.State == WorldCupState.Drafting)
            {
                if (!draftComplete)
                    return PlayerActionResults.DraftNotComplete;
            }
            else if (cup.State != WorldCupState.Active)
            {
                return PlayerActionResults.WorldCupNotActive;
            }

            if (commit)
            {
                if (cup.State == WorldCupState.Drafting)
                {
                    cup.State      = WorldCupState.Active;
                    cup.RoundIndex = 0;
                    player.RecordDraftedXiForPlay(); // scout the XI into My Club + track best-XI overall
                }

                int         round    = cup.RoundIndex;
                int         rounds    = WorldCup.RoundsTotal(global);
                NationInfo  opp       = WorldCup.OpponentNation(cup.RunCount, round, global);
                LineRatings you       = player.ResolveDraftRatings();
                LineRatings oppLines  = WorldCup.LinesForNation(opp);
                ulong       seed      = WorldCup.SeedFor(cup.RunCount, round);
                MatchResult result    = MatchSim.Resolve(you, oppLines, seed);
                bool        win       = WorldCup.IsWin(result, you, oppLines);

                cup.LastMyGoals  = result.HomeGoals;
                cup.LastOppGoals = result.AwayGoals;
                cup.LastScorers  = WorldCup.ScorersLine(result, MyWcSquad(player), seed);
                cup.LastOpponent = opp != null ? opp.Badge : "";
                bool wcChampion  = win && (round + 1 >= rounds);
                cup.LastReport   = MatchReport.Knockout(opp != null ? opp.DisplayName : "", result.HomeGoals, result.AwayGoals,
                                       cup.LastScorers, WorldCup.RoundName(global, round), win, wcChampion, seed);

                if (win)
                {
                    BracketRoundReward[] ladder = global.WorldCupRoundRewards;
                    if (round >= 0 && round < ladder.Length)
                    {
                        BracketRoundReward reward = ladder[round];
                        int pct = cup.Premium ? 100 + global.WorldCupPremiumBonusPct : 100;
                        player.Wallet.Earn(CurrencyType.Coins,  (long)reward.Coins  * pct / 100);
                        player.Wallet.Earn(CurrencyType.Gems,   (long)reward.Gems   * pct / 100);
                        player.Wallet.Earn(CurrencyType.Shards, (long)reward.Shards * pct / 100);
                    }
                    if (round > cup.BestRoundReached)
                        cup.BestRoundReached = round;
                    if (round > player.Honours.WorldCupBestRound)
                        player.Honours.WorldCupBestRound = round;
                    if (round + 1 >= rounds)
                    {
                        cup.State = WorldCupState.Champion;
                        player.Honours.WorldCupTitles++;
                        player.Cosmetics.Owned.Add("avatar_wc_champion"); // prestige flair (idempotent)
                    }
                    else
                        cup.RoundIndex = round + 1;
                }
                else
                {
                    cup.State = WorldCupState.Eliminated;
                }

                // A finished run (champion or eliminated) reports the manager's best WC stats to the leaderboard.
                if (cup.State == WorldCupState.Champion || cup.State == WorldCupState.Eliminated)
                    player.ServerListener.ReportWorldCupResult(player.Honours.WorldCupTitles, player.Honours.WorldCupBestRound,
                        player.Honours.BestDraftedXiOvr, player.Honours.WorldCupRuns);
            }
            return MetaActionResult.Success;
        }

        static List<(string Name, Position Pos, int Ovr)> MyWcSquad(PlayerModel player)
        {
            List<(string Name, Position Pos, int Ovr)> squad = new List<(string Name, Position Pos, int Ovr)>();
            foreach ((int _, LegendId id) in player.Draft.Picks)
            {
                LegendPlayer p = player.ResolveDraftPlayer(id);
                if (p != null)
                    squad.Add((p.Name, p.Position, p.Ovr));
            }
            return squad;
        }
    }

    /// <summary>
    /// Open a Scout Pack: spend Gems (or claim the once-per-day free pack), then roll + apply a reward bundle
    /// (currency + a chance of a new cosmetic and a "scouted" star for the club). The roll is deterministic from
    /// the open counter, so the client predicts the exact pull the server applies (no client-supplied RNG).
    /// </summary>
    [ModelAction(ActionCodes.PlayerOpenPack)]
    public class PlayerOpenPack : PlayerAction
    {
        public string PackId { get; private set; }

        PlayerOpenPack() { }
        public PlayerOpenPack(string packId) { PackId = packId; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            GlobalConfig global = player.GameConfig.Global;
            PackDef def = null;
            foreach (PackDef p in global.Packs)
                if (p.Id == PackId) { def = p; break; }
            if (def == null)
                return PlayerActionResults.UnknownProduct;

            long today = QuestEngine.DayOf(player.CurrentTime);
            if (def.IsFreeDaily)
            {
                if (player.Packs.LastFreePackDay == today)
                    return PlayerActionResults.PackAlreadyClaimedToday;
            }
            else if (!player.Wallet.CanAfford(CurrencyType.Gems, def.GemCost))
            {
                return PlayerActionResults.NotEnoughCurrency;
            }

            if (commit)
            {
                if (!def.IsFreeDaily)
                    player.Wallet.TrySpend(CurrencyType.Gems, def.GemCost);

                PackReward reward = PackEngine.Roll(def, PackEngine.SeedFor(player.Packs.OpenedCount, def.Id));

                if (reward.Coins > 0)      player.Wallet.Earn(CurrencyType.Coins, reward.Coins);
                if (reward.Rerolls > 0)    player.Boosts.Rerolls    += reward.Rerolls;     // spent on the next draft
                if (reward.EliteSpins > 0) player.Boosts.EliteSpins += reward.EliteSpins;

                player.Packs.LastReward = reward;
                player.Packs.LastPackId = def.Id;
                player.Packs.OpenedCount++;
                if (def.IsFreeDaily)
                    player.Packs.LastFreePackDay = today;
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Claim a completed objective's one-time reward. Progress is computed from the live model, so this just
    /// validates completion + not-already-claimed, then grants the reward and records the claim.
    /// </summary>
    [ModelAction(ActionCodes.PlayerClaimObjective)]
    public class PlayerClaimObjective : PlayerAction
    {
        public string ObjectiveId { get; private set; }

        PlayerClaimObjective() { }
        public PlayerClaimObjective(string objectiveId) { ObjectiveId = objectiveId; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            ObjectiveDef def = Objectives.Find(ObjectiveId);
            if (def == null)
                return PlayerActionResults.UnknownObjective;
            if (!Objectives.IsComplete(def, player))
                return PlayerActionResults.ObjectiveNotComplete;
            if (Objectives.IsClaimed(def, player))
                return PlayerActionResults.ObjectiveAlreadyClaimed;

            if (commit)
            {
                if (def.Coins > 0)  player.Wallet.Earn(CurrencyType.Coins,  def.Coins);
                if (def.Gems > 0)   player.Wallet.Earn(CurrencyType.Gems,   def.Gems);
                if (def.Shards > 0) player.Wallet.Earn(CurrencyType.Shards, def.Shards);
                player.Objectives.Claimed.Add(def.Id);
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Buy a Featured Offer bundle (simulated IAP — real builds validate the store receipt). Grants the
    /// bundle's currency + guaranteed cosmetic; one-time bundles are rejected if already owned.
    /// </summary>
    [ModelAction(ActionCodes.PlayerBuyBundle)]
    public class PlayerBuyBundle : PlayerAction
    {
        public string BundleId { get; private set; }

        PlayerBuyBundle() { }
        public PlayerBuyBundle(string bundleId) { BundleId = bundleId; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            BundleDef def = null;
            foreach (BundleDef b in player.GameConfig.Global.Bundles)
                if (b.Id == BundleId) { def = b; break; }
            if (def == null)
                return PlayerActionResults.UnknownProduct;
            if (def.OneTime && player.Store.Owns(def.Id))
                return PlayerActionResults.BundleAlreadyOwned;

            if (commit)
            {
                if (def.Coins > 0)  player.Wallet.Earn(CurrencyType.Coins,  def.Coins);
                if (def.Gems > 0)   player.Wallet.Earn(CurrencyType.Gems,   def.Gems);
                if (def.Shards > 0) player.Wallet.Earn(CurrencyType.Shards, def.Shards);
                if (!string.IsNullOrEmpty(def.CosmeticId) && player.GameConfig.Cosmetics.ContainsKey(def.CosmeticId))
                    player.Cosmetics.Owned.Add(def.CosmeticId);
                if (def.OneTime)
                    player.Store.BundlesPurchased.Add(def.Id);
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> Ask the server to fetch + cache the top World Cup leaderboard for display. </summary>
    [ModelAction(ActionCodes.PlayerRefreshWorldCupLeaderboard)]
    public class PlayerRefreshWorldCupLeaderboard : PlayerAction
    {
        public PlayerRefreshWorldCupLeaderboard() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
                player.ServerListener.RefreshWorldCupLeaderboard();
            return MetaActionResult.Success;
        }
    }

    /// <summary> Server-set: cache the fetched World Cup leaderboard snapshot for the WC hub. </summary>
    [ModelAction(ActionCodes.PlayerSetWcLeaderboard)]
    public class PlayerSetWcLeaderboard : PlayerSynchronizedServerAction
    {
        public WorldCupLeaderboardSnapshot Snapshot { get; private set; }

        PlayerSetWcLeaderboard() { }
        public PlayerSetWcLeaderboard(WorldCupLeaderboardSnapshot snapshot) { Snapshot = snapshot; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit && Snapshot != null)
                player.WcLeaderboard = Snapshot;
            return MetaActionResult.Success;
        }
    }

    /// <summary> Buy a cosmetic (avatar / dice skin) with Gems. </summary>
    [ModelAction(ActionCodes.PlayerBuyCosmetic)]
    public class PlayerBuyCosmetic : PlayerAction
    {
        public string Id { get; private set; }

        PlayerBuyCosmetic() { }
        public PlayerBuyCosmetic(string id) { Id = id; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.GameConfig.Cosmetics.TryGetValue(Id, out CosmeticItem item))
                return PlayerActionResults.UnknownCosmetic;
            if (player.Cosmetics.Owns(Id))
                return PlayerActionResults.CosmeticAlreadyOwned;
            if (!player.Wallet.CanAfford(CurrencyType.Gems, item.GemCost))
                return PlayerActionResults.NotEnoughCurrency;

            if (commit)
            {
                player.Wallet.TrySpend(CurrencyType.Gems, item.GemCost);
                player.Cosmetics.Owned.Add(Id);
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> Equip an owned cosmetic into its slot (avatar / dice skin). </summary>
    [ModelAction(ActionCodes.PlayerEquipCosmetic)]
    public class PlayerEquipCosmetic : PlayerAction
    {
        public string Id { get; private set; }

        PlayerEquipCosmetic() { }
        public PlayerEquipCosmetic(string id) { Id = id; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.GameConfig.Cosmetics.TryGetValue(Id, out CosmeticItem item))
                return PlayerActionResults.UnknownCosmetic;
            if (!player.Cosmetics.Owns(Id))
                return PlayerActionResults.CosmeticNotOwned;

            if (commit)
            {
                if (item.Kind == CosmeticKind.Avatar)
                    player.Cosmetics.EquippedAvatar = Id;
                else
                    player.Cosmetics.EquippedDiceSkin = Id;
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> Development-only: grant currency for local testing. Cannot execute in production. </summary>
    [ModelAction(ActionCodes.PlayerGrantCurrencyDebug)]
    [DevelopmentOnlyAction]
    public class PlayerGrantCurrencyDebug : PlayerAction
    {
        public CurrencyType Currency { get; private set; }
        public long         Amount   { get; private set; }

        PlayerGrantCurrencyDebug() { }
        public PlayerGrantCurrencyDebug(CurrencyType currency, long amount)
        {
            Currency = currency;
            Amount   = amount;
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
                player.Wallet.Earn(Currency, Amount);
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// LiveOps Dashboard action: grant currency to a player. Unlike <see cref="PlayerGrantCurrencyDebug"/> this is
    /// not development-only, so it works in every environment. The SDK auto-renders the form on the player page
    /// from the [MetaMember] fields; CS staff with the 'Gentle' permission can run it.
    /// </summary>
    [ModelAction(ActionCodes.PlayerAdminGrantCurrency)]
    [PlayerDashboardAction("Grant Currency", "Grant Coins, Gems or Shards to this player.", AdminActionPlacement.Gentle)]
    public class PlayerAdminGrantCurrency : PlayerSynchronizedServerAction
    {
        [MetaMember(1)] public CurrencyType Currency { get; private set; }
        [MetaMember(2)] public long         Amount   { get; private set; }

        PlayerAdminGrantCurrency() { }
        public PlayerAdminGrantCurrency(CurrencyType currency, long amount)
        {
            Currency = currency;
            Amount   = amount;
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (Amount <= 0)
                return PlayerActionResults.InvalidAmount; // grants only — a typo'd negative must not drain a wallet
            if (commit)
                player.Wallet.Earn(Currency, Amount);
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Server action issued on each session start: advance the daily-login streak and grant the streak reward.
    /// The day index is computed server-side and passed in so the action stays deterministic.
    /// </summary>
    [ModelAction(ActionCodes.PlayerUpdateLoginStreak)]
    public class PlayerUpdateLoginStreak : PlayerSynchronizedServerAction
    {
        [MetaMember(1)] public long Today { get; private set; } // days since epoch (UTC)

        PlayerUpdateLoginStreak() { }
        public PlayerUpdateLoginStreak(long today) { Today = today; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit && LoginStreakEngine.Advance(player.LoginStreak, Today))
            {
                long coins = player.GameConfig.Global.DailyStreakReward(player.LoginStreak.CurrentStreak);
                player.Wallet.Earn(CurrencyType.Coins, coins);
                player.ClientListener.DailyStreakClaimed(player.LoginStreak.CurrentStreak, coins);
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> Claim a completed quest's reward (daily: once per day; season: once per league season). Player-initiated. </summary>
    [ModelAction(ActionCodes.PlayerClaimQuest)]
    public class PlayerClaimQuest : PlayerAction
    {
        public string QuestIdValue { get; private set; }

        PlayerClaimQuest() { }
        public PlayerClaimQuest(string questId) { QuestIdValue = questId; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.GameConfig.Quests.TryGetValue(QuestId.FromString(QuestIdValue), out QuestInfo def))
                return PlayerActionResults.UnknownQuest;

            if (def.Scope == QuestScope.Daily)
            {
                // A day rollover means all stored progress/claims are stale, so nothing is claimable. Validate
                // against that WITHOUT mutating (dry-runs must be side-effect free); the actual reset happens in
                // QuestEngine.Advance / inside the commit below.
                if (player.Quests.DayKey != QuestEngine.DayOf(player.CurrentTime))
                    return PlayerActionResults.QuestNotComplete;
                // A rerolled-away quest can't be claimed even if its progress later completes.
                if (QuestEngine.SlotKeyFor(player.GameConfig.Quests.Values, player.Quests, player.GameConfig.Global.DailyQuestSlots, def.Id.Value) == null)
                    return PlayerActionResults.UnknownQuest;
            }
            else
            {
                // Season progress only counts for the league it was earned in.
                if (player.Quests.SeasonScopeKey != (player.League.Code ?? ""))
                    return PlayerActionResults.QuestNotComplete;
            }

            if (!QuestEngine.IsComplete(def, player.Quests))
                return PlayerActionResults.QuestNotComplete;
            if (QuestEngine.IsClaimed(def, player.Quests))
                return PlayerActionResults.QuestAlreadyClaimed;

            if (commit)
            {
                player.Wallet.Earn(CurrencyType.Coins, def.RewardCoins * LiveOpsBoosts.CoinMultiplier(player));
                if (def.RewardGems > 0)
                    player.Wallet.Earn(CurrencyType.Gems, def.RewardGems); // gems are never event-boosted
                if (def.Scope == QuestScope.Daily)
                    player.Quests.Claimed[def.Id.Value] = true;
                else
                    player.Quests.SeasonClaimed[def.Id.Value] = true;
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Swap one of today's unclaimed, uncompleted daily quests for the next unused daily definition, for Gems.
    /// Deterministic from config order + the model's reroll map, so it client-predicts cleanly.
    /// </summary>
    [ModelAction(ActionCodes.PlayerRerollQuest)]
    public class PlayerRerollQuest : PlayerAction
    {
        public string QuestIdValue { get; private set; }

        PlayerRerollQuest() { }
        public PlayerRerollQuest(string questId) { QuestIdValue = questId; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.GameConfig.Quests.TryGetValue(QuestId.FromString(QuestIdValue), out QuestInfo def))
                return PlayerActionResults.UnknownQuest;
            if (def.Scope != QuestScope.Daily)
                return PlayerActionResults.UnknownQuest;
            // Stale day = the visible slate is the fresh base set; rerolling against stale state would misfire.
            if (player.Quests.DayKey != QuestEngine.DayOf(player.CurrentTime))
                return PlayerActionResults.QuestNotComplete;

            GlobalConfig global = player.GameConfig.Global;
            string slotKey = QuestEngine.SlotKeyFor(player.GameConfig.Quests.Values, player.Quests, global.DailyQuestSlots, def.Id.Value);
            if (slotKey == null)
                return PlayerActionResults.UnknownQuest;
            if (QuestEngine.IsClaimed(def, player.Quests) || QuestEngine.IsComplete(def, player.Quests))
                return PlayerActionResults.QuestAlreadyClaimed; // completed quests get claimed, not rerolled

            QuestInfo replacement = QuestEngine.NextRerollCandidate(player.GameConfig.Quests.Values, player.Quests, global.DailyQuestSlots);
            if (replacement == null)
                return PlayerActionResults.NoRerollAvailable;
            if (!player.Wallet.CanAfford(CurrencyType.Gems, global.QuestRerollGemCost))
                return PlayerActionResults.NotEnoughCurrency;

            if (commit)
            {
                player.Wallet.TrySpend(CurrencyType.Gems, global.QuestRerollGemCost);
                player.Quests.Rerolled[slotKey] = replacement.Id.Value;
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Transfer-window swap: drop one drafted legend for another during an open window. The signing fee is charged
    /// from the player's WALLET here (Coins scaled by the incoming player's OVR, or Gems for a "marquee" star),
    /// transactionally with the relay — same spend-then-defer shape as <see cref="PlayerFindMatch"/>, so a hacked
    /// client can't skip the cost. If the LeagueActor then rejects the swap (window closed, player taken, …) the
    /// server refunds via <see cref="PlayerRefundLeagueCharge"/>.
    /// </summary>
    [ModelAction(ActionCodes.PlayerLeagueTransferSwap)]
    public class PlayerLeagueTransferSwap : PlayerAction
    {
        public string DropLegendId { get; private set; }
        public string AddLegendId  { get; private set; }
        public bool   PayWithGems  { get; private set; }

        PlayerLeagueTransferSwap() { }
        public PlayerLeagueTransferSwap(string dropLegendId, string addLegendId, bool payWithGems = false)
        {
            DropLegendId = dropLegendId;
            AddLegendId  = addLegendId;
            PayWithGems  = payWithGems;
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.League.InLeague)
                return PlayerActionResults.NotInLeague;
            if (!player.GameConfig.Legends.TryGetValue(LegendId.FromString(AddLegendId), out LegendPlayer addLegend))
                return PlayerActionResults.UnknownPlayer;

            LeagueDefinition def = LeagueEconomy.DefinitionFor(player);
            CurrencyType currency;
            long cost;
            if (PayWithGems)
            {
                if (addLegend.Ovr < def.MarqueeMinOvr)
                    return PlayerActionResults.NotMarqueeEligible;
                currency = CurrencyType.Gems;
                cost     = def.MarqueeGemCostFor(addLegend.Ovr);
            }
            else
            {
                currency = CurrencyType.Coins;
                cost     = def.TransferCostFor(addLegend.Ovr);
            }
            if (!player.Wallet.CanAfford(currency, cost))
                return PlayerActionResults.NotEnoughCurrency;

            if (commit)
            {
                player.Wallet.TrySpend(currency, cost);
                player.ServerListener.LeagueTransferSwap(player.League.Code, DropLegendId, AddLegendId, PayWithGems);
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Server action: return a league charge (transfer fee / elite-spin Gems) after the LeagueActor rejected the
    /// operation the player already paid for. Server-issued only — a client cannot submit this, so the refund
    /// path is not a currency faucet.
    /// </summary>
    [ModelAction(ActionCodes.PlayerRefundLeagueCharge)]
    public class PlayerRefundLeagueCharge : PlayerSynchronizedServerAction
    {
        [MetaMember(1)] public CurrencyType Currency { get; private set; }
        [MetaMember(2)] public long         Amount   { get; private set; }

        PlayerRefundLeagueCharge() { }
        public PlayerRefundLeagueCharge(CurrencyType currency, long amount)
        {
            Currency = currency;
            Amount   = amount;
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (Amount <= 0)
                return PlayerActionResults.InvalidAmount;
            if (commit)
                player.Wallet.Earn(Currency, Amount);
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Server action issued after the LeagueActor accepts a transfer: advances the "make N transfers" quests.
    /// Separate from the charge (which is client-predicted) because only the league knows the swap succeeded.
    /// </summary>
    [ModelAction(ActionCodes.PlayerRecordTransferMade)]
    public class PlayerRecordTransferMade : PlayerSynchronizedServerAction
    {
        public PlayerRecordTransferMade() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
                QuestEngine.Advance(player.Quests, player.GameConfig.Quests.Values, QuestMetric.TransfersMade, 1, player.CurrentTime, player.League.Code);
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// P2P: propose a player(+cash)-for-player trade to another manager in the league. The cash is escrowed from
    /// the wallet up-front (refunded by the server if the league rejects, or on reject/cancel/expiry).
    /// </summary>
    [ModelAction(ActionCodes.PlayerLeagueProposeTrade)]
    public class PlayerLeagueProposeTrade : PlayerAction
    {
        public int    ToIndex      { get; private set; }
        public string GiveLegendId { get; private set; }
        public string GetLegendId  { get; private set; }
        public int    Coins        { get; private set; }

        PlayerLeagueProposeTrade() { }
        public PlayerLeagueProposeTrade(int toIndex, string giveLegendId, string getLegendId, int coins)
        {
            ToIndex = toIndex; GiveLegendId = giveLegendId; GetLegendId = getLegendId; Coins = coins;
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.League.InLeague)
                return PlayerActionResults.NotInLeague;
            if (Coins < 0)
                return PlayerActionResults.InvalidAmount;
            if (Coins > 0 && !player.Wallet.CanAfford(CurrencyType.Coins, Coins))
                return PlayerActionResults.NotEnoughCurrency;

            if (commit)
            {
                if (Coins > 0)
                    player.Wallet.TrySpend(CurrencyType.Coins, Coins); // escrow; server refunds on rejection
                player.ServerListener.LeagueProposeTrade(player.League.Code, ToIndex, GiveLegendId, GetLegendId, Coins);
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> P2P: accept/reject a trade you received, or cancel (Accept=false) one you proposed. </summary>
    [ModelAction(ActionCodes.PlayerLeagueRespondTrade)]
    public class PlayerLeagueRespondTrade : PlayerAction
    {
        public int  OfferId { get; private set; }
        public bool Accept  { get; private set; }

        PlayerLeagueRespondTrade() { }
        public PlayerLeagueRespondTrade(int offerId, bool accept) { OfferId = offerId; Accept = accept; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.League.InLeague)
                return PlayerActionResults.NotInLeague;
            if (commit)
                player.ServerListener.LeagueRespondTrade(player.League.Code, OfferId, Accept);
            return MetaActionResult.Success;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────────────────────────────
    // FOOTDRAFT spin-draft actions (P1). The draft builds PlayerModel.Draft (a DraftedSquad) one slot at a time:
    //   ChooseFormation → (per open slot) SpinForSlot ⇒ server rolls ⇒ SetSpinOffer ⇒ PickFromOffer.
    // The spin RNG is server-only (cheat-proof), so SpinForSlot just validates + defers to the server listener.
    // ──────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary> Choose the formation to draft into. Resets any in-progress offer; rejected once the squad is locked. </summary>
    [ModelAction(ActionCodes.PlayerChooseFormation)]
    public class PlayerChooseFormation : PlayerAction
    {
        public string NewFormationId { get; private set; }

        PlayerChooseFormation() { }
        public PlayerChooseFormation(string newFormationId) { NewFormationId = newFormationId; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (player.Draft.Locked)
                return PlayerActionResults.DraftLocked;

            FormationId fid = FormationId.FromString(NewFormationId);
            if (!player.GameConfig.Formations.ContainsKey(fid))
                return PlayerActionResults.UnknownFormation;

            if (commit)
            {
                player.Draft.Formation = fid;
                player.Draft.ClearOffer();
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Request a server-rolled spin bucket for a draft slot. Validated client-side, then the actual (cheat-proof)
    /// RNG is deferred to the server via the listener; the result lands as the pending offer. Re-spinning a slot
    /// that already has an offer is a reroll, capped by <see cref="DraftEngine.DefaultRerollCap"/>.
    /// </summary>
    [ModelAction(ActionCodes.PlayerSpinForSlot)]
    public class PlayerSpinForSlot : PlayerAction
    {
        public int Slot { get; private set; }

        PlayerSpinForSlot() { }
        public PlayerSpinForSlot(int slot) { Slot = slot; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            DraftedSquad draft = player.Draft;
            if (draft.Locked)
                return PlayerActionResults.DraftLocked;
            if (!draft.HasFormation || !player.GameConfig.Formations.TryGetValue(draft.Formation, out FormationInfo formation))
                return PlayerActionResults.NoFormationChosen;
            if (Slot < 0 || Slot >= formation.Slots.Count)
                return PlayerActionResults.SlotOutOfRange;
            if (draft.IsSlotFilled(Slot))
                return PlayerActionResults.SlotAlreadyFilled;

            bool isReroll = draft.HasPendingOffer && draft.PendingOfferSlot == Slot;
            // A pending offer for a *different* slot must be resolved (picked) first.
            if (draft.HasPendingOffer && draft.PendingOfferSlot != Slot)
                return PlayerActionResults.OfferAlreadyPending;
            // Free rerolls up to the cap, then spend a Scout-Pack reroll boost if the manager has one.
            bool useBoostReroll = false;
            if (isReroll && draft.RerollsUsed >= DraftEngine.DefaultRerollCap)
            {
                if (player.Boosts.Rerolls <= 0)
                    return PlayerActionResults.NoRerollsLeft;
                useBoostReroll = true;
            }

            if (commit)
            {
                if (isReroll)
                    draft.RerollsUsed++;
                if (useBoostReroll)
                    player.Boosts.Rerolls--;
                // The server performs the actual roll and writes the offer back (no-op on the client).
                player.ServerListener.SpinDraftSlot(Slot);
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> Server-set: writes the server-rolled spin bucket as the pending offer for a slot. </summary>
    [ModelAction(ActionCodes.PlayerSetSpinOffer)]
    public class PlayerSetSpinOffer : PlayerSynchronizedServerAction
    {
        public int        Slot   { get; private set; }
        public SpinBucket Bucket { get; private set; }

        PlayerSetSpinOffer() { }
        public PlayerSetSpinOffer(int slot, SpinBucket bucket) { Slot = slot; Bucket = bucket; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit && Bucket != null)
            {
                player.Draft.PendingOfferSlot = Slot;
                player.Draft.PendingOffer     = Bucket;
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> Server-issued: consume one elite-spin boost (the server rolled a guaranteed top-tier bucket). </summary>
    [ModelAction(ActionCodes.PlayerConsumeEliteSpin)]
    public class PlayerConsumeEliteSpin : PlayerSynchronizedServerAction
    {
        public PlayerConsumeEliteSpin() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit && player.Boosts.EliteSpins > 0)
                player.Boosts.EliteSpins--;
            return MetaActionResult.Success;
        }
    }

    /// <summary> Claim a candidate from the pending spin offer into its slot (validates position + uniqueness). </summary>
    [ModelAction(ActionCodes.PlayerPickFromOffer)]
    public class PlayerPickFromOffer : PlayerAction
    {
        public string PickedLegendId { get; private set; }

        PlayerPickFromOffer() { }
        public PlayerPickFromOffer(string pickedLegendId) { PickedLegendId = pickedLegendId; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            DraftedSquad draft = player.Draft;
            if (draft.Locked)
                return PlayerActionResults.DraftLocked;
            if (!draft.HasPendingOffer)
                return PlayerActionResults.NoSpinPending;
            if (!draft.HasFormation || !player.GameConfig.Formations.TryGetValue(draft.Formation, out FormationInfo formation))
                return PlayerActionResults.NoFormationChosen;

            int slot = draft.PendingOfferSlot;
            if (slot < 0 || slot >= formation.Slots.Count)
                return PlayerActionResults.SlotOutOfRange;
            if (draft.IsSlotFilled(slot))
                return PlayerActionResults.SlotAlreadyFilled;

            // The legend must be one the server offered for this slot.
            bool inOffer = false;
            foreach (LegendId cand in draft.PendingOffer.CandidateIds)
            {
                if (cand.Value == PickedLegendId)
                {
                    inOffer = true;
                    break;
                }
            }
            if (!inOffer)
                return PlayerActionResults.LegendNotInOffer;

            LegendId legendId = LegendId.FromString(PickedLegendId);
            LegendPlayer legend = player.ResolveDraftPlayer(legendId); // legends OR World Cup squad players
            if (legend == null)
                return PlayerActionResults.LegendNotInOffer;
            if (legend.Position != formation.Slots[slot])
                return PlayerActionResults.WrongPositionForSlot;

            foreach ((int _, LegendId already) in draft.Picks)
            {
                if (already.Value == PickedLegendId)
                    return PlayerActionResults.LegendAlreadyDrafted;
            }

            if (commit)
            {
                draft.Picks[slot] = legendId;
                draft.ClearOffer();
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> Clear the in-progress draft (picks, offer, rerolls). Rejected once the squad is locked. </summary>
    [ModelAction(ActionCodes.PlayerResetDraft)]
    public class PlayerResetDraft : PlayerAction
    {
        public PlayerResetDraft() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (player.Draft.Locked)
                return PlayerActionResults.DraftLocked;

            if (commit)
            {
                player.Draft.Picks       = new MetaDictionary<int, LegendId>();
                player.Draft.RerollsUsed = 0;
                player.Draft.ClearOffer();
            }
            return MetaActionResult.Success;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────────────────────────────
    // FOOTDRAFT season league (P4). Client requests validate then defer to the server listener (the singleton
    // LeagueActor); the resulting snapshot/result is cached back via the PlayerSetLeagueState server action.
    // ──────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary> Create a private league under an invite code (caller becomes commissioner + first member). </summary>
    [ModelAction(ActionCodes.PlayerCreateLeague)]
    public class PlayerCreateLeague : PlayerAction
    {
        public string LeagueName  { get; private set; }
        public string Code        { get; private set; }
        public string TeamName    { get; private set; }
        public bool   HideRatings { get; private set; }
        public int    MaxPerClub  { get; private set; }
        public string CapBands    { get; private set; }
        public string DraftPin    { get; private set; }

        PlayerCreateLeague() { }
        public PlayerCreateLeague(string leagueName, string code, string teamName, bool hideRatings = false, int maxPerClub = 1, string capBands = "90:2,80:3,75:4", string draftPin = "")
        {
            LeagueName = leagueName; Code = code; TeamName = teamName;
            HideRatings = hideRatings; MaxPerClub = maxPerClub; CapBands = capBands ?? ""; DraftPin = draftPin ?? "";
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!LeagueCode.IsValid(Code))
                return PlayerActionResults.InvalidLeagueCode;
            if (player.League.InLeague)
                return PlayerActionResults.AlreadyInLeague;
            if (!string.IsNullOrWhiteSpace(TeamName))
                player.TeamName = TeamName.Trim(); // remembered for next time + used as the league member name
            if (commit)
                player.ServerListener.CreateLeague(LeagueName, Code, HideRatings, MaxPerClub, CapBands, DraftPin);
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Create a SINGLE-PLAYER season: no lobby — the caller drafts immediately and the league pads to a full
    /// 20 teams with CPU sides at season lock. Same league machinery as multiplayer (daily matchdays, transfers,
    /// standings); the caller is the commissioner so they can also force matchdays / simulate the season.
    /// </summary>
    [ModelAction(ActionCodes.PlayerCreateSoloLeague)]
    public class PlayerCreateSoloLeague : PlayerAction
    {
        public string Code        { get; private set; }
        public string TeamName    { get; private set; }
        public bool   HideRatings { get; private set; }
        public int    MaxPerClub  { get; private set; }
        public string CapBands    { get; private set; }

        public string DraftPin    { get; private set; }

        PlayerCreateSoloLeague() { }
        public PlayerCreateSoloLeague(string code, string teamName, bool hideRatings = false, int maxPerClub = 1, string capBands = "90:2,80:3,75:4", string draftPin = "")
        {
            Code = code; TeamName = teamName;
            HideRatings = hideRatings; MaxPerClub = maxPerClub; CapBands = capBands ?? ""; DraftPin = draftPin ?? "";
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!LeagueCode.IsValid(Code))
                return PlayerActionResults.InvalidLeagueCode;
            if (player.League.InLeague)
                return PlayerActionResults.AlreadyInLeague;
            if (!string.IsNullOrWhiteSpace(TeamName))
                player.TeamName = TeamName.Trim();
            if (commit)
                player.ServerListener.CreateSoloLeague(Code, HideRatings, MaxPerClub, CapBands, DraftPin);
            return MetaActionResult.Success;
        }
    }

    /// <summary> Join an existing league by invite code. </summary>
    [ModelAction(ActionCodes.PlayerJoinLeague)]
    public class PlayerJoinLeague : PlayerAction
    {
        public string Code     { get; private set; }
        public string TeamName { get; private set; }

        PlayerJoinLeague() { }
        public PlayerJoinLeague(string code, string teamName) { Code = code; TeamName = teamName; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!LeagueCode.IsValid(Code))
                return PlayerActionResults.InvalidLeagueCode;
            if (player.League.InLeague)
                return PlayerActionResults.AlreadyInLeague;
            if (!string.IsNullOrWhiteSpace(TeamName))
                player.TeamName = TeamName.Trim();
            if (commit)
                player.ServerListener.JoinLeague(Code);
            return MetaActionResult.Success;
        }
    }

    /// <summary> Leave the current league (only takes effect while it is still in the lobby). </summary>
    [ModelAction(ActionCodes.PlayerLeaveLeague)]
    public class PlayerLeaveLeague : PlayerAction
    {
        public PlayerLeaveLeague() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.League.InLeague)
                return PlayerActionResults.NotInLeague;
            if (commit)
                player.ServerListener.LeaveLeague(player.League.Code);
            return MetaActionResult.Success;
        }
    }

    /// <summary> Commissioner: kick off the season (locks every member's drafted XI + generates fixtures). </summary>
    [ModelAction(ActionCodes.PlayerStartLeagueSeason)]
    public class PlayerStartLeagueSeason : PlayerAction
    {
        public PlayerStartLeagueSeason() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.League.InLeague)
                return PlayerActionResults.NotInLeague;
            if (commit)
                player.ServerListener.StartLeagueSeason(player.League.Code);
            return MetaActionResult.Success;
        }
    }

    /// <summary> Play your next unplayed league fixture (your XI vs the opponent's locked XI). </summary>
    [ModelAction(ActionCodes.PlayerPlayLeagueFixture)]
    public class PlayerPlayLeagueFixture : PlayerAction
    {
        public PlayerPlayLeagueFixture() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.League.InLeague)
                return PlayerActionResults.NotInLeague;
            if (commit)
                player.ServerListener.PlayLeagueFixture(player.League.Code);
            return MetaActionResult.Success;
        }
    }

    /// <summary> Refresh the cached league snapshot from the server. </summary>
    [ModelAction(ActionCodes.PlayerRefreshLeague)]
    public class PlayerRefreshLeague : PlayerAction
    {
        public PlayerRefreshLeague() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.League.InLeague)
                return PlayerActionResults.NotInLeague;
            if (commit)
                player.ServerListener.RefreshLeague(player.League.Code);
            return MetaActionResult.Success;
        }
    }

    /// <summary> Server-set: caches a league op's result (membership + snapshot, an error, or a played fixture). </summary>
    [ModelAction(ActionCodes.PlayerSetLeagueState)]
    public class PlayerSetLeagueState : PlayerSynchronizedServerAction
    {
        public string           Code       { get; private set; }
        public LeagueSnapshot   Snapshot   { get; private set; }
        public string           Error      { get; private set; }
        public LeaguePlayResult PlayResult { get; private set; }

        PlayerSetLeagueState() { }
        public PlayerSetLeagueState(string code, LeagueSnapshot snapshot, string error, LeaguePlayResult playResult)
        {
            Code       = code;
            Snapshot   = snapshot;
            Error      = error ?? "";
            PlayResult = playResult;
        }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (commit)
            {
                if (!string.IsNullOrEmpty(Error))
                {
                    player.League.LastError = Error; // op failed; keep current membership
                }
                else if (string.IsNullOrEmpty(Code))
                {
                    player.League.Code     = ""; // left / no league
                    player.League.Snapshot = null;
                    player.League.LastError = "";
                }
                else
                {
                    player.League.Code      = Code;
                    player.League.Snapshot  = Snapshot;
                    player.League.LastError = "";
                    if (PlayResult != null)
                        player.League.LastPlayResult = PlayResult;

                    // Trophy cabinet: when a season finishes, record it once (deduped by league code) — a title
                    // if the manager topped the table, plus an invincible-season honour if they went unbeaten.
                    if (Snapshot != null && Snapshot.State == LeagueState.Finished
                        && player.Honours.LastScoredLeagueCode != Code)
                    {
                        player.Honours.LastScoredLeagueCode = Code;
                        player.Honours.LeagueSeasonsPlayed++;
                        if (Snapshot.Table.Count > 0 && Snapshot.Table[0].TeamIndex == Snapshot.MyIndex)
                            player.Honours.LeagueTitles++;
                        if (Snapshot.Invincible)
                        {
                            player.Honours.InvincibleSeasons++;
                            player.Cosmetics.Owned.Add("avatar_invincible"); // prestige flair (idempotent)
                        }
                    }
                }
            }
            return MetaActionResult.Success;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────────────────────────────
    // FOOTDRAFT league draft (the headline flow). Managers take turns drafting their XIs from ONE shared legend
    // pool — so no legend can be on two teams — each into a valid formation. Like the other league actions these
    // validate then defer to the server listener (the LeagueActor owns the authoritative draft state); the
    // resulting snapshot is cached back via PlayerSetLeagueState. They never mutate checksummed PlayerModel state.
    // ──────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary> Set the formation the caller drafts into (in the lobby, or before they make their first pick). </summary>
    [ModelAction(ActionCodes.PlayerSetLeagueFormation)]
    public class PlayerSetLeagueFormation : PlayerAction
    {
        public string FormationId { get; private set; }

        PlayerSetLeagueFormation() { }
        public PlayerSetLeagueFormation(string formationId) { FormationId = formationId; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.League.InLeague)
                return PlayerActionResults.NotInLeague;
            if (commit)
                player.ServerListener.SetLeagueFormation(player.League.Code, FormationId);
            return MetaActionResult.Success;
        }
    }

    /// <summary> Spin the wheel: ask the server to roll a random Club×Season squad to pick from (the caller's turn). </summary>
    [ModelAction(ActionCodes.PlayerLeagueSpin)]
    public class PlayerLeagueSpin : PlayerAction
    {
        /// <summary> True = pay Gems for a guaranteed top-tier club spin (charged here; refunded by the server if the league rejects). </summary>
        public bool Elite { get; private set; }

        public PlayerLeagueSpin() { }
        public PlayerLeagueSpin(bool elite) { Elite = elite; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.League.InLeague)
                return PlayerActionResults.NotInLeague;

            long gemCost = 0;
            if (Elite)
            {
                // Cheap fail-fast on the cached snapshot (the league re-validates turn/state authoritatively).
                if (player.League.Snapshot == null || player.League.Snapshot.State != LeagueState.Drafting)
                    return PlayerActionResults.DraftLocked;
                gemCost = LeagueEconomy.DefinitionFor(player).EliteSpinGemCost;
                if (!player.Wallet.CanAfford(CurrencyType.Gems, gemCost))
                    return PlayerActionResults.NotEnoughCurrency;
            }

            if (commit)
            {
                if (Elite)
                    player.Wallet.TrySpend(CurrencyType.Gems, gemCost);
                player.ServerListener.LeagueSpin(player.League.Code, Elite);
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary> Draft a legend into the caller's XI (server enforces turn + uniqueness + open-slot validity). </summary>
    [ModelAction(ActionCodes.PlayerLeagueDraftPick)]
    public class PlayerLeagueDraftPick : PlayerAction
    {
        public string LegendId { get; private set; }

        PlayerLeagueDraftPick() { }
        public PlayerLeagueDraftPick(string legendId) { LegendId = legendId; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.League.InLeague)
                return PlayerActionResults.NotInLeague;
            if (commit)
                player.ServerListener.LeagueDraftPick(player.League.Code, LegendId);
            return MetaActionResult.Success;
        }
    }

    /// <summary> Auto-pick the best available legend. <see cref="FillAll"/> (commissioner) drafts the rest of the league. </summary>
    [ModelAction(ActionCodes.PlayerLeagueAutoPick)]
    public class PlayerLeagueAutoPick : PlayerAction
    {
        public bool FillAll { get; private set; }

        PlayerLeagueAutoPick() { }
        public PlayerLeagueAutoPick(bool fillAll) { FillAll = fillAll; }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.League.InLeague)
                return PlayerActionResults.NotInLeague;
            if (commit)
                player.ServerListener.LeagueDraftAutoPick(player.League.Code, FillAll);
            return MetaActionResult.Success;
        }
    }

    /// <summary> Commissioner: simulate every remaining fixture and finish the season in one go. </summary>
    [ModelAction(ActionCodes.PlayerSimulateLeague)]
    public class PlayerSimulateLeague : PlayerAction
    {
        public PlayerSimulateLeague() { }

        public override MetaActionResult Execute(PlayerModel player, bool commit)
        {
            if (!player.League.InLeague)
                return PlayerActionResults.NotInLeague;
            if (commit)
                player.ServerListener.SimulateLeagueSeason(player.League.Code);
            return MetaActionResult.Success;
        }
    }
}
