using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Server.PublicWebApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System;
using System.Collections.Generic;
using System.IO;

namespace Game.Server
{
    /// <summary>
    /// Serves the Blazor WASM WebClient as a public, unauthenticated static web app.
    ///
    /// <para>
    /// Derives from <see cref="PublicWebApiController"/> so it is auto-registered on the public
    /// (unauthenticated) PublicWebApi host — the right place for a player-facing client, as opposed
    /// to the auth-gated AdminApi host that serves the LiveOps Dashboard. The files are served from
    /// the directory configured by <see cref="WebClientHostingOptions.WebRootPath"/> (the local
    /// build output during development, the baked-in <c>publicwebapp</c> directory in cloud).
    /// </para>
    ///
    /// <para>
    /// The <c>GET</c>-only catch-all route serves any static asset and falls back to
    /// <c>index.html</c> for client-side (SPA) navigation routes only — a missing path that looks
    /// like an asset (has a file extension) returns <c>404</c> rather than HTML, so the browser sees
    /// a clean error instead of a cryptic MIME/integrity failure. It has the lowest route precedence
    /// (catch-all), so more specific PublicWebApi routes (e.g. <c>/auth/...</c> or webhook POSTs) are
    /// never shadowed.
    /// </para>
    ///
    /// <para>
    /// Responses are content-negotiated: a pre-compressed Brotli (<c>.br</c>) or gzip (<c>.gz</c>)
    /// sibling produced by the publish is served when the client accepts it. Content-fingerprinted
    /// assets (their filename carries a content hash) are cached immutably for a year; every other
    /// file — the SPA entrypoint and the stable-named Blazor bootstrap files (<c>dotnet.js</c>,
    /// <c>blazor.webassembly.js</c>, …) — is served <c>no-cache</c> so redeploys are picked up
    /// immediately. See <see cref="LooksContentFingerprinted"/> for why this distinction is
    /// load-bearing (a cached boot manifest pins the client to the previous deploy's assemblies).
    /// </para>
    /// </summary>
    public class WebClientHostingController : PublicWebApiController
    {
        // .wasm must be served as application/wasm; Blazor's .dat/.dll/.blat fall back to octet-stream.
        static readonly FileExtensionContentTypeProvider s_contentTypeProvider = CreateContentTypeProvider();

        static FileExtensionContentTypeProvider CreateContentTypeProvider()
        {
            FileExtensionContentTypeProvider provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".wasm"] = "application/wasm";
            return provider;
        }

