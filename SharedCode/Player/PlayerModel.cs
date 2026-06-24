// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using System.Runtime.Serialization;

namespace Game.Logic
{
    /// <summary>
    /// Class for storing the state and updating the logic for a single player.
    /// <para>
    /// In this multiplayer game the persisted player state is intentionally minimal: it holds the player
    /// identity plus references to the matchmaking lobby and match the player is currently associated with.
    /// The actual gameplay state lives in the server-owned <see cref="LobbyModel"/> and <see cref="MatchModel"/>
    /// multiplayer entities.
    /// </para>
    /// </summary>
    [MetaSerializableDerived(1)]
    [SupportedSchemaVersions(1, 1)]
    public class PlayerModel : PlayerModelBase<PlayerModel, PlayerStatisticsCore>
    {
        public const int TicksPerSecond = 10;
        protected override int GetTicksPerSecond() => TicksPerSecond;

        // External services, not serialized or PrettyPrinted
        [IgnoreDataMember] public new SharedGameConfig       GameConfig => GetGameConfig<SharedGameConfig>();
        [IgnoreDataMember] public IPlayerModelServerListener ServerListener { get; set; } = EmptyPlayerModelServerListener.Instance;
        [IgnoreDataMember] public IPlayerModelClientListener ClientListener { get; set; } = EmptyPlayerModelClientListener.Instance;

        // Player profile
        [MetaMember(100)] public sealed override EntityId           PlayerId    { get; set; }
        [MetaMember(101), NoChecksum] public sealed override string PlayerName  { get; set; }
        [MetaMember(102)] public sealed override int                PlayerLevel { get; set; }

        // Matchmaking state. Server-only and managed entirely by the PlayerActor; the client observes the
        // lobby/match via entity associations rather than these fields.
        [MetaMember(110), ServerOnly] public EntityId CurrentLobby { get; set; } = EntityId.None;
        [MetaMember(111), ServerOnly] public EntityId CurrentMatch { get; set; } = EntityId.None;

