using Dalamud.Configuration;
using System.Collections.Generic;

namespace SimpleRollTracker
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public int ClearMinutes { get; set; }
        public bool IsRecording { get; set; } = true;

        public int LifetimeTotalRolls { get; set; }
        public long LifetimeRollSum { get; set; }
        public int LifetimeHighestRoll { get; set; }
        public string LifetimeHighestPlayer { get; set; } = string.Empty;
        public int LifetimeLowestRoll { get; set; }
        public string LifetimeLowestPlayer { get; set; } = string.Empty;

        public List<RollEntry> RollHistory { get; set; } = new();
        public List<LapEntry> Laps { get; set; } = new();
    }
}
