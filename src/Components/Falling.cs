using System.Collections;

namespace Celeste.Mod.CommunalHelper.Components;

public class Falling : Component
{
    public new Solid Entity => EntityAs<Solid>();

    // actions
    public Func<Solid, bool> PlayerFallCheck;
    public Func<Solid, bool> PlayerWaitCheck;
    public Action<Solid> ShakeSfx;
    public Action<Solid> ImpactSfx;
    public Action<Solid> LandParticles;
    public Action<Solid> FallParticles;

    // config
    public float FallDelay;
    public bool ShouldRumble = true;
    public bool ClimbFall = true;
    public float FallSpeed = 160f;
    public bool ShouldManageSafe = true;
    public bool FallUp = false;
    public float ShakeTime = 0.4f;
    public bool EndOnSolidTiles = true;
    public bool FallImmediately = false;

    // coroutine properties
    public bool Triggered;
    public bool HasStartedFalling { get; private set; }

    private Coroutine coroutine;

    public Falling() : base(false, false)
    {
        PlayerFallCheck = OnPlayerFallCheck;
        PlayerWaitCheck = OnPlayerWaitCheck;
        ShakeSfx = OnShakeSfx;
        ImpactSfx = OnImpactSfx;
        LandParticles = OnLandParticles;
        FallParticles = OnFallParticles;
    }

    public override void Added(Entity entity)
    {
        // need to call base first to ensure we balance Add/Remove
        base.Added(entity);

        if (entity is not Solid)
        {
            Util.Log(LogLevel.Warn, $"Attempted to add {nameof(Falling)} to a non-Solid ({entity.GetType().Name})");
            RemoveSelf();
            return;
        }

        Reset();
    }

    public void Reset()
    {
        coroutine?.RemoveSelf();
        Entity?.Add(coroutine = new Coroutine(FallingSequence()));
        if (ShouldManageSafe && Entity is not null)
            Entity.Safe = false;
    }

    public override void Removed(Entity entity)
    {
        coroutine?.RemoveSelf();
        base.Removed(entity);
    }

    private bool OnPlayerFallCheck(Solid solid) =>
        solid is not null && (ClimbFall ? solid.HasPlayerRider() : solid.HasPlayerOnTop());

    private bool OnPlayerWaitCheck(Solid solid)
    {
        if (solid is null)
            return false;
        if (Triggered || PlayerFallCheck?.Invoke(solid) == true)
            return true;
        if (!ClimbFall)
            return false;
        return solid.CollideCheck<Player>(solid.Position - Vector2.UnitX) || solid.CollideCheck<Player>(solid.Position + Vector2.UnitX);
    }

    private void OnShakeSfx(Solid solid)
    {
        if (solid is not null)
            Audio.Play(SFX.game_gen_fallblock_shake, solid.Center);
    }

    private void OnImpactSfx(Solid solid)
    {
        if (solid is not null)
            Audio.Play(SFX.game_gen_fallblock_impact, solid.BottomCenter);
    }

    private void OnFallParticles(Solid solid)
    {
        if (solid is null)
            return;

        var level = SceneAs<Level>();
        for (int x = 2; x < solid.Width; x += 4)
        {
            var position = new Vector2(solid.X + x, solid.Y);
            var range = Vector2.One * 4f;
            var direction = (float) Math.PI / 2f;
            var offset = new Vector2(x, -2f);
            var check = FallUp ? solid.BottomLeft - offset : solid.TopLeft + offset;
            if (level.CollideCheck<Solid>(check))
                level.Particles.Emit(FallingBlock.P_FallDustA, 2, position, range, FallUp ? -direction : direction);
            level.Particles.Emit(FallingBlock.P_FallDustB, 2, position, range);
        }
    }

    private void OnLandParticles(Solid solid)
    {
        if (solid is null)
            return;

        var level = SceneAs<Level>();
        for (int x = 2; x <= solid.Width; x += 4)
        {
            var offset = new Vector2(x, 3f);
            var checkPosition = FallUp ? solid.TopLeft - offset : solid.BottomLeft + offset;
            if (level.CollideCheck<Solid>(checkPosition))
            {
                var position = new Vector2(solid.X + x, FallUp ? solid.Top : solid.Bottom);
                var range = Vector2.One * 4f;
                var fallDustDirection = -(float) Math.PI / 2f;
                level.ParticlesFG.Emit(FallingBlock.P_FallDustA, 1, position, range, FallUp ? -fallDustDirection : fallDustDirection);
                var landDustDirection = x >= solid.Width / 2f ? 0f : (float) Math.PI;
                level.ParticlesFG.Emit(FallingBlock.P_LandDust, 1, position, range, FallUp ? -landDustDirection : landDustDirection);
            }
        }
    }

