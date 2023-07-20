using System.Collections;

namespace Celeste.Mod.CommunalHelper.Components;

public class Zipping : Component
{
    public new Solid Entity => EntityAs<Solid>();

    // actions
    public Func<Solid, bool> PlayerTriggerCheck;
    public Action<Solid> TriggerSfx;

    // config
    public Vector2 Target;
    public bool ManageSoundSource = true;

    private Coroutine coroutine;
    private float percent;
    private SoundSource sfx;

    public Zipping(bool active, bool visible) : base(active, visible)
    {
        PlayerTriggerCheck = OnPlayerTriggerCheck;
        TriggerSfx = OnTriggerSfx;
    }

    private bool OnPlayerTriggerCheck(Solid s) => s.HasPlayerRider();

    private void OnTriggerSfx(Solid solid)
    {
        if (solid is null || !ManageSoundSource || sfx is null)
            return;
        sfx.Play(SFX.game_01_zipmover);
    }

    public override void Added(Entity entity)
    {
        // need to call base first to ensure we balance Add/Remove
        base.Added(entity);

        if (entity is not Solid)
        {
            Util.Log(LogLevel.Warn, $"Attempted to add {nameof(Zipping)} to a non-Solid ({entity.GetType().Name})");
            RemoveSelf();
        }

        Reset();
    }

    public void Reset()
    {
        coroutine?.RemoveSelf();
        sfx?.RemoveSelf();
        coroutine = null;
        sfx = null;

        Entity?.Add(coroutine = new Coroutine(ZippingSequence()));
        if (ManageSoundSource)
            Entity?.Add(sfx = new SoundSource());
    }

    public override void Removed(Entity entity)
    {
        coroutine?.RemoveSelf();
        sfx?.RemoveSelf();
        coroutine = null;
        sfx = null;

        base.Removed(entity);
    }

    private IEnumerator ZippingSequence()
    {
        var start = Entity.Position;

        while (true)
        {
            while (PlayerTriggerCheck?.Invoke(Entity) != true)
                yield return null;

            TriggerSfx?.Invoke(Entity);
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);
            Entity.StartShaking(0.1f);
            yield return 0.1f;
            // zipMover.streetlight.SetAnimationFrame(3);
            Entity.StopPlayerRunIntoAnimation = false;

            float at = 0.0f;
            while (at < 1.0f)
            {
                yield return null;
                at = Calc.Approach(at, 1f, 2f * Engine.DeltaTime);
                percent = Ease.SineIn(at);
                Vector2 vector2 = Vector2.Lerp(start, Target, percent);
                // zipMover.ScrapeParticlesCheck(vector2);
                // if (Scene.OnInterval(0.1f))
                //     zipMover.pathRenderer.CreateSparks();
                Entity.MoveTo(vector2);
            }

            Entity.StartShaking(0.2f);
            Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
            SceneAs<Level>().Shake();
            Entity.StopPlayerRunIntoAnimation = true;
            yield return 0.5f;
            Entity.StopPlayerRunIntoAnimation = false;
            // zipMover.streetlight.SetAnimationFrame(2);
            at = 0.0f;
            while (at < 1.0f)
            {
                yield return null;
                at = Calc.Approach(at, 1f, 0.5f * Engine.DeltaTime);
                percent = 1f - Ease.SineIn(at);
                Vector2 position = Vector2.Lerp(Target, start, Ease.SineIn(at));
                Entity.MoveTo(position);
            }
            Entity.StopPlayerRunIntoAnimation = true;
            Entity.StartShaking(0.2f);
            // zipMover.streetlight.SetAnimationFrame(1);
            yield return 0.5f;
        }
    }
}
