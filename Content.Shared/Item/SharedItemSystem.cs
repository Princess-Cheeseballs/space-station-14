using System.Linq;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Storage;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Item;

public abstract class SharedItemSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private   readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] protected readonly SharedContainerSystem Container = default!;

    private EntityQuery<ItemComponent> _itemQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ItemComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ItemComponent, GetVerbsEvent<InteractionVerb>>(AddPickupVerb);
        SubscribeLocalEvent<ItemComponent, InteractHandEvent>(OnHandInteract);
        SubscribeLocalEvent<ItemComponent, AfterAutoHandleStateEvent>(OnItemAutoState);
        SubscribeLocalEvent<ItemComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<ItemToggleSizeComponent, ItemToggledEvent>(OnItemToggle);

        _itemQuery = GetEntityQuery<ItemComponent>();
    }

    private void OnComponentInit(Entity<ItemComponent> item, ref ComponentInit args)
    {
        if (item.Comp.Shape is not { } shape)
            return;

        UpdateWeight(item);
        Dirty(item);
    }

    private void OnItemAutoState(EntityUid uid, ItemComponent component, ref AfterAutoHandleStateEvent args)
    {
        SetHeldPrefix(uid, component.HeldPrefix, force: true, component);
    }

    #region Public API

    public void SetSize(Entity<ItemComponent?> item, ProtoId<ItemSizePrototype> size)
    {
        if (!_itemQuery.Resolve(item, ref item.Comp, false) || item.Comp.Size == size)
            return;

        item.Comp.Size = size;
        item.Comp.Weight = null;

        Dirty(item);
        var ev = new ItemSizeChangedEvent(item);
        RaiseLocalEvent(item, ref ev, broadcast: true);
    }

    public void SetShape(Entity<ItemComponent?> item, List<Box2i>? shape)
    {
        if (!_itemQuery.Resolve(item, ref item.Comp, false) || item.Comp.Shape == shape)
            return;

        item.Comp.Shape = shape;
        UpdateWeight((item, item.Comp));

        Dirty(item);
        var ev = new ItemSizeChangedEvent(item);
        RaiseLocalEvent(item, ref ev, broadcast: true);
    }

    /// <remarks>This method is private since we want the weight directly linked to the shape of the item.</remarks>
    private void UpdateWeight(Entity<ItemComponent> item)
    {
        if (item.Comp.Shape is not { } shape)
            return;

        item.Comp.Weight = shape.Sum(box => box.Area);
    }

    /// <summary>
    /// Sets the offset used for the item's sprite inside the storage UI.
    /// Dirties.
    /// </summary>
    [PublicAPI]
    public void SetStoredOffset(EntityUid uid, Vector2i newOffset, ItemComponent? component = null)
    {
        if (!_itemQuery.Resolve(uid, ref component, false))
            return;

        component.StoredOffset = newOffset;
        Dirty(uid, component);
    }

    public void SetHeldPrefix(EntityUid uid, string? heldPrefix, bool force = false, ItemComponent? component = null)
    {
        if (!_itemQuery.Resolve(uid, ref component, false))
            return;

        if (!force && component.HeldPrefix == heldPrefix)
            return;

        component.HeldPrefix = heldPrefix;
        Dirty(uid, component);
        VisualsChanged(uid);
    }

    /// <summary>
    ///     Copy all item specific visuals from another item.
    /// </summary>
    public void CopyVisuals(EntityUid uid, ItemComponent otherItem, ItemComponent? item = null)
    {
        if (!_itemQuery.Resolve(uid, ref item))
            return;

        item.RsiPath = otherItem.RsiPath;
        item.InhandVisuals = otherItem.InhandVisuals;
        item.HeldPrefix = otherItem.HeldPrefix;

        Dirty(uid, item);
        VisualsChanged(uid);
    }

    #endregion

    private void OnHandInteract(EntityUid uid, ItemComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = _handsSystem.TryPickup(args.User, uid, animateUser: false);
    }

    private void AddPickupVerb(EntityUid uid, ItemComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (args.Hands == null ||
            args.Using != null ||
            !args.CanAccess ||
            !args.CanInteract ||
            !_handsSystem.CanPickupAnyHand(args.User, args.Target, handsComp: args.Hands, item: component))
            return;

        InteractionVerb verb = new();
        verb.Act = () => _handsSystem.TryPickupAnyHand(args.User, args.Target, checkActionBlocker: false, handsComp: args.Hands, item: component);
        verb.Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/pickup.svg.192dpi.png"));

        // if the item already in a container (that is not the same as the user's), then change the text.
        // this occurs when the item is in their inventory or in an open backpack
        Container.TryGetContainingContainer((args.User, null, null), out var userContainer);
        if (Container.TryGetContainingContainer((args.Target, null, null), out var container) && container != userContainer)
            verb.Text = Loc.GetString("pick-up-verb-get-data-text-inventory");
        else
            verb.Text = Loc.GetString("pick-up-verb-get-data-text");

        args.Verbs.Add(verb);
    }

    private void OnExamine(EntityUid uid, ItemComponent component, ExaminedEvent args)
    {
        // show at end of message generally
        args.PushMarkup(Loc.GetString("item-component-on-examine-size",
            ("size", GetItemSizeLocale(component.Size))),
            priority: -2);
    }

    public ItemSizePrototype GetSizePrototype(ProtoId<ItemSizePrototype> id)
    {
        return _prototype.Index(id);
    }

    /// <summary>
    ///     Notifies any entity that is holding or wearing this item that they may need to update their sprite.
    /// </summary>
    /// <remarks>
    ///     This is used for updating both inhand sprites and clothing sprites, but it's here just cause it needs to
    ///     be in one place.
    /// </remarks>
    public virtual void VisualsChanged(EntityUid owner)
    {
    }

    [PublicAPI]
    public string GetItemSizeLocale(ProtoId<ItemSizePrototype> size)
    {
        return Loc.GetString(GetSizePrototype(size).Name);
    }

    [PublicAPI]
    public int GetItemWeight(Entity<ItemComponent?> item)
    {
        return _itemQuery.Resolve(item, ref item.Comp) ? GetItemWeight(item.Comp) : 0;
    }

    [PublicAPI]
    public int GetItemWeight(ItemComponent item)
    {
        return item.Weight ?? GetItemWeight(item.Size);
    }

    [PublicAPI]
    public int GetItemWeight(ProtoId<ItemSizePrototype> size)
    {
        return GetSizePrototype(size).Weight;
    }

    /// <summary>
    /// Gets the default shape of an item.
    /// </summary>
    public IReadOnlyList<Box2i> GetItemShape(Entity<ItemComponent?> uid)
    {
        if (!Resolve(uid, ref uid.Comp))
            return new Box2i[] { };

        return uid.Comp.Shape ?? GetSizePrototype(uid.Comp.Size).DefaultShape;
    }

    /// <summary>
    /// Gets the default shape of an item.
    /// </summary>
    public IReadOnlyList<Box2i> GetItemShape(ItemComponent component)
    {
        return component.Shape ?? GetSizePrototype(component.Size).DefaultShape;
    }

    /// <summary>
    /// Gets the shape of an item, adjusting for rotation and offset.
    /// </summary>
    public IReadOnlyList<Box2i> GetAdjustedItemShape(Entity<ItemComponent?> entity, ItemStorageLocation location)
    {
        return GetAdjustedItemShape(entity, location.Rotation, location.Position);
    }

    /// <summary>
    /// Gets the shape of an item, adjusting for rotation and offset.
    /// </summary>
    public IReadOnlyList<Box2i> GetAdjustedItemShape(Entity<ItemComponent?> entity, Angle rotation, Vector2i position)
    {
        if (!_itemQuery.Resolve(entity, ref entity.Comp))
            return [];

        var adjustedShapes = new List<Box2i>();
        GetAdjustedItemShape(adjustedShapes, entity, rotation, position);
        return adjustedShapes;
    }

    public void GetAdjustedItemShape(List<Box2i> adjustedShapes, Entity<ItemComponent?> entity, Angle rotation, Vector2i position)
    {
        var shapes = GetItemShape(entity);
        var boundingShape = shapes.GetBoundingBox();
        var boundingCenter = ((Box2) boundingShape).Center;
        var matty = Matrix3Helpers.CreateTransform(boundingCenter, rotation);
        var drift = boundingShape.BottomLeft - matty.TransformBox(boundingShape).BottomLeft;

        foreach (var shape in shapes)
        {
            var transformed = matty.TransformBox(shape).Translated(drift);
            var floored = new Box2i(transformed.BottomLeft.Floored(), transformed.TopRight.Floored());
            var translated = floored.Translated(position);

            adjustedShapes.Add(translated);
        }
    }

    /// <summary>
    /// Used to update the Item component on item toggle (specifically size).
    /// </summary>
    private void OnItemToggle(EntityUid uid, ItemToggleSizeComponent itemToggleSize, ItemToggledEvent args)
    {
        if (!_itemQuery.TryComp(uid, out var item))
            return;

        if (args.Activated)
        {
            if (itemToggleSize.ActivatedShape != null)
            {
                // Set the deactivated shape to the default item's shape before it gets changed.
                itemToggleSize.DeactivatedShape ??= [..GetItemShape(item)];
                SetShape((uid, item), itemToggleSize.ActivatedShape);
            }

            if (itemToggleSize.ActivatedSize != null)
            {
                // Set the deactivated size to the default item's size before it gets changed.
                itemToggleSize.DeactivatedSize ??= item.Size;
                SetSize((uid, item), itemToggleSize.ActivatedSize.Value);
            }
        }
        else
        {
            if (itemToggleSize.DeactivatedShape != null)
            {
                SetShape((uid, item), itemToggleSize.DeactivatedShape);
            }

            if (itemToggleSize.DeactivatedSize != null)
            {
                SetSize((uid, item), itemToggleSize.DeactivatedSize.Value);
            }
        }

        Dirty(uid, itemToggleSize);
    }
}
