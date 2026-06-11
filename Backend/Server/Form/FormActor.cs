// FOOTDRAFT — singleton live-"form" registry (real-world form sync).

using Game.Logic;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.MultiplayerEntity;
using Metaplay.Server.MultiplayerEntity;
using Metaplay.Server.MultiplayerEntity.InternalMessages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Game.Server
{
    [MetaSerializableDerived(4)]
    public class FormSetupParams : IMultiplayerEntitySetupParams
    {
        public FormSetupParams() { }
    }

    /// <summary> Set (or clear, when TierDelta == 0) a player's live die-tier override. </summary>
    [MetaMessage(MessageCodes.FormSetRequest, MessageDirection.ServerInternal)]
    public class FormSetRequest : EntityAskRequest<EntityAskOk>
    {
        public string PlayerName { get; private set; }
        public int    TierDelta  { get; private set; }

        FormSetRequest() { }
        public FormSetRequest(string playerName, int tierDelta)
        {
            PlayerName = playerName;
            TierDelta  = tierDelta;
        }
    }

    /// <summary> Clear all live-form overrides. </summary>
    [MetaMessage(MessageCodes.FormClearRequest, MessageDirection.ServerInternal)]
    public class FormClearRequest : EntityAskRequest<EntityAskOk>
    {
        public FormClearRequest() { }
    }

    /// <summary> Fetch all current live-form overrides (player name → tier delta). </summary>
    [MetaMessage(MessageCodes.FormGetRequest, MessageDirection.ServerInternal)]
    public class FormGetRequest : EntityAskRequest<FormGetResponse>
    {
        public FormGetRequest() { }
    }

    [MetaMessage(MessageCodes.FormGetRequest + 100_000, MessageDirection.ServerInternal)]
    public class FormGetResponse : EntityAskResponse
    {
        public MetaDictionary<string, int> Deltas { get; private set; }

        FormGetResponse() { }
        public FormGetResponse(MetaDictionary<string, int> deltas) { Deltas = deltas; }
    }

    [EntityConfig]
    public class FormConfig : EphemeralEntityConfig
    {
        public override EntityKind        EntityKind           => EntityKindGame.Form;
        public override Type              EntityActorType      => typeof(FormActor);
        public override NodeSetPlacement  NodeSetPlacement     => NodeSetPlacement.Service;
        public override IShardingStrategy ShardingStrategy     => ShardingStrategies.CreateSingletonService();
        public override TimeSpan          ShardShutdownTimeout => TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Singleton holding the live "form sync" overrides (player name → die-tier delta) in memory. An operator
    /// sets these at runtime (via <see cref="FormSetRequest"/>, exposed in the demo through a development action)
    /// and the change is read by every new match at setup — buffing/nerfing that player's dice live, with no
    /// redeploy. This is the demo's stand-in for a dashboard-driven game-config hot-update (the production path
    /// for the headline "real-world form sync" feature; see the design doc handoff).
    /// </summary>
    public class FormActor : EphemeralMultiplayerEntityActorBase<FormModel, FormAction>
    {
        public static readonly EntityId FormEntityId = EntityId.Create(EntityKindGame.Form, 0);

        readonly Dictionary<string, int> _form = new Dictionary<string, int>();

        protected override bool IsTicking => false;

        protected override async Task Initialize()
        {
            await base.Initialize();
            if (Model == null)
                await SetUpEntity(new FormSetupParams());
        }

        protected override Task SetUpModelAsync(FormModel model, IMultiplayerEntitySetupParams setupParams)
            => Task.CompletedTask;

        [EntityAskHandler]
        public EntityAskOk HandleFormSetRequest(EntityId fromEntityId, FormSetRequest request)
        {
            string name = (request.PlayerName ?? "").Trim();
            if (name.Length == 0)
                return EntityAskOk.Instance;
            if (request.TierDelta == 0)
                _form.Remove(name);
            else
                _form[name] = request.TierDelta;
            return EntityAskOk.Instance;
        }

        [EntityAskHandler]
        public EntityAskOk HandleFormClearRequest(EntityId fromEntityId, FormClearRequest request)
        {
            _form.Clear();
            return EntityAskOk.Instance;
        }

        [EntityAskHandler]
        public FormGetResponse HandleFormGetRequest(EntityId fromEntityId, FormGetRequest request)
        {
            MetaDictionary<string, int> deltas = new MetaDictionary<string, int>();
            foreach ((string name, int delta) in _form)
                deltas[name] = delta;
            return new FormGetResponse(deltas);
        }
    }
}
