using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Events;

namespace Content.Shared.Chemistry.EntitySystems;

public abstract partial class SharedSolutionsSystem
{
    private void InitializeRelay()
    {
        // Put events to relay here!
    }

    private void RefRelaySolutionEvent<T>(EntityUid uid, SolutionManagerComponent component, ref T args) where T : struct
    {
        RelayEvent((uid, component), ref args);
    }

    private void RelaySolutionEvent<T>(EntityUid uid, SolutionManagerComponent component, T args) where T : class
    {
        RelayEvent((uid, component), args);
    }

    public void RelayEvent<T>(Entity<SolutionManagerComponent> statusEffect, ref T args) where T : struct
    {
        // this copies the by-ref event if it is a struct
        var ev = new SolutionRelayedEvent<T>(args);
        foreach (var (_, solution) in statusEffect.Comp.Solutions)
        {
            RaiseLocalEvent(solution, ref ev);
        }
        // and now we copy it back
        args = ev.Event;
    }

    public void RelayEvent<T>(Entity<SolutionManagerComponent> statusEffect, T args) where T : class
    {
        // this copies the by-ref event if it is a struct
        var ev = new SolutionRelayedEvent<T>(args);
        foreach (var (_, solution) in statusEffect.Comp.Solutions)
        {
            RaiseLocalEvent(solution, ref ev);
        }
    }
}
