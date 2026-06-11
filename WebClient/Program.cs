using Game.Logic;
using WebClient.Services;
using WebClientBase.Components;
using WebClientBase.Startup;

await WebClientHostBuilder<App, PlayerModel, MetaplayClientService>
    .Create(args, appTitle: "38-0-20", logoEmoji: "⚽")
    .RunAsync();
