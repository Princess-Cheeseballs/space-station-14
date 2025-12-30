using System.Diagnostics.CodeAnalysis;
using Content.Shared.Chemistry.Components;
using JetBrains.Annotations;

namespace Content.Shared.Chemistry.EntitySystems;

// TODO: TEST AGGRESSIVE INLINING ON THE TRYGET METHODS TO SEE IF THEY IMPROVE PERFORMANCE!!!
public abstract partial class SharedSolutionsSystem
{
    /// <summary>
    /// Attempts to add two solutions together.
    /// </summary>
    /// <param name="solution">Solution we're trying to add to</param>
    /// <param name="toAdd">Solution we're trying to add</param>
    /// <returns>True if the solutions were successfully added together.</returns>
    [PublicAPI]
    public bool TryAddSolutions(Entity<SolutionComponent> solution, Entity<SolutionComponent> toAdd)
    {
        // TODO: Check if the solution we're trying to add can even be added... I.E. too big? Cannot fit? ect.
        return TryAddSolutions(solution, toAdd.Comp.Solution);
    }

    /// <inheritdoc cref="TryAddSolutions(Entity{SolutionComponent},Entity{SolutionComponent})"/>
    public bool TryAddSolutions(Entity<SolutionComponent> solution, Solution toAdd)
    {
        if (!CanAddSolutions(solution, toAdd))
            return false;

        solution.Comp.Solution.AddSolution(toAdd, Proto);
        return true;
    }

    public bool CanAddSolutions(Entity<SolutionComponent> solution, Solution toAdd)
    {
        var xform = Transform(solution);


        return true;
    }

    /// <summary>
    /// Checks if two solutions can be fully mixed together into one solution!
    /// </summary>
    /// <param name="solution"></param>
    /// <param name="toAdd"></param>
    /// <returns></returns>
    public bool CanMergeSolutions(Entity<SolutionComponent> solution, Solution toAdd)
    {
        if (solution.Comp.MaxVolume > solution.Comp.Volume + toAdd.Volume)
            return false;

        return true;
    }

    [PublicAPI, Pure]
    public bool TryGetSolution(EntityUid uid, [NotNullWhen(true)] out Entity<SolutionComponent>? solution)
    {
        solution = GetSolution(uid);
        return solution is not null;
    }

    [PublicAPI, Pure]
    public bool TryGetSolution<T>(EntityUid uid, [NotNullWhen(true)] out Entity<SolutionComponent, T>? solution) where T : IComponent
    {
        solution = GetSolution<T>(uid);
        return solution is not null;
    }

    [PublicAPI, Pure]
    public bool TryGetSolution<T>(EntityUid uid, EntityQuery<T> query, [NotNullWhen(true)] out Entity<SolutionComponent, T>? solution) where T : IComponent
    {
        solution = GetSolution(uid, query);
        return solution is not null;
    }

    [PublicAPI, Pure]
    public bool TryGetSolution(EntityUid uid, string name, [NotNullWhen(true)] out Entity<SolutionComponent>? solution)
    {
        solution = GetSolution(uid, name);
        return solution is not null;
    }

    [PublicAPI, Pure]
    public bool TryGetSolution<T>(EntityUid uid, string name, [NotNullWhen(true)] out Entity<SolutionComponent, T>? solution) where T : IComponent
    {
        solution = GetSolution<T>(uid, name);
        return solution is not null;
    }

    [PublicAPI, Pure]
    public bool TryGetSolution<T>(EntityUid uid, string name, EntityQuery<T> query, [NotNullWhen(true)] out Entity<SolutionComponent, T>? solution) where T : IComponent
    {
        solution = GetSolution(uid, query, name);
        return solution is not null;
    }

    [PublicAPI, Pure]
    public Entity<SolutionComponent, T>? GetSolution<T>(EntityUid uid) where T : IComponent
    {
        if (!_solutionQuery.TryComp(uid, out var solComp))
            return null;

        if (!TryComp<T>(uid, out var comp))
            return null;

        return (uid, solComp, comp);
    }

    [PublicAPI, Pure]
    public Entity<SolutionComponent, T>? GetSolution<T>(EntityUid uid, EntityQuery<T> query) where T : IComponent
    {
        if (!_solutionQuery.TryComp(uid, out var solComp))
            return null;

        if (!query.TryComp(uid, out var comp))
            return null;

        return (uid, solComp, comp);
    }

    /// <summary>
    /// Attempts to find a solution with a specific component.
    /// </summary>
    /// <param name="entity">entity we're looking for the solution on</param>
    /// <param name="name">optional name filter for the solution</param>
    /// <typeparam name="T">component this solution must also have</typeparam>
    /// <returns>The solution entity with the desired component</returns>
    [PublicAPI, Pure]
    public Entity<SolutionComponent, T>? GetSolution<T>(Entity<SolutionManagerComponent?> entity, string name) where T : IComponent
    {
        if (GetSolution(entity, name) is not { } sol)
            return null;

        if (!TryComp<T>(sol, out var comp))
            return null;

        return (sol, sol, comp);
    }

    /// <inheritdoc cref="GetSolution{T}(Entity{SolutionManagerComponent?},string?)"/>
    /// This overload takes a query as an argument for a faster TryComp.
    [PublicAPI, Pure]
    public Entity<SolutionComponent, T>? GetSolution<T>(Entity<SolutionManagerComponent?> entity, EntityQuery<T> query, string name) where T : IComponent
    {
        if (GetSolution(entity, name) is not { } sol)
            return null;

        if (!query.TryComp(sol, out var comp))
            return null;

        return (sol, sol, comp);
    }

    [PublicAPI, Pure]
    public Entity<SolutionComponent>? GetSolution(EntityUid uid)
    {
        if (!_solutionQuery.TryComp(uid, out var solComp))
            return null;

        return (uid, solComp);
    }

    /// <summary>
    /// Attempts to find an entity solution, first checking the entity itself before checking the manager.
    /// </summary>
    /// <param name="entity">Entity containing the solution.</param>
    /// <param name="name">Optional name filter</param>
    /// <returns></returns>
    [PublicAPI, Pure]
    public Entity<SolutionComponent>? GetSolution(Entity<SolutionManagerComponent?> entity, string name)
    {
        if (_solutionQuery.TryComp(entity, out var solComp) && solComp.Id == name)
            return (entity, solComp);

        return GetSolutionFromMananger(entity, name);
    }

    [PublicAPI, Pure]
    public bool TryGetSolutionFromMananger(Entity<SolutionManagerComponent?> manager, string name, [NotNullWhen(true)] out Entity<SolutionComponent>? solution)
    {
        solution = GetSolution(manager.Owner, name);
        return solution is not null;
    }

    [PublicAPI, Pure]
    public Entity<SolutionComponent>? GetSolutionFromMananger(Entity<SolutionManagerComponent?> manager, string name)
    {
        if (!_solutionManagerQuery.Resolve(manager, ref manager.Comp))
            return null;

        if (!manager.Comp.Solutions.TryGetValue(name, out var value))
            return null;

        return value;
    }
}
