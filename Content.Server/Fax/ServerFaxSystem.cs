using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Tools;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.Fax;
using Content.Shared.Fax.Components;
using Content.Shared.Interaction;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Fax;

public sealed partial class ServerFaxSystem : FaxSystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private ToolSystem _toolSystem = default!;

    private static readonly ProtoId<ToolQualityPrototype> ScrewingQuality = "Screwing";

    private static readonly SoundSpecifier AdminAlert = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");

    [SubscribeLocalEvent]
    private void OnInteractUsing(EntityUid uid, FaxMachineComponent component, InteractUsingEvent args)
    {
        if (args.Handled ||
            !TryComp<ActorComponent>(args.User, out var actor) ||
            !_toolSystem.HasQuality(args.Used, ScrewingQuality)) // Screwing because Pulsing already used by device linking
            return;

        _quickDialog.OpenDialog(actor.PlayerSession,
            Loc.GetString("fax-machine-dialog-rename"),
            Loc.GetString("fax-machine-dialog-field-name"),
            (string newName) =>
            {
                if (component.FaxName == newName)
                    return;

                if (newName.Length > 20)
                {
                    PopupSystem.PopupEntity(Loc.GetString("fax-machine-popup-name-long"), uid);
                    return;
                }

                if (component.KnownFaxes.ContainsValue(newName) && !Emag.CheckFlag(uid, EmagType.Interaction)) // Allow existing names if emagged for fun
                {
                    PopupSystem.PopupEntity(Loc.GetString("fax-machine-popup-name-exist"), uid);
                    return;
                }

                AdminLogger.Add(LogType.Action,
                    LogImpact.Low,
                    $"{ToPrettyString(args.User):user} renamed {ToPrettyString(uid):tool} from \"{component.FaxName}\" to \"{newName}\"");
                component.FaxName = newName;
                PopupSystem.PopupEntity(Loc.GetString("fax-machine-popup-name-set"), uid);
                UpdateUserInterface(uid, component);
            });

        args.Handled = true;
    }

    protected override void NotifyAdmins(string faxName)
    {
        _chat.SendAdminAnnouncement(Loc.GetString("fax-machine-chat-notify", ("fax", faxName)));
        AudioSystem.PlayGlobal(AdminAlert, Filter.Empty().AddPlayers(_adminManager.ActiveAdmins), false, AudioParams.Default.AddVolume(-8f));
    }
}
