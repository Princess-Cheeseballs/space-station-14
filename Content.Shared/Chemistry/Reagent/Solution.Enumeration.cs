using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Prototypes;

namespace Content.Shared.Chemistry.Reagent;

public partial struct Solution
{
    public IEnumerator<ReagentQuantity> GetEnumerator()
    {
        return Contents.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool TryGet(ReagentId key, [NotNullWhen(true)] out ReagentQuantity? reagentQuantity)
    {
        foreach (var reagent in Contents)
        {
            if (reagent.Reagent != key)
                continue;

            reagentQuantity = reagent;
            return true;
        }

        reagentQuantity = null;
        return false;
    }

    public bool Contains(ReagentId key)
    {
        foreach (var (id, _) in Contents)
        {
            if (id == key)
                return true;
        }

        return false;
    }

    public bool TryGet(ProtoId<ReagentPrototype> key, [NotNullWhen(true)] out ReagentQuantity? reagentQuantity)
    {
        return TryGet(new ReagentId(key), out reagentQuantity);
    }

    public bool Contains(ProtoId<ReagentPrototype> key)
    {
        foreach (var (id, _) in Contents)
        {
            if (id.Prototype == key)
                return true;
        }

        return false;
    }
}
