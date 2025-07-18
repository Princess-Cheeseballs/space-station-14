using Content.Shared.Hands.Components;
using Content.Shared.Stunnable;

namespace Content.Shared.Hands.EntitySystems;

/// <summary>
/// This is for events that don't affect normal hand functions but do care about hands.
/// </summary>
public abstract partial class SharedHandsSystem
{
    private void InitializeEventListeners()
    {
        SubscribeLocalEvent<HandsComponent, GetStandUpTimeEvent>(OnGetStandUpTime);
        SubscribeLocalEvent<HandsComponent, KnockedDownRefreshEvent>(OnKnockdownRefresh);
    }

    /// <summary>
    /// Reduces the time it takes to stand up based on the number of hands we have available.
    /// </summary>
    private void OnGetStandUpTime(Entity<HandsComponent> ent, ref GetStandUpTimeEvent time)
    {
        if (!HasComp<KnockedDownComponent>(ent))
            return;

        var hands = GetEmptyHandCount(ent.Owner);

        if (hands == 0)
            return;

        time.DoAfterTime *= (float)ent.Comp.Count / (hands + ent.Comp.Count);
    }

    private void OnKnockdownRefresh(Entity<HandsComponent> ent, ref KnockedDownRefreshEvent args)
    {
        //if (!TryGetActiveItem())
    }
}
