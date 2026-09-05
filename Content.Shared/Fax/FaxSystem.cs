using Content.Shared.Administration.Logs;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.Popups;
using Content.Shared.Fax.Components;
using Content.Shared.Labels.Components;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.NameModifier.Components;
using Content.Shared.Paper;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Fax;
/// <summary>
/// System for handling execution of a mob within fax when copy or send attempt is made.
/// </summary>
public abstract partial class FaxSystem : EntitySystem
{
    [Dependency] protected ISharedAdminLogManager AdminLogger = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] protected EmagSystem Emag = default!;
    [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private LabelSystem _labelSystem = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PaperSystem _paperSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] protected SharedAudioSystem AudioSystem = default!;
    [Dependency] private SharedDeviceNetworkSystem _deviceNetworkSystem = default!;
    [Dependency] protected SharedPopupSystem PopupSystem = default!;
    [Dependency] private SharedUserInterfaceSystem _userInterface = default!;

    private const string PaperSlotId = "Paper";

    [SubscribeLocalEvent]
    private void OnComponentInit(Entity<FaxMachineComponent> entity, ref ComponentInit args)
    {
        _itemSlotsSystem.AddItemSlot(entity.Owner, PaperSlotId, entity.Comp.PaperSlot);
        UpdateAppearance(entity);
    }

    [SubscribeLocalEvent]
    private void OnComponentRemove(Entity<FaxMachineComponent> entity, ref ComponentRemove args)
    {
        _itemSlotsSystem.RemoveItemSlot(entity.Owner, entity.Comp.PaperSlot);
    }

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<FaxMachineComponent> entity, ref MapInitEvent args)
    {
        // Load all faxes on map in cache each other to prevent taking same name by user created fax
        Refresh(entity);
    }

    [SubscribeLocalEvent]
    private void OnItemSlotChanged(Entity<FaxMachineComponent> entity, ref ContainerModifiedMessage args)
    {
        if (!entity.Comp.Initialized)
            return;

        if (args.Container.ID != entity.Comp.PaperSlot.ID)
            return;

        var isPaperInserted = entity.Comp.PaperSlot.Item.HasValue;
        if (isPaperInserted)
        {
            entity.Comp.InsertionEnd = entity.Comp.InsertionTime;
            _itemSlotsSystem.SetLock(entity.Owner, entity.Comp.PaperSlot, true);
        }

        UpdateUserInterface(entity);
    }

    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<FaxMachineComponent> entity, ref PowerChangedEvent args)
    {
        var isInsertInterrupted = !args.Powered && entity.Comp.InsertionEnd > TimeSpan.Zero;
        if (isInsertInterrupted)
        {
            entity.Comp.InsertionEnd = TimeSpan.Zero; // Reset animation

            // Drop from slot because animation did not play completely
            _itemSlotsSystem.SetLock(entity.Owner, entity.Comp.PaperSlot, false);
            _itemSlotsSystem.TryEject(entity, entity.Comp.PaperSlot, null, out _, true);
        }

        var isPrintInterrupted = !args.Powered && entity.Comp.PrintTimeEnd > TimeSpan.Zero;
        if (isPrintInterrupted)
        {
            entity.Comp.PrintTimeEnd = TimeSpan.Zero; // Reset animation
        }

        if (isInsertInterrupted || isPrintInterrupted)
            UpdateAppearance(entity);

        _itemSlotsSystem.SetLock(entity.Owner, entity.Comp.PaperSlot, !args.Powered); // Lock slot when power is off
    }

    [SubscribeLocalEvent]
    private void OnPingPayload(Entity<FaxMachineComponent> ent, ref DeviceNetworkPacketEvent<FaxPingPayload> args)
    {
        var isForSyndie = Emag.CheckFlag(ent.Owner, EmagType.Interaction) && args.Data.IsSyndicate;
        if (!isForSyndie && !ent.Comp.ResponsePings)
            return;

        var pong = new FaxPongPayload
        {
            FaxName = ent.Comp.FaxName,
        };

        _deviceNetworkSystem.SendPacket(ent.Owner, args.SenderAddress, ref pong);
    }

    [SubscribeLocalEvent]
    private void OnPongPayload(Entity<FaxMachineComponent> ent, ref DeviceNetworkPacketEvent<FaxPongPayload> args)
    {
        ent.Comp.KnownFaxes[args.SenderAddress] = args.Data.FaxName;
        UpdateUserInterface(ent.Owner, ent.Comp);
    }

    [SubscribeLocalEvent]
    private void OnPrintPayload(Entity<FaxMachineComponent> ent, ref DeviceNetworkPacketEvent<FaxPrintPayload> args)
    {
        Receive((ent, ent), args.Data.Data);
    }

    [SubscribeLocalEvent]
    private void OnToggleInterface(Entity<FaxMachineComponent> entity, ref AfterActivatableUIOpenEvent args)
    {
        UpdateUserInterface(entity);
    }

    [SubscribeLocalEvent]
    private void OnFileButtonPressed(Entity<FaxMachineComponent> entity, ref FaxFileMessage args)
    {
        args.Label = args.Label?[..Math.Min(args.Label.Length, FaxFileMessageValidation.MaxLabelSize)];
        args.Content = args.Content[..Math.Min(args.Content.Length, FaxFileMessageValidation.MaxContentSize)];
        PrintFile(entity, ref args);
    }

    [SubscribeLocalEvent]
    private void OnCopyButtonPressed(Entity<FaxMachineComponent> entity, ref FaxCopyMessage args)
    {
        if (HasComp<MobStateComponent>(entity.Comp.PaperSlot.Item))
            Faxecute(entity); // when button pressed it will hurt the mob.
        else
            Copy(entity, ref args);
    }

    [SubscribeLocalEvent]
    private void OnSendButtonPressed(Entity<FaxMachineComponent> entity, ref FaxSendMessage args)
    {
        if (HasComp<MobStateComponent>(entity.Comp.PaperSlot.Item))
            Faxecute(entity); // when button pressed it will hurt the mob.
        else
            Send(entity, args.Actor);
    }

    [SubscribeLocalEvent]
    private void OnRefreshButtonPressed(Entity<FaxMachineComponent> entity, ref FaxRefreshMessage args)
    {
        Refresh(entity);
    }

    [SubscribeLocalEvent]
    private void OnDestinationSelected(Entity<FaxMachineComponent> entity, ref FaxDestinationMessage args)
    {
        SetDestination(entity, args.Address);
    }

    // We can't predict power shutting off, so we just let the animation continue if power gets cut out.
    // If that ever changes, have this animation pause cause it would be pretty funny.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FaxMachineComponent>();
        while (query.MoveNext(out var uid, out var fax))
        {
            // TODO: Bitmask to know what we're currently doing if anything...
            ProcessPrint((uid, fax));
            ProcessInsertion((uid, fax));
            //ProcessSendingTimeout((uid, fax));
        }
    }

    protected void UpdateAppearance(Entity<FaxMachineComponent> entity)
    {
        if (TryComp<FaxableObjectComponent>(entity.Comp.PaperSlot.Item, out var faxable))
            entity.Comp.InsertingState = faxable.InsertingState;

        if (entity.Comp.InsertionEnd > TimeSpan.Zero)
        {
            _appearanceSystem.SetData(entity, FaxMachineVisuals.VisualState, FaxMachineVisualState.Inserting);
            Dirty(entity);
        }
        else if (entity.Comp.PrintTimeEnd > TimeSpan.Zero)
            _appearanceSystem.SetData(entity, FaxMachineVisuals.VisualState, FaxMachineVisualState.Printing);
        else
            _appearanceSystem.SetData(entity, FaxMachineVisuals.VisualState, FaxMachineVisualState.Normal);
    }

    protected void UpdateUserInterface(EntityUid uid, FaxMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var isPaperInserted = component.PaperSlot.Item != null;
        var canSend = isPaperInserted &&
                      component.DestinationFaxAddress != null &&
                      component.SendTimeoutRemaining <= TimeSpan.Zero &&
                      component.InsertionEnd <= TimeSpan.Zero;
        var canCopy = isPaperInserted &&
                      component.SendTimeoutRemaining <= TimeSpan.Zero &&
                      component.InsertionEnd <= TimeSpan.Zero;
        var state = new FaxUiState(component.FaxName, component.KnownFaxes, canSend, canCopy, isPaperInserted, component.DestinationFaxAddress);
        _userInterface.SetUiState(uid, FaxUiKey.Key, state);
    }


    private void ProcessPrint(Entity<FaxMachineComponent> entity)
    {
        if (_timing.CurTime < entity.Comp.PrintTimeEnd)
            return;

        SpawnPaperFromQueue(entity.AsNullable());
        UpdateUserInterface(entity);
        if (entity.Comp.PrintingQueue.Count == 0)
            return;

        // TODO: Call Print
        entity.Comp.PrintTimeEnd = entity.Comp.PrintingTime + _timing.CurTime;
    }

    private void ProcessInsertion(Entity<FaxMachineComponent> entity)
    {
        if (_timing.CurTime < entity.Comp.InsertionEnd)
            return;

        UpdateAppearance(entity);

        var isAnimationEnd = entity.Comp.InsertionEnd <= TimeSpan.Zero;
        if (isAnimationEnd)
        {
            _itemSlotsSystem.SetLock(entity.Owner, entity.Comp.PaperSlot, false);
            UpdateUserInterface(entity);
        }
    }

    private void ProcessSendingTimeout(Entity<FaxMachineComponent> entity, float frameTime)
    {
        if (entity.Comp.SendTimeoutRemaining > TimeSpan.Zero)
        {
            entity.Comp.SendTimeoutRemaining -= TimeSpan.FromSeconds(frameTime);

            if (entity.Comp.SendTimeoutRemaining <= TimeSpan.Zero)
                UpdateUserInterface(entity);
        }
    }

    protected void Faxecute(Entity<FaxMachineComponent> entity)
    {
        var sendEntity = entity.Comp.PaperSlot.Item;
        if (sendEntity == null)
            return;

        if (!TryComp<FaxecuteComponent>(entity, out var faxecute))
            return;

        var damageSpec = faxecute.Damage;
        _damageable.ChangeDamage(sendEntity.Value, damageSpec);
        PopupSystem.PopupEntity(Loc.GetString("fax-machine-popup-error", ("target", entity)), entity, PopupType.LargeCaution);
    }

    /// <summary>
    ///     Set fax destination address not checking if he knows it exists
    /// </summary>
    private void SetDestination(Entity<FaxMachineComponent> entity, string destAddress)
    {
        entity.Comp.DestinationFaxAddress = destAddress;
        entity.Comp.DestinationFaxName = entity.Comp.KnownFaxes[destAddress];

        UpdateUserInterface(entity);
    }

    /// <summary>
    ///     Clears current known fax info and make network scan ping
    ///     Adds special data to  payload if it was emagged to identify itself as a Syndicate
    /// </summary>
    private void Refresh(Entity<FaxMachineComponent> entity)
    {
        entity.Comp.DestinationFaxAddress = null;
        entity.Comp.KnownFaxes.Clear();

        var payload = new FaxPingPayload
        {
            IsSyndicate = Emag.CheckFlag(entity, EmagType.Interaction),
        };

        _deviceNetworkSystem.SendPacket(entity.Owner, null, ref payload);
    }

    private void PrintFile(Entity<FaxMachineComponent> entity, ref FaxFileMessage args)
    {
        PrintFile(entity, args.Content, args.OfficePaper, args.Label, args.Actor);
    }

    /// <summary>
    ///     Makes fax print from a file from the computer. A timeout is set after copying,
    ///     which is shared by the send button.
    /// </summary>
    [PublicAPI]
    public void PrintFile(Entity<FaxMachineComponent> entity, string content, bool officePaper, string? label = null, EntityUid? actor = null)
    {
        var prototype = officePaper ? entity.Comp.PrintOfficePaperId : entity.Comp.PrintPaperId;

        var name = Loc.GetString("fax-machine-printed-paper-name");

        var printout = new FaxPrintout(content, name, label, prototype);
        entity.Comp.PrintingQueue.Enqueue(printout);
        entity.Comp.SendTimeoutRemaining += entity.Comp.SendTimeout;

        UpdateUserInterface(entity);

        // Unfortunately, since a paper entity does not yet exist, we have to emulate what LabelSystem will do.
        AdminLogger.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(actor):actor} " +
            $"added print job to \"{entity.Comp.FaxName}\" {ToPrettyString(entity):tool} " +
            $"of {_labelSystem.Label(name, label)}: {content}");

        AudioSystem.PlayPredicted(entity.Comp.PrintSound, entity, actor);
    }

    /// <summary>
    ///     Copies the paper in the fax. A timeout is set after copying,
    ///     which is shared by the send button.
    /// </summary>
    private void Copy(Entity<FaxMachineComponent> entity, ref FaxCopyMessage args)
    {
        if (entity.Comp.SendTimeoutRemaining > TimeSpan.Zero)
            return;

        var sendEntity = entity.Comp.PaperSlot.Item;
        if (sendEntity == null)
            return;

        if (!TryComp(sendEntity, out MetaDataComponent? metadata) ||
            !TryComp<PaperComponent>(sendEntity, out var paper))
            return;

        TryComp<LabelComponent>(sendEntity, out var labelComponent);
        TryComp<NameModifierComponent>(sendEntity, out var nameMod);

        // Comment does not exist >:(
        // TODO: See comment in 'Send()' about not being able to copy whole entities
        var printout = new FaxPrintout(paper.Content,
                                       nameMod?.BaseName ?? metadata.EntityName,
                                       labelComponent?.CurrentLabel,
                                       metadata.EntityPrototype?.ID ?? entity.Comp.PrintPaperId,
                                       paper.StampState,
                                       paper.StampedBy,
                                       paper.EditingDisabled);

        entity.Comp.PrintingQueue.Enqueue(printout);
        entity.Comp.SendTimeoutRemaining += entity.Comp.SendTimeout;

        // Don't play component.SendSound - it clashes with the printing sound, which
        // will start immediately.

        UpdateUserInterface(entity);

        AdminLogger.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(args.Actor):actor} " +
            $"added copy job to \"{entity.Comp.FaxName}\" {ToPrettyString(entity):tool} " +
            $"of {ToPrettyString(sendEntity):subject}: {printout.Content}");
    }

    /// <summary>
    ///     Sends message to addressee if paper is set and a known fax is selected
    ///     A timeout is set after sending, which is shared by the copy button.
    /// </summary>
    public void Send(Entity<FaxMachineComponent> entity, EntityUid? user)
    {
        if (entity.Comp.SendTimeoutRemaining > TimeSpan.Zero)
            return;

        var sendEntity = entity.Comp.PaperSlot.Item;
        if (sendEntity == null)
            return;

        if (entity.Comp.DestinationFaxAddress == null)
            return;

        if (!entity.Comp.KnownFaxes.TryGetValue(entity.Comp.DestinationFaxAddress, out var faxName))
            return;

        if (!TryComp(sendEntity, out MetaDataComponent? metadata) ||
           !TryComp<PaperComponent>(sendEntity, out var paper))
            return;

        if (metadata.EntityPrototype == null)
            return;

        TryComp<NameModifierComponent>(sendEntity, out var nameMod);

        TryComp<LabelComponent>(sendEntity, out var labelComponent);

        var payload = new FaxPrintPayload
        {
            Data = new FaxPrintout(
                    paper.Content,
                    nameMod?.BaseName ?? metadata.EntityName,
                    labelComponent?.CurrentLabel,
                    metadata.EntityPrototype.ID,
                    paper.StampState,
                    paper.StampedBy,
                    paper.EditingDisabled),
        };

        _deviceNetworkSystem.SendPacket(entity.Owner, entity.Comp.DestinationFaxAddress, ref payload);

        AdminLogger.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(user):actor} " +
            $"sent fax from \"{entity.Comp.FaxName}\" {ToPrettyString(entity):tool} " +
            $"to \"{faxName}\" ({entity.Comp.DestinationFaxAddress}) " +
            $"of {ToPrettyString(sendEntity):subject}: {paper.Content}");

        entity.Comp.SendTimeoutRemaining += entity.Comp.SendTimeout;

        AudioSystem.PlayPredicted(entity.Comp.SendSound, entity, user);
        UpdateUserInterface(entity);
    }

    /// <summary>
    ///     Accepts a new message and adds it to the queue to print
    ///     If has parameter "notifyAdmins" also output a special message to admin chat.
    /// </summary>
    public void Receive(Entity<FaxMachineComponent?> entity, FaxPrintout printout)
    {
        if (!Resolve(entity, ref entity.Comp))
            return;

        var faxName = printout.SenderFaxName ?? Loc.GetString("fax-machine-popup-source-unknown");

        PopupSystem.PopupEntity(Loc.GetString("fax-machine-popup-received", ("from", faxName)), entity);
        _appearanceSystem.SetData(entity, FaxMachineVisuals.VisualState, FaxMachineVisualState.Printing);

        if (entity.Comp.NotifyAdmins)
            NotifyAdmins(faxName);

        // Can't predict this atm...
        AudioSystem.PlayPvs(entity.Comp.PrintSound, entity);
        entity.Comp.PrintingQueue.Enqueue(printout);
    }

    private void StartPrint()
    {
        // TODO: Play sound, queue print!
    }

    private void SpawnPaperFromQueue(Entity<FaxMachineComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp) || entity.Comp.PrintingQueue.Count == 0)
            return;

        var printout = entity.Comp.PrintingQueue.Dequeue();

        var entityToSpawn = ProtoMan.HasIndex(printout.PrototypeId) ? printout.PrototypeId : entity.Comp.PrintPaperId;
        var printed = Spawn(entityToSpawn, Transform(entity).Coordinates);

        if (TryComp<PaperComponent>(printed, out var paper))
        {
            _paperSystem.SetContent((printed, paper), printout.Content);

            // Apply stamps
            if (printout.StampState != null)
            {
                foreach (var stamp in printout.StampedBy)
                {
                    _paperSystem.TryStamp((printed, paper), stamp, printout.StampState);
                }
            }

            paper.EditingDisabled = printout.Locked;
        }

        _metaData.SetEntityName(printed, printout.Name);

        if (printout.Label is { } label)
        {
            _labelSystem.Label(printed, label);
        }

        AdminLogger.Add(LogType.Action, LogImpact.Low, $"\"{entity.Comp.FaxName}\" {ToPrettyString(entity):tool} printed {ToPrettyString(printed):subject}: {printout.Content}");
    }

    protected abstract void NotifyAdmins(string faxName);
}

