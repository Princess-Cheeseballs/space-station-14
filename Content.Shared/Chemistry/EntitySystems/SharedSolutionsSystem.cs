using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.EntitySystems;

/// <summary>
/// This handles solutions!
/// </summary>
public abstract partial class SharedSolutionsSystem : EntitySystem
{
    [Dependency] protected readonly ISharedAdminLogManager AdminLog = default!;
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly ReactiveSystem Reactive = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly OpenableSystem _openable = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;

    private EntityQuery<SolutionComponent> _solutionQuery;
    private EntityQuery<SolutionManagerComponent> _solutionManagerQuery;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<SolutionComponent, ComponentStartup>(OnSolutionStartup);
        SubscribeLocalEvent<SolutionComponent, ComponentShutdown>(OnSolutionShutdown);

        _solutionQuery = GetEntityQuery<SolutionComponent>();
        _solutionManagerQuery = GetEntityQuery<SolutionManagerComponent>();

        InitializeReactions();
        InitializeRelay();
    }

    private void OnSolutionStartup(Entity<SolutionComponent> entity, ref ComponentStartup args)
    {
        // Probably don't need to do anything?
    }

    private void OnSolutionShutdown(Entity<SolutionComponent> entity, ref ComponentShutdown args)
    {
        // Probably don't need to do anything?
    }
}
