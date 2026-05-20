using System;
using System.Collections.Generic;

namespace SimpleRollTracker
{
    public class RollEntry
    {
        public string PlayerName { get; set; } = string.Empty;
        public int RollValue { get; set; }
        public DateTime Time { get; set; }
    }

    public class LapEntry
    {
        public int Number { get; set; }
        public DateTime Time { get; set; }
        public string WinnerName { get; set; } = string.Empty;
        public int WinnerRoll { get; set; }
        public List<RollEntry> Rolls { get; set; } = new();
    }
}
