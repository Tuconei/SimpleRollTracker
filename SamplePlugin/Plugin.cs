#nullable enable
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SimpleRollTracker.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

namespace SimpleRollTracker
{
    public sealed class Plugin : IDalamudPlugin
    {
        private const string RollsCommand = "/rolls";
        private const string StartRollsCommand = "/startrolls";
        private const string StopRollsCommand = "/stoprolls";
        private const string ClearRollsCommand = "/clearrolls";

        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;

        [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
        [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;

        public List<RollEntry> RollHistory => Config.RollHistory;
        public List<LapEntry> Laps => Config.Laps;

        public bool IsRecording = true;
        public bool HighWins = true;
        public bool TargetMode;
        public bool ClosestWins;

        public List<int> TargetNumbers { get; } = new();
        public bool ThresholdMode;
        public int ThresholdValue = 500;
        public bool ThresholdAbove = true;
        public List<int> FunnyNumbers { get; } = new() { 0, 1, 69, 420, 777, 999 };

        public Configuration Config { get; private set; }

        public int ClearMinutes;
        public string LockedTargetName = string.Empty;
        public bool OneRollPerPerson;

        public string MsgOpen = "Rolls are now OPEN! Type /random to play!";
        public string MsgClose = "Rolls are now CLOSED! Calculating winner...";
        public string MsgWinner = "Congratulations to {winner} for rolling a {roll}!";

        private WindowSystem WindowSystem { get; } = new("RollTracker");
        private MainWindow MainWindow { get; init; }

        public Plugin()
        {
            Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            ClearMinutes = Config.ClearMinutes;
            IsRecording = Config.IsRecording;

            MainWindow = new MainWindow(this);
            WindowSystem.AddWindow(MainWindow);

            CommandManager.AddHandler(RollsCommand, new CommandInfo(OnCommand) { HelpMessage = "Opens window." });
            CommandManager.AddHandler(StartRollsCommand, new CommandInfo(OnMacroCommand) { HelpMessage = "Starts recording." });
            CommandManager.AddHandler(StopRollsCommand, new CommandInfo(OnMacroCommand) { HelpMessage = "Stops recording." });
            CommandManager.AddHandler(ClearRollsCommand, new CommandInfo(OnMacroCommand) { HelpMessage = "Clears history." });

            PluginInterface.UiBuilder.Draw += DrawUI;
            ChatGui.ChatMessage += OnChatMessage;
        }

        public void Dispose()
        {
            PluginInterface.UiBuilder.Draw -= DrawUI;
            ChatGui.ChatMessage -= OnChatMessage;
            CommandManager.RemoveHandler(RollsCommand);
            CommandManager.RemoveHandler(StartRollsCommand);
            CommandManager.RemoveHandler(StopRollsCommand);
            CommandManager.RemoveHandler(ClearRollsCommand);
            WindowSystem.RemoveAllWindows();
            MainWindow.Dispose();
        }

        private void OnCommand(string command, string args) => MainWindow.IsOpen = !MainWindow.IsOpen;

        private void OnMacroCommand(string command, string args)
        {
            switch (command)
            {
                case StartRollsCommand:
                    IsRecording = true;
                    Config.IsRecording = true;
                    SaveConfig();
                    ChatGui.Print("Roll Tracker: Recording STARTED.");
                    break;
                case StopRollsCommand:
                    IsRecording = false;
                    Config.IsRecording = false;
                    SaveConfig();
                    ChatGui.Print("Roll Tracker: Recording STOPPED.");
                    break;
                case ClearRollsCommand:
                    RollHistory.Clear();
                    SaveConfig();
                    ChatGui.Print("Roll Tracker: History CLEARED.");
                    break;
            }
        }

        private void DrawUI() => WindowSystem.Draw();

        internal void SaveConfig() => PluginInterface.SavePluginConfig(Config);

        public void RemoveRoll(RollEntry roll)
        {
            if (!RollHistory.Contains(roll)) return;
            RollHistory.Remove(roll);
            SaveConfig();
        }

        public RollEntry? GetCurrentWinner()
        {
            if (RollHistory.Count == 0) return null;
            if (TargetMode)
            {
                if (ThresholdMode) return null;
                if (TargetNumbers.Count == 0) return null;
                if (ClosestWins)
                    return RollHistory.OrderBy(r => TargetNumbers.Min(t => Math.Abs(r.RollValue - t))).ThenBy(r => r.Time).FirstOrDefault();
                return RollHistory.Where(r => TargetNumbers.Contains(r.RollValue)).OrderByDescending(r => r.Time).FirstOrDefault();
            }
            return HighWins ? RollHistory.OrderByDescending(r => r.RollValue).FirstOrDefault() : RollHistory.OrderBy(r => r.RollValue).FirstOrDefault();
        }

        public List<RollEntry> GetThresholdQualifiers()
        {
            if (!TargetMode || !ThresholdMode || RollHistory.Count == 0) return new List<RollEntry>();
            return ThresholdAbove
                ? RollHistory.Where(r => r.RollValue >= ThresholdValue).OrderByDescending(r => r.RollValue).ToList()
                : RollHistory.Where(r => r.RollValue <= ThresholdValue).OrderBy(r => r.RollValue).ToList();
        }

        public void SnapLap()
        {
            if (RollHistory.Count == 0) return;
            string winnerName;
            int winnerRoll;
            if (TargetMode && ThresholdMode)
            {
                var qualifiers = GetThresholdQualifiers();
                var dir = ThresholdAbove ? ">=" : "<=";
                winnerName = qualifiers.Count > 0
                    ? $"{qualifiers.Count} qualify ({dir}{ThresholdValue})"
                    : $"No qualifiers ({dir}{ThresholdValue})";
                winnerRoll = 0;
            }
            else
            {
                var winner = GetCurrentWinner();
                winnerName = winner?.PlayerName ?? string.Empty;
                winnerRoll = winner?.RollValue ?? 0;
            }
            Laps.Insert(0, new LapEntry
            {
                Number = Laps.Count + 1,
                Time = DateTime.Now,
                WinnerName = winnerName,
                WinnerRoll = winnerRoll,
                Rolls = RollHistory.ToList()
            });
            RollHistory.Clear();
            SaveConfig();
        }

        private void CleanOldRolls()
        {
            if (ClearMinutes <= 0) return;
            var cutoff = DateTime.Now.AddMinutes(-ClearMinutes);
            int removed = RollHistory.RemoveAll(r => r.Time < cutoff);
            if (removed > 0) SaveConfig();
        }

        private string CleanPlayerName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "Unknown";
            if (rawName == "You") return "You";

            string spacedName = Regex.Replace(rawName, "([a-z])([A-Z])", "$1 $2");
            var parts = spacedName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 3) return $"{parts[0]} {parts[1]}@{parts[2]}";
            else if (parts.Length >= 2) return $"{parts[0]} {parts[1]}";

            return spacedName;
        }

