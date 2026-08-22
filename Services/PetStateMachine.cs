using FBee.Models;

namespace FBee.Services;

public sealed class PetStateMachine
{
    public PetState Current { get; private set; } = PetState.Idle;
    public event Action<PetState, PetState>? Changed;

    public void Set(PetState next)
    {
        if (Current == next) return;
        var previous = Current;
        Current = next;
        Changed?.Invoke(previous, next);
    }
}