[Serializable, NetSerializable]
public enum FaxUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class FaxUiState : BoundUserInterfaceState
{
    public string DeviceName { get; }
    public Dictionary<string, string> AvailablePeers { get; }
    public string? DestinationAddress { get; }
    public bool IsPaperInserted { get; }
    public bool CanSend { get; }
    public bool CanCopy { get; }

    public FaxUiState(string deviceName,
        Dictionary<string, string> peers,
        bool canSend,
        bool canCopy,
        bool isPaperInserted,
        string? destAddress)
    {
        DeviceName = deviceName;
        AvailablePeers = peers;
        IsPaperInserted = isPaperInserted;
        CanSend = canSend;
        CanCopy = canCopy;
        DestinationAddress = destAddress;
    }
}

[Serializable, NetSerializable]
public sealed class FaxFileMessage : BoundUserInterfaceMessage
{
    public string? Label;
    public string Content;
    public bool OfficePaper;

    public FaxFileMessage(string? label, string content, bool officePaper)
    {
        Label = label;
        Content = content;
        OfficePaper = officePaper;
    }
}

public static class FaxFileMessageValidation
{
    public const int MaxLabelSize = HandLabelerComponent.MaxLabelLength; // parity with Content.Server.Labels.Components.HandLabelerComponent.MaxLabelChars
    public const int MaxContentSize = 10000;
}

[Serializable, NetSerializable]
public sealed class FaxCopyMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FaxSendMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FaxRefreshMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FaxDestinationMessage : BoundUserInterfaceMessage
{
    public string Address { get; }
    public FaxDestinationMessage(string address)
    {
        Address = address;
    }
}