        private void OnChatMessage(IHandleableChatMessage msg)
        {
            if (msg.LogKind == XivChatType.Echo) return;
            if (!IsRecording) return;

            CleanOldRolls();

            var text = msg.Message.ToString();
            if (!text.Contains("Random!")) return;

            string capturedName = "";
            int rollValue = -1;

            var standardMatch = Regex.Match(text, @"Random! (.+?) roll(?:s)? a (\d+)");
            var diceMatch = Regex.Match(text, @"Random!\s+(\d+)");

            if (standardMatch.Success)
            {
                capturedName = standardMatch.Groups[1].Value;
                rollValue = int.Parse(standardMatch.Groups[2].Value);
            }
            else if (diceMatch.Success)
            {
                capturedName = msg.Sender.ToString();
                rollValue = int.Parse(diceMatch.Groups[1].Value);
            }

            if (rollValue != -1)
            {
                if (string.IsNullOrEmpty(capturedName)) capturedName = msg.Sender.ToString();
                capturedName = CleanPlayerName(capturedName);

                if (!string.IsNullOrEmpty(LockedTargetName))
                {
                    var baseNameFromRoll = capturedName.Split('@')[0];
                    if (baseNameFromRoll != LockedTargetName) return;
                }

                if (OneRollPerPerson)
                {
                    bool alreadyRolled = RollHistory.Any(x => x.PlayerName == capturedName);
                    if (alreadyRolled) return;
                }

                RollHistory.Add(new RollEntry
                {
                    PlayerName = capturedName,
                    RollValue = rollValue,
                    Time = DateTime.Now
                });

                Config.LifetimeTotalRolls++;
                Config.LifetimeRollSum += rollValue;
                if (Config.LifetimeTotalRolls == 1 || rollValue > Config.LifetimeHighestRoll)
                {
                    Config.LifetimeHighestRoll = rollValue;
                    Config.LifetimeHighestPlayer = capturedName;
                }
                if (Config.LifetimeTotalRolls == 1 || rollValue < Config.LifetimeLowestRoll)
                {
                    Config.LifetimeLowestRoll = rollValue;
                    Config.LifetimeLowestPlayer = capturedName;
                }
                SaveConfig();

                bool isWinner = TargetMode && TargetNumbers.Contains(rollValue);
                bool isFunny = FunnyNumbers.Contains(rollValue);
                var typeMsg = (isWinner || isFunny) ? NotificationType.Warning : NotificationType.Success;

                NotificationManager.AddNotification(new Notification
                {
                    Content = $"{capturedName} rolled {rollValue}!",
                    Title = isWinner ? "WINNER FOUND!" : "Roll Tracker",
                    Type = typeMsg
                });
            }
        }

        public void TargetPlayerByName(string fullName)
        {
            var searchName = fullName.Split('@')[0];
            var player = ObjectTable.FirstOrDefault(obj => obj.Name.ToString() == searchName);

            if (player != null) TargetManager.Target = player;
            else ChatGui.PrintError($"[Roll Tracker] Could not find '{searchName}' nearby.");
        }

        public void Announce(string template, RollEntry? winner = null)
        {
            string message = template;

            if (winner != null)
            {
                var shortName = winner.PlayerName.Split('@')[0];
                message = message.Replace("{winner}", shortName)
                                 .Replace("{roll}", winner.RollValue.ToString());
            }

            if (TargetMode && TargetNumbers.Count > 0)
            {
                message = message.Replace("{target}", string.Join(", ", TargetNumbers));
            }
            else
            {
                message = message.Replace("{target}", "Highest");
            }

            CommandManager.ProcessCommand($"/s {message}");
        }
    }
}