        // Meta-game state (progression, economy, squad). Mutated only via player actions so it stays
        // server-authoritative and checksum-consistent.
        [MetaMember(120)] public PlayerWallet      Wallet         { get; private set; } = new PlayerWallet();
        [MetaMember(121)] public PlayerProgression Progression    { get; private set; } = new PlayerProgression();
        [MetaMember(122)] public SquadBook         SquadBook      { get; private set; } = new SquadBook();
        /// <summary> The national team the manager currently fields (config <see cref="TeamId"/> value). </summary>
        [MetaMember(123)] public string            SelectedTeamId { get; set; }
        [MetaMember(124)] public PlayerTickets     Tickets        { get; private set; } = new PlayerTickets();
        [MetaMember(125)] public CupProgress       Cup            { get; private set; } = new CupProgress();
        [MetaMember(126)] public PlayerClub        Club           { get; private set; } = new PlayerClub();
        [MetaMember(127)] public SeasonPass        Pass           { get; private set; } = new SeasonPass();
        [MetaMember(128)] public SeasonRank        Rank           { get; private set; } = new SeasonRank();
        /// <summary> Cached view of the live "form sync" overrides (for the in-form / operator panel). </summary>
        [MetaMember(129)] public FormSnapshot      FormView       { get; set; } = new FormSnapshot();
        [MetaMember(130)] public BracketRun        Bracket        { get; private set; } = new BracketRun();
        [MetaMember(131)] public PlayerCosmetics   Cosmetics      { get; private set; } = new PlayerCosmetics();
        /// <summary> The manager's spin-drafted XI of legends (FOOTDRAFT P1). Built via the draft player actions. </summary>
        [MetaMember(132)] public DraftedSquad      Draft          { get; private set; } = new DraftedSquad();
        /// <summary> Cached membership + standings for the manager's season league (FOOTDRAFT P4). </summary>
        [MetaMember(133)] public PlayerLeague      League         { get; private set; } = new PlayerLeague();
        /// <summary> The manager's chosen team name (set on create/join); shown in leagues instead of "Guest …". Persists. </summary>
        [MetaMember(134), NoChecksum] public string TeamName      { get; set; } = "";
        /// <summary> Daily-login streak (WS4 retention); advanced server-side on each session start. </summary>
        [MetaMember(135)] public LoginStreak       LoginStreak    { get; private set; } = new LoginStreak();
        /// <summary> Daily-quest progress (WS4 retention); advanced on match results, claimed for Coins. </summary>
        [MetaMember(136)] public PlayerQuests      Quests         { get; private set; } = new PlayerQuests();
        /// <summary> The manager's current Draft Cup run (FUT-Draft-style paid mode). </summary>
        [MetaMember(137)] public DraftCupRun       DraftCup       { get; private set; } = new DraftCupRun();
        /// <summary> The manager's current World Cup 2026 run (draft a WC XI from real squads → knockout vs real nations). </summary>
        [MetaMember(138)] public WorldCupRun       WorldCup       { get; private set; } = new WorldCupRun();
        /// <summary> Lifetime trophy cabinet + all-time peaks (drives the manager profile + achievements). </summary>
        [MetaMember(139)] public PlayerHonours     Honours        { get; private set; } = new PlayerHonours();
        /// <summary> Scouted-player collection ("My Club" gallery) — every player ever drafted into a knockout XI. </summary>
        [MetaMember(140)] public PlayerCollection  Collection     { get; private set; } = new PlayerCollection();
        /// <summary> Scout Pack state (open counter, daily-free claim, last pull for the reveal). </summary>
        [MetaMember(141)] public PlayerPacks       Packs          { get; private set; } = new PlayerPacks();
        /// <summary> Claimed-objective state (the career reward track; progress is computed, not stored). </summary>
        [MetaMember(142)] public PlayerObjectives  Objectives     { get; private set; } = new PlayerObjectives();
        /// <summary> Store state — which one-time Featured Offer bundles have been bought. </summary>
        [MetaMember(143)] public PlayerStore       Store          { get; private set; } = new PlayerStore();
        /// <summary> Cached top-N World Cup leaderboard (fetched from the leaderboard service for the WC hub view). </summary>
        [MetaMember(144)] public WorldCupLeaderboardSnapshot WcLeaderboard { get; set; }
        /// <summary> Draft-edge consumables from Scout Packs, spent on the next spin-draft (rerolls / elite spins). </summary>
        [MetaMember(145)] public DraftBoosts       Boosts         { get; private set; } = new DraftBoosts();

        protected override void GameInitializeNewPlayerModel(MetaTime now, ISharedGameConfig gameConfig, EntityId playerId, string name)
        {
            PlayerId    = playerId;
            PlayerName  = name;
            PlayerLevel = 1;

            GlobalConfig global = ((SharedGameConfig)gameConfig).Global;
            Wallet.Earn(CurrencyType.Coins,  global.StartingCoins);
            Wallet.Earn(CurrencyType.Gems,   global.StartingGems);
            Wallet.Earn(CurrencyType.Shards, global.StartingShards);

            // Start with a full ticket bar.
            Tickets.Count = global.MaxMatchTickets;
            Tickets.LastRegenAt = now;

            // Default to the first configured team so a new manager has a playable squad immediately.
            SelectedTeamId = TeamContent.Teams[0].Id.Value;

            // Grant + equip the free default cosmetics.
            Cosmetics.Owned.Add("avatar_default");
            Cosmetics.Owned.Add("dice_default");
            Cosmetics.EquippedAvatar   = "avatar_default";
            Cosmetics.EquippedDiceSkin = "dice_default";
        }

        protected override void GameTick(IChecksumContext checksumCtx)
        {
            // No per-tick player logic; gameplay happens in the match entity.
        }

