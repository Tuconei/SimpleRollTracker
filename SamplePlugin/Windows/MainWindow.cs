using System;
using System.Numerics;
using System.Linq;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using SamplePlugin;

// API 13 Compatibility Aliases
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiTableFlags = Dalamud.Bindings.ImGui.ImGuiTableFlags;
using ImGuiTableColumnFlags = Dalamud.Bindings.ImGui.ImGuiTableColumnFlags;
using ImGuiCond = Dalamud.Bindings.ImGui.ImGuiCond;

namespace SamplePlugin.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private Plugin plugin;

        public MainWindow(Plugin plugin) : base("Simple Roll Tracker")
        {
            this.Size = new Vector2(375, 520);
            this.SizeCondition = ImGuiCond.FirstUseEver;
            this.plugin = plugin;
        }

        public void Dispose() { }

        public override void Draw()
        {
            // --- CONTROL PANEL ---
            if (ImGui.Button(this.plugin.IsRecording ? "STOP CAPTURING" : "START CAPTURING"))
                this.plugin.IsRecording = !this.plugin.IsRecording;

            ImGui.SameLine();
            if (ImGui.Button("Clear List"))
                this.plugin.RollHistory.Clear();

            ImGui.Separator();

            // --- GAME MODES ---
            // Updated Label: EXACT wins
            if (ImGui.Checkbox("Target Mode (EXACT wins)", ref this.plugin.TargetMode))
            {
                // Optional: Clear list when switching modes
                // this.plugin.RollHistory.Clear(); 
            }

            if (this.plugin.TargetMode)
            {
                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("Target #", ref this.plugin.TargetNumber);
            }
            else
            {
                var modeText = this.plugin.HighWins ? "Mode: HIGHEST Wins" : "Mode: LOWEST Wins";
                if (ImGui.Button(modeText))
                    this.plugin.HighWins = !this.plugin.HighWins;
            }

            ImGui.Separator();

            // --- THE WINNER'S PODIUM ---
            Plugin.RollEntry winner = null;

            if (this.plugin.RollHistory.Count > 0)
            {
                if (this.plugin.TargetMode)
                {
                    // LOGIC CHANGE: Only look for EXACT matches.
                    // We take the FIRST person to hit it (classic contest rules).
                    winner = this.plugin.RollHistory
                        .Where(r => r.RollValue == this.plugin.TargetNumber)
                        .FirstOrDefault();
                }
                else
                {
                    // STANDARD LOGIC
                    if (this.plugin.HighWins)
                        winner = this.plugin.RollHistory.OrderByDescending(r => r.RollValue).FirstOrDefault();
                    else
                        winner = this.plugin.RollHistory.OrderBy(r => r.RollValue).FirstOrDefault();
                }
            }

            if (winner != null)
            {
                var windowWidth = ImGui.GetWindowSize().X;

                string winLabel;
                // Updated Label for Podium
                if (this.plugin.TargetMode) winLabel = $"🎯 MATCHED {this.plugin.TargetNumber} 🎯";
                else winLabel = this.plugin.HighWins ? "👑 HIGH ROLLER 👑" : "💀 LOW ROLLER 💀";

                // Center Label
                var labelWidth = ImGui.CalcTextSize(winLabel).X;
                ImGui.SetCursorPosX((windowWidth - labelWidth) * 0.5f);
                ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), winLabel);

                // Center Name
                var resultText = $"{winner.PlayerName}: {winner.RollValue}";
                var resultWidth = ImGui.CalcTextSize(resultText).X;
                ImGui.SetCursorPosX((windowWidth - resultWidth) * 0.5f);
                ImGui.Text(resultText);

                // COPY BUTTON
                if (ImGui.Button("Copy Result", new Vector2(-1, 0)))
                {
                    var winType = this.plugin.TargetMode ? $"Exact Match {this.plugin.TargetNumber}" : (this.plugin.HighWins ? "Highest" : "Lowest");
                    ImGui.SetClipboardText($"Current Winner ({winType}): {winner.PlayerName} with {winner.RollValue}!");
                }
            }
            else
            {
                // Text changed to reflect that we are waiting for a specific number
                if (this.plugin.TargetMode)
                    ImGui.Text($"Waiting for a {this.plugin.TargetNumber}...");
                else
                    ImGui.Text("Waiting for rolls...");
            }

            ImGui.Separator();

            // --- STATS & TABLE ---
            ImGui.Text($"Total Rolls: {this.plugin.RollHistory.Count}");

            if (ImGui.BeginTable("RollsTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
            {
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 70);
                ImGui.TableSetupColumn("Player");
                ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 50);
                ImGui.TableHeadersRow();

                foreach (var roll in ((System.Collections.Generic.IEnumerable<Plugin.RollEntry>)this.plugin.RollHistory).Reverse())
                {
                    ImGui.TableNextRow();

                    // COLOR LOGIC
                    Vector4 textColor = new Vector4(1, 1, 1, 1); // Default White

                    if (this.plugin.TargetMode)
                    {
                        // If in Target Mode, ONLY highlight the exact match
                        if (roll.RollValue == this.plugin.TargetNumber)
                            textColor = new Vector4(0, 1, 0, 1); // Bright Green for Winner
                    }
                    else
                    {
                        // Standard Mode: Gold for 777
                        if (roll.RollValue == 777)
                            textColor = new Vector4(1, 0.8f, 0, 1);
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextColored(textColor, roll.Time.ToString("HH:mm"));
                    ImGui.TableNextColumn();
                    ImGui.TextColored(textColor, roll.PlayerName);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(textColor, roll.RollValue.ToString());
                }
                ImGui.EndTable();
            }
        }
    }
}
