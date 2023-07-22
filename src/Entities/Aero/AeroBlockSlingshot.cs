﻿using Celeste.Mod.CommunalHelper.Components;
using System.Collections;
using System.Linq;

namespace Celeste.Mod.CommunalHelper.Entities;

[CustomEntity("CommunalHelper/AeroBlockSlingshot")]
public class AeroBlockSlingshot : AeroBlock
{
    private const float DefaultLaunchTime = 0.5f;
    private const float DefaultCooldownTime = 0.5f;
    private const float DefaultSetTime = 0.2f;
    private const float DefaultDelayTime = 0.2f;
    private const float DefaultPushSpeed = 35f;
    private const Pushable.MoveActionType DefaultPushActions = Pushable.MoveActionType.Push;

    private float releaseTimer = -1;
    private readonly Vector2 startPosition;
    private readonly Pushable pushable;
    private readonly Vector2[] positions;
    private readonly Vector2[] sortedPositions;
    private readonly Vector2 leftPosition;
    private readonly Vector2 rightPosition;
    private float percent;
    private PathRenderer pathRenderer;

    private readonly SoundSource sfx;

    public readonly float LaunchTime;
    public readonly float CooldownTime;
    public readonly float SetTime;
    public readonly float DelayTime;
    public readonly float PushSpeed;

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
            data.Float("pushSpeed", DefaultPushSpeed),
            data.Enum("pushActions", DefaultPushActions))
    {
    }

    public AeroBlockSlingshot(Vector2[] positions, int width, int height,
        float launchTime = DefaultLaunchTime,
        float cooldownTime = DefaultCooldownTime,
        float setTime = DefaultSetTime,
        float delayTime = DefaultDelayTime,
        float pushSpeed = DefaultPushSpeed,
        Pushable.MoveActionType pushActions = DefaultPushActions)
        : base(positions[0], width, height)
    {
        LaunchTime = launchTime;
        CooldownTime = cooldownTime;
        SetTime = setTime;
        DelayTime = delayTime;
        PushSpeed = pushSpeed;

        this.positions = positions;
        startPosition = Position;
        sortedPositions = positions.OrderBy(p => p.X).ToArray();
        leftPosition = sortedPositions.First();
        rightPosition = sortedPositions.Last();

        Add(pushable = new Pushable
        {
            OnPush = OnPush,
            PushCheck = PushCheck,
            MaxPushSpeed = PushSpeed,
            Active = false,
            MoveActions = pushActions,
        });

        Add(new Coroutine(Sequence()));

        Add(sfx = new SoundSource()
        {
            Position = new Vector2(width, height) / 2.0f,
        });
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        scene.Add(pathRenderer = new PathRenderer(this));
    }

    public override void Removed(Scene scene)
    {
        pathRenderer?.RemoveSelf();
        pathRenderer = null;
        base.Removed(scene);
    }

    private bool PushCheck(int moveX, Pushable.MoveActionType moveAction)
    {
        if (moveX > 0 && Position.X < rightPosition.X)
            return true;
        if (moveX < 0 && Position.X > leftPosition.X)
            return true;
        return false;
    }

    private void OnPush(int moveX, Pushable.MoveActionType moveAction)
    {
        State = SlingshotStates.WindingUp;
        releaseTimer = SetTime;
    }

    private bool HasMoved() => Math.Abs(Position.X - startPosition.X) > 0.01f;

    private IEnumerator Sequence()
    {
        Color startColor = Calc.HexToColor("4BC0C8");
        Color endColor = Calc.HexToColor("FEAC5E");

        while (true)
        {
            AeroScreen_Percentage progressScreen = new((int) Width, (int) Height)
            {
                Color = Color.Tomato
            };

            State = SlingshotStates.Idle;
            pushable.Active = true;

            while (releaseTimer > 0 || !HasMoved())
            {
                if (HasMoved())
                {
                    if (!sfx.Playing)
                        sfx.Play(CustomSFX.game_aero_block_push);
                    AddScreenLayer(progressScreen);
                }
                else
                {
                    if (sfx.Playing)
                        sfx.Stop();
                    RemoveScreenLayer(progressScreen);
                }

                releaseTimer -= Engine.DeltaTime;
                var trackLength = Position.X > startPosition.X ? rightPosition.X - startPosition.X : startPosition.X - leftPosition.X;
                percent = trackLength == 0 ? 0 : (Position.X - startPosition.X) / trackLength;
                
                progressScreen.Percentage = Math.Abs(percent);
                progressScreen.Color = Color.Lerp(startColor, endColor, Math.Abs(percent));
                
                yield return null;
            }

            State = SlingshotStates.Ready;
            pushable.Active = false;
            sfx.Param("lock", 1.0f);
            Audio.Play(CustomSFX.game_aero_block_lock, Center);
            StartShaking(0.2f);
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);
            progressScreen.ShowNumbers = false;
            
            Color currentColor = progressScreen.Color = Color.Lerp(startColor, endColor, percent);
            float currentPercent = Math.Abs(percent);

            // wait, but do progress screen animation stuff at the same time.
            yield return Util.Interpolate(DelayTime, t =>
            {
                progressScreen.Color = Color.Lerp(Color.White, currentColor, t);
                progressScreen.Percentage = (1 - t) * currentPercent;
            });

            Audio.Play(CustomSFX.game_aero_block_ding, Center);
            sfx.Play(CustomSFX.game_aero_block_wind_up);

            RemoveScreenLayer(progressScreen);

            AeroScreen_Blinker blinker;
            AddScreenLayer(blinker = new AeroScreen_Blinker(null)
            {
                BackgroundColor = currentColor,
                FadeIn = 0.0f,
                Hold = 0.1f,
                FadeOut = 0.5f,
            });

            blinker.Update();
            blinker.Complete = true;

            Vector2 windVel = Vector2.UnitX * Math.Sign(startPosition.X - Position.X) * 200;
            AeroScreen_Wind windScreen;
            AddScreenLayer(windScreen = new((int) Width, (int) Height, windVel)
            {
                Color = currentColor,
                Wind = windVel,
            });

            State = SlingshotStates.Launching;
            var releasePosition = Position;
            yield return Util.Interpolate(LaunchTime, at =>
            {
                percent = Ease.SineIn(at);
                if (Scene.OnInterval(0.1f))
                    pathRenderer.CreateSparks();
                var target = Vector2.Lerp(releasePosition, startPosition, percent);
                MoveTo(target);
                sfx.Param("wind_percent", percent);
            });

            percent = 0;

            Level level = Scene as Level;
            level.Shake();

            Audio.Play(CustomSFX.game_aero_block_impact, Center);
            sfx.Play(CustomSFX.game_aero_block_push);
            sfx.Pause();
            StartShaking(0.3f);
            Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
            State = SlingshotStates.Cooldown;
            windScreen.Wind = Vector2.Zero;  

            // wait, but do wind particle stuff at the same time.
            yield return Util.Interpolate(CooldownTime, t =>
            {
                windScreen.Color = Color.Lerp(currentColor, Color.Transparent, t);
            });

            RemoveScreenLayer(windScreen);
        }
    }

    private class PathRenderer : Entity
    {
        private static readonly Color ropeColor = Calc.HexToColor("663931");
        private static readonly Color ropeLightColor = Calc.HexToColor("9b6157");

        private readonly AeroBlockSlingshot slingshot;
        private readonly MTexture cog;
        private readonly Vector2 sparkAdd = Vector2.UnitY * 5f;

        public PathRenderer(AeroBlockSlingshot slingshot)
        {
            Depth = 5000;
            this.slingshot = slingshot;
            cog = GFX.Game["objects/zipmover/cog"];
        }

        public void CreateSparks()
        {
            for (int i = 0; i < slingshot.positions.Length; i++)
            {
                var position = slingshot.positions[i] + new Vector2(slingshot.Width / 2, slingshot.Height / 2).Round();
                SceneAs<Level>().ParticlesBG.Emit(ZipMover.P_Sparks, position + sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), -Vector2.UnitY.Angle());
                SceneAs<Level>().ParticlesBG.Emit(ZipMover.P_Sparks, position - sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), Vector2.UnitY.Angle());
            }
        }

        public override void Render()
        {
            DrawCogs(Vector2.UnitY, Color.Black);
            DrawCogs(Vector2.Zero);
            Draw.Rect(new Rectangle((int) (slingshot.X + (double) slingshot.Shake.X - 1.0), (int) (slingshot.Y + (double) slingshot.Shake.Y - 1.0), (int) slingshot.Width + 2, (int) slingshot.Height + 2), Color.Black);
        }

        private void DrawCogs(Vector2 offset, Color? colorOverride = null)
        {
            var percent = slingshot.percent;
            offset += new Vector2(slingshot.Width / 2, slingshot.Height / 2).Round();

            for (int i = 0; i < slingshot.sortedPositions.Length - 1; i++)
            {
                var from = slingshot.sortedPositions[i];
                var to = slingshot.sortedPositions[i + 1];
                Vector2 normal = (to - from).SafeNormalize();
                Vector2 normalPerp = normal.Perpendicular() * 3f;
                Vector2 normalNegPerp = -normal.Perpendicular() * 4f;
                float rotation = (float) (percent * Math.PI * 2.0);

                Draw.Line(from + normalPerp + offset, to + normalPerp + offset, colorOverride ?? ropeColor);
                Draw.Line(from + normalNegPerp + offset, to + normalNegPerp + offset, colorOverride ?? ropeColor);

                for (float num = (float) (4.0 - percent * Math.PI * 8.0 % 4.0); num < (double) (to - from).Length(); num += 4f)
                {
                    Vector2 lineOnePosition = from + normalPerp + normal.Perpendicular() + normal * num;
                    Vector2 lineTwoPosition = to + normalNegPerp - normal * num;
                    Draw.Line(lineOnePosition + offset, lineOnePosition + normal * 2f + offset, colorOverride ?? ropeLightColor);
                    Draw.Line(lineTwoPosition + offset, lineTwoPosition - normal * 2f + offset, colorOverride ?? ropeLightColor);
                }

                cog.DrawCentered(from + offset, colorOverride ?? Color.White, 1f, rotation);
                cog.DrawCentered(to + offset, colorOverride ?? Color.White, 1f, rotation);
            }
        }
    }
}