    private IEnumerator FallingSequence()
    {
        // cache things
        var self = this;
        var entity = self.Entity;
        var level = entity?.SceneAs<Level>();

        // unlikely but safety
        if (entity is null)
            yield break;

        // reset things
        if (self.ShouldManageSafe) entity.Safe = false;
        self.Triggered = self.FallImmediately;
        self.HasStartedFalling = false;

        // wait until we should fall
        while (!self.Triggered && self.PlayerFallCheck?.Invoke(Entity) != true)
            yield return null;

        if (FallImmediately)
        {
            // wait until we can fall
            while (entity.CollideCheck<Platform>(entity.Position + (FallUp ? -Vector2.UnitY : Vector2.UnitY)))
                yield return 0.1f;
        }
        else
        {
            // wait for the delay
            float fallDelayRemaining = self.FallDelay;
            while (fallDelayRemaining > 0)
            {
                fallDelayRemaining -= Engine.DeltaTime;
                yield return null;
            }
        }

        self.HasStartedFalling = true;

        // loop forever
        while (true)
        {
            if (ShakeTime > 0)
            {
                // start shaking
                self.ShakeSfx?.Invoke(Entity);
                entity.StartShaking();
                if (self.ShouldRumble) Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);

                // shake for a while
                for (float timer = ShakeTime; timer > 0 && self.PlayerWaitCheck?.Invoke(Entity) != false; timer -= Engine.DeltaTime)
                    yield return null;

                // stop shaking
                entity.StopShaking();
            }

            // particles
            self.FallParticles?.Invoke(Entity);

            // fall
            float speed = 0f;
            float maxSpeed = self.FallSpeed;
            while (true)
            {
                // update the speed
                speed = Calc.Approach(speed, maxSpeed, 500f * Engine.DeltaTime);
                // try to move
                if (!entity.MoveVCollideSolids(speed * Engine.DeltaTime * (FallUp ? -1 : 1), true))
                {
                    // if we've fallen out the bottom of the screen, we should remove the entity
                    // otherwise yield for a frame and loop
                    if (!FallUp && entity.Top <= level.Bounds.Bottom + 16 && (entity.Top <= level.Bounds.Bottom - 1 || !entity.CollideCheck<Solid>(entity.Position + Vector2.UnitY)) ||
                        FallUp && entity.Bottom >= level.Bounds.Top - 16 && (entity.Bottom >= level.Bounds.Top + 1 || !entity.CollideCheck<Solid>(entity.Position - Vector2.UnitY)))
                        yield return null;
                    else
                    {
                        // we've fallen out of the screen and should remove the entity
                        entity.Collidable = entity.Visible = false;
                        yield return 0.2f;
                        if (level.Session.MapData.CanTransitionTo(level, new Vector2(entity.Center.X, FallUp ? (entity.Top - 12f) : (entity.Bottom + 12f))))
                        {
                            yield return 0.2f;
                            level.Shake();
                            if (ShouldRumble) Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
                        }

                        entity.RemoveSelf();
                        entity.DestroyStaticMovers();
                        yield break;
                    }
                }
                else
                {
                    // if we hit something, break
                    break;
                }
            }

            // impact effects
            self.ImpactSfx?.Invoke(Entity);
            if (self.ShouldRumble) Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
            level.DirectionalShake(FallUp ? -Vector2.UnitY : Vector2.UnitY);
            entity.StartShaking();
            self.LandParticles?.Invoke(Entity);
            yield return 0.2f;
            entity.StopShaking();

            // if it's hit the fg tiles then make it safe and end
            if (EndOnSolidTiles && entity.CollideCheck<SolidTiles>(entity.Position + (FallUp ? -Vector2.UnitY : Vector2.UnitY)))
            {
                entity.Safe |= self.ShouldManageSafe;
                yield break;
            }

            // wait until we can fall again
            while (entity.CollideCheck<Platform>(entity.Position + (FallUp ? -Vector2.UnitY : Vector2.UnitY)))
                yield return 0.1f;
        }
    }
}
