namespace Content.Shared.EntityEffects;

/// <summary>
/// This handles entity conditions.
/// Specifically, it is a public API that any system can use to check a given condition on an entity!
/// </summary>
public sealed partial class SharedEntityConditionsSystem : EntitySystem, IEntityConditionRaiser
{
    /// <inheritdoc/>
    public override void Initialize()
    {

    }

    public void RaiseConditionEvent<T>(EntityUid target, T effect) where T : EntityConditionBase<T>
    {
        var effectEv = new EntityConditionEvent<T>(effect);
        RaiseLocalEvent(target, ref effectEv);
    }
}

public abstract partial class EntityConditionSystem<T, TCon> : EntitySystem where T : Component where TCon : EntityConditionBase<TCon>;

public interface IEntityConditionRaiser
{
    void RaiseConditionEvent<T>(EntityUid target, T effect) where T : EntityConditionBase<T>;
}

public abstract partial class EntityConditionBase<T> : AnyEntityCondition where T : EntityConditionBase<T>
{
    public override void RaiseEvent(EntityUid target, IEntityConditionRaiser raiser)
    {
        if (this is not T type)
            return;

        raiser.RaiseConditionEvent(target, type);
    }
}

public abstract partial class AnyEntityCondition
{
    public abstract void RaiseEvent(EntityUid target, IEntityConditionRaiser raiser);

    // TODO: Define this...
    [DataField]
    public readonly string EntityConditionGuidebookText = "shitcode";
}

[ByRefEvent]
public readonly record struct EntityConditionEvent<T>(T Effect) where T : EntityConditionBase<T>;
