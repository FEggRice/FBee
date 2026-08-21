using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FBee.Models;
using FBee.Services;

namespace FBee;

public partial class MainWindow : Window
{
    private readonly EnergyService energy = new();
    private readonly TaskbarService taskbar = new();
    private readonly DispatcherTimer stateTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private PetState state = PetState.Idle;
    private DateTime idleSince = DateTime.UtcNow;
    private bool dragging;
    private Point dragOffset;

    public MainWindow()
    {
        InitializeComponent(); Loaded += (_, _) => SnapToTaskbar();
        energy.Changed += value => Dispatcher.Invoke(() => EnergyBar.Value = value);
        stateTimer.Tick += (_, _) => UpdateDailyBehavior(); stateTimer.Start();
    }
    private void SnapToTaskbar() { var p = taskbar.GetDefaultPetPosition(Width, Height); Left = p.X; Top = p.Y; }
    private void UpdateDailyBehavior()
    {
        var sleeping = state == PetState.Sleep; energy.Tick(sleeping);
        if (state is PetState.Drag or PetState.Fall or PetState.Escape) return;
        if (!sleeping && energy.Value <= energy.SleepThreshold) { SetState(PetState.Sleep); return; }
        if (sleeping) { if (energy.Value >= energy.WakeThreshold) { energy.Wake(); SetState(PetState.Idle); } return; }
        if (state == PetState.Idle && energy.Value >= 70 && DateTime.UtcNow - idleSince > TimeSpan.FromSeconds(45)) { SetState(PetState.Run); DispatcherTimerExtensions.RunOnce(TimeSpan.FromSeconds(4), () => { SetState(PetState.Idle); SnapToTaskbar(); }); }
    }
    private void SetState(PetState next) { state = next; StateText.Text = next.ToString().ToLowerInvariant(); if (next == PetState.Idle) idleSince = DateTime.UtcNow; }
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var woke = energy.RegisterClick(state == PetState.Sleep); if (state == PetState.Sleep && !woke) return; if (woke) energy.Wake();
        SetState(PetState.Drag); dragging = true; dragOffset = e.GetPosition(this); CaptureMouse();
    }
    private void OnMouseMove(object sender, MouseEventArgs e) { if (!dragging || e.LeftButton != MouseButtonState.Pressed) return; var p = e.GetPosition(this); Left += p.X - dragOffset.X; Top += p.Y - dragOffset.Y; }
    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (!dragging) return; dragging = false; ReleaseMouseCapture(); SetState(PetState.Idle); if (taskbar.IsBottomTaskbarVisible && Top + Height >= taskbar.GetTaskbarBounds().Top - 30) SnapToTaskbar(); }
}

internal static class DispatcherTimerExtensions
{
    public static void RunOnce(TimeSpan delay, Action action) { var timer = new DispatcherTimer { Interval = delay }; timer.Tick += (_, _) => { timer.Stop(); action(); }; timer.Start(); }
}
