// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

namespace Game.Logic
{
    public interface IPlayerModelServerListener
    {
        /// <summary> Triggered by <see cref="PlayerFindMatch"/>: add the player to the matchmaking queue. </summary>
        void FindMatch();

        /// <summary> Triggered by <see cref="PlayerLeaveMatch"/>: leave the current match and return to the lobby. </summary>
        void LeaveMatch();

        /// <summary> Triggered by <see cref="PlayerStartMatchNow"/>: ask the lobby to start a match immediately. </summary>
        void StartMatchNow();

        /// <summary> Triggered by <see cref="PlayerCreateFriendly"/>: open a private room under <paramref name="code"/>. </summary>
        void CreateFriendly(string code);

        /// <summary> Triggered by <see cref="PlayerJoinFriendly"/>: join the private room with <paramref name="code"/>. </summary>
        void JoinFriendly(string code);

        /// <summary> Triggered by <see cref="PlayerCancelFriendly"/>: close any private room this player created. </summary>
        void CancelFriendly();

        /// <summary> Triggered by <see cref="PlayerJoinClub"/>: join or create the club named <paramref name="clubName"/>. </summary>
        void JoinClub(string clubName);

        /// <summary> Triggered by <see cref="PlayerLeaveClub"/>: leave the current club. </summary>
        void LeaveClub();

        /// <summary> Triggered by <see cref="PlayerRefreshClub"/>: refresh the cached Club League standings. </summary>
        void RefreshClub();

        /// <summary> Operator/dev: set a player's live form die-tier override (0 clears it). </summary>
        void ApplyForm(string playerName, int tierDelta);

        /// <summary> Operator/dev: clear all live-form overrides. </summary>
        void ClearForm();

        /// <summary> Refresh the cached live-form snapshot for display. </summary>
        void RefreshForm();

        /// <summary>
        /// Triggered by <see cref="PlayerSpinForSlot"/>: roll a (Club, Era) spin bucket for <paramref name="slot"/>
        /// server-side (cheat-proof) and write it back as the pending offer via a server action.
        /// </summary>
        void SpinDraftSlot(int slot);

        // ----- Season league (P4): talk to the singleton LeagueActor, then cache the resulting snapshot. -----
        void CreateLeague(string leagueName, string code);
        void CreateSoloLeague(string code);
        void JoinLeague(string code);
        void LeaveLeague(string code);
        void StartLeagueSeason(string code);
        void PlayLeagueFixture(string code);
        void RefreshLeague(string code);

        // ----- League draft (the headline flow): managers take turns drafting their XIs from a shared pool. -----
        /// <summary> Set the caller's drafting formation (lobby, or before their first pick). </summary>
        void SetLeagueFormation(string code, string formationId);
        /// <summary> Spin the wheel for a random Club×Season squad to pick from (the caller's turn). <paramref name="elite"/> = Gem-paid spin restricted to top-tier clubs. </summary>
        void LeagueSpin(string code, bool elite);
        /// <summary> Draft a legend into the caller's XI (only valid on their turn, if undrafted + position open). </summary>
        void LeagueDraftPick(string code, string legendId);
        /// <summary> Auto-pick the best available legend; <paramref name="fillAll"/> (commissioner) drafts the whole rest of the league. </summary>
        void LeagueDraftAutoPick(string code, bool fillAll);
        /// <summary> Commissioner: simulate every remaining fixture at once and finish the season. </summary>
        void SimulateLeagueSeason(string code);
        /// <summary> Transfer-window swap: drop one drafted legend for another during an open window (WS3). The fee was already charged from the wallet; <paramref name="payWithGems"/> says which currency (for the refund if the league rejects). </summary>
        void LeagueTransferSwap(string code, string dropLegendId, string addLegendId, bool payWithGems);
    }

    public interface IPlayerModelClientListener
    {
        /// <summary> Match-end rewards were granted (drives the post-match reward popup). </summary>
        void MatchRewardsGranted(bool won, int coins, int xp, int shards, int cupTokens);

        /// <summary> The manager reached <paramref name="newLevel"/> (level-up celebration). </summary>
        void ManagerLeveledUp(int newLevel);

        /// <summary> A squad card was upgraded to <paramref name="newLevel"/> (refresh the squad UI). </summary>
        void CardUpgraded(string cardKey, int newLevel);

        /// <summary> A Cup milestone was claimed (refresh the Cup UI). </summary>
        void CupMilestoneClaimed(int claimedCount);
    }

    public class EmptyPlayerModelServerListener : IPlayerModelServerListener
    {
        public static readonly EmptyPlayerModelServerListener Instance = new EmptyPlayerModelServerListener();

        public void FindMatch() { }
        public void LeaveMatch() { }
        public void StartMatchNow() { }
        public void CreateFriendly(string code) { }
        public void JoinFriendly(string code) { }
        public void CancelFriendly() { }
        public void JoinClub(string clubName) { }
        public void LeaveClub() { }
        public void RefreshClub() { }
        public void ApplyForm(string playerName, int tierDelta) { }
        public void ClearForm() { }
        public void RefreshForm() { }
        public void SpinDraftSlot(int slot) { }
        public void CreateLeague(string leagueName, string code) { }
        public void CreateSoloLeague(string code) { }
        public void JoinLeague(string code) { }
        public void LeaveLeague(string code) { }
        public void StartLeagueSeason(string code) { }
        public void PlayLeagueFixture(string code) { }
        public void RefreshLeague(string code) { }
        public void SetLeagueFormation(string code, string formationId) { }
        public void LeagueSpin(string code, bool elite) { }
        public void LeagueDraftPick(string code, string legendId) { }
        public void LeagueDraftAutoPick(string code, bool fillAll) { }
        public void SimulateLeagueSeason(string code) { }
        public void LeagueTransferSwap(string code, string dropLegendId, string addLegendId, bool payWithGems) { }
    }

    public class EmptyPlayerModelClientListener : IPlayerModelClientListener
    {
        public static readonly EmptyPlayerModelClientListener Instance = new EmptyPlayerModelClientListener();

        public void MatchRewardsGranted(bool won, int coins, int xp, int shards, int cupTokens) { }
        public void ManagerLeveledUp(int newLevel) { }
        public void CardUpgraded(string cardKey, int newLevel) { }
        public void CupMilestoneClaimed(int claimedCount) { }
    }
}
