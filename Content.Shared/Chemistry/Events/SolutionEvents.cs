using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared.Chemistry.Events;

/// <summary>
/// The event raised whenever a solution entity is modified.
/// </summary>
/// <remarks>
/// Raised after chemcial reactions and <see cref="SolutionOverflowEvent"/> are handled.
/// </remarks>
/// <param name="Solution">The solution entity that has been modified.</param>
[ByRefEvent]
public readonly partial record struct SolutionChangedEvent(Entity<SolutionComponent> Solution);

/// <summary>
/// The event raised whenever a solution entity is filled past its capacity.
/// </summary>
/// <param name="Solution">The solution entity that has been overfilled.</param>
/// <param name="Overflow">The amount by which the solution entity has been overfilled.</param>
[ByRefEvent]
public partial record struct SolutionOverflowEvent(Entity<SolutionComponent> Solution, FixedPoint2 Overflow)
{
    public bool Handled = false;
}

/// <summary>
/// Attempt event for trying to access a solution.
/// </summary>
/// <param name="SolutionName">Name of the solution that we want to access.</param>
[ByRefEvent]
public partial record struct SolutionAccessAttemptEvent(string SolutionName)
{
    public bool Cancelled;
}
