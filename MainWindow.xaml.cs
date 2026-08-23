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
    private readonly PetStateMachine states = new();
    private readonly PetPhysicsService physics;
    private readonly AnimationPlayer animation;
    private readonly AudioPlayerService audio = new();
    private readonly DispatcherTimer dailyTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime idleSince = DateTime.UtcNow;
    private Point pressPosition;
    private bool dragVoicePlayed;

    public MainWindow()
    {
        InitializeComponent();
        animation = new AnimationPlayer(PetImage);
        physics = new PetPhysicsService(this, taskbar, states);
        states.Changed += (_, next) => ApplyStateVisual(next);
        physics.RunDirectionChanged += direction =>
        {
            // The supplied run frames face right; mirror them while moving left.
            if (states.Current == PetState.Run) animation.SetFlip(direction < 0);
        };
        Loaded += (_, _) => { physics.SnapToTaskbar(); ApplyStateVisual(PetState.Idle); };
        energy.Changed += value => Dispatcher.Invoke(() => EnergyBar.Value = value);
        dailyTimer.Tick += (_, _) => UpdateDailyBehavior();
        dailyTimer.Start();
        Closed += (_, _) => audio.Dispose();
    }

    private void UpdateDailyBehavior()
    {
        var sleeping = states.Current == PetState.Sleep;
        energy.Tick(sleeping);
        if (states.Current is PetState.Drag or PetState.Fall or PetState.Escape or PetState.Run) return;
        if (!sleeping && energy.Value <= energy.SleepThreshold) { states.Set(PetState.Sleep); return; }
        if (sleeping)
        {
            if (energy.Value >= energy.WakeThreshold) { energy.Wake(); states.Set(PetState.Idle); }
            return;
        }
        if (states.Current == PetState.Idle && energy.Value >= 70 && DateTime.UtcNow - idleSince > TimeSpan.FromSeconds(10))
            physics.StartRun();
    }

    private void ApplyStateVisual(PetState next)
    {
        StateText.Text = next.ToString().ToLowerInvariant();
        var animationName = next switch
        {
            PetState.Run => "run",
            PetState.Drag or PetState.Escape or PetState.Fall => "drag",
            PetState.Sleep => "sleep",
            _ => "idle"
        };
        animation.SetFlip(next == PetState.Run && physics.RunDirection < 0);
        animation.Play(animationName, repeat: true, fps: next == PetState.Sleep ? 8 : 12);
        if (next == PetState.Idle) idleSince = DateTime.UtcNow;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (states.Current is PetState.Fall or PetState.Escape) return;
        var woke = energy.RegisterClick(states.Current == PetState.Sleep);
        if (states.Current == PetState.Sleep && !woke) return;
        if (woke) energy.Wake();
        pressPosition = e.GetPosition(this);
        dragVoicePlayed = false;
        physics.StartDrag(pressPosition);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var position = e.GetPosition(this);
        if (!dragVoicePlayed && (position - pressPosition).Length >= 8)
        {
            audio.PlayDragVoice();
            dragVoicePlayed = true;
        }
        physics.UpdateDrag(position);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        physics.EndDrag();
        if (!dragVoicePlayed) audio.PlayRandomVoice();
    }
}
