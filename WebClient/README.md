# WebClient (Blazor WebAssembly)

The FOOTDRAFT web client runs **entirely in the browser** as a Blazor WebAssembly app. The Metaplay
game client (connection, `PlayerModel`, actions) executes client-side and talks to the game server
over a WebSocket — there is no server-side Blazor circuit.

Modelled on the SDK reference `MetaplaySDK` sample `Samples/MultiClient/BlazorWasmClient`.

## Projects involved

- **WebClient** (this project) — the Blazor WebAssembly app (`Microsoft.NET.Sdk.BlazorWebAssembly`).
- **GameLogic** (`../GameLogic`) — the shared game-logic assembly. Source-includes `../SharedCode`
  and references `Metaplay.Client`. The WASM client references this *as an assembly* (not source) so
  the pre-built serializer's assembly identity matches at runtime. (It is the client-side counterpart
  of `Backend/SharedCode/SharedCode.csproj`, which references the server-only `Metaplay.Cloud`.)
- **WebClientBase** (`../WebClientBase`) — generic Blazor WASM framework library (host builder,
  shared UI, connection service).
- **tools/SerializerGen** (`../tools/SerializerGen`) — build-time tool that emits the pre-built
  serializer (see below).

## The `MetaplayWebAssembly=true` flag is mandatory

Every `dotnet build` / `run` / `publish` of this project **must** pass the MSBuild global property
`-p:MetaplayWebAssembly=true`. It must be on the command line (not just in the csproj): a *global*
property propagates across the whole `WebClient -> GameLogic/WebClientBase -> Metaplay.Client`
reference graph, selecting the `ClientWebSocket` transport and disabling runtime serializer codegen
(unsupported on mono-wasm). Without it the SDK compiles with the wrong transport + runtime codegen
and the app fails at runtime.

## The pre-built serializer

mono-wasm cannot generate the Metaplay serializer at runtime, so the client loads a pre-built
`Serializer/Metaplay.Generated.WebAssembly.dll`. **Regenerate it whenever `[MetaSerializable]` types
in `SharedCode` change:**

```
dotnet run --project tools/SerializerGen -- WebClient/Serializer
```

The committed DLL keeps a clean checkout building without running the generator first.

## Build & run

1. **Start the game server** (`metaplay dev server`). It exposes the WebSocket listener on port 9380
   by default; the `localhost` environment in
   `../WebClientBase/Configuration/StaticEnvironmentConfigProvider.cs` points at it.

