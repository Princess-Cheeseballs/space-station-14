using Content.Server.Atmos.Piping.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Visuals;

namespace Content.Server.Atmos.Visuals;

/// <summary>
/// This handles...
/// </summary>
public sealed class PressureVisualsSystem : SharedPressureVisualsSystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PressureVisualsComponent, AtmosDeviceUpdateEvent>(OnDeviceUpdate);
    }

    private void OnDeviceUpdate(Entity<PressureVisualsComponent> entity, ref AtmosDeviceUpdateEvent args)
    {

    }
}
