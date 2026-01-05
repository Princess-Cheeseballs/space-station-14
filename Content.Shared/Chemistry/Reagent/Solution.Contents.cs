using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Atmos;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Temperature.HeatContainer;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Chemistry.Reagent;

public partial struct Solution
{
    public bool TryGet(ReagentId id, out ReagentQuantity quantity)
    {
        quantity = Get(id);
        return quantity.Quantity > 0;
    }

    public ReagentQuantity Get(ReagentId id)
    {
        foreach (var quantity in Contents)
        {
            if (quantity.Reagent == id)
                return quantity;
        }

        return new ReagentQuantity(id, FixedPoint2.Zero);
    }

    public bool TryGet(ProtoId<ReagentPrototype> id, out ReagentQuantity quantity)
    {
        quantity = Get(id);
        return quantity.Quantity > 0;
    }

    public ReagentQuantity Get(ProtoId<ReagentPrototype> id)
    {
        var reagent = new ReagentQuantity(id, FixedPoint2.Zero);

        foreach (var quantity in Contents)
        {
            if (quantity.Reagent.Prototype == id)
                reagent.Add(quantity.Quantity);
        }

        return reagent;
    }

    public bool TryGet(ReagentPrototype id, out ReagentQuantity quantity)
    {
        return TryGet(id.ID, out quantity);
    }

    public ReagentQuantity Get(ReagentPrototype id)
    {
        return Get(id.ID);
    }
}
