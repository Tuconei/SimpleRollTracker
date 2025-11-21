using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SamplePlugin.Windows;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;

using ImGui = Dalamud.Bindings.ImGui.ImGui;

namespace SamplePlugin
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Simple Roll Tracker";

        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static INotificationManager NotificationManager { get; private set; } = null!;

        public class RollEntry
        {
            public string PlayerName { get; set; }
            public int RollValue { get; set; }
            public DateTime Time { get; set; }
        }

        public List<RollEntry> RollHistory = new List<RollEntry>();

        public bool IsRecording = true;

        // TARGET VARIABLES
        public bool HighWins = true;
        public bool TargetMode = false;
        public int TargetNumber = 777;

        public WindowSystem WindowSystem = new("RollTracker");
        private MainWindow MainWindow { get; init; }

        public Plugin()
        {
            this.MainWindow = new MainWindow(this);
            this.WindowSystem.AddWindow(this.MainWindow);

            CommandManager.AddHandler("/rolls", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the roll tracker window."
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            ChatGui.ChatMessage += OnChatMessage;
        }

        public void Dispose()
        {
            this.WindowSystem.RemoveAllWindows();
            CommandManager.RemoveHandler("/rolls");
            ChatGui.ChatMessage -= OnChatMessage;
        }

        private void OnCommand(string command, string args) => MainWindow.IsOpen = !MainWindow.IsOpen;
        private void DrawUI() => this.WindowSystem.Draw();

        // --- THE NAME-STEALING EAR ---
        private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (type == XivChatType.Echo) return;
            if (!this.IsRecording) return;

            var text = message.ToString();
            if (!text.Contains("Random!")) return;

            string capturedName = "";
            int rollValue = -1;

            // REGEX 1: The Standard Roll (Extract Name from Text)
            // Matches: "Random! [Cloud Strife] rolls a 99"
            // Group 1 = Name
            // Group 2 = Number
            var standardMatch = Regex.Match(text, @"Random! (.+?) roll(?:s)? a (\d+)");

            // REGEX 2: The Dice Roll (Short)
            // Matches: "Random! 586" (Name must be in 'sender' for this type)
            var diceMatch = Regex.Match(text, @"Random!\s+(\d+)");

            if (standardMatch.Success)
            {
                capturedName = standardMatch.Groups[1].Value; // Grab name from text
                rollValue = int.Parse(standardMatch.Groups[2].Value);
            }
            else if (diceMatch.Success)
            {
                // For custom dice commands, the name IS usually in the sender field
                capturedName = sender.ToString();
                rollValue = int.Parse(diceMatch.Groups[1].Value);
            }

            if (rollValue != -1)
            {
                // Cleanup: If the name from text is "You", keep it "You".
                // If capturedName is somehow empty (rare), fallback to sender.
                if (string.IsNullOrEmpty(capturedName)) capturedName = sender.ToString();

                this.RollHistory.Add(new RollEntry
                {
                    PlayerName = capturedName,
                    RollValue = rollValue,
                    Time = DateTime.Now
                });

                NotificationManager.AddNotification(new Notification
                {
                    Content = $"{capturedName} rolled {rollValue}!",
                    Title = "Roll Tracker",
                    Type = NotificationType.Success
                });
            }
        }
    }
}
