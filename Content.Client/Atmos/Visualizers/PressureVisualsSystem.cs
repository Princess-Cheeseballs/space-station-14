using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Visuals;
using Robust.Client.GameObjects;

namespace Content.Client.Atmos.Visualizers;

/// <summary>
/// This handles...
/// </summary>
public sealed class PressureVisualsSystem : SharedPressureVisualsSystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PressureVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<PressureVisualsComponent> entity, ref AppearanceChangeEvent args)
    {
        
    }
}
