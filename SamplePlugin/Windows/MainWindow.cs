#nullable enable
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using SimpleRollTracker;
using System;
using System.Linq;
using System.Numerics;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiCond = Dalamud.Bindings.ImGui.ImGuiCond;
using ImGuiSortDirection = Dalamud.Bindings.ImGui.ImGuiSortDirection;
using ImGuiTableColumnFlags = Dalamud.Bindings.ImGui.ImGuiTableColumnFlags;
using ImGuiTableFlags = Dalamud.Bindings.ImGui.ImGuiTableFlags;

namespace SimpleRollTracker.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private Plugin plugin;
        private int nextTargetInput = 0;
        private int nextFunnyInput = 0;

        public MainWindow(Plugin plugin) : base("Simple Roll Tracker")
        {
            this.Size = new Vector2(400, 850);
            this.SizeCondition = ImGuiCond.FirstUseEver;
            this.plugin = plugin;
        }

        public void Dispose() { }

        public override void Draw()
        {
            // --- CONTROL ---
            if (ImGui.Button(this.plugin.IsRecording ? "STOP CAPTURING" : "START CAPTURING"))
                this.plugin.IsRecording = !this.plugin.IsRecording;

            ImGui.SameLine();
            if (ImGui.Button("Clear List"))
                this.plugin.RollHistory.Clear();

            ImGui.SameLine();
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt("Clear > X Mins", ref this.plugin.ClearMinutes))
            {
                if (this.plugin.ClearMinutes < 0) this.plugin.ClearMinutes = 0;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Automatically remove rolls older than X minutes (0 = Disabled)");

            ImGui.Separator();

            // --- LOCK ---
            if (string.IsNullOrEmpty(this.plugin.LockedTargetName))
            {
                if (ImGui.Button("Lock to Current Target"))
                {
                    var currentTarget = Plugin.TargetManager.Target;
                    if (currentTarget != null) this.plugin.LockedTargetName = currentTarget.Name.ToString();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Only accept rolls from your currently selected target.");
            }
            else
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1), $"LOCKED: {this.plugin.LockedTargetName}");
                ImGui.SameLine();
                if (ImGui.Button("UNLOCK")) this.plugin.LockedTargetName = string.Empty;
            }

            if (ImGui.Checkbox("Allow only 1 roll per person", ref this.plugin.OneRollPerPerson)) { }

            ImGui.Separator();

            // --- TARGET MODE ---
            if (ImGui.Checkbox("Target Mode (Specific Wins)", ref this.plugin.TargetMode)) { }

            if (this.plugin.TargetMode)
            {
                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("##NewTarget", ref nextTargetInput, 0);
                ImGui.SameLine();
                if (ImGui.Button("Add #"))
                {
                    if (!this.plugin.TargetNumbers.Contains(nextTargetInput) && nextTargetInput > 0)
                        this.plugin.TargetNumbers.Add(nextTargetInput);
                }

                ImGui.SameLine();
                ImGui.Checkbox("Closest Wins", ref this.plugin.ClosestWins);

                if (this.plugin.TargetNumbers.Count > 0)
                {
                    ImGui.Text("Targets:");
                    foreach (var num in this.plugin.TargetNumbers.ToList())
                    {
                        ImGui.SameLine();
                        if (ImGui.SmallButton($"{num} x"))
                        {
                            this.plugin.TargetNumbers.Remove(num);
                        }
                    }
                }
            }
            else
            {
                var modeText = this.plugin.HighWins ? "Mode: HIGHEST Wins" : "Mode: LOWEST Wins";
                if (ImGui.Button(modeText))
                    this.plugin.HighWins = !this.plugin.HighWins;
            }

            ImGui.Separator();

            // --- ANALYTICS ---
            if (ImGui.CollapsingHeader("Session Analytics"))
            {
                if (this.plugin.RollHistory.Count > 0)
                {
                    var highest = this.plugin.RollHistory.MaxBy(r => r.RollValue);
                    var lowest = this.plugin.RollHistory.MinBy(r => r.RollValue);
                    var avg = this.plugin.RollHistory.Average(r => r.RollValue);
                    var total = this.plugin.RollHistory.Count;

                    ImGui.Text($"Total Rolls: {total}");
                    ImGui.Text($"Highest Roll: {highest?.RollValue} ({highest?.PlayerName})");
                    ImGui.Text($"Lowest Roll: {lowest?.RollValue} ({lowest?.PlayerName})");
                    ImGui.Text($"Average Roll: {avg:F1}");
                }
                else
                {
                    ImGui.TextDisabled("No data recorded yet.");
                }
            }

            ImGui.Separator();

            // --- ANNOUNCEMENT SETTINGS (RESTORED) ---
            if (ImGui.CollapsingHeader("Announcement Template"))
            {
                ImGui.TextDisabled("Use {winner}, {roll} as placeholders.");
                ImGui.InputText("Template", ref this.plugin.MsgWinner, 256);
            }

            ImGui.Separator();

            // --- FUNNY NUMBERS ---
            if (ImGui.CollapsingHeader("Funny Numbers (Cyan Highlight)"))
            {
                ImGui.SetNextItemWidth(100);
                ImGui.InputInt("##NewFunny", ref nextFunnyInput, 0);
                ImGui.SameLine();
                if (ImGui.Button("Add Funny #"))
                {
                    if (!this.plugin.FunnyNumbers.Contains(nextFunnyInput) && nextFunnyInput >= 0)
                        this.plugin.FunnyNumbers.Add(nextFunnyInput);
                }

                if (this.plugin.FunnyNumbers.Count > 0)
                {
                    foreach (var num in this.plugin.FunnyNumbers.ToList())
                    {
                        ImGui.SameLine();
                        if (ImGui.SmallButton($"{num} x##funny"))
                        {
                            this.plugin.FunnyNumbers.Remove(num);
                        }
                    }
                }
            }

            ImGui.Separator();

            // --- WINNER ---
            Plugin.RollEntry? winner = null;

            if (this.plugin.RollHistory.Count > 0)
            {
                if (this.plugin.TargetMode && this.plugin.TargetNumbers.Count > 0)
                {
                    if (this.plugin.ClosestWins)
                    {
                        winner = this.plugin.RollHistory
                            .OrderBy(r => this.plugin.TargetNumbers.Min(t => Math.Abs(r.RollValue - t)))
                            .ThenBy(r => r.Time)
                            .FirstOrDefault();
                    }
                    else
                    {
                        winner = this.plugin.RollHistory
                            .Where(r => this.plugin.TargetNumbers.Contains(r.RollValue))
                            .OrderByDescending(r => r.Time)
                            .FirstOrDefault();
                    }
                }
                else if (!this.plugin.TargetMode)
                {
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
                if (this.plugin.TargetMode)
                {
                    if (this.plugin.ClosestWins)
                    {
                        int closestTarget = this.plugin.TargetNumbers.OrderBy(t => Math.Abs(winner.RollValue - t)).First();
                        int diff = Math.Abs(winner.RollValue - closestTarget);
                        winLabel = diff == 0 ? $"🎯 EXACT MATCH ({closestTarget}) 🎯" : $"🎯 CLOSEST TO {closestTarget} (Diff: {diff}) 🎯";
                    }
                    else
                    {
                        winLabel = $"🎯 WINNER! ({winner.RollValue}) 🎯";
                    }
                }
                else
                {
                    winLabel = this.plugin.HighWins ? "👑 HIGH ROLLER 👑" : "💀 LOW ROLLER 💀";
                }

                var labelWidth = ImGui.CalcTextSize(winLabel).X;
                ImGui.SetCursorPosX((windowWidth - labelWidth) * 0.5f);
                ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), winLabel);

                var resultText = $"{winner.PlayerName}: {winner.RollValue}";
                var resultWidth = ImGui.CalcTextSize(resultText).X;
                ImGui.SetCursorPosX((windowWidth - resultWidth) * 0.5f);

                if (ImGui.Selectable(resultText, false, ImGuiSelectableFlags.None, new Vector2(resultWidth, 0)))
                {
                    this.plugin.TargetPlayerByName(winner.PlayerName);
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to Target Winner");

                // --- COPY ANNOUNCEMENT BUTTON (RESTORED) ---
                if (ImGui.Button("Copy Announcement", new Vector2(-1, 0)))
                {
                    string message = this.plugin.MsgWinner;
                    var shortName = winner.PlayerName.Split('@')[0];
                    message = message.Replace("{winner}", shortName)
                                     .Replace("{roll}", winner.RollValue.ToString());

                    ImGui.SetClipboardText(message);
                }
            }
            else
            {
                if (this.plugin.TargetMode) ImGui.Text($"Waiting for matches...");
                else ImGui.Text("Waiting for rolls...");
            }

            ImGui.Separator();

            ImGui.Text($"Total Rolls: {this.plugin.RollHistory.Count}");
            ImGui.SameLine();
            ImGui.TextDisabled("(Click name to target)");

            // --- TABLE ---
            if (ImGui.BeginTable("RollsTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Sortable | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort, 90, 0);
                ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.None, 0, 1);
                ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 50, 2);
                ImGui.TableSetupColumn("Del", ImGuiTableColumnFlags.WidthFixed, 45, 3);
                ImGui.TableHeadersRow();

                var sortedList = this.plugin.RollHistory.ToList();
                var sortSpecs = ImGui.TableGetSortSpecs();
                if (sortSpecs.SpecsCount > 0)
                {
                    var spec = sortSpecs.Specs;
                    switch (spec.ColumnIndex)
                    {
                        case 0: sortedList = spec.SortDirection == ImGuiSortDirection.Ascending ? sortedList.OrderBy(r => r.Time).ToList() : sortedList.OrderByDescending(r => r.Time).ToList(); break;
                        case 1: sortedList = spec.SortDirection == ImGuiSortDirection.Ascending ? sortedList.OrderBy(r => r.PlayerName).ToList() : sortedList.OrderByDescending(r => r.PlayerName).ToList(); break;
                        case 2: sortedList = spec.SortDirection == ImGuiSortDirection.Ascending ? sortedList.OrderBy(r => r.RollValue).ToList() : sortedList.OrderByDescending(r => r.RollValue).ToList(); break;
                    }
                }
                else sortedList = sortedList.OrderByDescending(r => r.Time).ToList();

                foreach (var roll in sortedList)
                {
                    ImGui.TableNextRow();

                    Vector4 textColor = new Vector4(1, 1, 1, 1);

                    if (this.plugin.TargetMode && this.plugin.TargetNumbers.Count > 0)
                    {
                        if (this.plugin.TargetNumbers.Contains(roll.RollValue)) textColor = new Vector4(0, 1, 0, 1);
                        else if (this.plugin.ClosestWins && winner != null && roll == winner) textColor = new Vector4(1, 0.8f, 0, 1);
                    }
                    else if (!this.plugin.TargetMode && roll.RollValue == 777) textColor = new Vector4(1, 0.8f, 0, 1);
                    else if (this.plugin.FunnyNumbers.Contains(roll.RollValue)) textColor = new Vector4(0, 1, 1, 1);

                    ImGui.TableNextColumn();
                    ImGui.TextColored(textColor, roll.Time.ToString("HH:mm:ss"));

                    ImGui.TableNextColumn();
                    if (roll.PlayerName == "You")
                    {
                        ImGui.TextColored(textColor, "You");
                    }
                    else
                    {
                        ImGui.PushID(roll.PlayerName + roll.Time.Ticks);
                        if (ImGui.Selectable(roll.PlayerName))
                        {
                            this.plugin.TargetPlayerByName(roll.PlayerName);
                        }
                        ImGui.PopID();
                    }

                    ImGui.TableNextColumn();
                    ImGui.TextColored(textColor, roll.RollValue.ToString());

                    ImGui.TableNextColumn();
                    ImGui.PushID("del" + roll.PlayerName + roll.Time.Ticks);
                    if (ImGui.SmallButton("x"))
                    {
                        this.plugin.RemoveRoll(roll);
                    }
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
        }
    }
}
