﻿using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections;

namespace Celeste.Mod.CommunalHelper.Entities {
    [CustomEntity("CommunalHelper/ResetStateCrystal")]
    public class ResetStateCrystal : Refill {
        DynData<Refill> baseData;

        public ResetStateCrystal(EntityData data, Vector2 offset) 
            : base(data, offset) {
            baseData = new DynData<Refill>(this);

            Remove(baseData.Get<Sprite>("sprite"));
            Sprite sprite = new Sprite(GFX.Game, "objects/CommunalHelper/resetStateCrystal");
            sprite.AddLoop("idle", "ghostIdle", 0.1f);
            sprite.Play("idle");
            sprite.CenterOrigin();
            sprite.Color = Calc.HexToColor("676767");
            Add(sprite);
            baseData["sprite"] = sprite;
            Remove(Get<PlayerCollider>());

            Add(new PlayerCollider(OnCollide));
        }

        public void OnCollide(Player player) {
            Audio.Play("event:/game/general/diamond_touch", Position);
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            Collidable = false;
            Add(new Coroutine(RefillRoutine(player)));
            baseData.Set("respawnTimer", 2.5f);
        }

        public IEnumerator RefillRoutine(Player player) {
            Celeste.Freeze(0.025f);
            baseData.Get<Sprite>("sprite").Visible = baseData.Get<Sprite>("flash").Visible = false;
            if (!baseData.Get<bool>("oneUse")) {
                baseData.Get<Image>("outline").Visible = true;
            }
            yield return 0.05;
            player.StateMachine.State = 0;
            float num = player.Speed.Angle();
            baseData.Get<Level>("level").ParticlesFG.Emit(P_Shatter, 5, Position, Vector2.One * 4f, num - (float) Math.PI / 2f);
            baseData.Get<Level>("level").ParticlesFG.Emit(P_Shatter, 5, Position, Vector2.One * 4f, num + (float) Math.PI / 2f);
            SlashFx.Burst(Position, num);
            if (baseData.Get<bool>("oneUse")) {
                RemoveSelf();
            }
        }
    }
}