        /// <summary>
        /// Grants manager XP and advances <see cref="IPlayerModelBase.PlayerLevel"/> across any thresholds it
        /// crosses (carrying the overflow), capped at <see cref="GlobalConfig.MaxManagerLevel"/>. Called from
        /// within reward actions. Fires the client listener once per level gained.
        /// </summary>
        public void GrantXp(long amount, GlobalConfig global)
        {
            if (amount <= 0)
                return;

            Progression.Xp += amount;
            while (PlayerLevel < global.MaxManagerLevel && Progression.Xp >= global.XpToReachNextLevel(PlayerLevel))
            {
                Progression.Xp -= global.XpToReachNextLevel(PlayerLevel);
                PlayerLevel++;
                ClientListener.ManagerLeveledUp(PlayerLevel);
            }

            // At max level XP no longer accumulates toward anything.
            if (PlayerLevel >= global.MaxManagerLevel)
                Progression.Xp = 0;
        }

        /// <summary>
        /// Rolls the player's Cup progress to the Cup live at <paramref name="now"/>, resetting tokens and claimed
        /// milestones when the window has changed.
        /// </summary>
        public void SyncCup(MetaTime now)
        {
            long cupId = CupSchedule.CurrentCupId(now, GameConfig.Global);
            if (Cup.CupId != cupId)
            {
                Cup.CupId = cupId;
                Cup.Tokens = 0;
                Cup.ClaimedMilestones = 0;
            }
        }

        /// <summary> Adds Cup Tokens to the current Cup (syncing the window first). </summary>
        public void GrantCupTokens(int amount, MetaTime now)
        {
            if (amount <= 0)
                return;
            SyncCup(now);
            Cup.Tokens += amount;
        }

        /// <summary> Accrues the player's Club Points contribution for the current league week (resetting weekly). </summary>
        public void AccrueClubPoints(int points, MetaTime now)
        {
            if (points <= 0 || !Club.InClub)
                return;
            long weekId = ClubWeek.CurrentWeekId(now, GameConfig.Global);
            if (Club.ContribWeekId != weekId)
            {
                Club.ContribWeekId = weekId;
                Club.ContribThisWeek = 0;
            }
            Club.ContribThisWeek += points;
        }

        /// <summary>
        /// Rolls the Season Pass and ranked-ladder state to the season live at <paramref name="now"/>, resetting
        /// pass XP / claims / premium ownership and Season Rank Points when the season has changed.
        /// </summary>
        public void SyncSeason(MetaTime now)
        {
            long seasonId = SeasonSchedule.CurrentSeasonId(now, GameConfig.Global);
            if (Pass.SeasonId != seasonId)
            {
                Pass.SeasonId       = seasonId;
                Pass.Xp             = 0;
                Pass.ClaimedFree    = 0;
                Pass.ClaimedPremium = 0;
                Pass.PremiumOwned   = false;
            }
            if (Rank.SeasonId != seasonId)
            {
                Rank.SeasonId          = seasonId;
                Rank.Points            = 0;
                Rank.BestDivisionIndex = 0;
            }
        }

        /// <summary> Adds Season Pass XP (syncing the season first). </summary>
        public void GrantPassXp(long amount, MetaTime now)
        {
            if (amount <= 0)
                return;
            SyncSeason(now);
            Pass.Xp += amount;
        }

        /// <summary> Applies a Season Rank Points delta (floored at 0) and tracks the best division reached. </summary>
        public void GrantSeasonRankPoints(int delta, MetaTime now)
        {
            SyncSeason(now);
            Rank.Points += delta;
            if (Rank.Points < 0)
                Rank.Points = 0;
            int division = GameConfig.Global.DivisionIndex(Rank.Points);
            if (division > Rank.BestDivisionIndex)
                Rank.BestDivisionIndex = division;
            if (division > Honours.BestRankDivision)
                Honours.BestRankDivision = division;
        }

