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
        private readonly Plugin plugin;
        private int nextTargetInput;
        private int nextFunnyInput;
        private string playerHistoryFilter = string.Empty;
        private string selectedHistoryPlayer = string.Empty;
        private bool openPlayerHistory;

        public MainWindow(Plugin plugin) : base("Simple Roll Tracker")
        {
            this.Size = new Vector2(400, 850);
            this.SizeCondition = ImGuiCond.FirstUseEver;
            this.plugin = plugin;
        }

        public void Dispose() { }

        public override void Draw()
        {
            var winner = this.plugin.GetCurrentWinner();

            // --- CONTROL ---
            if (ImGui.Button(this.plugin.IsRecording ? "STOP CAPTURING" : "START CAPTURING"))
            {
                this.plugin.IsRecording = !this.plugin.IsRecording;
                this.plugin.Config.IsRecording = this.plugin.IsRecording;
                this.plugin.SaveConfig();
            }

            ImGui.SameLine();
            if (ImGui.Button("Clear List"))
            {
                this.plugin.RollHistory.Clear();
                this.plugin.SaveConfig();
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt("Clear > X Mins", ref this.plugin.ClearMinutes))
            {
                if (this.plugin.ClearMinutes < 0) this.plugin.ClearMinutes = 0;
                this.plugin.Config.ClearMinutes = this.plugin.ClearMinutes;
                this.plugin.SaveConfig();
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

            ImGui.Checkbox("Allow only 1 roll per person", ref this.plugin.OneRollPerPerson);

            ImGui.Separator();

            // --- TARGET MODE ---
            ImGui.Checkbox("Target Mode", ref this.plugin.TargetMode);

            if (this.plugin.TargetMode)
            {
                if (ImGui.Checkbox("Threshold", ref this.plugin.ThresholdMode))
                {
                    if (this.plugin.ThresholdMode) this.plugin.ClosestWins = false;
                }

                if (this.plugin.ThresholdMode)
                {
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(80);
                    ImGui.InputInt("##ThreshVal", ref this.plugin.ThresholdValue, 0);
                    ImGui.SameLine();
                    if (ImGui.Button(this.plugin.ThresholdAbove ? "Above" : "Below"))
                        this.plugin.ThresholdAbove = !this.plugin.ThresholdAbove;
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to toggle Above / Below");
                }
                else
                {
                    ImGui.SameLine();
                    if (ImGui.Checkbox("Closest Wins", ref this.plugin.ClosestWins))
                        if (this.plugin.ClosestWins) this.plugin.ThresholdMode = false;

                    ImGui.SetNextItemWidth(100);
                    ImGui.InputInt("##NewTarget", ref nextTargetInput, 0);
                    ImGui.SameLine();
                    if (ImGui.Button("Add #"))
                    {
                        if (!this.plugin.TargetNumbers.Contains(nextTargetInput) && nextTargetInput > 0)
                            this.plugin.TargetNumbers.Add(nextTargetInput);
                    }

                    if (this.plugin.TargetNumbers.Count > 0)
                    {
                        ImGui.Text("Targets:");
                        foreach (var num in this.plugin.TargetNumbers.ToList())
                        {
                            ImGui.SameLine();
                            if (ImGui.SmallButton($"{num} x"))
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
            if (ImGui.CollapsingHeader("Analytics"))
            {
                var cfg = this.plugin.Config;
                if (cfg.LifetimeTotalRolls > 0)
                {
                    ImGui.Text($"Total Rolls: {cfg.LifetimeTotalRolls}");
                    ImGui.Text($"Highest Roll: {cfg.LifetimeHighestRoll} ({cfg.LifetimeHighestPlayer})");
                    ImGui.Text($"Lowest Roll:  {cfg.LifetimeLowestRoll} ({cfg.LifetimeLowestPlayer})");
                    ImGui.Text($"Average Roll: {(double)cfg.LifetimeRollSum / cfg.LifetimeTotalRolls:F1}");

                    ImGui.Spacing();
                    if (ImGui.Button("Reset Analytics"))
                    {
                        cfg.LifetimeTotalRolls = 0;
                        cfg.LifetimeRollSum = 0;
                        cfg.LifetimeHighestRoll = 0;
                        cfg.LifetimeHighestPlayer = string.Empty;
                        cfg.LifetimeLowestRoll = 0;
                        cfg.LifetimeLowestPlayer = string.Empty;
                        this.plugin.SaveConfig();
                    }
                }
                else
                {
                    ImGui.TextDisabled("No data recorded yet.");
                }
            }

            ImGui.Separator();

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

            // --- WINNER / QUALIFIERS ---
            if (this.plugin.TargetMode && this.plugin.ThresholdMode)
            {
                var qualifiers = this.plugin.GetThresholdQualifiers();
                var dir = this.plugin.ThresholdAbove ? ">=" : "<=";
                var windowWidth = ImGui.GetWindowSize().X;

                if (qualifiers.Count > 0)
                {
                    var header = $"🎯 {qualifiers.Count} QUALIFIER{(qualifiers.Count == 1 ? "" : "S")} ({dir}{this.plugin.ThresholdValue}) 🎯";
                    var hw = ImGui.CalcTextSize(header).X;
                    ImGui.SetCursorPosX((windowWidth - hw) * 0.5f);
                    ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), header);

                    foreach (var q in qualifiers)
                    {
                        var qt = $"{q.PlayerName}: {q.RollValue}";
                        var qw = ImGui.CalcTextSize(qt).X;
                        ImGui.SetCursorPosX((windowWidth - qw) * 0.5f);
                        ImGui.PushID("q" + q.PlayerName + q.Time.Ticks);
                        if (ImGui.Selectable(qt, false, ImGuiSelectableFlags.None, new Vector2(qw, 0)))
                            this.plugin.TargetPlayerByName(q.PlayerName);
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to Target");
                        ImGui.PopID();
                    }
                }
                else
                {
                    ImGui.Text($"Waiting for rolls {dir} {this.plugin.ThresholdValue}...");
                }
            }
            else if (winner != null)
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
                    this.plugin.TargetPlayerByName(winner.PlayerName);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Click to Target Winner");

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
                if (this.plugin.TargetMode) ImGui.Text("Waiting for matches...");
                else ImGui.Text("Waiting for rolls...");
            }

            ImGui.Separator();

            // --- PREVIOUS ROUNDS ---
            if (ImGui.Button("New Round"))
                this.plugin.SnapLap();

            if (this.plugin.Laps.Count > 0)
            {
                ImGui.SameLine();
                if (ImGui.CollapsingHeader($"Previous Rounds ({this.plugin.Laps.Count})"))
                {
                    if (ImGui.Button("Clear Rounds"))
                    {
                        this.plugin.Laps.Clear();
                        this.plugin.SaveConfig();
                    }

                    foreach (var lap in this.plugin.Laps)
                    {
                        var lapLabel = string.IsNullOrEmpty(lap.WinnerName)
                            ? $"Round {lap.Number} — No winner  ({lap.Rolls.Count} rolls)  {lap.Time:HH:mm}"
                            : $"Round {lap.Number} — {lap.WinnerName}: {lap.WinnerRoll}  ({lap.Rolls.Count} rolls)  {lap.Time:HH:mm}";

                        ImGui.PushID($"lap{lap.Number}");
                        if (ImGui.TreeNode(lapLabel))
                        {
                            if (ImGui.BeginTable($"LapTable{lap.Number}", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                            {
                                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 70);
                                ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.None);
                                ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 45);
                                ImGui.TableHeadersRow();

                                foreach (var roll in lap.Rolls.OrderByDescending(r => r.RollValue))
                                {
                                    bool isWin = roll.PlayerName == lap.WinnerName && roll.RollValue == lap.WinnerRoll;
                                    var color = isWin ? new Vector4(1, 0.8f, 0, 1) : new Vector4(1, 1, 1, 1);
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn(); ImGui.TextColored(color, roll.Time.ToString("HH:mm:ss"));
                                    ImGui.TableNextColumn();
                                    ImGui.PushStyleColor(ImGuiCol.Text, color);
                                    if (ImGui.Selectable(roll.PlayerName))
                                    {
                                        this.selectedHistoryPlayer = roll.PlayerName;
                                        this.playerHistoryFilter = string.Empty;
                                        this.openPlayerHistory = true;
                                    }
                                    ImGui.PopStyleColor();
                                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open player history");
                                    ImGui.TableNextColumn(); ImGui.TextColored(color, roll.RollValue.ToString());
                                }
                                ImGui.EndTable();
                            }
                            ImGui.TreePop();
                        }
                        ImGui.PopID();
                    }
                }
                ImGui.Separator();
            }

            // --- PLAYER HISTORY ---
            if (this.openPlayerHistory)
            {
                ImGui.SetNextItemOpen(true, ImGuiCond.Always);
                this.openPlayerHistory = false;
            }

            if (ImGui.CollapsingHeader("Player History"))
            {
                var savedPlayers = this.plugin.Laps
                    .SelectMany(lap => lap.Rolls)
                    .Select(roll => roll.PlayerName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (savedPlayers.Count == 0)
                {
                    ImGui.TextDisabled("Save a round to build player history.");
                }
                else
                {
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputTextWithHint("##PlayerHistoryFilter", "Search players...", ref this.playerHistoryFilter, 100);

                    var filteredPlayers = savedPlayers
                        .Where(name => string.IsNullOrWhiteSpace(this.playerHistoryFilter)
                            || name.Contains(this.playerHistoryFilter, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    ImGui.SetNextItemWidth(-1);
                    var preview = string.IsNullOrEmpty(this.selectedHistoryPlayer)
                        ? "Select a player..."
                        : this.selectedHistoryPlayer;

                    if (ImGui.BeginCombo("##PlayerHistorySelect", preview))
                    {
                        foreach (var playerName in filteredPlayers)
                        {
                            var isSelected = playerName.Equals(this.selectedHistoryPlayer, StringComparison.OrdinalIgnoreCase);
                            if (ImGui.Selectable(playerName, isSelected))
                                this.selectedHistoryPlayer = playerName;
                            if (isSelected)
                                ImGui.SetItemDefaultFocus();
                        }

                        if (filteredPlayers.Count == 0)
                            ImGui.TextDisabled("No matching players.");

                        ImGui.EndCombo();
                    }

                    var playerRolls = this.plugin.Laps
                        .SelectMany(lap => lap.Rolls
                            .Where(roll => roll.PlayerName.Equals(this.selectedHistoryPlayer, StringComparison.OrdinalIgnoreCase))
                            .Select(roll => new { Lap = lap, Roll = roll }))
                        .OrderByDescending(entry => entry.Lap.Time)
                        .ThenByDescending(entry => entry.Roll.Time)
                        .ToList();

                    if (playerRolls.Count > 0)
                    {
                        var roundsPlayed = playerRolls.Select(entry => entry.Lap).Distinct().Count();
                        var wins = playerRolls
                            .Select(entry => entry.Lap)
                            .Distinct()
                            .Count(lap => lap.WinnerName.Equals(this.selectedHistoryPlayer, StringComparison.OrdinalIgnoreCase));

                        ImGui.Spacing();
                        ImGui.Text($"Saved rounds: {roundsPlayed}    Rolls: {playerRolls.Count}    Wins: {wins}");
                        ImGui.Text($"High: {playerRolls.Max(entry => entry.Roll.RollValue)}    " +
                                   $"Low: {playerRolls.Min(entry => entry.Roll.RollValue)}    " +
                                   $"Average: {playerRolls.Average(entry => entry.Roll.RollValue):F1}");

                        if (ImGui.BeginTable("PlayerHistoryTable", 4,
                            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable,
                            new Vector2(0, 220)))
                        {
                            ImGui.TableSetupColumn("Round", ImGuiTableColumnFlags.WidthFixed, 55);
                            ImGui.TableSetupColumn("Saved", ImGuiTableColumnFlags.WidthFixed, 120);
                            ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 50);
                            ImGui.TableSetupColumn("Result", ImGuiTableColumnFlags.None);
                            ImGui.TableHeadersRow();

                            foreach (var entry in playerRolls)
                            {
                                var isWinner = entry.Lap.WinnerName.Equals(
                                    this.selectedHistoryPlayer,
                                    StringComparison.OrdinalIgnoreCase);
                                var color = isWinner
                                    ? new Vector4(1, 0.8f, 0, 1)
                                    : new Vector4(1, 1, 1, 1);

                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.TextColored(color, entry.Lap.Number.ToString());
                                ImGui.TableNextColumn();
                                ImGui.TextColored(color, entry.Lap.Time.ToString("yyyy-MM-dd HH:mm"));
                                ImGui.TableNextColumn();
                                ImGui.TextColored(color, entry.Roll.RollValue.ToString());
                                ImGui.TableNextColumn();
                                ImGui.TextColored(color, isWinner ? "Winner" : entry.Lap.WinnerName);
                            }

                            ImGui.EndTable();
                        }
                    }
                    else if (!string.IsNullOrEmpty(this.selectedHistoryPlayer))
                    {
                        this.selectedHistoryPlayer = string.Empty;
                    }
                }

                ImGui.Separator();
            }

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

                    if (this.plugin.TargetMode && this.plugin.ThresholdMode)
                    {
                        bool qualifies = this.plugin.ThresholdAbove
                            ? roll.RollValue >= this.plugin.ThresholdValue
                            : roll.RollValue <= this.plugin.ThresholdValue;
                        if (qualifies) textColor = new Vector4(1, 0.8f, 0, 1);
                    }
                    else if (this.plugin.TargetMode && this.plugin.TargetNumbers.Count > 0)
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
