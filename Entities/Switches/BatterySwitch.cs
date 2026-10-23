// Copyright (c) 2022 Edith Boles and Communal Helper Team. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Maps;
using Celeste.Core.Mathematics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Stories.Temple;
using Celeste.Graphics.Animation;
using Celeste.Mods.Batteries.Entities.Gates;
using Celeste.Mods.Batteries.Entities.Holdables;
using Celeste.Mods.Batteries.Modules;
using Celeste.Mods.Batteries.Runtime.Particles;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Collision;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.Batteries.Entities.Switches
{
    [CustomEntity("batteries/battery_switch")]
    [RuntimeTypeAlias("Celeste.Mod.Batteries.BatterySwitch")]
    public class BatterySwitch : SourceSolidEntityState
    {
        private static readonly string sourcePlayerRegistryKey =
            typeof(global::Celeste.Simulation.RoomPlayerSession).FullName;

        private readonly ParticleTypePresentation signalParticle;
        private readonly ISourceEngineContext engine;
        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;

        private readonly Sides side;
        private readonly bool persistent;
        private readonly bool allGates;
        private readonly Sprite sprite;
        private readonly bool alwaysFlag;
        private bool pressed;
        private Vector2 pressDirection;
        private MapEntityId id;

        public BatterySwitch(Vector2 position, Sides side, bool persistent, bool allGates, bool alwaysFlag,
            MapEntityId id,
            ISourceSolidWorld world)
            : base(position, 0f, 0f, true, world)
        {
            engine = world.EngineContext;
            signalParticle = BatterySwitchParticles.For(engine).Signal;

            this.side = side;
            this.persistent = persistent;
            this.allGates = allGates;
            this.id = id;
            this.alwaysFlag = alwaysFlag;
            Add(sprite = BatteriesModule.For(engine).Sprites.Create("battery_switch"));
            if (side is Sides.Up or Sides.Down)
            {
                Collider.Width = 16f;
                Collider.Height = 8f;
            }
            else
            {
                Collider.Width = 8f;
                Collider.Height = 16f;
            }

            switch (side)
            {
                case Sides.Up:
                    sprite.Position = new Vector2(-2f, -2f);
                    sprite.Rotation = (float)Math.PI / 2f;
                    pressDirection = Vector2.UnitY;
                    break;
                case Sides.Down:
                    sprite.Position = new Vector2(18f, 10f);
                    sprite.Rotation = -(float)Math.PI / 2f;
                    pressDirection = -Vector2.UnitY;
                    break;
                case Sides.Right:
                    sprite.Position = new Vector2(10f, 18f);
                    sprite.FlipX = true;
                    pressDirection = -Vector2.UnitX;
                    break;
                case Sides.Left:
                    sprite.Position = new Vector2(-2f, 18f);
                    sprite.FlipX = false;
                    pressDirection = Vector2.UnitX;
                    break;
            }
        }

        public BatterySwitch(EntityData data, Vector2 offset, MapEntityId id, ISourceEngineContext engine,
            ISourceSolidWorldFactory factory)
            : this(data.Position + offset, direction(data), data.Bool("persistent"), data.Bool("allGates"),
                data.Bool("alwaysFlag"), id, factory.Create(data.Position + offset, true))
        {
        }

        public static BatterySwitch Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) =>
            new(data, offset, new MapEntityId(room.Name, data.ID), level.EngineContext,
                ((ISourcePlatformCollisionScene)level).SolidWorldFactory);

        public enum Sides
        {
            Up,
            Down,
            Left,
            Right
        }

        private string FlagName => GetFlagName(id);

        public static string GetFlagName(MapEntityId id)
        {
            return "batterySwitch_" + id.Key;
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            if (persistent && Level.Session.GetFlag(FlagName))
            {
                sprite.Play("buzz");
                pressed = true;
                Collidable = true;
                LitState();
                foreach (BatteryGate entity in GetGate())
                {
                    if (entity.closes)
                    {
                        entity.StartClosed();
                    }
                    else
                    {
                        entity.StartOpen();
                    }
                }
            }
        }

        public override void Update()
        {
            base.Update();
            OriginalLevelSceneRoot level = Level;
            if (Level.Scheduler.Clock.OnInterval(0.3f, engine))
            {
                if (pressed)
                {
                    level.ParticlesFG.Emit(((IOriginalGameplayProcess)engine).ParticleTypes.DashSwitch.P_PressA, 1,
                        Collider.AbsolutePosition + Collider.Center, Vector2Geometry.Perpendicular(pressDirection) * 6f,
                        Vector2Geometry.Angle(pressDirection * -1));
                }
                else
                {
                    RoomPlayerSession player =
                        level.Scheduler.Entities.GetTrackedEntity<RoomPlayerSession>(sourcePlayerRegistryKey);
                    if (player?.Holding?.Holding?.Entity is Battery battery)
                    {
                        if (battery.onlyFits == id.ID)
                        {
                            level.ParticlesFG.Emit(signalParticle, 8,
                                Collider.AbsolutePosition + Collider.Center + (pressDirection * -28),
                                Vector2Geometry.Perpendicular(pressDirection) * 6f,
                                Vector2Geometry.Angle(pressDirection));
                        }
                    }
                }
            }
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);
            if (!persistent && alwaysFlag)
            {
                (scene as OriginalLevelSceneRoot).Session.SetFlag(FlagName, false);
            }
        }

        public void Hit(Battery battery, Vector2 direction)
        {
            if (!pressed && direction == pressDirection && battery.Charge > 0 &&
                (battery.onlyFits < 0 || battery.onlyFits == id.ID))
            {
                battery.Use();
                battery.RemoveSelf();
                LitState();
                ((IOriginalGameInputProcessEnvironment)engine).InputSource.Rumble(RumbleStrength.Medium,
                    RumbleLength.Medium);
                ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play(
                    "event:/game/05_mirror_temple/button_activate",
                    Position);
                sprite.Play("insert");
                pressed = true;
                RoomPlayerSession player =
                    Level.Scheduler.Entities.GetTrackedEntity<RoomPlayerSession>(sourcePlayerRegistryKey);
                if (player != null && Collider.Collide(player.Collider))
                {
                    player.Position += -5 * pressDirection;
                }

                Level.ParticlesFG.Emit(((IOriginalGameplayProcess)engine).ParticleTypes.DashSwitch.P_PressA, 10,
                    Collider.AbsolutePosition + Collider.Center, Vector2Geometry.Perpendicular(direction) * 6f,
                    Vector2Geometry.Angle(pressDirection * -1));
                Level.ParticlesFG.Emit(((IOriginalGameplayProcess)engine).ParticleTypes.DashSwitch.P_PressB, 4,
                    Collider.AbsolutePosition + Collider.Center, Vector2Geometry.Perpendicular(direction) * 6f,
                    Vector2Geometry.Angle(pressDirection * -1));
                if (allGates)
                {
                    foreach (BatteryGate entity in Level.Scheduler.Entities.GetTrackedEntities(typeof(BatteryGate)))
                    {
                        if (entity.entityID.Level == id.Level)
                        {
                            entity.SwitchOpen();
                        }
                    }
                }
                else
                {
                    foreach (BatteryGate gate in GetGate())
                    {
                        gate.SwitchOpen();
                    }
                }

                Level.Scheduler.Entities.FindFirst<TempleMirrorPortalState>()
                    ?.OnSwitchHit(Math.Sign(X - (Scene as OriginalLevelSceneRoot).Bounds.Center.X));
                if (persistent || alwaysFlag)
                {
                    Level.Session.SetFlag(FlagName);
                }
            }
        }

        private static Sides direction(EntityData data)
        {
            return data.Bool("horizontal") ? data.Bool("rightSide") ? Sides.Right : Sides.Left :
                data.Bool("ceiling") ? Sides.Down : Sides.Up;
        }


        private void LitState()
        {
            switch (side)
            {
                case Sides.Up:
                    Collider.Width = 16f;
                    Collider.Height = 12f;
                    Collider.Position.Y -= 4;
                    break;
                case Sides.Down:
                    Collider.Width = 16f;
                    Collider.Height = 12f;
                    break;
                case Sides.Left:
                    Collider.Width = 12f;
                    Collider.Height = 16f;
                    Collider.Position.X -= 4;
                    break;
                case Sides.Right:
                    Collider.Width = 12f;
                    Collider.Height = 16f;
                    break;
            }

            Vector2 offset = side switch
            {
                Sides.Down => Collider.BottomCenter + new Vector2(0, 1),
                Sides.Up => Collider.TopCenter + new Vector2(0, -1),
                Sides.Right => Collider.CenterRight + new Vector2(1, 0),
                Sides.Left => Collider.CenterLeft + new Vector2(-1, 0),
                _ => Vector2.Zero
            };

            Add(new VertexLightState(engine, offset, PackedColor.Lime, 1f, 12, 24));
        }

        private List<BatteryGate> GetGate()
        {
            List<object> entities = Level.Scheduler.Entities.GetTrackedEntities(typeof(BatteryGate));
            List<BatteryGate> batteryGates = new();
            float num = 0f;

            foreach (BatteryGate item in entities)
            {
                if (!item.ClaimedByASwitch && item.bound && item.openWith == id.ID)
                {
                    batteryGates.Add(item);
                }
            }

            if (batteryGates.Count != 0)
            {
                return batteryGates;
            }

            BatteryGate batteryGate = null;
            foreach (BatteryGate item in entities)
            {
                if (!item.ClaimedByASwitch && item.entityID.Level == id.Level && !item.bound)
                {
                    float num2 = Vector2.DistanceSquared(Position, item.Position);
                    if (batteryGate == null || num2 < num)
                    {
                        batteryGate = item;
                        num = num2;
                    }
                }
            }

            if (batteryGate != null)
            {
                batteryGate.ClaimedByASwitch = true;
                batteryGates.Add(batteryGate);
            }

            return batteryGates;
        }
    }
}
