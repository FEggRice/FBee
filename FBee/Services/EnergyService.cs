using System.Windows.Threading;

namespace FBee.Services;

public sealed class EnergyService
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    public double Value { get; private set; } = 100;
    public double Max { get; init; } = 100;
    public double DecayPerSecond { get; init; } = 0.05;
    public double SleepRecoveryPerSecond { get; init; } = 0.15;
    public double SleepThreshold { get; init; } = 20;
    public double WakeThreshold { get; init; } = 60;
    public double ClickRecovery { get; init; } = 5;
    public int SleepClickCount { get; init; } = 3;
    public int ClicksWhileSleeping { get; private set; }
    public event Action<double>? Changed;
    public EnergyService() => timer.Tick += (_, _) => Tick();
    public void Start() => timer.Start();
    public void Tick(bool sleeping = false) { Value = Math.Clamp(Value + (sleeping ? SleepRecoveryPerSecond : -DecayPerSecond), 0, Max); Changed?.Invoke(Value); }
    public bool RegisterClick(bool sleeping) { Value = Math.Clamp(Value + ClickRecovery, 0, Max); if (sleeping) ClicksWhileSleeping++; Changed?.Invoke(Value); return sleeping && ClicksWhileSleeping >= SleepClickCount; }
    public void Wake() { ClicksWhileSleeping = 0; Value = Math.Max(Value, WakeThreshold); Changed?.Invoke(Value); }
}
