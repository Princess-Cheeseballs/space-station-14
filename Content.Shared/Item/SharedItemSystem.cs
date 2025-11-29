using System.Linq;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Item;

public abstract class SharedItemSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] protected readonly SharedContainerSystem Container = default!;
    [Dependency] private   readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    private EntityQuery<ItemComponent> _itemQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ItemComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ItemComponent, GetVerbsEvent<InteractionVerb>>(AddPickupVerb);
        SubscribeLocalEvent<ItemComponent, InteractHandEvent>(OnHandInteract);
        SubscribeLocalEvent<ItemComponent, AfterAutoHandleStateEvent>(OnItemAutoState);
        SubscribeLocalEvent<ItemComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<ItemToggleSizeComponent, ComponentInit>(OnItemToggleSizeComponentInit);
        SubscribeLocalEvent<ItemToggleSizeComponent, ItemToggledEvent>(OnItemToggle);
        SubscribeLocalEvent<ItemToggleSizeComponent, ItemToggleActivateAttemptEvent>(OnItemActivateAttempt);
        SubscribeLocalEvent<ItemToggleSizeComponent, ItemToggleDeactivateAttemptEvent>(OnItemDeactivateAttempt);

        _itemQuery = GetEntityQuery<ItemComponent>();
    }

    private void OnComponentInit(Entity<ItemComponent> item, ref ComponentInit args)
    {
        UpdateWeight(item);
    }

    private void OnItemAutoState(EntityUid uid, ItemComponent component, ref AfterAutoHandleStateEvent args)
    {
        SetHeldPrefix(uid, component.HeldPrefix, force: true, component);
    }

    #region Public API

    public bool TrySetSize(Entity<ItemComponent?> item, ProtoId<ItemSizePrototype> size)
    {
        if (!CanSetSize(item, size))
            return false;

        SetSize(item, size);
        return true;
    }

    public bool TrySetShape(Entity<ItemComponent?> item, List<Box2i> shape)
    {
        if (!CanSetShape(item, shape))
            return false;

        SetShape(item, shape);
        return true;
    }

    public bool CanSetSize(Entity<ItemComponent?> item, ProtoId<ItemSizePrototype> size)
    {
        // Doesn't exist, so don't do it!
        if (!_prototype.Resolve(size, out var proto))
            return false;

        return CanSetShape(item, proto.DefaultShape);
    }

    public bool CanSetShape(Entity<ItemComponent?> item, IReadOnlyList<Box2i> shape)
    {
        // Not stored in anything, nothing to limit it here.
        if (!_storage.TryGetStorageLocation(item, out var container, out var storage, out var loc))
            return true;

        return _storage.ItemFitsInGridLocation(item, (container.Owner, storage), loc.Position, GetAdjustedShapes(shape, loc.Rotation, loc.Position));
    }

    /// <remarks>This method is private since we want the weight directly linked to the shape of the item.</remarks>
    private void UpdateWeight(Entity<ItemComponent> item)
    {
        if (item.Comp.Shape is not { } shape)
            return;

        item.Comp.Weight = shape.Sum(box => box.Area);
        Dirty(item);
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

    private void SetSize(Entity<ItemComponent?> item, ProtoId<ItemSizePrototype> size)
    {
        if (!_itemQuery.Resolve(item, ref item.Comp, false) || item.Comp.Size == size)
            return;

        if (!_prototype.Resolve(size, out var proto))
            return;

        item.Comp.Size = size;
        item.Comp.Weight = null;
        SetShape(item, proto.DefaultShape);
    }

    private void SetShape(Entity<ItemComponent?> item, IReadOnlyList<Box2i> shape)
    {
        if (!_itemQuery.Resolve(item, ref item.Comp, false))
            return;

        item.Comp.Shape = [..shape];
        UpdateWeight((item, item.Comp));

        var ev = new ItemSizeChangedEvent(item);
        RaiseLocalEvent(item, ref ev, broadcast: true);
    }

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

    public IReadOnlyList<Box2i> GetAdjustedShapes(IReadOnlyList<Box2i> shapes, Angle rotation, Vector2i position)
    {
        var adjustedShapes = new List<Box2i>();
        GetAdjustedShapes(adjustedShapes, shapes, rotation, position);
        return adjustedShapes;
    }

    public void GetAdjustedItemShape(List<Box2i> adjustedShapes, Entity<ItemComponent?> entity, Angle rotation, Vector2i position)
    {
        var shapes = GetItemShape(entity);
        GetAdjustedShapes(adjustedShapes, shapes, rotation, position);
    }

    public void GetAdjustedShapes(List<Box2i> adjustedShapes, IReadOnlyList<Box2i> shapes, Angle rotation, Vector2i position)
    {
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

    private void OnItemToggleSizeComponentInit(Entity<ItemToggleSizeComponent> entity, ref ComponentInit args)
    {
        if (!_itemQuery.TryComp(entity, out var item) || !TryComp<ItemToggleComponent>(entity, out var toggle))
            return;

        // Set the other datafield while we're here.
        if (toggle.Activated)
        {
            entity.Comp.ActivatedShape ??= [..GetItemShape(item)];
            entity.Comp.ActivatedSize ??= item.Size;
        }
        else
        {
            entity.Comp.DeactivatedShape ??= [..GetItemShape(item)];
            entity.Comp.DeactivatedSize ??= item.Size;
        }

        Dirty(entity);
    }

    private void OnItemActivateAttempt(Entity<ItemToggleSizeComponent> item, ref ItemToggleActivateAttemptEvent args)
    {
        if ((item.Comp.ActivatedSize is not { } size || CanSetSize(item.Owner, size))
            && (item.Comp.ActivatedShape is not { } shape || CanSetShape(item.Owner, shape)))
            return;

        args.Cancelled = true;
        args.Popup = "Activation failure test message!";
    }

    private void OnItemDeactivateAttempt(Entity<ItemToggleSizeComponent> item, ref ItemToggleDeactivateAttemptEvent args)
    {
        if ((item.Comp.DeactivatedSize is not { } size || CanSetSize(item.Owner, size))
            && (item.Comp.DeactivatedShape is not { } shape || CanSetShape(item.Owner, shape)))
            return;

        args.Cancelled = true;
        args.Popup = "Deactivation failure test message!";
    }

    /// <summary>
    /// Used to update the Item component on item toggle (specifically size).
    /// </summary>
    private void OnItemToggle(Entity<ItemToggleSizeComponent> entity, ref ItemToggledEvent args)
    {
        if (!_itemQuery.TryComp(entity, out var item))
            return;

        if (args.Activated)
        {
            if (entity.Comp.ActivatedShape != null)
                SetShape((entity, item), entity.Comp.ActivatedShape);

            if (entity.Comp.ActivatedSize != null)
                SetSize((entity, item), entity.Comp.ActivatedSize.Value);
        }
        else
        {
            if (entity.Comp.DeactivatedShape != null)
                SetShape((entity, item), entity.Comp.DeactivatedShape);

            if (entity.Comp.DeactivatedSize != null)
                SetSize((entity, item), entity.Comp.DeactivatedSize.Value);
        }

        Dirty(entity);
    }
}
