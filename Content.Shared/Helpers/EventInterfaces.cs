namespace Content.Shared.Helpers;

// These events are effectively the same, with the only difference being the names to designate their purpose.

/// <remarks>
/// I am not implementing <see cref="IHandleableEvent.Unhandle"/> or <see cref="ICancellableEvent.Uncancel"/>
/// since those methods can easily cause bad behaviors. You'll need to implement them explicitly in your event if you need them.
/// </remarks>
public static partial class EventInerfaceHelpers
{
    public static void Handle(this IHandleableEvent ev)
    {
        ev.Handle();
    }

    public static void Cancel(this ICancellableEvent ev)
    {
        ev.Cancel();
    }
}

/// <summary>
/// Used to designate an event which is able to be handled by a system.
/// Typically, once this event has been handled, it stops being listened to.
/// </summary>
public interface IHandleableEvent
{
    /// <summary>
    ///     If this message has already been "handled" by a previous system.
    /// </summary>
    public bool Handled { get; protected set; }

    /// <summary>
    ///     Cancels the event.
    /// </summary>
    void Handle() => Handled = true;

    /// <summary>
    ///     Uncancels the event. Don't call this unless you know what you're doing.
    /// </summary>
    void Unhandle() => Handled = false;
}

/// <summary>
/// Used to designate an event that can be canceled by a system.
/// Typically, once this event has been cancelled, it stops being listened to.
/// </summary>
public interface ICancellableEvent
{
    /// <summary>
    ///     Whether this even has been canceled.
    /// </summary>
    public bool Cancelled { get; protected set; }

    /// <summary>
    ///     Cancels the event.
    /// </summary>
    void Cancel() => Cancelled = true;

    /// <summary>
    ///     Uncancels the event. Don't call this unless you know what you're doing.
    /// </summary>
    void Uncancel() => Cancelled = true;
}
