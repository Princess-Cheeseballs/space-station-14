using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Map;

namespace Content.Shared.Chemistry.EntitySystems;

/// <summary>
/// This handles solutions!
/// </summary>
public abstract partial class SharedSolutionsSystem
{
    private EntityQuery<ReactiveComponent> _reactiveQuery;

    private void InitializeReactions()
    {
        _reactiveQuery = new EntityQuery<ReactiveComponent>();
    }

    public void DoTileReactions(TileRef tileRef, Solution solution)
    {
        for (var i = solution.Contents.Count - 1; i >= 0; i--)
        {
            var (reagent, quantity) = solution.Contents[i];
            if (!Proto.TryIndex<ReagentPrototype>(reagent.Prototype, out var proto))
                continue;

            var removed = proto.ReactionTile(tileRef, quantity, EntityManager, reagent.Data);
            solution.RemoveReagent(reagent, removed);
        }
    }
}
