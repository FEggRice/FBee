using System.Windows;

namespace FBee.Services;

public sealed class TaskbarService
{
    public Rect GetTaskbarBounds() { var work = SystemParameters.WorkArea; var h = Math.Max(0, SystemParameters.PrimaryScreenHeight - work.Bottom); return new Rect(0, work.Bottom, SystemParameters.PrimaryScreenWidth, h); }
    public bool IsBottomTaskbarVisible => GetTaskbarBounds().Height > 0;
    public Point GetDefaultPetPosition(double width, double height) { var work = SystemParameters.WorkArea; var taskbar = GetTaskbarBounds(); var x = (work.Left + work.Right - width) / 2; var y = taskbar.Height > 0 ? taskbar.Top - height + 8 : work.Bottom - height; return new Point(x, Math.Max(work.Top, y)); }
}
