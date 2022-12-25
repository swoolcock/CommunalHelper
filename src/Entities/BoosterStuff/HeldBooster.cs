﻿using Celeste.Mod.CommunalHelper.Imports;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections;

namespace Celeste.Mod.CommunalHelper.Entities.BoosterStuff {
    [CustomEntity("CommunalHelper/HeldBooster")]
    public class HeldBooster : CustomBooster {
        public class PathRenderer : PathRendererBase<HeldBooster> {
            private readonly Vector2 dir, perp;

            public PathRenderer(float alpha, Vector2 direction, HeldBooster booster)
                : base(alpha, PathStyle.Arrow, PathColors, booster) {
                dir = direction;
                perp = direction.Perpendicular();
                Depth = booster.Depth + 1;
            }

            public override void Render() {
                base.Render();

                if (Alpha <= 0f)
                    return;

                Color color = Booster.BoostingPlayer ? Color : Color.White;

                Util.TryGetPlayer(out Player player);

                float length = 128 * Percent;
                for (float f = 0f; f < length; f += 6f) {
                    float t = f / length;
                    float opacity = 1 - Ease.QuadOut(t);
                    DrawPathLine(Calc.Round(Booster.Start + dir * f), dir, perp, f, player, color, opacity);
                }
            }
        }

        public static readonly Color[] PathColors = new[] {
            Calc.HexToColor("0abd32"),
            Calc.HexToColor("0df235"),
            Calc.HexToColor("32623a"),
            Calc.HexToColor("6ef7ad"),
        };

        public static readonly Color PurpleBurstColor = Calc.HexToColor("7a1053");
        public static readonly Color PurpleAppearColor = Calc.HexToColor("e619c3");
        public static readonly Color GreenBurstColor = Calc.HexToColor("174f21");
        public static readonly Color GreenAppearColor = Calc.HexToColor("0df235");

        private readonly Sprite arrow;

        private readonly bool green;

        public Vector2 Start { get; }
        private readonly Vector2 dir;

        private Vector2 aim, prevAim;
        private float targetAngle;
        private float anim;
        private bool hasPlayer;

        private PathRenderer pathRenderer;

        public HeldBooster(EntityData data, Vector2 offset)
            : this(data.Position + offset, data.FirstNodeNullable(offset)) { }

        public HeldBooster(Vector2 position, Vector2? node = null)
            : base(position, redBoost: true) {
            green = node is not null && node.Value != position;

            ReplaceSprite(CommunalHelperModule.SpriteBank.Create(green ? "greenHeldBooster" : "purpleHeldBooster"));

            Add(arrow = CommunalHelperModule.SpriteBank.Create("heldBoosterArrow"));
            arrow.Visible = false;

            SetParticleColors(
                green ? GreenBurstColor : PurpleBurstColor,
                green ? GreenAppearColor : PurpleAppearColor
            );

            SetSoundEvent(
                SFX.game_05_redbooster_enter,
                CustomSFX.game_customBoosters_heldBooster_move,
                playMoveEnd: true
            );

            MovementInBubbleFactor = green ? 0 : 1.5f;

            Start = position;
            dir = ((node ?? Vector2.Zero) - Start).SafeNormalize();
        }

        public override void Added(Scene scene) {
            base.Added(scene);
            if (green) scene.Add(pathRenderer = new PathRenderer(1f, dir, this));
        }

        public override void Removed(Scene scene) {
            base.Removed(scene);
            if (green) scene.Remove(pathRenderer);
            pathRenderer = null;
        }

        protected override void OnPlayerEnter(Player player) {
            base.OnPlayerEnter(player);
            Collidable = false;
            hasPlayer = true;

            if (green)
                anim = 1f;
            else
                SetAim(Vector2.UnitX * (int) player.Facing, pulse: true);

            arrow.Play("inside");
        }

        protected override void OnPlayerExit(Player player) {
            base.OnPlayerExit(player);
            Collidable = true;
            hasPlayer = false;

            arrow.Play("pop");
        }

        // prevents held boosters from starting automatically (which red boosters do after 0.25 seconds).
        protected override IEnumerator BoostRoutine(Player player) {
            while (true)
                yield return null;
        }

        protected override int? RedDashUpdateAfter(Player player) {
            if (!Input.Dash.Check)
                return Player.StNormal;

            return null;
        }

        protected override IEnumerator RedDashCoroutineAfter(Player player) {
            DynamicData data = new(player);

            Vector2 direction = green ? dir : aim;

            player.DashDir = direction;
            data.Set("gliderBoostDir", direction);
            player.Speed = direction * 240f;

            // If the player is inverted, invert its vertical speed so that it moves in the same direction no matter what.
            if (GravityHelper.IsPlayerInverted?.Invoke() ?? false)
                player.Speed.Y *= -1f;

            player.SceneAs<Level>().DirectionalShake(player.DashDir, 0.2f);
            if (player.DashDir.X != 0f)
                player.Facing = (Facings) Math.Sign(player.DashDir.X);

            yield break;
        }

        private void SetAim(Vector2 v, bool pulse = false) {
            if (v == Vector2.Zero)
                return;

            v.Normalize();
            aim = v;
            targetAngle = v.Angle();

            if (pulse)
                anim = 1f;
        }

        public override void Update() {
            base.Update();

            if (!green && hasPlayer && !BoostingPlayer) {
                Vector2 v = Input.Aim.Value == Vector2.Zero ? Vector2.Zero : Input.GetAimVector();
                SetAim(v, v != prevAim);
                prevAim = v;
            }

            anim = Calc.Approach(anim, 0f, Engine.DeltaTime * 2f);
        }

        public override void Render() {
            Sprite sprite = Sprite;

            float ease = Ease.BounceIn(anim);

            Vector2 offset = aim * ease;

            bool inside = sprite.CurrentAnimationID is "inside";
            float verticalCorrection = inside && !green ? 3 : 2;
            Vector2 pos = Center + sprite.Position + offset - new Vector2(0, verticalCorrection);

            float angle = green
                ? dir.Angle()
                : targetAngle;

            Vector2 scale = new(1 + ease * 0.25f, 1 - ease * 0.25f);

            bool greenFlag = true;
            bool purpleFlag = sprite.CurrentAnimationID is not "loop";

            bool visibleArrow = (green && greenFlag) || (!green && purpleFlag);
            bool pop = sprite.CurrentAnimationID is "pop";

            Console.WriteLine($"{sprite.CurrentAnimationID}[{sprite.CurrentAnimationFrame}]");

            if (visibleArrow && !pop) {
                arrow.Texture.DrawCentered(pos + Vector2.UnitX, Color.Black, scale, angle);
                arrow.Texture.DrawCentered(pos - Vector2.UnitX, Color.Black, scale, angle);
                arrow.Texture.DrawCentered(pos + Vector2.UnitY, Color.Black, scale, angle);
                arrow.Texture.DrawCentered(pos - Vector2.UnitY, Color.Black, scale, angle);
            }

            base.Render();

            if (visibleArrow) {
                arrow.Texture.DrawCentered(pos, Color.White, scale, angle);
            }
        }
    }
}