// FOOTDRAFT — singleton season-league registry. Holds private leagues (≤20 managers) by invite code; runs a
// turn-based snake DRAFT from a shared legend pool, then the double round-robin; resolves each fixture via
// MatchSim against the opponent's drafted XI.
//
// The headline flow: create → friends join by code → commissioner starts the DRAFT → managers take turns picking
// legends from ONE shared pool (so no legend can be on two teams), each into a valid formation → once every XI is
// full the fixtures are generated and the season can be simulated. All draft state lives here in actor memory;
// clients drive it via EntityAsks (relayed by their PlayerActor) and cache a LeagueSnapshot for display.

using Game.Logic;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Game.Server
{
    [EntityConfig]
    public class LeagueConfig : PersistedEntityConfig
    {
        public override EntityKind        EntityKind           => EntityKindGame.League;
        public override Type              EntityActorType      => typeof(LeagueActor);
        public override NodeSetPlacement  NodeSetPlacement     => NodeSetPlacement.Service;
        public override IShardingStrategy ShardingStrategy     => ShardingStrategies.CreateSingletonService();
        public override TimeSpan          ShardShutdownTimeout => TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Singleton service holding private season leagues. Managers create/join by invite code; the commissioner
    /// starts the draft, everyone takes turns drafting their XI from the shared legend pool, then a matchday is
    /// simulated each day. State is held in memory and snapshotted to the DB (<see cref="PersistedLeagueRegistry"/>)
    /// so multi-day seasons survive server restarts and redeploys.
    /// </summary>
    public class LeagueActor : PersistedEntityActor<PersistedLeagueRegistry, LeagueRegistryModel>
    {
        public static readonly EntityId LeagueEntityId = EntityId.Create(EntityKindGame.League, 0);

        public const string DefaultFormation = "4-3-3"; // hard fallback when the config / formation lookup misses.

        // League size, the daily sim hour, the default formation and the scripted matchday events now live in
        // game config (LeagueDefinition) instead of as constants here — so they're tunable, and dashboard-editable
        // once the config is Google-Sheet-backed, with no redeploy. The singleton reads the "default" definition
        // from the server's active baseline game config (via the global state proxy).
        LeagueDefinition DefaultDef()
        {
            ActiveGameConfig active = GlobalStateProxyActor.ActiveGameConfig.Get();
            if (active?.BaselineGameConfig?.SharedConfig is SharedGameConfig shared
                && shared.LeagueDefinitions.TryGetValue(LeagueDefinitionId.FromString(LeagueDefinitionContent.DefaultId), out LeagueDefinition def))
                return def;
            return LeagueDefinitionContent.Default; // fallback before the config is loaded / if the entry is missing
        }

        /// <summary> Whether a league's transfer window is currently open (admin override wins; else the daily schedule). </summary>
        bool IsTransferWindowOpen(LeagueReg reg, LeagueDefinition def)
        {
            if (reg.State != LeagueState.Active) return false;
            if (reg.TransferWindowOverride == 1) return true;   // admin forced open
            if (reg.TransferWindowOverride == 2) return false;  // admin forced closed
            return def.IsTransferWindowHour(MetaTime.Now.ToDateTime().Hour);
        }

        // CPU team names used to fill a league up to 20 (kept fictional — only the player corpus uses real names).
        static readonly string[] BotNames =
        {
            "CPU Athletic", "CPU Rovers", "CPU United", "CPU City", "CPU Wanderers", "CPU Albion",
            "CPU Town", "CPU County", "CPU Rangers", "CPU Athletico", "CPU Olympic", "CPU Dynamo",
            "CPU Sporting", "CPU Real", "CPU Inter", "CPU Forest", "CPU Villa", "CPU Palace", "CPU Orient",
        };
        static string BotName(int botIndex)  => BotNames[botIndex % BotNames.Length];
        static string BotCrest(int botIndex) => "🤖";

        // ---- Static legend + formation content (the corpus the draft picks from). Built once. ----
        static readonly List<LegendPlayer>               CorpusList    = new List<LegendPlayer>(LegendContent.Legends);
        static readonly Dictionary<string, LegendPlayer> CorpusById    = BuildCorpusIndex();
        static readonly Dictionary<string, FormationInfo> FormationById = BuildFormationIndex();
        static readonly Func<LegendId, LegendPlayer>     LegendLookup  = id => CorpusById.TryGetValue(id.Value, out LegendPlayer p) ? p : null;
        // The league draft pool: one entry per real player (used for bot/auto picks — no two teams hold the same person).
        static readonly List<LegendPlayer>               DraftPool     = LeagueDraftEngine.BuildDraftPool(LegendContent.Legends);
        // (Club, Season) → that squad's players, for the spin wheel.
        static readonly Dictionary<string, List<LegendPlayer>> SeasonSquads = BuildSeasonSquads();
        static readonly List<string>                     SeasonKeys    = new List<string>(SeasonSquads.Keys);
        // (Club, Season) → (sum of squad OVRs, player count), for the Gem-paid "elite spin" tier filter.
        static readonly Dictionary<string, (long SumOvr, int Count)> SquadOvrTotals = BuildSquadOvrTotals();

        static string SeasonKey(string club, string season) => $"{club}||{season}";

        static Dictionary<string, (long, int)> BuildSquadOvrTotals()
        {
            Dictionary<string, (long, int)> map = new Dictionary<string, (long, int)>();
            foreach ((string key, List<LegendPlayer> squad) in SeasonSquads)
            {
                long sum = 0;
                foreach (LegendPlayer p in squad)
                    sum += p.Ovr;
                map[key] = (sum, squad.Count);
            }
            return map;
        }

        /// <summary> Club-seasons whose squad average OVR is at least <paramref name="minAvgOvr"/> (integer math — no floats on an actor that feeds checksummed state). </summary>
        static List<string> EliteSeasonKeys(int minAvgOvr)
        {
            List<string> keys = new List<string>();
            foreach (string key in SeasonKeys)
            {
                (long sumOvr, int count) = SquadOvrTotals[key];
                if (count > 0 && sumOvr >= (long)minAvgOvr * count)
                    keys.Add(key);
            }
            return keys;
        }

        static Dictionary<string, List<LegendPlayer>> BuildSeasonSquads()
        {
            Dictionary<string, List<LegendPlayer>> map = new Dictionary<string, List<LegendPlayer>>();
            foreach (LegendPlayer p in CorpusList)
            {
                string key = SeasonKey(p.Club, p.Season);
                if (!map.TryGetValue(key, out List<LegendPlayer> list))
                {
                    list = new List<LegendPlayer>();
                    map[key] = list;
                }
                list.Add(p);
            }
            return map;
        }

        static Dictionary<string, LegendPlayer> BuildCorpusIndex()
        {
            Dictionary<string, LegendPlayer> map = new Dictionary<string, LegendPlayer>();
            foreach (LegendPlayer p in CorpusList)
                map[p.Id.Value] = p;
            return map;
        }

        static Dictionary<string, FormationInfo> BuildFormationIndex()
        {
            Dictionary<string, FormationInfo> map = new Dictionary<string, FormationInfo>();
            foreach (FormationInfo f in FormationContent.Formations)
                map[f.Id.Value] = f;
            return map;
        }

        static LegendPlayer  LookupLegend(string id)    => CorpusById.TryGetValue(id ?? "", out LegendPlayer p) ? p : null;
        static FormationInfo FormationInfoById(string id) => FormationById.TryGetValue(id ?? "", out FormationInfo f) ? f : FormationById[DefaultFormation];

        sealed class Member
        {
            public EntityId Id;
            public string   Name;
            public string   Crest;
            public bool     IsBot;
            public long     TransferBudget; // Coins this manager can spend on transfers (humans only; granted at season lock)
            public Member(EntityId id, string name, string crest, bool isBot = false) { Id = id; Name = name; Crest = crest; IsBot = isBot; }
        }

        /// <summary> A pending P2P trade: proposer (FromIndex) gives a player + Coins for the target's (ToIndex) player. </summary>
        sealed class TradeOffer
        {
            public int      OfferId;
            public int      FromIndex;
            public int      ToIndex;
            public string   GiveLegendId;
            public string   GetLegendId;
            public int      Coins;
            public MetaTime ExpiresAt;
        }

        sealed class LeagueReg
        {
            public string                     Code;
            public string                     Name;
            public EntityId                   Commissioner;
            public LeagueState                State = LeagueState.Lobby;
            public readonly List<Member>      Members  = new List<Member>();
            public List<List<LeagueFixture>>  Fixtures = new List<List<LeagueFixture>>();
            public readonly List<LeagueResult> Results = new List<LeagueResult>();
            public readonly HashSet<(int, int)> Played = new HashSet<(int, int)>();
            public readonly Dictionary<int, LineRatings> Locked = new Dictionary<int, LineRatings>();

            // ---- Draft state (valid while State == Drafting; rosters survive into the season). ----
            public int                                     DraftPick;                                      // global pick counter (0-based)
            public readonly Dictionary<int, string>        Formations = new Dictionary<int, string>();      // memberIndex → formation id value
            public readonly Dictionary<int, Dictionary<int, string>> Rosters = new Dictionary<int, Dictionary<int, string>>(); // memberIndex → (slot → legend id)
            public readonly HashSet<string>                Taken      = new HashSet<string>();              // player NAMES drafted by ANYONE (uniqueness)
            // The current drafter's pending spin (a Club × Season squad to pick from); cleared on pick. Transient.
            public string                                  SpinClub;
            public string                                  SpinSeason;

            // ---- Season schedule (valid while State == Active/Finished): one matchday simmed per day. ----
            public int                  CurrentMatchday;                                  // matchdays played so far
            public MetaTime             NextSimTime;                                       // when the next matchday auto-sims
            public int                  LastMatchdayNumber;                                // 1-based; 0 = none yet
            public readonly List<string> LastMatchdayLines = new List<string>();           // scorelines of the last matchday

            // Admin transfer-window override: 0 = follow the daily schedule, 1 = force open, 2 = force closed.
            public int                  TransferWindowOverride;

            // Squad-building rules, chosen at creation: hard mode (hide ratings), max players per club, OVR caps.
            public bool                 HideRatings;
            public int                  MaxPerClub = 1;                 // 0 = no club limit, 1 = one-per-club, N = max N
            public string               CapBands   = "90:2,80:3,75:4";  // "" = no caps
            public string               DraftPin   = "";                // pin-draft filter ("era:E2010s,elite:1"); "" = none

            // ---- P2P trades (pending offers between two managers; cash escrowed from the proposer). ----
            public readonly List<TradeOffer> TradeOffers = new List<TradeOffer>();
            public int                       NextTradeOfferId;

            public LeagueReg(string code, string name, EntityId commissioner)
            {
                Code = code; Name = name; Commissioner = commissioner;
            }

            public int IndexOf(EntityId id) => Members.FindIndex(m => m.Id == id);
            public int TotalFixtures => Members.Count * (Members.Count - 1);
            public int FixturesPerTeam => 2 * (Members.Count - 1);

            public Dictionary<int, string> RosterFor(int index)
            {
                if (!Rosters.TryGetValue(index, out Dictionary<int, string> roster))
                {
                    roster = new Dictionary<int, string>();
                    Rosters[index] = roster;
                }
                return roster;
            }

            public string FormationValueFor(int index) => Formations.TryGetValue(index, out string f) ? f : DefaultFormation;
        }

        readonly Dictionary<string, LeagueReg> _leagues = new Dictionary<string, LeagueReg>();

        sealed class KeepAliveCommand { public static readonly KeepAliveCommand Instance = new KeepAliveCommand(); }

        // Snapshot the registry to the DB every 30s (+ a final snapshot on shutdown) so a multi-day season survives
        // restarts/redeploys. Never auto-shut down — this is the singleton league service.
        protected override TimeSpan           SnapshotInterval => TimeSpan.FromSeconds(30);
        protected override AutoShutdownPolicy ShutdownPolicy   => AutoShutdownPolicy.ShutdownNever();

        protected override async Task Initialize()
        {
            PersistedLeagueRegistry persisted = await MetaDatabase.Get().TryGetAsync<PersistedLeagueRegistry>(_entityId.ToString());
            await InitializePersisted(persisted);
            // Drive the daily matchday cadence: auto-sim due matchdays (and catch up any missed during downtime).
            StartPeriodicTimer(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30), KeepAliveCommand.Instance);
        }

        protected override Task<LeagueRegistryModel> InitializeNew()
            => Task.FromResult(new LeagueRegistryModel());

        protected override Task<LeagueRegistryModel> RestoreFromPersisted(PersistedLeagueRegistry persisted)
            => Task.FromResult(DeserializePersistedPayload<LeagueRegistryModel>(persisted.Payload, resolver: null, logicVersion: null));

        protected override Task PostLoad(LeagueRegistryModel payload, DateTime persistedAt, TimeSpan elapsedTime)
        {
            LoadRegistry(payload);
            return Task.CompletedTask;
        }

        protected override async Task PersistStateImpl(bool isInitial, bool isFinal)
        {
            byte[] payload = SerializeToPersistedPayload(SaveRegistry(), resolver: null, logicVersion: null);
            PersistedLeagueRegistry persisted = new PersistedLeagueRegistry
            {
                EntityId      = _entityId.ToString(),
                PersistedAt   = DateTime.UtcNow,
                Payload       = payload,
                SchemaVersion = CurrentSchemaVersion,
                IsFinal       = isFinal,
            };
            await MetaDatabase.Get().InsertOrUpdateAsync(persisted).ConfigureAwait(false);
        }

        /// <summary> Rebuilds the in-memory registry from a persisted snapshot (deriving fixtures/played/taken/ratings). </summary>
        void LoadRegistry(LeagueRegistryModel model)
        {
            _leagues.Clear();
            if (model?.Leagues == null)
                return;
            foreach (PersistedLeague pl in model.Leagues)
            {
                LeagueReg reg = new LeagueReg(pl.Code, pl.Name, pl.Commissioner)
                {
                    State                  = pl.State,
                    DraftPick              = pl.DraftPick,
                    CurrentMatchday        = pl.CurrentMatchday,
                    NextSimTime            = pl.NextSimTime,
                    LastMatchdayNumber     = pl.LastMatchdayNumber,
                    TransferWindowOverride = pl.TransferWindowOverride,
                    HideRatings            = pl.HideRatings,
                    MaxPerClub             = pl.MaxPerClub > 0 ? pl.MaxPerClub : (pl.NoSameClub ? 1 : 0),
                    CapBands               = !string.IsNullOrEmpty(pl.CapBands) ? pl.CapBands : (pl.SquadCaps ? DefaultDef().OvrCapBands : ""),
                    DraftPin               = pl.DraftPin ?? "",
                };
                reg.LastMatchdayLines.AddRange(pl.LastMatchdayLines);
                reg.NextTradeOfferId = pl.NextTradeOfferId;
                if (pl.TradeOffers != null)
                    foreach (PersistedTradeOffer t in pl.TradeOffers)
                        reg.TradeOffers.Add(new TradeOffer
                        {
                            OfferId = t.OfferId, FromIndex = t.FromIndex, ToIndex = t.ToIndex,
                            GiveLegendId = t.GiveLegendId, GetLegendId = t.GetLegendId, Coins = t.Coins, ExpiresAt = t.ExpiresAt,
                        });
                for (int i = 0; i < pl.Members.Count; i++)
                {
                    PersistedLeagueMember pm = pl.Members[i];
                    reg.Members.Add(new Member(pm.Id, pm.Name, pm.Crest, pm.IsBot) { TransferBudget = pm.TransferBudget });
                    if (!string.IsNullOrEmpty(pm.Formation))
                        reg.Formations[i] = pm.Formation;
                    Dictionary<int, string> roster = reg.RosterFor(i);
                    foreach ((int slot, string legendId) in pm.Roster)
                    {
                        roster[slot] = legendId;
                        reg.Taken.Add(LookupLegend(legendId)?.Name ?? legendId); // uniqueness is by player name
                    }
                }
                reg.Results.AddRange(pl.Results);
                foreach (LeagueResult r in pl.Results)
                    reg.Played.Add((r.HomeIndex, r.AwayIndex));
                // Re-derive locked line-ratings from each drafted roster.
                for (int i = 0; i < reg.Members.Count; i++)
                {
                    if (reg.RosterFor(i).Count > 0)
                        reg.Locked[i] = LeagueDraftEngine.ResolveRosterRatings(FormationInfoById(reg.FormationValueFor(i)), reg.RosterFor(i), LegendLookup);
                }
                // Regenerate fixtures for started seasons (deterministic).
                if (reg.State == LeagueState.Active || reg.State == LeagueState.Finished)
                    reg.Fixtures = LeagueEngine.GenerateDoubleRoundRobin(reg.Members.Count);
                _leagues[Key(pl.Code)] = reg;
            }
            if (_leagues.Count > 0)
                _log.Info("Restored {Count} league(s) from the persisted registry", _leagues.Count);
        }

        /// <summary> Builds a persistable snapshot of the whole in-memory registry. </summary>
        LeagueRegistryModel SaveRegistry()
        {
            LeagueRegistryModel model = new LeagueRegistryModel();
            foreach (LeagueReg reg in _leagues.Values)
            {
                // Only in-progress leagues need to survive a restart; a completed season doesn't, and skipping
                // them keeps the persisted blob small as leagues accumulate.
                if (reg.State == LeagueState.Finished)
                    continue;
                PersistedLeague pl = new PersistedLeague
                {
                    Code                   = reg.Code,
                    Name                   = reg.Name,
                    Commissioner           = reg.Commissioner,
                    State                  = reg.State,
                    DraftPick              = reg.DraftPick,
                    CurrentMatchday        = reg.CurrentMatchday,
                    NextSimTime            = reg.NextSimTime,
                    LastMatchdayNumber     = reg.LastMatchdayNumber,
                    TransferWindowOverride = reg.TransferWindowOverride,
                    HideRatings            = reg.HideRatings,
                    MaxPerClub             = reg.MaxPerClub,
                    CapBands               = reg.CapBands,
                    NoSameClub             = reg.MaxPerClub >= 1,
                    SquadCaps              = !string.IsNullOrEmpty(reg.CapBands),
                    DraftPin               = reg.DraftPin ?? "",
                    NextTradeOfferId       = reg.NextTradeOfferId,
                };
                foreach (TradeOffer t in reg.TradeOffers)
                    pl.TradeOffers.Add(new PersistedTradeOffer
                    {
                        OfferId = t.OfferId, FromIndex = t.FromIndex, ToIndex = t.ToIndex,
                        GiveLegendId = t.GiveLegendId, GetLegendId = t.GetLegendId, Coins = t.Coins, ExpiresAt = t.ExpiresAt,
                    });
                pl.LastMatchdayLines.AddRange(reg.LastMatchdayLines);
                for (int i = 0; i < reg.Members.Count; i++)
                {
                    Member m = reg.Members[i];
                    PersistedLeagueMember pm = new PersistedLeagueMember
                    {
                        Id             = m.Id,
                        Name           = m.Name,
                        Crest          = m.Crest,
                        IsBot          = m.IsBot,
                        Formation      = reg.FormationValueFor(i),
                        TransferBudget = m.TransferBudget,
                    };
                    foreach ((int slot, string legendId) in reg.RosterFor(i))
                        pm.Roster[slot] = legendId;
                    pl.Members.Add(pm);
                }
                pl.Results.AddRange(reg.Results);
                model.Leagues.Add(pl);
            }
            return model;
        }

        // Fires on the periodic timer: auto-sim any due matchdays (the daily 7pm cadence). The while-loop catches
        // up if several days' worth came due at once (e.g. after a gap), capped well above a full season.
        [CommandHandler]
        void HandleKeepAlive(KeepAliveCommand _)
        {
            MetaTime now = MetaTime.Now;
            LeagueDefinition def = DefaultDef();
            bool advancedAny = false;
            foreach (LeagueReg reg in _leagues.Values)
            {
                int guard = 0;
                while (reg.State == LeagueState.Active && now >= reg.NextSimTime && guard++ < reg.Fixtures.Count + 2)
                {
                    AdvanceOneMatchday(reg, def);
                    advancedAny = true;
                }
            }
            if (advancedAny)
                SchedulePersistState(); // persist the day's results promptly
        }

        static string Key(string code) => (code ?? "").Trim().ToUpperInvariant();

        LeagueReg Find(string code) => _leagues.TryGetValue(Key(code), out LeagueReg reg) ? reg : null;

        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueCreateRequest(EntityId playerId, LeagueCreateRequest request)
        {
            string key = Key(request.Code);
            if (string.IsNullOrEmpty(key))
                return new LeagueOpResponse("Invalid code", null);
            if (_leagues.ContainsKey(key))
                return new LeagueOpResponse("That code is taken — try another", null);

            LeagueReg reg = new LeagueReg(request.Code.Trim().ToUpperInvariant(), (request.LeagueName ?? "").Trim(), playerId);
            reg.HideRatings = request.HideRatings; // squad-building rules, chosen on the create screen
            reg.MaxPerClub  = request.MaxPerClub;
            reg.CapBands    = request.CapBands ?? "";
            reg.DraftPin    = request.DraftPin ?? "";
            reg.Members.Add(new Member(playerId, request.PlayerName, request.Crest ?? "⚽"));
            _leagues[key] = reg;

            // Single-player season: no lobby — the sole manager drafts immediately, and the league pads to a
            // full 20 with CPU teams at season lock (FinalizeDraft), exactly like a part-filled friends league.
            if (request.Solo)
                BeginDraft(reg);

            SchedulePersistState();
            return new LeagueOpResponse("", Snapshot(reg, playerId));
        }

        /// <summary> Lobby → Drafting: initialise the snake draft (default formations, empty rosters, clear pool). </summary>
        void BeginDraft(LeagueReg reg)
        {
            reg.State     = LeagueState.Drafting;
            reg.DraftPick = 0;
            reg.Taken.Clear();
            string defaultFormation = DefaultDef().DefaultFormation;
            for (int i = 0; i < reg.Members.Count; i++)
            {
                if (!reg.Formations.ContainsKey(i))
                    reg.Formations[i] = defaultFormation;
                reg.Rosters[i] = new Dictionary<int, string>();
            }
        }

        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueJoinRequest(EntityId playerId, LeagueJoinRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null)
                return new LeagueOpResponse("No league with that code", null);
            if (reg.IndexOf(playerId) >= 0)
                return new LeagueOpResponse("", Snapshot(reg, playerId)); // already a member
            if (reg.State != LeagueState.Lobby)
                return new LeagueOpResponse("That season has already kicked off", null);
            int leagueSize = DefaultDef().LeagueSize;
            if (reg.Members.Count >= leagueSize)
                return new LeagueOpResponse($"League is full ({leagueSize} managers)", null);

            reg.Members.Add(new Member(playerId, request.PlayerName, request.Crest ?? "⚽"));
            SchedulePersistState();
            return new LeagueOpResponse("", Snapshot(reg, playerId));
        }

        [EntityAskHandler]
        public EntityAskOk HandleLeagueLeaveRequest(EntityId playerId, LeagueLeaveRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg != null && reg.State == LeagueState.Lobby)
            {
                int idx = reg.IndexOf(playerId);
                if (idx >= 0)
                {
                    reg.Members.RemoveAt(idx);
                    reg.Formations.Remove(idx);
                    reg.Rosters.Remove(idx);
                    if (reg.Members.Count == 0)
                        _leagues.Remove(Key(reg.Code));
                    else if (reg.Commissioner == playerId)
                        reg.Commissioner = reg.Members[0].Id; // hand the armband to the next member
                }
            }
            return EntityAskOk.Instance;
        }

        /// <summary>
        /// Commissioner starts the DRAFT (Lobby → Drafting). Initialises the snake draft: each member without a
        /// chosen formation defaults to <see cref="DefaultFormation"/>, rosters are emptied and the shared pool is
        /// cleared. Managers then take turns picking via <see cref="HandleLeagueDraftPickRequest"/>.
        /// </summary>
        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueStartRequest(EntityId playerId, LeagueStartRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null)
                return new LeagueOpResponse("No league with that code", null);
            if (reg.Commissioner != playerId)
                return new LeagueOpResponse("Only the commissioner can start the draft", null);
            if (reg.State != LeagueState.Lobby)
                return new LeagueOpResponse("Draft already started", null);
            if (reg.Members.Count < 2)
                return new LeagueOpResponse("Need at least 2 managers", null);

            BeginDraft(reg);
            SchedulePersistState();
            return new LeagueOpResponse("", Snapshot(reg, playerId));
        }

        /// <summary> Set the caller's drafting formation (in the lobby, or during the draft before their first pick). </summary>
        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueSetFormationRequest(EntityId playerId, LeagueSetFormationRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null)
                return new LeagueOpResponse("No league with that code", null);
            int myIndex = reg.IndexOf(playerId);
            if (myIndex < 0)
                return new LeagueOpResponse("You're not in this league", null);
            if (!FormationById.ContainsKey(request.FormationId ?? ""))
                return new LeagueOpResponse("Unknown formation", null);
            if (reg.State == LeagueState.Drafting && reg.RosterFor(myIndex).Count > 0)
                return new LeagueOpResponse("Too late to change formation — you've already started drafting", null);
            if (reg.State != LeagueState.Lobby && reg.State != LeagueState.Drafting)
                return new LeagueOpResponse("The season has already kicked off", null);

            reg.Formations[myIndex] = request.FormationId;
            SchedulePersistState();
            return new LeagueOpResponse("", Snapshot(reg, playerId));
        }

        /// <summary>
        /// The spin pool for a league's pin-draft House Rule: <see cref="SeasonKeys"/> filtered by era and/or
        /// elite clubs. Falls back to the full pool if the filter would leave nothing (config/data safety).
        /// </summary>
        List<string> PoolForPin(string pin)
        {
            (string era, bool elite) = LeaguePin.Parse(pin);
            if (string.IsNullOrEmpty(era) && !elite)
                return SeasonKeys;
            HashSet<string> eliteSet = elite ? new HashSet<string>(EliteSeasonKeys(DefaultDef().EliteSpinMinAvgOvr)) : null;
            List<string> keys = new List<string>();
            foreach (string key in SeasonKeys)
            {
                if (!string.IsNullOrEmpty(era))
                {
                    List<LegendPlayer> squad = SeasonSquads[key];
                    if (squad.Count == 0 || squad[0].Era.ToString() != era)
                        continue;
                }
                if (eliteSet != null && !eliteSet.Contains(key))
                    continue;
                keys.Add(key);
            }
            return keys.Count > 0 ? keys : SeasonKeys;
        }

        /// <summary> Spin the wheel: roll a random Club × Season squad for the current drafter to pick a player from. </summary>
        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueSpinRequest(EntityId playerId, LeagueSpinRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null)
                return new LeagueOpResponse("No league with that code", null);
            if (reg.State != LeagueState.Drafting)
                return new LeagueOpResponse("The draft isn't running", null);

            int myIndex = reg.IndexOf(playerId);
            if (myIndex < 0)
                return new LeagueOpResponse("You're not in this league", null);
            int current = LeagueDraftEngine.CurrentDrafterIndex(reg.DraftPick, reg.Members.Count);
            if (current != myIndex)
                return new LeagueOpResponse("It's not your turn", null);
            if (SeasonKeys.Count == 0)
                return new LeagueOpResponse("No squads available", null);

            // Elite spin (Gem-paid, charged by the PlayerAction): restrict the roll to top-tier club-seasons.
            // Fall back to the full pool if the config threshold leaves the filter empty (config safety).
            // Base pool respects the league's pin-draft rule (era / elite-clubs); a Gem-paid elite spin then
            // narrows to elite club-seasons within that pinned pool.
            List<string> pool = PoolForPin(reg.DraftPin);
            if (request.Elite)
            {
                HashSet<string> elite = new HashSet<string>(EliteSeasonKeys(DefaultDef().EliteSpinMinAvgOvr));
                List<string> filtered = new List<string>();
                foreach (string k in pool)
                    if (elite.Contains(k))
                        filtered.Add(k);
                if (filtered.Count > 0)
                    pool = filtered;
            }

            // Server-rolled (cheat-proof) random squad.
            Metaplay.Core.RandomPCG rng = Metaplay.Core.RandomPCG.CreateNew();
            List<LegendPlayer> squad = SeasonSquads[pool[rng.NextInt(pool.Count)]];
            reg.SpinClub   = squad[0].Club;
            reg.SpinSeason = squad[0].Season;
            return new LeagueOpResponse("", Snapshot(reg, playerId));
        }

        /// <summary> Pick a player from the squad you just spun: enforces turn, spin, uniqueness (by name) and an open slot. </summary>
        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueDraftPickRequest(EntityId playerId, LeagueDraftPickRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null)
                return new LeagueOpResponse("No league with that code", null);
            if (reg.State != LeagueState.Drafting)
                return new LeagueOpResponse("The draft isn't running", null);

            int myIndex = reg.IndexOf(playerId);
            if (myIndex < 0)
                return new LeagueOpResponse("You're not in this league", null);

            int current = LeagueDraftEngine.CurrentDrafterIndex(reg.DraftPick, reg.Members.Count);
            if (current != myIndex)
                return new LeagueOpResponse("It's not your turn to pick", null);

            LegendPlayer legend = LookupLegend(request.LegendId);
            if (legend == null)
                return new LeagueOpResponse("Unknown player", null);
            if (string.IsNullOrEmpty(reg.SpinClub))
                return new LeagueOpResponse("Spin the wheel first", null);
            if (legend.Club != reg.SpinClub || legend.Season != reg.SpinSeason)
                return new LeagueOpResponse("That player isn't in your spun squad", null);
            if (reg.Taken.Contains(legend.Name))
                return new LeagueOpResponse($"{legend.Name} is already on another team", null);

            FormationInfo formation = FormationInfoById(reg.FormationValueFor(myIndex));
            if (LeagueDraftEngine.NextOpenSlotForPosition(formation, reg.RosterFor(myIndex), legend.Position) < 0)
                return new LeagueOpResponse($"Your formation has no open {legend.Position} slot", null);

            string ruleErr = LeagueDraftEngine.SquadRuleError(reg.RosterFor(myIndex), LegendLookup, reg.MaxPerClub, LeagueDefinition.ParseOvrCaps(reg.CapBands), legend);
            if (!string.IsNullOrEmpty(ruleErr))
                return new LeagueOpResponse(ruleErr, null);

            ApplyPick(reg, myIndex, legend, formation);
            reg.SpinClub = null; reg.SpinSeason = null; // consumed — next drafter spins fresh
            if (LeagueDraftEngine.IsComplete(reg.DraftPick, reg.Members.Count))
                FinalizeDraft(reg, DefaultDef());
            SchedulePersistState();
            return new LeagueOpResponse("", Snapshot(reg, playerId));
        }

        /// <summary>
        /// Auto-pick the best available legend. With <see cref="LeagueAutoPickRequest.FillAll"/> the commissioner
        /// auto-drafts the entire rest of the league (handy for demos / absent friends); otherwise it auto-picks a
        /// single legend for the current drafter (callable by that drafter, or by the commissioner to unstall).
        /// </summary>
        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueAutoPickRequest(EntityId playerId, LeagueAutoPickRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null)
                return new LeagueOpResponse("No league with that code", null);
            if (reg.State != LeagueState.Drafting)
                return new LeagueOpResponse("The draft isn't running", null);

            int myIndex = reg.IndexOf(playerId);
            if (myIndex < 0)
                return new LeagueOpResponse("You're not in this league", null);

            if (request.FillAll)
            {
                if (reg.Commissioner != playerId)
                    return new LeagueOpResponse("Only the commissioner can auto-draft the league", null);
                int guard = 0;
                while (!LeagueDraftEngine.IsComplete(reg.DraftPick, reg.Members.Count) && guard++ < LeagueDraftEngine.TotalPicks(reg.Members.Count) + 5)
                {
                    if (!AutoPickCurrent(reg))
                        break; // pool exhausted (shouldn't happen with the full corpus)
                }
            }
            else
            {
                int current = LeagueDraftEngine.CurrentDrafterIndex(reg.DraftPick, reg.Members.Count);
                if (current != myIndex && reg.Commissioner != playerId)
                    return new LeagueOpResponse("It's not your turn to pick", null);
                if (!AutoPickCurrent(reg))
                    return new LeagueOpResponse("No available players for an open slot", null);
            }

            reg.SpinClub = null; reg.SpinSeason = null; // auto-pick bypasses any pending spin
            if (LeagueDraftEngine.IsComplete(reg.DraftPick, reg.Members.Count))
                FinalizeDraft(reg, DefaultDef());
            SchedulePersistState();
            return new LeagueOpResponse("", Snapshot(reg, playerId));
        }

        /// <summary> Commissioner: simulate every remaining fixture at once and finish the season. </summary>
        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueSimulateRequest(EntityId playerId, LeagueSimulateRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null)
                return new LeagueOpResponse("No league with that code", null);
            if (reg.Commissioner != playerId)
                return new LeagueOpResponse("Only the commissioner can simulate the season", null);
            if (reg.State != LeagueState.Active)
                return new LeagueOpResponse("The season isn't ready to simulate", null);

            // Play out every remaining matchday at once (skips the daily wait).
            LeagueDefinition def = DefaultDef();
            int guard = 0;
            while (reg.State == LeagueState.Active && guard++ < reg.Fixtures.Count + 2)
                AdvanceOneMatchday(reg, def);
            SchedulePersistState();
            return new LeagueOpResponse("", Snapshot(reg, playerId));
        }

        /// <summary> Admin force: play the next matchday immediately (rather than waiting for the daily 7pm sim). </summary>
        [EntityAskHandler]
        public LeaguePlayResponse HandleLeaguePlayRequest(EntityId playerId, LeaguePlayRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null)
                return new LeaguePlayResponse("No league with that code", null, null);
            if (reg.Commissioner != playerId)
                return new LeaguePlayResponse("Only the admin can advance the matchday", null, null);
            if (reg.State != LeagueState.Active)
                return new LeaguePlayResponse("Season isn't running", null, null);

            int playedMatchday = reg.CurrentMatchday; // index into Fixtures of the matchday about to play
            AdvanceOneMatchday(reg, DefaultDef());
            SchedulePersistState();
            return new LeaguePlayResponse("", MyFixtureResult(reg, playerId, playedMatchday), Snapshot(reg, playerId));
        }

        /// <summary> The caller's own scoreline from the given matchday (drives the client's match cinematic), or null. </summary>
        static LeaguePlayResult MyFixtureResult(LeagueReg reg, EntityId playerId, int matchdayIndex)
        {
            int myIndex = reg.IndexOf(playerId);
            if (myIndex < 0 || matchdayIndex < 0 || matchdayIndex >= reg.Fixtures.Count)
                return null;
            foreach (LeagueFixture f in reg.Fixtures[matchdayIndex])
            {
                if (f.HomeIndex != myIndex && f.AwayIndex != myIndex)
                    continue;
                // The result was just appended — search from the end for this pairing.
                for (int i = reg.Results.Count - 1; i >= 0; i--)
                {
                    LeagueResult r = reg.Results[i];
                    if (r.HomeIndex != f.HomeIndex || r.AwayIndex != f.AwayIndex)
                        continue;
                    bool home = f.HomeIndex == myIndex;
                    int myGoals  = home ? r.HomeGoals : r.AwayGoals;
                    int oppGoals = home ? r.AwayGoals : r.HomeGoals;
                    string oppName = reg.Members[home ? f.AwayIndex : f.HomeIndex].Name;
                    // My scorers (the goals on my side of the fixture), for the flavour report.
                    List<string> mine = new List<string>();
                    if (r.Goals != null)
                        foreach (MatchGoalDetail g in r.Goals)
                            if (g.HomeSide == home && !string.IsNullOrEmpty(g.Scorer))
                                mine.Add($"{Surname(g.Scorer)} {g.Minute}'");
                    ulong reportSeed = (ulong)(matchdayIndex + 1) * 2654435761ul ^ (ulong)(myIndex + 1);
                    return new LeaguePlayResult
                    {
                        OpponentName = oppName,
                        Home         = home,
                        MyGoals      = myGoals,
                        OppGoals     = oppGoals,
                        Outcome      = myGoals > oppGoals ? 1 : myGoals < oppGoals ? -1 : 0,
                        Goals        = r.Goals, // named scorers, in fixture-home perspective (client maps via Home)
                        Report       = MatchReport.Fixture(oppName, myGoals, oppGoals, string.Join(", ", mine), reportSeed),
                    };
                }
                return null;
            }
            return null;
        }

        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueGetSnapshotRequest(EntityId playerId, LeagueGetSnapshotRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null)
                return new LeagueOpResponse("No league with that code", null);
            // Read-only fetch (polled every few seconds by every client) — do NOT persist here.
            return new LeagueOpResponse("", Snapshot(reg, playerId));
        }

        // ---- Admin / dashboard handlers (LiveOps Dashboard "Season Leagues" page; auth is enforced by the controller) ----

        /// <summary> List every league in the registry (flat, admin-wide) for the dashboard. Read-only. </summary>
        [EntityAskHandler]
        public LeagueListResponse HandleLeagueListRequest(LeagueListRequest _)
        {
            LeagueRegistryListView view = new LeagueRegistryListView();
            foreach (LeagueReg reg in _leagues.Values)
            {
                bool started = reg.State == LeagueState.Active || reg.State == LeagueState.Finished;
                view.Leagues.Add(new LeagueRegistryEntryView
                {
                    Code            = reg.Code,
                    Name            = reg.Name,
                    State           = reg.State,
                    MemberCount     = reg.Members.Count,
                    HumanCount      = reg.Members.Count(m => !m.IsBot),
                    CurrentMatchday = reg.CurrentMatchday,
                    TotalMatchdays  = started ? reg.Fixtures.Count : 0,
                    NextSimAtMillis = reg.State == LeagueState.Active ? reg.NextSimTime.MillisecondsSinceEpoch : 0,
                });
            }
            return new LeagueListResponse(view);
        }

        /// <summary> Admin (non-member) snapshot of one league by code. Read-only. </summary>
        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueAdminSnapshotRequest(LeagueAdminSnapshotRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null)
                return new LeagueOpResponse("No league with that code", null);
            return new LeagueOpResponse("", Snapshot(reg, EntityId.None)); // EntityId.None → admin/spectator viewer
        }

        /// <summary> A manager swaps a drafted legend for another during an open transfer window (pays Coins from their budget). </summary>
        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueTransferSwapRequest(EntityId playerId, LeagueTransferSwapRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null)
                return new LeagueOpResponse("No league with that code", null);
            if (reg.State != LeagueState.Active)
                return new LeagueOpResponse("Transfers are only open during the season", null);
            int myIndex = reg.IndexOf(playerId);
            if (myIndex < 0)
                return new LeagueOpResponse("You're not in this league", null);

            LeagueDefinition def = DefaultDef();
            if (!IsTransferWindowOpen(reg, def))
                return new LeagueOpResponse("The transfer window is closed", null);

            LegendPlayer dropLegend = LookupLegend(request.DropLegendId);
            LegendPlayer addLegend  = LookupLegend(request.AddLegendId);
            FormationInfo formation = FormationInfoById(reg.FormationValueFor(myIndex));
            Dictionary<int, string> roster = reg.RosterFor(myIndex);

            // Affordability is the PlayerAction's job now: the fee was charged from the player's wallet before
            // this ask, and the PlayerActor refunds it if we reject here.
            string err = LeagueTransferEngine.ValidateSwap(formation, roster, reg.Taken, dropLegend, addLegend, out int slot);
            if (!string.IsNullOrEmpty(err))
                return new LeagueOpResponse(err, null);

            // Squad-building rules: check the candidate against the roster with the dropped player removed, so
            // a like-for-like swap (e.g. one 90+ for another, or same-club out/same-club in) stays legal.
            Dictionary<int, string> afterDrop = new Dictionary<int, string>(roster);
            afterDrop.Remove(slot);
            string ruleErr = LeagueDraftEngine.SquadRuleError(afterDrop, LegendLookup, reg.MaxPerClub, LeagueDefinition.ParseOvrCaps(reg.CapBands), addLegend);
            if (!string.IsNullOrEmpty(ruleErr))
                return new LeagueOpResponse(ruleErr, null);

            // Apply: free the dropped player, install the new one in the same slot, recompute ratings.
            reg.Taken.Remove(dropLegend.Name);
            roster[slot] = addLegend.Id.Value;
            reg.Taken.Add(addLegend.Name);
            reg.Locked[myIndex] = LeagueDraftEngine.ResolveRosterRatings(formation, roster, LegendLookup);

            SchedulePersistState();
            return new LeagueOpResponse("", Snapshot(reg, playerId));
        }

        // ---- P2P trades (player + cash) ----

        /// <summary> Propose a player(+cash)-for-player trade to another manager. The proposer's coins were escrowed
        /// from their wallet by the PlayerAction; the PlayerActor refunds if this rejects. </summary>
        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueTradeOfferRequest(EntityId playerId, LeagueTradeOfferRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null) return new LeagueOpResponse("No league with that code", null);
            if (reg.State != LeagueState.Active) return new LeagueOpResponse("Trades open once the season starts", null);
            SweepExpiredTrades(reg);

            int fromIdx = reg.IndexOf(playerId);
            if (fromIdx < 0) return new LeagueOpResponse("You're not in this league", null);
            int toIdx = request.ToIndex;
            if (toIdx < 0 || toIdx >= reg.Members.Count || toIdx == fromIdx) return new LeagueOpResponse("Pick another manager to trade with", null);
            if (reg.Members[toIdx].IsBot) return new LeagueOpResponse("You can only trade with human managers", null);

            LegendPlayer give = LookupLegend(request.GiveLegendId);
            LegendPlayer get  = LookupLegend(request.GetLegendId);
            string err = LeagueTradeEngine.ValidatePlayers(give, get);
            if (!string.IsNullOrEmpty(err)) return new LeagueOpResponse(err, null);

            Dictionary<int, string> fromRoster = reg.RosterFor(fromIdx);
            Dictionary<int, string> toRoster   = reg.RosterFor(toIdx);
            if (RosterSlotOf(fromRoster, give.Id.Value) < 0) return new LeagueOpResponse("You don't have that player", null);
            if (RosterSlotOf(toRoster, get.Id.Value) < 0)    return new LeagueOpResponse("They don't have that player", null);

            foreach (TradeOffer o in reg.TradeOffers)
                if (o.FromIndex == fromIdx && o.ToIndex == toIdx && o.GiveLegendId == give.Id.Value && o.GetLegendId == get.Id.Value)
                    return new LeagueOpResponse("You already have that offer pending", null);

            reg.TradeOffers.Add(new TradeOffer
            {
                OfferId = ++reg.NextTradeOfferId, FromIndex = fromIdx, ToIndex = toIdx,
                GiveLegendId = give.Id.Value, GetLegendId = get.Id.Value,
                Coins = System.Math.Max(0, request.Coins), ExpiresAt = MetaTime.Now + MetaDuration.FromHours(24),
            });
            SchedulePersistState();
            return new LeagueOpResponse("", Snapshot(reg, playerId));
        }

        /// <summary> Respond to a trade: the recipient accepts/rejects; the proposer cancels (Accept=false). </summary>
        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueTradeRespondRequest(EntityId playerId, LeagueTradeRespondRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null) return new LeagueOpResponse("No league with that code", null);
            int meIdx = reg.IndexOf(playerId);
            if (meIdx < 0) return new LeagueOpResponse("You're not in this league", null);

            TradeOffer offer = reg.TradeOffers.Find(o => o.OfferId == request.OfferId);
            if (offer == null) return new LeagueOpResponse("That offer is no longer available", null);

            // Proposer cancelling their own offer, or recipient rejecting → refund the proposer's escrow.
            if (meIdx == offer.FromIndex || (meIdx == offer.ToIndex && !request.Accept))
            {
                reg.TradeOffers.Remove(offer);
                RefundProposer(reg, offer);
                SchedulePersistState();
                return new LeagueOpResponse("", Snapshot(reg, playerId));
            }
            if (meIdx != offer.ToIndex) return new LeagueOpResponse("That's not your trade", null);

            // Accept: re-validate, swap the two players into each other's freed slots, pay the escrow to the recipient.
            LegendPlayer give = LookupLegend(offer.GiveLegendId);
            LegendPlayer get  = LookupLegend(offer.GetLegendId);
            Dictionary<int, string> fromRoster = reg.RosterFor(offer.FromIndex);
            Dictionary<int, string> toRoster   = reg.RosterFor(offer.ToIndex);
            int giveSlot = RosterSlotOf(fromRoster, offer.GiveLegendId);
            int getSlot  = RosterSlotOf(toRoster, offer.GetLegendId);
            if (!string.IsNullOrEmpty(LeagueTradeEngine.ValidatePlayers(give, get)) || giveSlot < 0 || getSlot < 0)
            {
                reg.TradeOffers.Remove(offer);
                RefundProposer(reg, offer);
                SchedulePersistState();
                return new LeagueOpResponse("That trade is no longer valid — the offer was withdrawn", Snapshot(reg, playerId));
            }

            // Taken (league-wide uniqueness by name) is unchanged — both names stay taken, by the other manager now.
            fromRoster[giveSlot] = offer.GetLegendId;
            toRoster[getSlot]    = offer.GiveLegendId;
            reg.Locked[offer.FromIndex] = LeagueDraftEngine.ResolveRosterRatings(FormationInfoById(reg.FormationValueFor(offer.FromIndex)), fromRoster, LegendLookup);
            reg.Locked[offer.ToIndex]   = LeagueDraftEngine.ResolveRosterRatings(FormationInfoById(reg.FormationValueFor(offer.ToIndex)), toRoster, LegendLookup);

            if (offer.Coins > 0)
                CastMessage(reg.Members[offer.ToIndex].Id, new LeagueAdjustCoinsMessage(offer.Coins)); // proposer's escrow → recipient
            reg.TradeOffers.Remove(offer);
            SchedulePersistState();
            return new LeagueOpResponse("", Snapshot(reg, playerId));
        }

        void RefundProposer(LeagueReg reg, TradeOffer offer)
        {
            if (offer.Coins > 0 && offer.FromIndex >= 0 && offer.FromIndex < reg.Members.Count)
                CastMessage(reg.Members[offer.FromIndex].Id, new LeagueAdjustCoinsMessage(offer.Coins));
        }

        void SweepExpiredTrades(LeagueReg reg)
        {
            MetaTime now = MetaTime.Now;
            for (int i = reg.TradeOffers.Count - 1; i >= 0; i--)
            {
                if (reg.TradeOffers[i].ExpiresAt <= now)
                {
                    RefundProposer(reg, reg.TradeOffers[i]);
                    reg.TradeOffers.RemoveAt(i);
                }
            }
        }

        static int RosterSlotOf(Dictionary<int, string> roster, string legendId)
        {
            foreach ((int slot, string id) in roster)
                if (id == legendId) return slot;
            return -1;
        }

        static string Surname(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            string[] parts = name.Split(' ');
            return parts.Length > 1 ? parts[parts.Length - 1] : name;
        }

        /// <summary> Admin (dashboard): open/close a league's transfer window, overriding the daily schedule. </summary>
        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueSetTransferWindowRequest(LeagueSetTransferWindowRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null)
                return new LeagueOpResponse("No league with that code", null);
            reg.TransferWindowOverride = request.Override; // 0 = follow schedule, 1 = force open, 2 = force closed
            SchedulePersistState();
            return new LeagueOpResponse("", Snapshot(reg, EntityId.None));
        }

        /// <summary> Admin (dashboard): force-advance the next matchday immediately. </summary>
        [EntityAskHandler]
        public LeagueOpResponse HandleLeagueAdminPlayMatchdayRequest(LeagueAdminPlayMatchdayRequest request)
        {
            LeagueReg reg = Find(request.Code);
            if (reg == null)
                return new LeagueOpResponse("No league with that code", null);
            if (reg.State != LeagueState.Active)
                return new LeagueOpResponse("Season isn't running", null);
            AdvanceOneMatchday(reg, DefaultDef());
            SchedulePersistState();
            return new LeagueOpResponse("", Snapshot(reg, EntityId.None));
        }

        // ---- Draft helpers ----

        /// <summary> Places a legend into a member's open slot for its position + marks it taken (no pick-counter change). </summary>
        static void PlaceInRoster(LeagueReg reg, int memberIndex, LegendPlayer legend, FormationInfo formation)
        {
            Dictionary<int, string> roster = reg.RosterFor(memberIndex);
            int slot = LeagueDraftEngine.NextOpenSlotForPosition(formation, roster, legend.Position);
            if (slot < 0)
                return;
            roster[slot] = legend.Id.Value;     // the season-specific entry the manager drafted
            reg.Taken.Add(legend.Name);         // the real person is now unavailable to everyone
        }

        /// <summary> A human pick during the snake draft: place it + advance the global pick counter. </summary>
        static void ApplyPick(LeagueReg reg, int memberIndex, LegendPlayer legend, FormationInfo formation)
        {
            PlaceInRoster(reg, memberIndex, legend, formation);
            reg.DraftPick++;
        }

        /// <summary> Auto-picks the best available legend for whoever's turn it currently is. Returns false if the pool is dry. </summary>
        bool AutoPickCurrent(LeagueReg reg) // instance (not static): reads DefaultDef() for the squad rules
        {
            int current = LeagueDraftEngine.CurrentDrafterIndex(reg.DraftPick, reg.Members.Count);
            if (current < 0)
                return false;
            FormationInfo formation = FormationInfoById(reg.FormationValueFor(current));
            LegendPlayer best = LeagueDraftEngine.BestAvailablePick(formation, reg.RosterFor(current), reg.Taken, DraftPool, LegendLookup, reg.MaxPerClub, LeagueDefinition.ParseOvrCaps(reg.CapBands));
            if (best == null)
                return false;
            ApplyPick(reg, current, best, formation);
            return true;
        }

        /// <summary>
        /// Human draft complete: resolve each human XI, fill the league up to 20 teams with auto-drafted CPU sides
        /// (from the remaining shared pool), generate the full double round-robin, and schedule the first matchday.
        /// </summary>
        static void FinalizeDraft(LeagueReg reg, LeagueDefinition def)
        {
            int humanCount = reg.Members.Count;
            for (int i = 0; i < humanCount; i++)
            {
                FormationInfo formation = FormationInfoById(reg.FormationValueFor(i));
                reg.Locked[i] = LeagueDraftEngine.ResolveRosterRatings(formation, reg.RosterFor(i), LegendLookup);
                reg.Members[i].TransferBudget = def.TransferBudget; // grant each human a transfer budget for the season
            }

            FormationInfo botFormation = FormationInfoById(def.DefaultFormation);
            int targetSize = Math.Clamp(def.LeagueSize, 2, 20); // designer-editable; >20 breaks fixtures/bot names, <2 has no fixtures
            for (int i = humanCount; i < targetSize; i++)
            {
                reg.Members.Add(new Member(EntityId.None, BotName(i - humanCount), BotCrest(i - humanCount), isBot: true));
                reg.Formations[i] = def.DefaultFormation;
                Dictionary<int, string> roster = reg.RosterFor(i);
                int guard = 0;
                while (roster.Count < LeagueDraftEngine.PicksPerTeam && guard++ < 50)
                {
                    LegendPlayer best = LeagueDraftEngine.BestAvailablePick(botFormation, roster, reg.Taken, DraftPool, LegendLookup, reg.MaxPerClub, LeagueDefinition.ParseOvrCaps(reg.CapBands));
                    if (best == null)
                        break;
                    PlaceInRoster(reg, i, best, botFormation);
                }
                reg.Locked[i] = LeagueDraftEngine.ResolveRosterRatings(botFormation, roster, LegendLookup);
            }

            reg.Fixtures        = LeagueEngine.GenerateDoubleRoundRobin(reg.Members.Count); // 20 teams → 38 matchdays
            reg.CurrentMatchday = 0;
            reg.NextSimTime     = NextDailyTime(MetaTime.Now, def.DailySimHourUtc);
            reg.State           = LeagueState.Active;
        }

        /// <summary> Plays one matchday (all its fixtures), records the scorelines, and schedules the next day's sim. </summary>
        void AdvanceOneMatchday(LeagueReg reg, LeagueDefinition def)
        {
            if (reg.State != LeagueState.Active)
                return;
            int count = reg.Fixtures.Count;
            if (reg.CurrentMatchday >= count)
            {
                reg.State = LeagueState.Finished;
                return;
            }

            List<LeagueFixture> matchday = reg.Fixtures[reg.CurrentMatchday];
            reg.LastMatchdayLines.Clear();
            reg.LastMatchdayNumber = reg.CurrentMatchday + 1;

            // A config-defined matchday "event" (the dashboard-tunable twist) can add goals to every fixture.
            MatchdayEvent matchdayEvent = def?.EventForMatchday(reg.LastMatchdayNumber);
            int goalBonus = matchdayEvent?.GoalBonus ?? 0;
            if (matchdayEvent != null)
                reg.LastMatchdayLines.Add($"⚡ {matchdayEvent.Name}");

            foreach (LeagueFixture f in matchday)
            {
                if (reg.Played.Contains((f.HomeIndex, f.AwayIndex)))
                    continue;
                ulong seed = SeedFor(reg.Code, f.HomeIndex, f.AwayIndex);
                MatchResult sim = MatchSim.Resolve(LockedOrBaseline(reg, f.HomeIndex), LockedOrBaseline(reg, f.AwayIndex), seed);
                int homeGoals = sim.HomeGoals + goalBonus;
                int awayGoals = sim.AwayGoals + goalBonus;
                List<MatchGoalDetail> goals = MatchSim.AttributeScorers(sim, SquadFor(reg, f.HomeIndex), SquadFor(reg, f.AwayIndex), seed);
                reg.Results.Add(new LeagueResult(f.HomeIndex, f.AwayIndex, homeGoals, awayGoals) { Goals = goals });
                reg.Played.Add((f.HomeIndex, f.AwayIndex));
                reg.LastMatchdayLines.Add($"{reg.Members[f.HomeIndex].Name} {homeGoals}–{awayGoals} {reg.Members[f.AwayIndex].Name}");

                // League matches ARE the game now (1v1 left the menu) — feed each human side's outcome into the
                // player reward pipeline (Coins / pass XP / ranked SRP / club points / daily quests), exactly as a
                // MatchActor would. Works offline: the grant lands on the player's own timeline.
                GrantFixtureRewards(reg.Members[f.HomeIndex], won: homeGoals > awayGoals, goals: homeGoals);
                GrantFixtureRewards(reg.Members[f.AwayIndex], won: awayGoals > homeGoals, goals: awayGoals);
            }

            reg.CurrentMatchday++;
            if (reg.CurrentMatchday >= count)
                reg.State = LeagueState.Finished;
            else
                reg.NextSimTime = reg.NextSimTime + MetaDuration.FromDays(1);
        }

        /// <summary>
        /// Feed one human side's fixture outcome into the standard player reward pipeline (Coins / pass XP /
        /// ranked SRP / club points / daily quests) — the same message a 1v1 MatchActor sends at full time.
        /// </summary>
        void GrantFixtureRewards(Member member, bool won, int goals)
        {
            if (!member.IsBot && member.Id != EntityId.None)
                CastMessage(member.Id, new GrantMatchRewardsMessage(won: won, roundsWon: goals));
        }

        /// <summary> The next instant at <paramref name="simHourUtc"/>:00 UTC strictly after <paramref name="now"/>. </summary>
        static MetaTime NextDailyTime(MetaTime now, int simHourUtc)
        {
            simHourUtc = Math.Clamp(simHourUtc, 0, 23); // config is designer-editable — a bad hour must not crash the singleton actor
            DateTime n = now.ToDateTime(); // UTC
            DateTime t = new DateTime(n.Year, n.Month, n.Day, simHourUtc, 0, 0, DateTimeKind.Utc);
            if (n >= t)
                t = t.AddDays(1);
            return MetaTime.FromDateTime(t);
        }

        static LineRatings Baseline() => new LineRatings { Attack = 72, Midfield = 72, Defence = 72, Goalkeeping = 70 };
        static LineRatings LockedOrBaseline(LeagueReg reg, int index) => reg.Locked.TryGetValue(index, out LineRatings r) ? r : Baseline();

        /// <summary> A member's drafted XI as (name, position, ovr) tuples — fed to scorer attribution. </summary>
        static List<(string Name, Position Pos, int Ovr)> SquadFor(LeagueReg reg, int index)
        {
            List<(string Name, Position Pos, int Ovr)> squad = new List<(string Name, Position Pos, int Ovr)>();
            foreach ((int _, string legendId) in reg.RosterFor(index))
            {
                LegendPlayer p = LookupLegend(legendId);
                if (p != null)
                    squad.Add((p.Name, p.Position, p.Ovr));
            }
            return squad;
        }

        static ulong SeedFor(string code, int home, int away)
        {
            ulong h = 1469598103934665603ul; // FNV-1a over the code
            foreach (char c in code ?? "")
                h = (h ^ c) * 1099511628211ul;
            return h ^ ((ulong)(home + 1) * 0x9E3779B97F4A7C15ul) ^ ((ulong)(away + 1) * 0xC2B2AE3D27D4EB4Ful);
        }

        LeagueSnapshot Snapshot(LeagueReg reg, EntityId viewer)
        {
            // Admin/spectator viewers pass EntityId.None — never resolve them to a member (bots also carry
            // EntityId.None, so a plain IndexOf would alias the admin view onto the first CPU team).
            int myIndex = viewer == EntityId.None ? -1 : reg.IndexOf(viewer);
            bool seasonStarted = reg.State == LeagueState.Active || reg.State == LeagueState.Finished;
            LeagueSnapshot snap = new LeagueSnapshot
            {
                Code           = reg.Code,
                Name           = reg.Name,
                State          = reg.State,
                MyIndex        = myIndex,
                IsCommissioner = reg.Commissioner == viewer,
                FixturesTotal  = seasonStarted ? reg.TotalFixtures : 0,
                FixturesPlayed = reg.Played.Count,
            };

            for (int i = 0; i < reg.Members.Count; i++)
            {
                LeagueMemberView view = new LeagueMemberView(i, reg.Members[i].Name, reg.Members[i].Crest);
                view.IsBot = reg.Members[i].IsBot;
                if (reg.Formations.TryGetValue(i, out string fid))
                    view.FormationName = fid;
                if (reg.Rosters.TryGetValue(i, out Dictionary<int, string> roster))
                {
                    view.PicksCount     = roster.Count;
                    view.RosterComplete = roster.Count >= LeagueDraftEngine.PicksPerTeam;
                }
                snap.Members.Add(view);
            }

            snap.Table = LeagueEngine.ComputeTable(reg.Members.Count, reg.Results);

            // Names already on some team — the client filters draft/transfer pick lists with this.
            snap.TakenNames = new List<string>(reg.Taken);

            // The viewer's own formation + XI (shown while drafting, and the basis of transfer swaps in-season).
            if (myIndex >= 0)
            {
                snap.MyFormation = reg.FormationValueFor(myIndex);
                foreach ((int slot, string legendId) in reg.RosterFor(myIndex))
                    snap.MyRoster[slot] = legendId;
            }

            if (reg.State == LeagueState.Drafting)
            {
                int count = reg.Members.Count;
                int current = LeagueDraftEngine.CurrentDrafterIndex(reg.DraftPick, count);
                snap.DraftPick           = reg.DraftPick;
                snap.DraftTotalPicks     = LeagueDraftEngine.TotalPicks(count);
                snap.DraftRound          = LeagueDraftEngine.RoundNumber(reg.DraftPick, count);
                snap.CurrentDrafterIndex = current;
                snap.CurrentDrafterName  = (current >= 0 && current < count) ? reg.Members[current].Name : "";
                snap.IsMyTurn            = current >= 0 && current == myIndex;
                if (myIndex >= 0)
                {
                    // If it's my turn and I've spun, attach the spun squad (with per-player availability flags).
                    if (current == myIndex && !string.IsNullOrEmpty(reg.SpinClub)
                        && SeasonSquads.TryGetValue(SeasonKey(reg.SpinClub, reg.SpinSeason), out List<LegendPlayer> squad))
                    {
                        FormationInfo myFormation = FormationInfoById(reg.FormationValueFor(myIndex));
                        Dictionary<int, string> myRoster = reg.RosterFor(myIndex);
                        SpunSquadView view = new SpunSquadView { Club = reg.SpinClub, Season = reg.SpinSeason };
                        foreach (LegendPlayer pl in squad)
                        {
                            view.Players.Add(new SpunPlayer
                            {
                                LegendId     = pl.Id.Value,
                                Name         = pl.Name,
                                Position     = pl.Position,
                                Ovr          = pl.Ovr,
                                Nation       = pl.Nation,
                                Taken        = reg.Taken.Contains(pl.Name),
                                FitsOpenSlot = LeagueDraftEngine.NextOpenSlotForPosition(myFormation, myRoster, pl.Position) >= 0,
                            });
                        }
                        snap.MySpin = view;
                    }
                }
            }

            if (seasonStarted)
            {
                snap.CurrentMatchday    = reg.CurrentMatchday;
                snap.TotalMatchdays     = reg.Fixtures.Count;
                snap.NextSimAtMillis    = reg.State == LeagueState.Active ? reg.NextSimTime.MillisecondsSinceEpoch : 0;
                snap.LastMatchdayNumber = reg.LastMatchdayNumber;
                snap.LastMatchdayLines  = new List<string>(reg.LastMatchdayLines);

                // Browsable results history (visible to all members) — cap to the most recent fixtures to bound size.
                const int maxResultLines = 120;
                Dictionary<(int, int), int> fixtureMatchday = new Dictionary<(int, int), int>();
                for (int md = 0; md < reg.Fixtures.Count; md++)
                    foreach (LeagueFixture fx in reg.Fixtures[md])
                        fixtureMatchday[(fx.HomeIndex, fx.AwayIndex)] = md + 1;
                int firstResult = Math.Max(0, reg.Results.Count - maxResultLines);
                for (int ri = firstResult; ri < reg.Results.Count; ri++)
                {
                    LeagueResult r = reg.Results[ri];
                    fixtureMatchday.TryGetValue((r.HomeIndex, r.AwayIndex), out int md);
                    snap.SeasonResults.Add(new LeagueResultLine
                    {
                        Matchday  = md,
                        HomeName  = r.HomeIndex >= 0 && r.HomeIndex < reg.Members.Count ? reg.Members[r.HomeIndex].Name : "?",
                        AwayName  = r.AwayIndex >= 0 && r.AwayIndex < reg.Members.Count ? reg.Members[r.AwayIndex].Name : "?",
                        HomeGoals = r.HomeGoals,
                        AwayGoals = r.AwayGoals,
                        Goals     = r.Goals,
                    });
                }
            }

            // Invincible = the viewing manager played every fixture and won them all (38-0 in a full 20-team league).
            if (myIndex >= 0)
            {
                foreach (LeagueRow row in snap.Table)
                {
                    if (row.TeamIndex == myIndex)
                    {
                        snap.Invincible = row.Played == reg.FixturesPerTeam && row.Played > 0 && row.Lost == 0 && row.Drawn == 0;
                        break;
                    }
                }
            }

            // Transfer window state.
            snap.TransferWindowOpen = IsTransferWindowOpen(reg, DefaultDef());
            if (myIndex >= 0)
                snap.MyTransferBudget = reg.Members[myIndex].TransferBudget;

            snap.HideRatings = reg.HideRatings; // hard mode → client masks OVRs in the draft/roster UI
            snap.MaxPerClub  = reg.MaxPerClub;
            snap.CapBands    = reg.CapBands;
            snap.DraftPin    = reg.DraftPin;
            // Pending P2P trades involving the viewer (incoming to accept/reject + outgoing to cancel).
            SweepExpiredTrades(reg);
            if (myIndex >= 0)
            {
                foreach (TradeOffer o in reg.TradeOffers)
                {
                    if (o.FromIndex != myIndex && o.ToIndex != myIndex) continue;
                    LegendPlayer give = LookupLegend(o.GiveLegendId);
                    LegendPlayer get  = LookupLegend(o.GetLegendId);
                    if (give == null || get == null) continue;
                    bool incoming = o.ToIndex == myIndex;
                    int otherIdx = incoming ? o.FromIndex : o.ToIndex;
                    snap.MyTradeOffers.Add(new TradeOfferView
                    {
                        OfferId = o.OfferId, Incoming = incoming,
                        OtherName = otherIdx >= 0 && otherIdx < reg.Members.Count ? reg.Members[otherIdx].Name : "",
                        GiveName = give.Name, GiveOvr = give.Ovr, GivePos = give.Position,
                        GetName  = get.Name,  GetOvr  = get.Ovr,  GetPos  = get.Position, Coins = o.Coins,
                    });
                }
                // Other human managers' rosters — the trade-proposal picker (only in-season).
                if (reg.State == LeagueState.Active)
                {
                    for (int i = 0; i < reg.Members.Count; i++)
                    {
                        if (i == myIndex || reg.Members[i].IsBot) continue;
                        Dictionary<int, string> r = reg.RosterFor(i);
                        if (r.Count == 0) continue;
                        LeagueRosterView rv = new LeagueRosterView { MemberIndex = i, Name = reg.Members[i].Name };
                        foreach ((int _, string id) in r) rv.LegendIds.Add(id);
                        snap.TradeRosters.Add(rv);
                    }
                }
            }
            snap.NoSameClub  = reg.MaxPerClub >= 1;                    // legacy back-compat for older clients
            snap.SquadCaps   = !string.IsNullOrEmpty(reg.CapBands);

            return snap;
        }
    }
}
