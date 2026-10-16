using ArknightsBaseOptimizer.Data;
using ArknightsBaseOptimizer.Models;
using ArknightsBaseOptimizer.Optimization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static ArknightsBaseOptimizer.Optimization.PlanModels;

namespace ArknightsBaseOptimizer
{
    /// <summary>
    /// How full a room's storage is at check-in time, for the fill bar.
    /// </summary>
    public record StorageFill(double Filled, double Capacity, double HoursUntilFull);

    /// <summary>
    /// Shows the optimized base as a map of rooms with operator icons.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double CheckInIntervalHours = 24;
        private const int FactoryCount = 4;
        private const int DormitoryCount = 4;
        private const int DormitorySeats = 5;
        private const double AvatarSize = 64;
        private const double DormitoryAvatarSize = 44;
        private double goldSupplyRatio = 1;

        private static readonly Color ControlCenterColor = Color.FromRgb(0x4F, 0xC3, 0xC7);
        private static readonly Color TradingPostColor = Color.FromRgb(0x4A, 0x9B, 0xE8);
        private static readonly Color FactoryColor = Color.FromRgb(0xF2, 0xC1, 0x4E);
        private static readonly Color PowerPlantColor = Color.FromRgb(0x8B, 0xC3, 0x4A);
        private static readonly Color DormitoryColor = Color.FromRgb(0xB3, 0x88, 0xEB);
        private static readonly Color SideRoomColor = Color.FromRgb(0x8A, 0x8A, 0x95);
        private static readonly Color WarningColor = Color.FromRgb(0xFF, 0x9F, 0x43);
        private static readonly Color ButtonColor = Color.FromRgb(0x3A, 0x3A, 0x42);
        private static readonly Brush TileBackground = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x31));

        private readonly Dictionary<bool, OptimizationResults> resultsByPromotionMode = new();
        private bool assumeFullyPromoted;
        private OptimizationResults? optimizationResults;
        private OptimizationGoal currentGoal = OptimizationGoal.Balanced;
        private bool showingShiftB;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// Runs the first optimization once the window is visible.
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs eventArgs)
        {
            await RunOptimizationSafelyAsync();
        }

        /// <summary>
        /// Toggles between current levels and the everyone-max-promotion preview.
        /// </summary>
        private async void PromotionButton_Click(object sender, RoutedEventArgs eventArgs)
        {
            assumeFullyPromoted = !assumeFullyPromoted;
            PromotionButton.Content = assumeFullyPromoted ? "Everyone max promotion" : "Current levels";
            PromotionButton.Background = new SolidColorBrush(assumeFullyPromoted ? WarningColor : ButtonColor);
            PromotionButton.Foreground = assumeFullyPromoted ? Brushes.Black : Brushes.White;
            await RunOptimizationSafelyAsync();
        }

        /// <summary>
        /// Optimizes for the current promotion mode (cached after the first time) and shows any error in a dialog.
        /// </summary>
        private async Task RunOptimizationSafelyAsync()
        {
            PromotionButton.IsEnabled = false;
            try
            {
                bool promotionMode = assumeFullyPromoted;
                if (!resultsByPromotionMode.TryGetValue(promotionMode, out OptimizationResults? results))
                {
                    SummaryText.Text = promotionMode ? "Optimizing with everyone at max promotion…" : "Optimizing with current levels…";
                    List<RosterOperator> roster = RosterLoader.LoadRoster(promotionMode);
                    results = await Task.Run(() => BaseOptimizer.Optimize(roster, CheckInIntervalHours));
                    resultsByPromotionMode[promotionMode] = results;
                }
                optimizationResults = results;
                RenderPlan();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.ToString(), "Failed to optimize", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                PromotionButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Balanced / LMD / EXP buttons: the button's Tag holds the OptimizationGoal name.
        /// </summary>
        private void GoalButton_Click(object sender, RoutedEventArgs eventArgs)
        {
            currentGoal = Enum.Parse<OptimizationGoal>((string)((Button)sender).Tag);
            RenderPlan();
        }

        /// <summary>
        /// Switches between showing Shift A and Shift B working.
        /// </summary>
        private void ShiftButton_Click(object sender, RoutedEventArgs eventArgs)
        {
            showingShiftB = !showingShiftB;
            RenderPlan();
        }

        /// <summary>
        /// Redraws every room for the selected goal and shift.
        /// </summary>
        private void RenderPlan()
        {
            if (optimizationResults == null)
            {
                return;
            }

            BasePlanResult plan = currentGoal switch { OptimizationGoal.Lmd => optimizationResults.Lmd, OptimizationGoal.Experience => optimizationResults.Experience, _ => optimizationResults.Balanced };
            goldSupplyRatio = plan.GoldSupplyRatio;
            ShiftResult shownShift = showingShiftB ? plan.ShiftB : plan.ShiftA;
            ShiftResult restingShift = showingShiftB ? plan.ShiftA : plan.ShiftB;

            int promotionsUsed = plan.ShiftA.Plan.AllWorkingOperators()
                                    .Concat(plan.ShiftB.Plan.AllWorkingOperators())
                                    .Count(rosterOperator => rosterOperator.PromotionChangesSkills);
            string promotionNote = assumeFullyPromoted ? $"  ·  preview: plan uses {promotionsUsed} promotions (orange badges)" : "";
            SummaryText.Text = $"{plan.LmdPerCheckIn:N0} LMD  ·  {plan.ExperiencePerCheckIn:N0} EXP per check-in  ·  {plan.GoldFactoryCount} gold / {FactoryCount - plan.GoldFactoryCount} EXP Factories  ·  gold {plan.GoldProducedPerCheckIn:F0} made / {plan.GoldNeededPerCheckIn:F0} needed{promotionNote}";
            ShiftButton.Content = showingShiftB ? "Showing Shift B  ⇄" : "Showing Shift A  ⇄";
            HighlightGoalButtons();

            ControlCenterSlot.Content = CreateControlCenterTile(shownShift);
            RenderProductionGrid(shownShift);
            RenderDormitories(restingShift, showingShiftB ? "A" : "B");
            RenderSideRooms(shownShift);
        }

        /// <summary>
        /// Fills the 3×3 production area in the in-game order: TP TP PP / F F PP / F F PP.
        /// </summary>
        private void RenderProductionGrid(ShiftResult shift)
        {
            ProductionGrid.Children.Clear();
            ProductionGrid.Children.Add(CreateTradingPostTile(shift, 0));
            ProductionGrid.Children.Add(CreateTradingPostTile(shift, 1));
            ProductionGrid.Children.Add(CreatePowerPlantTile(shift, 0));
            ProductionGrid.Children.Add(CreateFactoryTile(shift, 0));
            ProductionGrid.Children.Add(CreateFactoryTile(shift, 1));
            ProductionGrid.Children.Add(CreatePowerPlantTile(shift, 1));
            ProductionGrid.Children.Add(CreateFactoryTile(shift, 2));
            ProductionGrid.Children.Add(CreateFactoryTile(shift, 3));
            ProductionGrid.Children.Add(CreatePowerPlantTile(shift, 2));
        }

        /// <summary>
        /// The resting shift spread over the dorms, 5 per dorm. Production workers get beds first, then Control Center
        /// buffers, then Control Center morale seats; anyone left over starts the next shift tired.
        /// </summary>
        private void RenderDormitories(ShiftResult restingShift, string restingShiftName)
        {
            DormitoryColumn.Children.Clear();
            List<RosterOperator> restingOperators = restingShift.Plan.AllWorkingOperators()
                .Except(restingShift.Plan.ControlCenter)
                .Concat(restingShift.Plan.ControlCenter)
                .ToList();
            List<RosterOperator> withoutBed = restingOperators
                .Skip(DormitoryCount * DormitorySeats)
                .ToList();

            for (int dormitoryIndex = 0; dormitoryIndex < DormitoryCount; dormitoryIndex++)
            {
                List<RosterOperator> residents = restingOperators
                    .Skip(dormitoryIndex * DormitorySeats)
                    .Take(DormitorySeats)
                    .ToList();
                bool isLastDormitory = dormitoryIndex == DormitoryCount - 1;
                string tiredNote = isLastDormitory && withoutBed.Count > 0 ? $"\nNo bed, starts tired: {string.Join(", ", withoutBed.Select(rosterOperator => rosterOperator.Name))}" : "";
                DormitoryColumn.Children.Add(CreateRoomTile($"Dormitory {dormitoryIndex + 1}", residents, $"Shift {restingShiftName} resting{tiredNote}", DormitoryColor, DormitoryAvatarSize, null));
            }
        }

        /// <summary>
        /// Reception, Workshop, Office and Training Room. Empty unless a partner is placed there for someone's bonus.
        /// </summary>
        private void RenderSideRooms(ShiftResult shift)
        {
            SideRoomColumn.Children.Clear();
            (string Title, RoomType Room)[] sideRooms = { ("Reception Room", RoomType.Meeting), ("Workshop", RoomType.Workshop), ("Office", RoomType.Hire), ("Training Room", RoomType.Training) };

            foreach ((string title, RoomType room) in sideRooms)
            {
                List<RosterOperator> occupants = shift.Plan.SupportRooms.TryGetValue(room, out List<RosterOperator>? placedOperators) ? placedOperators : new List<RosterOperator>();
                string detail = string.Join("\n", occupants.Select(occupant => $"Here so {GetDependentNames(shift, occupant)} gets their bonus"));
                SideRoomColumn.Children.Add(CreateRoomTile(title, occupants, detail, SideRoomColor, AvatarSize, null));
            }
        }

        /// <summary>
        /// Control Center tile showing the buffs this shift's operators give.
        /// </summary>
        private Border CreateControlCenterTile(ShiftResult shift)
        {
            double tradingBonus = ControlCenterPlanner.GetBestBuff(shift.Plan.ControlCenter, EffectType.TradingEfficiency, EffectScope.AllTradingPosts);
            double factoryBonus = ControlCenterPlanner.GetBestBuff(shift.Plan.ControlCenter, EffectType.FactoryProductivity, EffectScope.AllFactories);
            string detail = $"+{tradingBonus:F0}% all Trading Posts  ·  +{factoryBonus:F0}% all Factories";
            return CreateRoomTile("Control Center", shift.Plan.ControlCenter, detail, ControlCenterColor, AvatarSize, null);
        }

        /// <summary>
        /// Trading Post tile with orders, LMD and the order-limit fill bar.
        /// </summary>
        private Border CreateTradingPostTile(ShiftResult shift, int index)
        {
            List<RosterOperator> members = shift.Plan.TradingPosts[index];
            if (members.Count == 0)
            {
                return CreateRoomTile($"Trading Post {index + 1}", members, "Empty: no gold to sell in this plan", TradingPostColor, AvatarSize, null);
            }
            TradingPostResult result = shift.TradingResults[index];
            string goldWarning = goldSupplyRatio < 0.995 ? $"\nonly {goldSupplyRatio:P0} has gold to sell" : "";
            string detail = $"{result.OrdersCompleted * goldSupplyRatio:F1} / {result.OrderLimit} orders  ·  {result.LmdEarned * goldSupplyRatio:N0} LMD\nuses {result.GoldUsed * goldSupplyRatio:F0} gold{goldWarning}";
            StorageFill? storageFill = members.Count > 0 ? new StorageFill(result.OrdersCompleted, result.OrderLimit, result.HoursUntilFull) : null;
            return CreateRoomTile($"Trading Post {index + 1}", members, detail, TradingPostColor, AvatarSize, storageFill);
        }

        /// <summary>
        /// Factory tile with its recipe, output and the storage fill bar.
        /// </summary>
        private Border CreateFactoryTile(ShiftResult shift, int index)
        {
            PlannedFactory factory = shift.Plan.Factories[index];
            FactoryResult result = shift.FactoryResults[index];
            bool isExperience = factory.Formula == FactoryFormula.BattleRecord;
            string title = isExperience ? $"Factory {index + 1}  ·  EXP" : $"Factory {index + 1}  ·  Gold";
            string detail = isExperience ? $"{result.ItemsProduced:F1} / {result.ItemCapacity} records  ·  {result.Experience:N0} EXP" : $"{result.ItemsProduced:F1} / {result.ItemCapacity} gold bars";
            StorageFill? storageFill = factory.Members.Count > 0 ? new StorageFill(result.ItemsProduced, result.ItemCapacity, result.HoursUntilFull) : null;
            return CreateRoomTile(title, factory.Members, detail, FactoryColor, AvatarSize, storageFill);
        }

        /// <summary>
        /// Power Plant tile; drones aren't used, so it either parks a synergy partner or takes anyone.
        /// </summary>
        private Border CreatePowerPlantTile(ShiftResult shift, int index)
        {
            List<RosterOperator> members = index < shift.Plan.PowerPlants.Count ? new List<RosterOperator> { shift.Plan.PowerPlants[index] } : new List<RosterOperator>();
            string dependentNames = members.Count == 0 ? "" : GetDependentNames(shift, members[0]);
            string detail = members.Count == 0 ? "Anyone (drones not used)" : dependentNames.Length > 0 ? $"Here so {dependentNames} gets their bonus" : "Not needed by anyone";
            return CreateRoomTile($"Power Plant {index + 1}", members, detail, PowerPlantColor, AvatarSize, null);
        }

        /// <summary>
        /// Names of working operators whose skills need this operator somewhere in the base.
        /// </summary>
        private static string GetDependentNames(ShiftResult shift, RosterOperator parkedOperator)
        {
            IEnumerable<string> dependentNames = shift.Plan.AllWorkingOperators()
                .Where(workingOperator => workingOperator.ActiveSkills
                    .SelectMany(skill => skill.Effects)
                    .Any(skillEffect => string.Equals(skillEffect.Condition?.OperatorInBase, parkedOperator.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(workingOperator => workingOperator.Name);
            return string.Join(" and ", dependentNames);
        }

        /// <summary>
        /// Builds one room tile: coloured title, operator icons, optional storage bar, and a detail line.
        /// </summary>
        private static Border CreateRoomTile(string title, IReadOnlyList<RosterOperator> members, string detail, Color accentColor, double avatarSize, StorageFill? storageFill)
        {
            StackPanel content = new();
            content.Children.Add(new TextBlock { Text = title, Foreground = new SolidColorBrush(accentColor), FontWeight = FontWeights.Bold, FontSize = 15 });

            WrapPanel avatarRow = new() { Margin = new Thickness(0, 6, 0, 6) };
            foreach (RosterOperator member in members)
            {
                avatarRow.Children.Add(CreateAvatar(member, avatarSize));
            }
            content.Children.Add(avatarRow);

            if (storageFill != null)
            {
                content.Children.Add(CreateFillBar(storageFill, accentColor));
            }
            content.Children.Add(new TextBlock { Text = detail, Foreground = Brushes.Gainsboro, TextWrapping = TextWrapping.Wrap });

            return new Border { Child = content, Background = TileBackground, BorderBrush = new SolidColorBrush(accentColor), BorderThickness = new Thickness(0, 3, 0, 0), CornerRadius = new CornerRadius(4), Padding = new Thickness(10), Margin = new Thickness(5) };
        }

        /// <summary>
        /// Storage bar: room colour if it's still working at check-in, orange if it fills up and sits idle first.
        /// </summary>
        private static FrameworkElement CreateFillBar(StorageFill storageFill, Color accentColor)
        {
            double idleHours = Math.Max(0, CheckInIntervalHours - storageFill.HoursUntilFull);
            bool fillsBeforeCheckIn = idleHours > 0.05;
            Color barColor = fillsBeforeCheckIn ? WarningColor : accentColor;

            ProgressBar fillBar = new() { Minimum = 0, Maximum = storageFill.Capacity, Value = storageFill.Filled, Height = 8, Foreground = new SolidColorBrush(barColor), Background = new SolidColorBrush(ButtonColor), BorderThickness = new Thickness(0) };
            string statusText = fillsBeforeCheckIn ? $"Full after {storageFill.HoursUntilFull:F1}h, idle {idleHours:F1}h before check-in" : "Still working at check-in";

            StackPanel fillPanel = new() { Margin = new Thickness(0, 0, 0, 4) };
            fillPanel.Children.Add(fillBar);
            fillPanel.Children.Add(new TextBlock { Text = statusText, FontSize = 11, Foreground = new SolidColorBrush(fillsBeforeCheckIn ? WarningColor : Colors.Gray) });
            return fillPanel;
        }

        /// <summary>
        /// Operator icon with the name underneath; the full name also shows on hover.
        /// </summary>
        private static FrameworkElement CreateAvatar(RosterOperator member, double avatarSize)
        {
            Image avatarImage = new() { Width = avatarSize, Height = avatarSize, ToolTip = member.Name };
            _ = LoadAvatarAsync(avatarImage, member.CharId);

            StackPanel avatarWithName = new() { Width = avatarSize, Margin = new Thickness(2) };
            Grid avatarFrame = new() { Width = avatarSize, Height = avatarSize };
            avatarFrame.Children.Add(avatarImage);
            if (member.PromotionChangesSkills)
            {
                TextBlock badgeText = new() { Text = $"E{member.PreviewElite}", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.Black };
                avatarFrame.Children.Add(new Border { Child = badgeText, Background = new SolidColorBrush(WarningColor), CornerRadius = new CornerRadius(3), Padding = new Thickness(3, 0, 3, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top });
            }
            avatarWithName.Children.Add(avatarFrame);
            avatarWithName.Children.Add(new TextBlock { Text = member.Name, FontSize = 10, Foreground = Brushes.Gainsboro, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center });
            return avatarWithName;
        }

        /// <summary>
        /// Fills in an icon once it has loaded (instant when cached, a moment on first run).
        /// </summary>
        private static async Task LoadAvatarAsync(Image avatarImage, string charId)
        {
            avatarImage.Source = await AvatarCache.GetAvatarAsync(charId);
        }

        /// <summary>
        /// Colours the selected goal button so it's clear which plan is showing.
        /// </summary>
        private void HighlightGoalButtons()
        {
            foreach (Button goalButton in new[] { BalancedButton, LmdButton, ExperienceButton })
            {
                bool isSelected = (string)goalButton.Tag == currentGoal.ToString();
                goalButton.Background = new SolidColorBrush(isSelected ? ControlCenterColor : ButtonColor);
                goalButton.Foreground = isSelected ? Brushes.Black : Brushes.White;
            }
        }
    }
}