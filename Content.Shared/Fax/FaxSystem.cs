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
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared.Fax;
/// <summary>
/// System for handling execution of a mob within fax when copy or send attempt is made.
/// </summary>
public abstract partial class FaxSystem : EntitySystem
{
    [Dependency] protected ISharedAdminLogManager AdminLogger = default!;
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
            entity.Comp.InsertingTimeRemaining = entity.Comp.InsertionTime;
            _itemSlotsSystem.SetLock(entity.Owner, entity.Comp.PaperSlot, true);
        }

        UpdateUserInterface(entity);
    }

    [SubscribeLocalEvent]
    private void OnPowerChanged(EntityUid uid, FaxMachineComponent component, ref PowerChangedEvent args)
    {
        var isInsertInterrupted = !args.Powered && component.InsertingTimeRemaining > 0;
        if (isInsertInterrupted)
        {
            component.InsertingTimeRemaining = 0f; // Reset animation

            // Drop from slot because animation did not play completely
            _itemSlotsSystem.SetLock(uid, component.PaperSlot, false);
            _itemSlotsSystem.TryEject(uid, component.PaperSlot, null, out var _, true);
        }

        var isPrintInterrupted = !args.Powered && component.PrintingTimeRemaining > 0;
        if (isPrintInterrupted)
        {
            component.PrintingTimeRemaining = 0f; // Reset animation
        }

        if (isInsertInterrupted || isPrintInterrupted)
            UpdateAppearance(uid, component);

        _itemSlotsSystem.SetLock(uid, component.PaperSlot, !args.Powered); // Lock slot when power is off
    }

    [SubscribeLocalEvent]
    private void OnEmagged(EntityUid uid, FaxMachineComponent component, ref GotEmaggedEvent args)
    {
        if (!Emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (Emag.CheckFlag(uid, EmagType.Interaction))
            return;

        args.Handled = true;
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
        Receive(ent, args.Data.Data, args.SenderAddress);
    }

    [SubscribeLocalEvent]
    private void OnToggleInterface(EntityUid uid, FaxMachineComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    [SubscribeLocalEvent]
    private void OnFileButtonPressed(EntityUid uid, FaxMachineComponent component, FaxFileMessage args)
    {
        args.Label = args.Label?[..Math.Min(args.Label.Length, FaxFileMessageValidation.MaxLabelSize)];
        args.Content = args.Content[..Math.Min(args.Content.Length, FaxFileMessageValidation.MaxContentSize)];
        PrintFile(uid, component, args);
    }

    [SubscribeLocalEvent]
    private void OnCopyButtonPressed(EntityUid uid, FaxMachineComponent component, FaxCopyMessage args)
    {
        if (HasComp<MobStateComponent>(component.PaperSlot.Item))
            Faxecute(uid, component); // when button pressed it will hurt the mob.
        else
            Copy(uid, component, args);
    }

    [SubscribeLocalEvent]
    private void OnSendButtonPressed(EntityUid uid, FaxMachineComponent component, FaxSendMessage args)
    {
        if (HasComp<MobStateComponent>(component.PaperSlot.Item))
            Faxecute(uid, component); // when button pressed it will hurt the mob.
        else
            Send(uid, component, args);
    }

    [SubscribeLocalEvent]
    private void OnRefreshButtonPressed(EntityUid uid, FaxMachineComponent component, FaxRefreshMessage args)
    {
        Refresh(uid, component);
    }

    [SubscribeLocalEvent]
    private void OnDestinationSelected(EntityUid uid, FaxMachineComponent component, FaxDestinationMessage args)
    {
        SetDestination(uid, args.Address, component);
    }

    // We can't predict power shutting off, so we just let the animation continue if power gets cut out.
    // If that ever changes, have this animation pause cause it would be pretty funny.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FaxMachineComponent>();
        while (query.MoveNext(out var uid, out var fax))
        {
            ProcessPrintingAnimation(uid, frameTime, fax);
            ProcessInsertingAnimation(uid, frameTime, fax);
            ProcessSendingTimeout(uid, frameTime, fax);
        }
    }

    protected void UpdateAppearance(EntityUid uid, FaxMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (TryComp<FaxableObjectComponent>(component.PaperSlot.Item, out var faxable))
            component.InsertingState = faxable.InsertingState;


        if (component.InsertingTimeRemaining > 0)
        {
            _appearanceSystem.SetData(uid, FaxMachineVisuals.VisualState, FaxMachineVisualState.Inserting);
            Dirty(uid, component);
        }
        else if (component.PrintingTimeRemaining > 0)
            _appearanceSystem.SetData(uid, FaxMachineVisuals.VisualState, FaxMachineVisualState.Printing);
        else
            _appearanceSystem.SetData(uid, FaxMachineVisuals.VisualState, FaxMachineVisualState.Normal);
    }

    protected void UpdateUserInterface(EntityUid uid, FaxMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var isPaperInserted = component.PaperSlot.Item != null;
        var canSend = isPaperInserted &&
                      component.DestinationFaxAddress != null &&
                      component.SendTimeoutRemaining <= 0 &&
                      component.InsertingTimeRemaining <= 0;
        var canCopy = isPaperInserted &&
                      component.SendTimeoutRemaining <= 0 &&
                      component.InsertingTimeRemaining <= 0;
        var state = new FaxUiState(component.FaxName, component.KnownFaxes, canSend, canCopy, isPaperInserted, component.DestinationFaxAddress);
        _userInterface.SetUiState(uid, FaxUiKey.Key, state);
    }


    private void ProcessPrintingAnimation(EntityUid uid, float frameTime, FaxMachineComponent comp)
    {
        if (comp.PrintingTimeRemaining > 0)
        {
            comp.PrintingTimeRemaining -= frameTime;
            UpdateAppearance(uid, comp);

            var isAnimationEnd = comp.PrintingTimeRemaining <= 0;
            if (isAnimationEnd)
            {
                SpawnPaperFromQueue(uid, comp);
                UpdateUserInterface(uid, comp);
            }

            return;
        }

        if (comp.PrintingQueue.Count > 0)
        {
            comp.PrintingTimeRemaining = comp.PrintingTime;
            AudioSystem.PlayPvs(comp.PrintSound, uid);
        }
    }

    private void ProcessInsertingAnimation(EntityUid uid, float frameTime, FaxMachineComponent comp)
    {
        if (comp.InsertingTimeRemaining <= 0)
            return;

        comp.InsertingTimeRemaining -= frameTime;
        UpdateAppearance(uid, comp);

        var isAnimationEnd = comp.InsertingTimeRemaining <= 0;
        if (isAnimationEnd)
        {
            _itemSlotsSystem.SetLock(uid, comp.PaperSlot, false);
            UpdateUserInterface(uid, comp);
        }
    }

    private void ProcessSendingTimeout(EntityUid uid, float frameTime, FaxMachineComponent comp)
    {
        if (comp.SendTimeoutRemaining > 0)
        {
            comp.SendTimeoutRemaining -= frameTime;

            if (comp.SendTimeoutRemaining <= 0)
                UpdateUserInterface(uid, comp);
        }
    }

    protected void Faxecute(EntityUid uid, FaxMachineComponent component, DamageOnFaxecuteEvent? args = null)
    {
        var sendEntity = component.PaperSlot.Item;
        if (sendEntity == null)
            return;

        if (!TryComp<FaxecuteComponent>(uid, out var faxecute))
            return;

        var damageSpec = faxecute.Damage;
        _damageable.ChangeDamage(sendEntity.Value, damageSpec);
        PopupSystem.PopupEntity(Loc.GetString("fax-machine-popup-error", ("target", uid)), uid, PopupType.LargeCaution);
    }

    /// <summary>
    ///     Set fax destination address not checking if he knows it exists
    /// </summary>
    public void SetDestination(EntityUid uid, string destAddress, FaxMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.DestinationFaxAddress = destAddress;
        component.DestinationFaxName = component.KnownFaxes[destAddress];

        UpdateUserInterface(uid, component);
    }

    /// <summary>
    ///     Clears current known fax info and make network scan ping
    ///     Adds special data to  payload if it was emagged to identify itself as a Syndicate
    /// </summary>
    public void Refresh(EntityUid uid, FaxMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.DestinationFaxAddress = null;
        component.KnownFaxes.Clear();

        var payload = new FaxPingPayload
        {
            IsSyndicate = Emag.CheckFlag(uid, EmagType.Interaction),
        };

        _deviceNetworkSystem.SendPacket(uid, null, ref payload);
    }

    /// <summary>
    ///     Makes fax print from a file from the computer. A timeout is set after copying,
    ///     which is shared by the send button.
    /// </summary>
    public void PrintFile(EntityUid uid, FaxMachineComponent component, FaxFileMessage args)
    {
        var prototype = args.OfficePaper ? component.PrintOfficePaperId : component.PrintPaperId;

        var name = Loc.GetString("fax-machine-printed-paper-name");

        var printout = new FaxPrintout(args.Content, name, args.Label, prototype);
        component.PrintingQueue.Enqueue(printout);
        component.SendTimeoutRemaining += component.SendTimeout;

        UpdateUserInterface(uid, component);

        // Unfortunately, since a paper entity does not yet exist, we have to emulate what LabelSystem will do.
        var nameWithLabel = (args.Label is { } label) ? $"{name} ({label})" : name;
        AdminLogger.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(args.Actor):actor} " +
            $"added print job to \"{component.FaxName}\" {ToPrettyString(uid):tool} " +
            $"of {nameWithLabel}: {args.Content}");
    }

    /// <summary>
    ///     Copies the paper in the fax. A timeout is set after copying,
    ///     which is shared by the send button.
    /// </summary>
    public void Copy(EntityUid uid, FaxMachineComponent? component, FaxCopyMessage args)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.SendTimeoutRemaining > 0)
            return;

        var sendEntity = component.PaperSlot.Item;
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
                                       metadata.EntityPrototype?.ID ?? component.PrintPaperId,
                                       paper.StampState,
                                       paper.StampedBy,
                                       paper.EditingDisabled);

        component.PrintingQueue.Enqueue(printout);
        component.SendTimeoutRemaining += component.SendTimeout;

        // Don't play component.SendSound - it clashes with the printing sound, which
        // will start immediately.

        UpdateUserInterface(uid, component);

        AdminLogger.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(args.Actor):actor} " +
            $"added copy job to \"{component.FaxName}\" {ToPrettyString(uid):tool} " +
            $"of {ToPrettyString(sendEntity):subject}: {printout.Content}");
    }

    /// <summary>
    ///     Sends message to addressee if paper is set and a known fax is selected
    ///     A timeout is set after sending, which is shared by the copy button.
    /// </summary>
    public void Send(EntityUid uid, FaxMachineComponent? component, FaxSendMessage args)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.SendTimeoutRemaining > 0)
            return;

        var sendEntity = component.PaperSlot.Item;
        if (sendEntity == null)
            return;

        if (component.DestinationFaxAddress == null)
            return;

        if (!component.KnownFaxes.TryGetValue(component.DestinationFaxAddress, out var faxName))
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

        _deviceNetworkSystem.SendPacket(uid, component.DestinationFaxAddress, ref payload);

        AdminLogger.Add(LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(args.Actor):actor} " +
            $"sent fax from \"{component.FaxName}\" {ToPrettyString(uid):tool} " +
            $"to \"{faxName}\" ({component.DestinationFaxAddress}) " +
            $"of {ToPrettyString(sendEntity):subject}: {paper.Content}");

        component.SendTimeoutRemaining += component.SendTimeout;

        AudioSystem.PlayPvs(component.SendSound, uid);

        UpdateUserInterface(uid, component);
    }

    /// <summary>
    ///     Accepts a new message and adds it to the queue to print
    ///     If has parameter "notifyAdmins" also output a special message to admin chat.
    /// </summary>
    public void Receive(EntityUid uid, FaxPrintout printout, string? fromAddress = null, FaxMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var faxName = printout.SenderFaxName ?? Loc.GetString("fax-machine-popup-source-unknown");

        PopupSystem.PopupEntity(Loc.GetString("fax-machine-popup-received", ("from", faxName)), uid);
        _appearanceSystem.SetData(uid, FaxMachineVisuals.VisualState, FaxMachineVisualState.Printing);

        if (component.NotifyAdmins)
            NotifyAdmins(faxName);

        component.PrintingQueue.Enqueue(printout);
    }

    private void SpawnPaperFromQueue(EntityUid uid, FaxMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.PrintingQueue.Count == 0)
            return;

        var printout = component.PrintingQueue.Dequeue();

        var entityToSpawn = ProtoMan.HasIndex(printout.PrototypeId) ? printout.PrototypeId : component.PrintPaperId;
        var printed = Spawn(entityToSpawn, Transform(uid).Coordinates);

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

        AdminLogger.Add(LogType.Action, LogImpact.Low, $"\"{component.FaxName}\" {ToPrettyString(uid):tool} printed {ToPrettyString(printed):subject}: {printout.Content}");
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
    public const int MaxLabelSize = 50; // parity with Content.Server.Labels.Components.HandLabelerComponent.MaxLabelChars
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
