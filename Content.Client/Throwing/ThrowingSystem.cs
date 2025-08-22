using System.Numerics;
using Content.Shared.Camera;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Client.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Client.Throwing;

/// <summary>
/// This handles...
/// </summary>
public sealed class ThrowingSystem : SharedThrowingSystem
{
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(OnBeforeSolve);
        SubscribeLocalEvent<PhysicsUpdateAfterSolveEvent>(OnAfterSolve);
        SubscribeLocalEvent<PredictedThrownItemComponent, UpdateIsPredictedEvent>(OnUpdateIsPredicted);
    }

    public override void FrameUpdate(float deltaSeconds)
    {
        base.FrameUpdate(deltaSeconds);

        var thrown = EntityQueryEnumerator<PredictedThrownItemComponent, TransformComponent>();
        while (thrown.MoveNext(out _, out var xform))
        {
            xform.ActivelyLerping = false;
        }
    }

    private void OnUpdateIsPredicted(Entity<PredictedThrownItemComponent> entity, ref UpdateIsPredictedEvent args)
    {
        args.IsPredicted = true;
    }

    private void OnBeforeSolve(ref PhysicsUpdateBeforeSolveEvent ev)
    {
        var query = EntityQueryEnumerator<PredictedThrownItemComponent>();
        while (query.MoveNext(out var uid, out var predicted))
        {
            predicted.Coordinates = Transform(uid).Coordinates;
        }
    }

    private void OnAfterSolve(ref PhysicsUpdateAfterSolveEvent ev)
    {
        var query = EntityQueryEnumerator<PredictedThrownItemComponent>();
        while (query.MoveNext(out var uid, out var predicted))
        {
            if (Timing.IsFirstTimePredicted)
                continue;

            if (predicted.Coordinates is { } coordinates)
                Transform.SetCoordinates(uid, coordinates);

            predicted.Coordinates = null;
        }
    }

    public override bool TryThrow(EntityUid uid,
        Vector2 direction,
        PhysicsComponent physics,
        TransformComponent transform,
        EntityQuery<ProjectileComponent> projectileQuery,
        float baseThrowSpeed = 10.0f,
        EntityUid? user = null,
        float pushbackRatio = PushbackDefault,
        float? friction = null,
        bool compensateFriction = false,
        bool recoil = true,
        bool animated = true,
        bool playSound = true,
        bool doSpin = true,
        bool unanchor = false)
    {
        if (base.TryThrow(uid,
                direction,
                physics,
                transform,
                projectileQuery,
                baseThrowSpeed,
                user,
                pushbackRatio,
                friction,
                compensateFriction,
                recoil,
                animated,
                playSound,
                doSpin,
                unanchor) && recoil && user != null)
        {
            EnsureComp<PredictedThrownItemComponent>(uid);
            Physics.UpdateIsPredicted(uid);
            _recoil.KickCamera(user.Value, -direction * 0.04f);
            return true;
        }

        return false;
    }
}
