using Metaplay.Core;
using Metaplay.Core.Serialization;
using System;
using System.IO;
using System.Linq;

namespace Game.WebAssemblySerializerGen
{
    /// <summary>
    /// Emits <c>Metaplay.Generated.WebAssembly.dll</c>, the pre-built serializer the WebAssembly WebClient loads at
    /// runtime (mono-wasm cannot generate it on the fly). Mirrors what <c>Unity/Editor/SerializerBuilder.cs</c> does
    /// for Unity player builds, but as a standalone console tool.
    ///
    /// Re-run this whenever the <c>[MetaSerializable]</c> game types in SharedCode change. Output:
    ///   dotnet run --project tools/SerializerGen -- WebClient/Serializer
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            string outputDir = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
            Directory.CreateDirectory(outputDir);

            // Initialize the SDK with runtime serializer generation enabled. This scans the entry assembly's
            // integration reference graph (which includes GameLogic, the shared game types) and populates the
            // serializer type registry.
            MetaplayCore.ClientIntegrationAssemblies = IntegrationAssembly.FindRoots().ToList();
            MetaplayCore.InitializeForExternalApp("WebAssemblyGen");

            // Emit the serializer the WebClient loads via
            //   MetaplayCore.InitializeForClient(ClientPlatform.WebAssembly) -> Assembly.Load("Metaplay.Generated.WebAssembly").
            // generateRuntimeTypeInfo:true embeds the type metadata so no runtime scanning is needed.
            // isMono / useMemberAccessTrampolines:true: the wasm interpreter enforces member accessibility, so the
            // default direct private-field access throws FieldAccessException at deserialization time (e.g. on
            // MetaActivableSet._activableStates). The trampoline path routes member access through generated accessor
            // methods instead, which the interpreter accepts. (This mirrors the Mono/IL2CPP serializer build.)
            MetaSerializerTypeInfo typeInfo = MetaplayServices.Get<MetaSerializerTypeRegistry>().TypeInfo;
            RoslynSerializerCompileCache.EnsureDllUpToDate(
                outputDir,
                "Metaplay.Generated.WebAssembly.dll",
                Path.Combine(outputDir, "Errors"),
                false, // enableCaching
                true,  // forceRoslyn
                true,  // isMono
                true,  // useMemberAccessTrampolines
                true,  // generateRuntimeTypeInfo
                typeInfo);

            Console.WriteLine($"Wrote {Path.Combine(outputDir, "Metaplay.Generated.WebAssembly.dll")}");
            return 0;
        }
    }
}
