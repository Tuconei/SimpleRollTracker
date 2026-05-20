using Dalamud.Configuration;
using System.Collections.Generic;

namespace SimpleRollTracker
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public int ClearMinutes { get; set; } = 0;
        public bool IsRecording { get; set; } = true;

        public int LifetimeTotalRolls { get; set; } = 0;
        public long LifetimeRollSum { get; set; } = 0;
        public int LifetimeHighestRoll { get; set; } = 0;
        public string LifetimeHighestPlayer { get; set; } = string.Empty;
        public int LifetimeLowestRoll { get; set; } = 0;
        public string LifetimeLowestPlayer { get; set; } = string.Empty;

        public List<RollEntry> RollHistory { get; set; } = new();
        public List<LapEntry> Laps { get; set; } = new();
    }
}
