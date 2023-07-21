using Celeste.Mod.CommunalHelper.Components;
using System.Collections;

namespace Celeste.Mod.CommunalHelper.Entities;

public class AeroBlockSlingshot : AeroBlock
{
    private const float DefaultLaunchTime = 0.5f;
    private const float DefaultCooldownTime = 0.5f;
    private const float DefaultSetTime = 0.1f;
    private const float DefaultDelayTime = 0.2f;
    private const float DefaultPushSpeed = 20f;

    private float releaseTimer = -1;
    private readonly Vector2 startPosition;
    private readonly Vector2 endPosition;
    private readonly Pushable pushable;
    private float percent;

    public float LaunchTime;
    public float CooldownTime;
    public float SetTime;
    public float DelayTime;
    public float PushSpeed;

    public SlingshotStates State = SlingshotStates.Idle;

    public enum SlingshotStates
    {
        Idle,
        WindingUp,
        Ready,
        Launching,
        Cooldown,
    }

    public AeroBlockSlingshot(EntityData data, Vector2 offset)
        : this(data.NodesWithPosition(offset), data.Width, data.Height,
            data.Float("launchTime", DefaultLaunchTime),
            data.Float("cooldownTime", DefaultCooldownTime),
            data.Float("setTime", DefaultSetTime),
            data.Float("delayTime", DefaultDelayTime),
            data.Float("pushSpeed", DefaultPushSpeed))
    {
    }

    public AeroBlockSlingshot(Vector2[] positions, int width, int height,
        float launchTime = DefaultLaunchTime,
        float cooldownTime = DefaultCooldownTime,
        float setTime = DefaultSetTime,
        float delayTime = DefaultDelayTime,
        float pushSpeed = DefaultPushSpeed)
        : base(positions[0], width, height)
    {
        LaunchTime = launchTime;
        CooldownTime = cooldownTime;
        SetTime = setTime;
        DelayTime = delayTime;
        PushSpeed = pushSpeed;

        startPosition = Position;
        endPosition = positions[1];

        Add(pushable = new Pushable
        {
            OnPush = OnPush,
            MaxPushSpeed = PushSpeed,
            Active = false,
            MoveActions = Pushable.MoveActionType.Push,
        });

        Add(new Coroutine(Sequence()));
    }

    // public override void Render()
    // {
    //     base.Render();
    //     Draw.HollowRect(Position, Width, Height, Color.Green);
    // }

    private void OnPush(int moveX, Pushable.MoveActionType moveAction)
    {
        State = SlingshotStates.WindingUp;
        releaseTimer = SetTime;
    }

    private bool HasMoved() => Math.Abs(Position.X - startPosition.X) > 0.01f;

    private IEnumerator Sequence()
    {
        while (true)
        {
            State = SlingshotStates.Idle;
            pushable.Active = true;

            while (releaseTimer > 0 || !HasMoved())
            {
                releaseTimer -= Engine.DeltaTime;
                yield return null;
            }

            State = SlingshotStates.Ready;
            pushable.Active = false;

            // TODO: play sound, do animations, etc.
            yield return DelayTime;

            State = SlingshotStates.Launching;
            var releasePosition = Position;
            yield return Util.Interpolate(LaunchTime, at =>
            {
                percent = Ease.SineIn(at);
                var target = Vector2.Lerp(releasePosition, startPosition, percent);
                MoveTo(target);
            });

            State = SlingshotStates.Cooldown;
            yield return CooldownTime;
        }
    }
}
