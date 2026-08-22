using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FBee.Models;

namespace FBee.Services;

public sealed class PetPhysicsService
{
    private readonly Window window;
    private readonly TaskbarService taskbar;
    private readonly PetStateMachine states;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private const double Gravity = 1400;
    private const double RunSpeed = 180;
    private const double DragTimeoutSeconds = 3;
    private const double EscapeDurationSeconds = 0.45;
    private DateTime dragStartedAt;
    private DateTime escapeEndsAt;
    private DateTime runEndsAt;
    private Point dragOffset;
    private double fallVelocity;
    private int runDirection = 1;

    public bool IsDragging { get; private set; }

    public PetPhysicsService(Window window, TaskbarService taskbar, PetStateMachine states)
    {
        this.window = window;
        this.taskbar = taskbar;
        this.states = states;
        timer.Tick += (_, _) => Tick();
    }

    public void StartDrag(Point offset)
    {
        if (states.Current is PetState.Fall or PetState.Escape) return;
        StopRun();
        IsDragging = true;
        dragOffset = offset;
        dragStartedAt = DateTime.UtcNow;
        states.Set(PetState.Drag);
        timer.Start();
    }

    public void UpdateDrag(Point pointer)
    {
        if (!IsDragging) return;
        window.Left += pointer.X - dragOffset.X;
        window.Top += pointer.Y - dragOffset.Y;
    }

    public void EndDrag()
    {
        if (!IsDragging) return;
        IsDragging = false;
        window.ReleaseMouseCapture();
        BeginFall();
    }

    public void StartRun()
    {
        if (states.Current != PetState.Idle || !taskbar.IsBottomTaskbarVisible) return;
        var bounds = taskbar.GetTaskbarBounds();
        if (bounds.Width <= window.Width) return;
        runDirection = window.Left + window.Width / 2 < bounds.Left + bounds.Width / 2 ? 1 : -1;
        runEndsAt = DateTime.UtcNow.AddSeconds(4);
        states.Set(PetState.Run);
        SnapToTaskbarHeight();
        timer.Start();
    }

    public void SnapToTaskbar()
    {
        var p = taskbar.GetDefaultPetPosition(window.Width, window.Height);
        window.Left = p.X;
        window.Top = p.Y;
    }

    private void Tick()
    {
        if (IsDragging && DateTime.UtcNow - dragStartedAt >= TimeSpan.FromSeconds(DragTimeoutSeconds))
        {
            TriggerEscape();
            return;
        }
        if (states.Current == PetState.Escape)
        {
            if (DateTime.UtcNow >= escapeEndsAt) BeginFall();
            return;
        }
        if (states.Current == PetState.Fall) UpdateFall();
        else if (states.Current == PetState.Run) UpdateRun();
        else if (!IsDragging) timer.Stop();
    }

    private void TriggerEscape()
    {
        IsDragging = false;
        window.ReleaseMouseCapture();
        states.Set(PetState.Escape);
        escapeEndsAt = DateTime.UtcNow.AddSeconds(EscapeDurationSeconds);
    }

    private void BeginFall()
    {
        fallVelocity = 0;
        states.Set(PetState.Fall);
        timer.Start();
    }

    private void UpdateFall()
    {
        var dt = timer.Interval.TotalSeconds;
        fallVelocity += Gravity * dt;
        window.Top += fallVelocity * dt;
        var floor = GetFloorTop();
        if (window.Top < floor) return;
        window.Top = floor;
        fallVelocity = 0;
        states.Set(PetState.Idle);
        if (taskbar.IsBottomTaskbarVisible) SnapToTaskbar();
    }

    private void UpdateRun()
    {
        var bounds = taskbar.GetTaskbarBounds();
        if (!taskbar.IsBottomTaskbarVisible || bounds.Width <= window.Width)
        {
            states.Set(PetState.Idle);
            SnapToTaskbar();
            return;
        }
        var minX = bounds.Left;
        var maxX = bounds.Right - window.Width;
        window.Left += runDirection * RunSpeed * timer.Interval.TotalSeconds;
        if (window.Left <= minX) { window.Left = minX; runDirection = 1; }
        else if (window.Left >= maxX) { window.Left = maxX; runDirection = -1; }
        SnapToTaskbarHeight();
        if (DateTime.UtcNow >= runEndsAt)
        {
            states.Set(PetState.Idle);
            SnapToTaskbar();
        }
    }

    private void StopRun()
    {
        if (states.Current == PetState.Run) states.Set(PetState.Idle);
    }

    private void SnapToTaskbarHeight()
    {
        var bounds = taskbar.GetTaskbarBounds();
        if (bounds.Height > 0) window.Top = bounds.Top - window.Height + 8;
    }

    private double GetFloorTop()
    {
        var bounds = taskbar.GetTaskbarBounds();
        var floor = taskbar.IsBottomTaskbarVisible ? bounds.Top - window.Height + 8 : SystemParameters.WorkArea.Bottom - window.Height;
        return Math.Max(SystemParameters.WorkArea.Top, floor);
    }
}
