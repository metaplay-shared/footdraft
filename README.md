# FOOTDRAFT (38-0-20)

FOOTDRAFT is a head-to-head football-manager game built on [Metaplay](https://metaplay.io): spin-draft eleven legends into a formation, then take on your friends in a 20-team, 38-matchday season league — one matchday simulated per day, a transfer market between matchdays, and a full live-service meta around it (wallet economy, daily and season quests, season pass, ranked ladder, LiveOps events, in-game inbox).

It is an online, scalable reimagining of the viral single-player ["38-0"](https://38-0.app/) concept — can your XI go a whole season unbeaten? — and it was built almost entirely with the [Metaplay Agent](https://docs.metaplay.io/miscellaneous/ai-assistants/metaplay-agent) over a couple of days of background prompting. The client is a Blazor WebAssembly web app, so the whole game runs in the browser.

## Key Takeaways

FOOTDRAFT shows how Metaplay's architecture carries a multiplayer live-service game that was largely written by an AI agent:

- **Deterministic client-server model** — the same `PlayerModel` and `PlayerAction` code runs on both client and server. Actions execute optimistically on the client for responsiveness while the server validates everything, so the economy is cheat-proof by construction.
- **Charge-in-action economy pattern** — transfer fees and gem spends are charged inside client-predicted actions (deterministic, validated server-side), with server-issued refund actions if a cross-entity operation is rejected. See `PlayerLeagueTransferSwap` in `SharedCode/Player/PlayerActions.cs`.
- **A server-authoritative league singleton** — the 20-team season league (turn-based shared-pool draft, daily matchday sims, transfer market) lives in a persisted `LeagueActor` that survives restarts and redeploys (`Backend/Server/League/`).
- **Sheet-backed game configs** — legends, formations, league rules and quests compile from a Google Sheet into type-safe classes both client and server consume. Designers rebalance the game from the LiveOps Dashboard with no deploy.
- **LiveOps events, mail and a custom dashboard** — Coin Rush multiplier events, reward mail, and custom "Season Leagues" admin pages on the Metaplay LiveOps Dashboard.
- **A web client without Unity** — `WebClientBase/` is a small generic Blazor WASM framework over the Metaplay client SDK; `WebClient/` is the game UI on top of it.

## How to Explore

- Open the game config sheet to see how the content is authored: [Footdraft Game Config](https://docs.google.com/spreadsheets/d/1r0fpYtDIbFii_9R6ggWFBbiEH-HoDhZK35WQEByto2E) (view-only).
- Read the code: start from `SharedCode/Player/PlayerModel.cs` (player state), `SharedCode/Player/PlayerActions.cs` (every mutation in the game), and `Backend/Server/League/LeagueActor.cs` (the season league).
- Run it locally (below) and open the LiveOps Dashboard at [localhost:5550](http://localhost:5550/).

## Setup Instructions

1. Clone the repository
2. Install the [Metaplay CLI](https://github.com/metaplay/cli)
3. Initialize the Metaplay SDK with `python3 init-sdk.py`
4. Run the server with `metaplay dev server`
5. Run the web client with `dotnet run --project WebClient/WebClient.csproj -p:MetaplayWebAssembly=true` and open [localhost:5290](http://localhost:5290)

> **Note:** this project was built with an internal preview of Metaplay SDK **Release 38**, which ships publicly soon — until then `init-sdk.py` can't fetch it, so treat this repo as a peek behind the curtain at what the source of a Metaplay live-service game looks like. Once R38 lands, clone and run.

Unit tests: `dotnet test Backend/SharedCode.Tests`. End-to-end tests (Playwright, needs the server and client running): `dotnet test WebClient.Tests`. After changing `[MetaSerializable]` types, regenerate the WASM serializer: `dotnet run --project tools/SerializerGen -- WebClient/Serializer`.

## License

The code in this repository is licensed under [Apache-2.0](./CODE-LICENSE). The bundled player dataset (real footballer names, clubs and seasons in `SharedCode/Draft/SeasonSquadsGenerated.cs` and the game config sheet) and other assets are included for demonstration purposes only and are **not** covered by the open-source license — they may not be redistributed or used commercially.
