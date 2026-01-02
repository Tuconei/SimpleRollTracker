#nullable enable
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
        public string Name => "Simple Roll Tracker";

        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;

        [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
        [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;

        public class RollEntry
        {
            public string PlayerName { get; set; } = string.Empty;
            public int RollValue { get; set; }
            public DateTime Time { get; set; }
        }

        public List<RollEntry> RollHistory = new List<RollEntry>();

        public bool IsRecording = true;
        public bool HighWins = true;
        public bool TargetMode = false;
        public bool ClosestWins = false;

        public List<int> TargetNumbers = new List<int>();
        public List<int> FunnyNumbers = new List<int> { 0, 1, 69, 420, 777, 999 };

        public int ClearMinutes = 0;
        public string LockedTargetName = string.Empty;
        public bool OneRollPerPerson = false;

        // --- NEW: CHAT TEMPLATES ---
        public string MsgOpen = "Rolls are now OPEN! Type /random to play!";
        public string MsgClose = "Rolls are now CLOSED! Calculating winner...";
        public string MsgWinner = "Congratulations to {winner} for rolling a {roll}!";

        public WindowSystem WindowSystem = new("RollTracker");
        private MainWindow MainWindow { get; init; }

        public Plugin()
        {
            this.MainWindow = new MainWindow(this);
            this.WindowSystem.AddWindow(this.MainWindow);

            CommandManager.AddHandler("/rolls", new CommandInfo(OnCommand) { HelpMessage = "Opens window." });
            CommandManager.AddHandler("/startrolls", new CommandInfo(OnMacroCommand) { HelpMessage = "Starts recording." });
            CommandManager.AddHandler("/stoprolls", new CommandInfo(OnMacroCommand) { HelpMessage = "Stops recording." });
            CommandManager.AddHandler("/clearrolls", new CommandInfo(OnMacroCommand) { HelpMessage = "Clears history." });

            PluginInterface.UiBuilder.Draw += DrawUI;
            ChatGui.ChatMessage += OnChatMessage;
        }

        public void Dispose()
        {
            this.WindowSystem.RemoveAllWindows();
            CommandManager.RemoveHandler("/rolls");
            CommandManager.RemoveHandler("/startrolls");
            CommandManager.RemoveHandler("/stoprolls");
            CommandManager.RemoveHandler("/clearrolls");
            ChatGui.ChatMessage -= OnChatMessage;
        }

        private void OnCommand(string command, string args) => MainWindow.IsOpen = !MainWindow.IsOpen;

        private void OnMacroCommand(string command, string args)
        {
            if (command == "/startrolls") { this.IsRecording = true; ChatGui.Print("Roll Tracker: Recording STARTED."); }
            else if (command == "/stoprolls") { this.IsRecording = false; ChatGui.Print("Roll Tracker: Recording STOPPED."); }
            else if (command == "/clearrolls") { this.RollHistory.Clear(); ChatGui.Print("Roll Tracker: History CLEARED."); }
        }

        private void DrawUI() => this.WindowSystem.Draw();

        public void RemoveRoll(RollEntry roll)
        {
            if (RollHistory.Contains(roll)) RollHistory.Remove(roll);
        }

        private void CleanOldRolls()
        {
            if (this.ClearMinutes <= 0) return;
            var cutoff = DateTime.Now.AddMinutes(-this.ClearMinutes);
            this.RollHistory.RemoveAll(r => r.Time < cutoff);
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

        private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (type == XivChatType.Echo) return;
            if (!this.IsRecording) return;

            CleanOldRolls();

            var text = message.ToString();
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
                capturedName = sender.ToString();
                rollValue = int.Parse(diceMatch.Groups[1].Value);
            }

            if (rollValue != -1)
            {
                if (string.IsNullOrEmpty(capturedName)) capturedName = sender.ToString();
                capturedName = CleanPlayerName(capturedName);

                if (!string.IsNullOrEmpty(this.LockedTargetName))
                {
                    // Compare base names only (strip @Server for cross-world players)
                    var baseNameFromRoll = capturedName.Split('@')[0];
                    if (baseNameFromRoll != this.LockedTargetName) return;
                }

                if (this.OneRollPerPerson)
                {
                    bool alreadyRolled = this.RollHistory.Any(x => x.PlayerName == capturedName);
                    if (alreadyRolled) return;
                }

                this.RollHistory.Add(new RollEntry
                {
                    PlayerName = capturedName,
                    RollValue = rollValue,
                    Time = DateTime.Now
                });

                bool isWinner = this.TargetMode && this.TargetNumbers.Contains(rollValue);
                bool isFunny = this.FunnyNumbers.Contains(rollValue);
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

        // --- NEW: CHAT ANNOUNCER HELPER ---
        // Sends the message to the default chat channel (usually /say or /party depending on context)
        // For plugins, we often have to "ExecuteCommand" to speak.
        public void Announce(string template, RollEntry? winner = null)
        {
            string message = template;

            // Replace Placeholders
            if (winner != null)
            {
                // Strip @World for chat aesthetics
                var shortName = winner.PlayerName.Split('@')[0];
                message = message.Replace("{winner}", shortName)
                                 .Replace("{roll}", winner.RollValue.ToString());
            }

            // Inject global variables
            if (this.TargetMode && this.TargetNumbers.Count > 0)
            {
                message = message.Replace("{target}", string.Join(", ", this.TargetNumbers));
            }
            else
            {
                message = message.Replace("{target}", "Highest");
            }

            // Send to chat!
            // We use /s (Say) by default, or you could make this configurable.
            // Safer to copy to clipboard? Or force send?
            // Let's force send to /say for now as it's a venue tool.
            CommandManager.ProcessCommand($"/s {message}");
        }
    }
}
