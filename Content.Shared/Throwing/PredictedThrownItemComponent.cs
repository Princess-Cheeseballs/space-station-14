using Robust.Shared.Map;

namespace Content.Shared.Throwing;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class PredictedThrownItemComponent : Component
{
    [DataField]
    public EntityCoordinates? Coordinates;
}