        /// <summary> Rolls the Bracket Cup run to the current weekly window, resetting it when the week changed. </summary>
        public void SyncBracket(MetaTime now)
        {
            long weekId = BracketCup.CurrentWeekId(now, GameConfig.Global);
            if (Bracket.WeekId != weekId)
            {
                Bracket.WeekId           = weekId;
                Bracket.State            = BracketState.None;
                Bracket.RoundIndex       = 0;
                Bracket.BestRoundReached = -1;
                Bracket.RunCount         = 0;
            }
        }

        /// <summary>
        /// Resolves the manager's drafted XI to the <see cref="LineRatings"/> the match sim consumes. A complete
        /// draft uses its computed lines; an incomplete/absent draft falls back to a baseline "scratch XI" so the
        /// manager can still play. Server-authoritative (called from the match-setup squad fetch).
        /// </summary>
        public LineRatings ResolveDraftRatings()
        {
            SharedGameConfig config = GameConfig;
            if (Draft.HasFormation
                && config.Formations.TryGetValue(Draft.Formation, out FormationInfo formation)
                && Draft.IsComplete(formation))
            {
                return DraftEngine.ComputeLines(Draft, formation, ResolveDraftPlayer);
            }

            // Baseline scratch XI for managers who haven't finished a draft yet.
            return new LineRatings { Attack = 72, Midfield = 72, Defence = 72, Goalkeeping = 70, Chemistry = 0 };
        }

        /// <summary>
        /// Resolves a drafted player id to its record. Checks the legend corpus first, then the World Cup squads,
        /// so a single <see cref="DraftedSquad"/> can hold a legend XI (Draft Cup / matches) OR a World-Cup XI.
        /// </summary>
        public LegendPlayer ResolveDraftPlayer(LegendId id)
        {
            SharedGameConfig config = GameConfig;
            if (config.Legends.TryGetValue(id, out LegendPlayer legend))
                return legend;
            if (config.WorldCupPlayers.TryGetValue(id, out LegendPlayer wc))
                return wc;
            return null;
        }

        /// <summary>
        /// Records the manager's current drafted XI as it locks into a knockout: scouts each player into the
        /// collection and tracks the all-time best-XI overall. Called at the Drafting→Active transition of the
        /// Draft Cup / World Cup so the profile + My Club fill up as the manager plays.
        /// </summary>
        public void RecordDraftedXiForPlay()
        {
            foreach ((int _, LegendId id) in Draft.Picks)
            {
                LegendPlayer p = ResolveDraftPlayer(id);
                if (p != null)
                    Collection.Record(p.Id.Value);
            }
            LineRatings r = ResolveDraftRatings();
            int ovr = System.Math.Clamp((r.Attack * 3 + r.Midfield * 3 + r.Defence * 3 + r.Goalkeeping) / 10, 0, 99);
            if (ovr > Honours.BestDraftedXiOvr)
                Honours.BestDraftedXiOvr = ovr;
        }

        /// <summary> The total dice "strength" (sum of die sides) of the manager's current selected squad. </summary>
        public int SelectedSquadStrength()
        {
            SharedGameConfig config = GameConfig;
            TeamInfo team = null;
            if (SelectedTeamId != null)
                config.Teams.TryGetValue(TeamId.FromString(SelectedTeamId), out team);
            if (team == null)
                team = TeamContent.Teams[0];
            return SquadBuilder.TotalSides(SquadBuilder.Build(team, SquadBook, config.Global));
        }

        /// <summary> Records a finished match in the lifetime stats (matches played/won and win streaks). </summary>
        public void RecordMatchResult(bool won)
        {
            Progression.MatchesPlayed++;
            if (won)
            {
                Progression.MatchesWon++;
                Progression.CurrentWinStreak++;
                if (Progression.CurrentWinStreak > Progression.BestWinStreak)
                    Progression.BestWinStreak = Progression.CurrentWinStreak;
            }
            else
            {
                Progression.CurrentWinStreak = 0;
            }
        }
    }
}
