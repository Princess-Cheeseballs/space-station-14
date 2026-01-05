using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.Reagent;

public static partial class SolutionHelpers
{
    [Pure]
    public static bool TryGet(this Solution solution, out ReagentQuantity reagentQuantity, ReagentId key)
    {
        reagentQuantity = solution.Get(key);
        return reagentQuantity.Quantity > FixedPoint2.Zero;
    }

    [Pure]
    public static ReagentQuantity Get(this Solution solution, ReagentId key)
    {
        foreach (var reagent in solution.Contents)
        {
            if (key == reagent.Reagent)
                return reagent;
        }

        return new ReagentQuantity(key, FixedPoint2.Zero);
    }

    [Pure]
    public static bool TryGet(this Solution solution, out ReagentQuantity reagentQuantity, ProtoId<ReagentPrototype> key)
    {
        return solution.TryGet(out reagentQuantity, new ReagentId(key));
    }

    [Pure]
    public static bool TryGetTotal(this Solution solution, out FixedPoint2 reagentQuantity, params ReagentId[] key)
    {
        reagentQuantity = solution.GetTotal(key);
        return reagentQuantity > FixedPoint2.Zero;
    }

    [Pure]
    public static FixedPoint2 GetTotal(this Solution solution, params ReagentId[] key)
    {
        var volume = FixedPoint2.Zero;
        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (key.Contains (reagent))
                continue;

            volume += quantity;
        }

        return volume;
    }

    [Pure]
    public static bool TryGetTotal(this Solution solution, out FixedPoint2 volume, params ProtoId<ReagentPrototype>[] key)
    {
        volume = solution.GetTotal(key);
        return volume > FixedPoint2.Zero;
    }

    [Pure]
    public static FixedPoint2 GetTotal(this Solution solution, params ProtoId<ReagentPrototype>[] key)
    {
        var volume = FixedPoint2.Zero;
        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (!key.Contains(reagent.Prototype))
                continue;

            volume += quantity;
        }

        return volume;
    }

    [Pure]
    public static FixedPoint2 GetTotal(this Solution solution, ProtoId<ReagentPrototype> key)
    {
        var volume = FixedPoint2.Zero;
        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (key != reagent.Prototype)
                continue;

            volume += quantity;
        }

        return volume;
    }

    [Pure]
    public static bool Contains(this Solution solution, ReagentId key)
    {
        foreach (var (id, _) in solution.Contents)
        {
            if (key == id)
                return true;
        }

        return false;
    }

    [Pure]
    public static bool Contains(this Solution solution, params ProtoId<ReagentPrototype>[] key)
    {
        foreach (var (id, _) in solution.Contents)
        {
            if (key.Contains(id.Prototype))
                return true;
        }

        return false;
    }

    [Pure]
    public static Dictionary<ReagentPrototype, FixedPoint2> GetReagentPrototypes(this Solution solution, IPrototypeManager protoMan)
    {
        var dict = new Dictionary<ReagentPrototype, FixedPoint2>(solution.Contents.Count);
        foreach (var (reagent, quantity) in solution.Contents)
        {
            var proto = protoMan.Index(reagent.Prototype);
            dict[proto] = quantity + dict.GetValueOrDefault(proto);
        }
        return dict;
    }

    [Pure]
    public static bool TryGetPrimaryReagentId(this Solution solution, [NotNullWhen(true)] out ReagentId? reagentId)
    {
        reagentId = solution.GetPrimaryReagentId();
        return reagentId != null;
    }

    [Pure]
    public static ReagentId? GetPrimaryReagentId(this Solution solution)
    {
        if (solution.Contents.Count == 0)
            return null;

        ReagentQuantity max = default;

        foreach (var reagent in solution.Contents)
        {
            if (reagent.Quantity >= max.Quantity)
                max = reagent;
        }

        return max.Reagent;
    }

    [Pure]
    public static Color GetColor(this Solution solution, IPrototypeManager? protoMan)
    {
        return solution.GetColorBlacklist(protoMan);
    }

    [Pure]
    public static Color GetColorWhitelist(this Solution solution, IPrototypeManager? protoMan, params ReagentId[] whitelist)
    {
        if (solution.Volume == FixedPoint2.Zero)
            return Color.Transparent;

        IoCManager.Resolve(ref protoMan);

        Color color = default;
        var runningTotalQuantity = FixedPoint2.Zero;
        var first = true;

        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (!whitelist.Contains(reagent))
                continue;

            runningTotalQuantity += quantity;

            if (!protoMan.Resolve(reagent.Prototype, out var proto))
                continue;

            if (first)
            {
                first = false;
                color = proto.SubstanceColor;
                continue;
            }

            var interpolateValue = quantity.Float() / runningTotalQuantity.Float();
            color = Color.InterpolateBetween(color, proto.SubstanceColor, interpolateValue);
        }

        return color;
    }

    [Pure]
    public static Color GetColorBlacklist(this Solution solution, IPrototypeManager? protoMan, params ReagentId[] blacklist)
    {
        if (solution.Volume == FixedPoint2.Zero)
            return Color.Transparent;

        IoCManager.Resolve(ref protoMan);

        Color color = default;
        var runningTotalQuantity = FixedPoint2.Zero;
        var first = true;

        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (blacklist.Contains(reagent))
                continue;

            runningTotalQuantity += quantity;

            if (!protoMan.Resolve(reagent.Prototype, out ReagentPrototype? proto))
                continue;

            if (first)
            {
                first = false;
                color = proto.SubstanceColor;
                continue;
            }

            var interpolateValue = quantity.Float() / runningTotalQuantity.Float();
            color = Color.InterpolateBetween(color, proto.SubstanceColor, interpolateValue);
        }

        return color;
    }

    /// <summary>
    /// Scales a solution to a specified volume
    /// </summary>
    /// <param name="solution">Solution we're scaling.</param>
    /// <param name="volume">Volume we're scaling to.</param>
    public static void ScaleSolution(this Solution solution, FixedPoint2 volume)
    {
        if (volume == FixedPoint2.Zero)
            solution.Clear();
        else if (volume > solution.Volume)
            solution.Add(volume);
        else if (volume < solution.Volume)
            solution.Remove(solution.Volume - volume);
    }

    /// <inheritdoc cref="ScaleSolution(Solution,FixedPoint2)"/>
    public static void ScaleSolution(this Solution solution, float scale)
    {
        ScaleSolution(solution, scale / solution.Volume);
    }

    public static void SetReagentData(this Solution solution, List<ReagentData>? data)
    {
        for (var i = 0; i < solution.Contents.Count; i++)
        {
            var old = solution.Contents[i];
            solution.Contents[i] = new ReagentQuantity(new ReagentId(old.Reagent.Prototype, data), old.Quantity);
        }
        solution.ValidateSolution();
    }
}
