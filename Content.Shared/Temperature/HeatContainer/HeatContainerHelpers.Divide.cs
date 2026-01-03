using JetBrains.Annotations;

namespace Content.Shared.Temperature.HeatContainer;

public static partial class HeatContainerHelpers
{
    /// <summary>
    /// Splits a <see cref="HeatContainer"/> into two.
    /// </summary>
    /// <param name="c">The <see cref="HeatContainer"/> to split. This will be modified to contain the remaining heat capacity.</param>
    /// <param name="fraction">The fraction of the heat capacity to move to the new container. Clamped between 0 and 1.</param>
    /// <returns>A new <see cref="HeatContainer"/> containing the specified fraction of the original container's heat capacity and the same temperature.</returns>
    [PublicAPI]
    public static HeatContainer Split(this ref HeatContainer c, float fraction = 0.5f)
    {
        if (fraction <= 0 || fraction >= 1)
            throw new ArgumentOutOfRangeException(nameof(fraction), "Param must be between 0 and 1");

        return c.Subtract(c.HeatCapacity * fraction);
    }

    /// <summary>
    /// Splits a <see cref="HeatContainer"/> into two.
    /// </summary>
    /// <param name="c">The <see cref="HeatContainer"/> to split. This will be modified to contain the remaining heat capacity.</param>
    /// <param name="capacity">How much of the heat capacity we're taking from a HeatContainer.</param>
    /// <returns>A new <see cref="HeatContainer"/> containing the specified fraction of the original container's heat capacity and the same temperature.</returns>
    [PublicAPI]
    public static HeatContainer Subtract(this ref HeatContainer c, float capacity)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(capacity, c.HeatCapacity);

        var newContainer = new HeatContainer
        {
            HeatCapacity = capacity,
            Temperature = c.Temperature,
        };

        c.HeatCapacity -= capacity;

        return newContainer;
    }

    /// <summary>
    /// Divides a source <see cref="HeatContainer"/> into a specified number of equal parts.
    /// </summary>
    /// <param name="c">The input <see cref="HeatContainer"/> to split.</param>
    /// <param name="num">The number of <see cref="HeatContainer"/>s
    /// to split the source <see cref="HeatContainer"/> into.</param>
    /// <exception cref="ArgumentException">Thrown when attempting to divide the source container by zero.</exception>
    /// <returns>An array of <see cref="HeatContainer"/>s equally split from the source <see cref="HeatContainer"/>.</returns>
    [PublicAPI]
    public static HeatContainer[] Divide(this HeatContainer c, uint num)
    {
        if (num == 0)
            throw new ArgumentException("Cannot divide by zero.", nameof(num));

        var fraction = 1f / num;
        var cFrac = c.Split(fraction);
        var containers = new HeatContainer[num];

        for (var i = 0; i < num; i++)
        {
            containers[i] = cFrac;
        }

        return containers;
    }
}
