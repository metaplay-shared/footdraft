using Metaplay.Core;
using Metaplay.Unity;
using System;
using System.IO;
using System.Threading.Tasks;

namespace WebClientBase.Integration;

/// <summary>
/// Offline server for Blazor WebClient.
/// Overrides config path to use the actual StaticGameConfig.mpa file location.
/// </summary>
public class BlazorOfflineServer : DefaultOfflineServer
{
    public override async Task InitializeAsync(MetaplayOfflineOptions offlineOptions)
    {
        try
        {
            await base.InitializeAsync(offlineOptions);
        }
        catch (Exception ex)
        {
            string configPath = GetBuiltinGameConfigArchivePath();
            throw new InvalidOperationException(
                $"Failed to initialize offline server. Config file not found at '{configPath}'. " +
                $"Ensure the game config has been built.", ex);
        }
    }

    /// <summary>
    /// Override to return the path to the Backend/Server/GameConfig directory.
    /// </summary>
    protected override string GetBuiltinGameConfigDirectoryPath()
    {
        // Go up one level from WebClient to the project root
        string projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
        return Path.Combine(projectRoot, "Backend", "Server", "GameConfig");
    }

    /// <summary>
    /// Override to return the path to StaticGameConfig.mpa instead of SharedGameConfig.mpa.
    /// </summary>
    protected override string GetBuiltinGameConfigArchivePath()
    {
        return Path.Combine(GetBuiltinGameConfigDirectoryPath(), "StaticGameConfig.mpa");
    }
}