2. **Run the client:**

   ```
   dotnet run --project WebClient/WebClient.csproj -p:MetaplayWebAssembly=true
   ```

   Open the printed URL. The page boots as WebAssembly, connects, and creates a guest session.

   For day-to-day development, prefer the helper scripts — they add `--watch` (hot reload), bind to
   all interfaces, and print the LAN URL (see [Running on a phone / over the LAN](#running-on-a-phone--over-the-lan)):

   ```
   .\run.ps1     # PowerShell
   run.bat       # cmd
   ```

3. **Build / publish:**

   ```
   dotnet build   WebClient/WebClient.csproj -p:MetaplayWebAssembly=true
   dotnet publish WebClient/WebClient.csproj -c Release -p:MetaplayWebAssembly=true
   ```

   Serve the published `wwwroot` over HTTP (a Blazor WASM app cannot be opened via `file://`).

## Served by the game server

The game server can serve this client itself (publicly, unauthenticated) from its **PublicWebApi**
host — no separate dev server needed to view it. This is handled game-side by
`Backend/Server/WebClientHosting/WebClientHostingController.cs` (a `PublicWebApiController` catch-all)
and the `WebClientHosting.WebRootPath` runtime option.

- **Locally:** *publish* the client (a plain `dotnet build` does not produce a self-contained
  `wwwroot` — `index.html` and static assets are resolved via a manifest, not copied — so a publish is
  required), then run the server:

  ```
  dotnet publish WebClient/WebClient.csproj -c Release -p:MetaplayWebAssembly=true
  metaplay dev server
  ```

  Open `http://localhost:5560/` (the PublicWebApi port). `Backend/Server/Config/Options.local.yaml`
  points `WebClientHosting.WebRootPath` at this project's `bin/Release/net10.0/publish/wwwroot`. The
  standalone dev server (`run.ps1`, port 5290) remains available for hot-reload development.

- **In the cloud:** `MetaplaySDK/Dockerfile.server` publishes this client (`build-webclient` stage)
  and bakes its `wwwroot` into the server image at `/gameserver/publicwebapp`, which the cloud-default
  `WebClientHosting.WebRootPath = "publicwebapp"` serves. The PublicWebApi host and its `-public`
  ingress must be enabled in the deployment's Helm values.

## Running on a phone / over the LAN

To open the client on another device on the same network (e.g. an iPhone for app-like testing):

1. **Start the game server** (`metaplay dev server`) — it exposes the client WebSocket on port
   **9380**.
2. **Start the dev server with the helper script**, which binds to `0.0.0.0:5290` and prints the
   LAN URL:

   ```
   .\WebClient\run.ps1     # PowerShell
   WebClient\run.bat       # cmd
   ```

   It prints e.g. `Network: http://192.168.x.x:5290`.
3. **Open that URL on the phone** (same Wi-Fi as the dev machine). No separate publish is needed —
   the dev server builds and serves the client, with hot reload.

How the connection finds its way back: `StaticEnvironmentConfigProvider.UsePageHostForLocalEnvironment`
(called from `WebClientHostBuilder`) rewrites the `localhost` environment's server/CDN host to
whatever host the page was served from. So a page loaded from `192.168.x.x` connects its WebSocket
to `192.168.x.x:9380` instead of the phone's own `localhost`.

**Windows Firewall** must allow inbound TCP on **both** ports — the dev server (5290) *and* the game
server WebSocket (9380). If the page loads but won't connect, this is almost always why. Allow them
(run PowerShell as admin):

```powershell
New-NetFirewallRule -DisplayName "Metaplay WebClient dev (5290)" -Direction Inbound -Protocol TCP -LocalPort 5290 -Action Allow -Profile Private
New-NetFirewallRule -DisplayName "Metaplay game server WS (9380)" -Direction Inbound -Protocol TCP -LocalPort 9380 -Action Allow -Profile Private
```

(Use `Private` if your network is classified Private; add `Public` if it's a Public network.)

## Installing as a home-screen app (PWA)

`index.html` and `wwwroot/manifest.webmanifest` configure the client as an installable web app:
`display: standalone`, theme/background colors, and home-screen icons. On iOS the
`apple-mobile-web-app-capable` meta tag plus the `apple-touch-icon` links provide a chrome-free,
full-screen launch — **but only when launched from the home screen**, not while browsing in Safari
(Safari's address bar can't be hidden during normal browsing).

To use it on iPhone: open the site in **Safari** (Chrome/Firefox on iOS can't create standalone web
apps) → **Share → Add to Home Screen** → launch from the icon. It opens full-screen with no address
bar. Add-to-Home-Screen works over plain HTTP from a LAN IP, so no HTTPS is needed for testing.

**Icons** live in `wwwroot/icons/` (`apple-touch-icon.png` 180×180, `icon-192.png`, `icon-512.png`),
with a copy at the root well-known path `wwwroot/apple-touch-icon.png` that iOS auto-discovers. They
must be **fully opaque** (no alpha) — iOS composites any transparency over black. To regenerate them
from a square source image (e.g. `icon.png`), resize into those sizes with the background flattened;
the icon link tags use **absolute** paths (`/icons/...`) so they resolve to the site root regardless
of the current SPA route. After changing icons, restart the dev server (a running instance won't
serve new `wwwroot` files) and re-add to the home screen — iOS caches the icon at add time.

## Notes

- Credentials live on Blazor's in-memory filesystem and do **not** persist across reloads (a new
  guest account is created each session). A `localStorage`-backed store would add persistence.
- The wire-level `ClientPlatform` reported to the server is `Unknown`; the `WebAssembly` platform
  value is only used to name/load the serializer assembly.
- Static assets (e.g. `wwwroot/globe/world-meta.json`) are loaded over HTTP via `HttpClient`, not the
  server filesystem.
