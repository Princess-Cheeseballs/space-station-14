namespace Content.Shared.EntityEffects.Effects.Botany.PlantAttributes;

public sealed partial class PlantAdjustWeeds : BasePlantAdjustAttribute
{
    public override string GuidebookAttributeName { get; set; } = "plant-attribute-weeds";
    public override bool GuidebookIsAttributePositive { get; protected set; } = false;
}