        // Blazor bootstrap/loader files that keep a STABLE filename across builds (no content hash).
        // They must always revalidate: caching them immutably pins the browser to a previous deploy.
        // In .NET 9+ the boot manifest is the stable-named dotnet.js (there is no blazor.boot.json) —
        // it lists the content-hashed assembly filenames, so a stale copy loads the old client even
        // though the assemblies themselves are fresh. blazor.boot.json is kept for SDK ≤8 / safety.
        static readonly HashSet<string> s_neverCacheNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "index.html",
            "blazor.boot.json",
            "blazor.webassembly.js",
            "dotnet.js",
            "service-worker.js",
            "service-worker-assets.js",
        };

        [HttpGet("{**path}")]
        public IActionResult ServeWebClient(string path)
        {
            WebClientHostingOptions options = RuntimeOptionsRegistry.Instance.GetCurrent<WebClientHostingOptions>();
            if (string.IsNullOrEmpty(options.WebRootPath))
                return NotFound();

            string rootPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), options.WebRootPath));
            if (!Directory.Exists(rootPath))
                return NotFound();

            // Map the request to a file under the web root, guarding against path traversal.
            string requestedRelative = string.IsNullOrEmpty(path) ? "index.html" : path;
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, requestedRelative));

            bool isUnderRoot = fullPath == rootPath || fullPath.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            if (isUnderRoot && System.IO.File.Exists(fullPath))
                return ServeFile(rootPath, fullPath);

            // The request didn't resolve to a real file. Only fall back to index.html for client-side
            // (SPA) navigation routes — i.e. extensionless paths. A request that looks like a static
            // asset (has a file extension) is a genuine miss: return 404 so the browser sees a clean
            // error instead of HTML masquerading as the requested .js/.wasm (which surfaces as a
            // cryptic MIME or SRI integrity failure).
            if (Path.HasExtension(requestedRelative))
                return NotFound();

            // SPA fallback: serve index.html so client-side routes resolve.
            string indexPath = Path.Combine(rootPath, "index.html");
            if (System.IO.File.Exists(indexPath))
                return ServeFile(rootPath, indexPath);

            return NotFound();
        }

        IActionResult ServeFile(string rootPath, string fullPath)
        {
            if (!s_contentTypeProvider.TryGetContentType(fullPath, out string contentType))
                contentType = "application/octet-stream";

            // Caching policy is keyed on whether the filename carries a content hash, NOT on the
            // _framework directory: in .NET 9+ that directory mixes content-fingerprinted assemblies
            // (safe to cache forever) with stable-named loader/manifest files (must revalidate, or a
            // redeploy serves a stale boot manifest that pins the client to the old assemblies).
            // The asymmetry is deliberate: caching a fingerprinted file as no-cache only costs a 304,
            // whereas caching a stable manifest as immutable breaks the deploy — so we cache immutably
            // ONLY when confidently fingerprinted, and revalidate everything else.
            string fileName = Path.GetFileName(fullPath);
            if (LooksContentFingerprinted(fileName))
                Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            else
                Response.Headers.CacheControl = "no-cache";

            // Content negotiation: serve a pre-compressed sibling (.br/.gz) the publish produced when
            // the client accepts it. The Content-Type stays that of the original file; Content-Encoding
            // marks the wire format. Vary lets shared caches key on the negotiated encoding.
            Response.Headers.Vary = "Accept-Encoding";
            string acceptEncoding = Request.Headers.AcceptEncoding.ToString();
            if (acceptEncoding.Contains("br", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(fullPath + ".br"))
            {
                Response.Headers.ContentEncoding = "br";
                return PhysicalFile(fullPath + ".br", contentType);
            }
            if (acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(fullPath + ".gz"))
            {
                Response.Headers.ContentEncoding = "gzip";
                return PhysicalFile(fullPath + ".gz", contentType);
            }

            return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
        }

        /// <summary>
        /// True if <paramref name="fileName"/> is a content-fingerprinted asset (safe to cache
        /// immutably). A Blazor publish fingerprints content-addressable assets by inserting a base36
        /// hash segment before the extension — e.g. <c>GameLogic.2n5q3t6y8w.wasm</c>,
        /// <c>dotnet.runtime.r2kbxkuujc.js</c>. Any change to the file's contents changes that hash and
        /// thus the URL, so a cached copy can never be stale.
        /// <para>
        /// The loader/bootstrap files keep stable names (<c>index.html</c>, <c>dotnet.js</c>,
        /// <c>blazor.webassembly.js</c>, source maps, …) and are explicitly excluded — they MUST
        /// revalidate on every load. This is the crux of the "browser loads the old client after a
        /// redeploy" bug: <c>dotnet.js</c> is the .NET 9+ boot manifest (it lists the hashed assembly
        /// filenames), so caching it immutably keeps pointing the client at the previous deploy's
        /// assemblies even though those assemblies are themselves correctly cache-busted.
        /// </para>
        /// </summary>
        static bool LooksContentFingerprinted(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || s_neverCacheNames.Contains(fileName))
                return false;

            // Source maps keep stable names; never cache them immutably.
            if (fileName.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
                return false;

            // Strip a pre-compression suffix the publish may have appended (foo.<hash>.wasm.br).
            string name = fileName;
            if (name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 3);
            else if (name.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 3);

            // Require a "<name>.<hash>.<ext>" shape where <hash> is a base36 token of at least 8
            // characters. Blazor currently emits 10-char hashes; the >=8 floor tolerates future
            // changes while still excluding stable words like "webassembly" only via the explicit
            // never-cache set above (those would otherwise look hash-like).
            int lastDot = name.LastIndexOf('.');
            if (lastDot <= 0)
                return false;
            int prevDot = name.LastIndexOf('.', lastDot - 1);
            if (prevDot < 0)
                return false;

            string segment = name.Substring(prevDot + 1, lastDot - prevDot - 1);
            if (segment.Length < 8)
                return false;
            foreach (char c in segment)
            {
                bool isBase36 = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (!isBase36)
                    return false;
            }
            return true;
        }
    }
}
