using Content.Shared.Temperature.Components;
using Robust.Shared.Containers;

namespace Content.Shared.Temperature.Systems;

/// <summary>
/// This handles...
/// </summary>
public sealed class TemperatureRegulatedContainerSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTemperatureSystem _temperature = default!;

    public override void Update(float frameTime)
    {
        // TODO: OPTIMIZE THIS CODE

        var query = EntityQueryEnumerator<TemperatureRegulatedContainerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (GetContainer((uid, comp)) is not { } container)
                continue;

            foreach (var ent in container.ContainedEntities)
            {
                _temperature.ConductHeat(ent, comp.TargetTemperature, frameTime, comp.Conductance);
            }
        }
    }

    private BaseContainer? GetContainer(Entity<TemperatureRegulatedContainerComponent> entity)
    {
        if (entity.Comp.Container is { } container)
            return container;

        if (!_container.TryGetContainer(entity, entity.Comp.ContainerId, out container))
            return null;

        entity.Comp.Container = container;
        return container;
    }
}
