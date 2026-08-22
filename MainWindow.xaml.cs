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
    private readonly AnimationPlayer animation;
    private readonly DispatcherTimer stateTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer runTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private const double RunSpeed = 180;
    private PetState state = PetState.Idle;
    private DateTime idleSince = DateTime.UtcNow;
    private DateTime runEndsAt;
    private int runDirection = 1;
    private bool dragging;
    private Point dragOffset;

    public MainWindow()
    {
        InitializeComponent();
        animation = new AnimationPlayer(PetImage);
        Loaded += (_, _) => { SnapToTaskbar(); SetState(PetState.Idle); };
        energy.Changed += value => Dispatcher.Invoke(() => EnergyBar.Value = value);
        stateTimer.Tick += (_, _) => UpdateDailyBehavior();
        runTimer.Tick += (_, _) => UpdateRunMovement();
        stateTimer.Start();
    }

    private void SnapToTaskbar()
    {
        var p = taskbar.GetDefaultPetPosition(Width, Height);
        Left = p.X;
        Top = p.Y;
    }

    private void UpdateDailyBehavior()
    {
        var sleeping = state == PetState.Sleep;
        energy.Tick(sleeping);
        if (state is PetState.Drag or PetState.Fall or PetState.Escape) return;
        if (!sleeping && energy.Value <= energy.SleepThreshold) { SetState(PetState.Sleep); return; }
        if (sleeping)
        {
            if (energy.Value >= energy.WakeThreshold) { energy.Wake(); SetState(PetState.Idle); }
            return;
        }
        if (state == PetState.Idle && energy.Value >= 70 && DateTime.UtcNow - idleSince > TimeSpan.FromSeconds(45))
            SetState(PetState.Run);
    }

    private void SetState(PetState next)
    {
        if (state == PetState.Run && next != PetState.Run) runTimer.Stop();
        state = next;
        StateText.Text = next.ToString().ToLowerInvariant();
        if (next == PetState.Run)
        {
            BeginRun();
        }
        else
        {
            var animationName = next switch
            {
                PetState.Drag => "drag",
                PetState.Sleep => "sleep",
                _ => "idle"
            };
            animation.Play(animationName, repeat: true, fps: next == PetState.Sleep ? 8 : 12);
        }
        if (next == PetState.Idle) idleSince = DateTime.UtcNow;
    }

    private void BeginRun()
    {
        var bounds = taskbar.GetTaskbarBounds();
        if (!taskbar.IsBottomTaskbarVisible || bounds.Width <= Width)
        {
            SetState(PetState.Idle);
            return;
        }

        runDirection = Left + Width / 2 < bounds.Left + bounds.Width / 2 ? 1 : -1;
        runEndsAt = DateTime.UtcNow.AddSeconds(4);
        animation.Play("run", repeat: true, fps: 12);
        SnapToTaskbarHeight();
        runTimer.Start();
    }

    private void UpdateRunMovement()
    {
        if (state != PetState.Run) return;
        var bounds = taskbar.GetTaskbarBounds();
        if (!taskbar.IsBottomTaskbarVisible || bounds.Width <= Width)
        {
            SetState(PetState.Idle);
            SnapToTaskbar();
            return;
        }

        var minX = bounds.Left;
        var maxX = bounds.Right - Width;
        Left += runDirection * RunSpeed * runTimer.Interval.TotalSeconds;
        if (Left <= minX) { Left = minX; runDirection = 1; }
        else if (Left >= maxX) { Left = maxX; runDirection = -1; }
        SnapToTaskbarHeight();

        if (DateTime.UtcNow >= runEndsAt)
        {
            SetState(PetState.Idle);
            SnapToTaskbar();
        }
    }

    private void SnapToTaskbarHeight()
    {
        var bounds = taskbar.GetTaskbarBounds();
        if (bounds.Height > 0) Top = bounds.Top - Height + 8;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var woke = energy.RegisterClick(state == PetState.Sleep);
        if (state == PetState.Sleep && !woke) return;
        if (woke) energy.Wake();
        SetState(PetState.Drag);
        dragging = true;
        dragOffset = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(this);
        Left += p.X - dragOffset.X;
        Top += p.Y - dragOffset.Y;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!dragging) return;
        dragging = false;
        ReleaseMouseCapture();
        SetState(PetState.Idle);
        if (taskbar.IsBottomTaskbarVisible && Top + Height >= taskbar.GetTaskbarBounds().Top - 30) SnapToTaskbar();
    }
}
