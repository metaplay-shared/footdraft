// FOOTDRAFT — a player's club membership & contribution.

using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// A player's club membership: the club they belong to (by unique name; empty = none), the Club Points they
    /// have personally contributed this league week, and a cached <see cref="ClubSnapshot"/> of the league
    /// standings for display. The authoritative cross-member aggregation lives in the server's <c>ClubsActor</c>
    /// (which mirrors what Metaplay's first-party Guild + Leagues frameworks provide in production).
    /// </summary>
    [MetaSerializable]
    public class PlayerClub
    {
        /// <summary> The club's unique name, or empty if the player is not in a club. </summary>
        [MetaMember(1)] public string       Name             { get; set; } = "";
        [MetaMember(2)] public long         ContribWeekId    { get; set; } = -1;
        [MetaMember(3)] public long         ContribThisWeek  { get; set; }
        [MetaMember(4)] public ClubSnapshot Snapshot         { get; set; } = new ClubSnapshot();

        public PlayerClub() { }

        public bool InClub => !string.IsNullOrEmpty(Name);
    }
}
