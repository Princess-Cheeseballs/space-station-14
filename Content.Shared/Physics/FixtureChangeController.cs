using Robust.Shared.Physics;

namespace Content.Shared.Physics;

/// <summary>
/// This handles the changing of fixtures on an entity, so that multiple systems can remove/add fixtures without causing problems.
/// </summary>
public sealed class FixtureChangeController : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {

    }

    public bool TryReenableFixtures(Entity<FixturesComponent?> entity)
    {


        return true;
    }
}
