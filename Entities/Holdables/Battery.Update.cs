// Copyright (c) 2022 Edith Boles and Communal Helper Team. Licensed under the MIT License.

using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Maps;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Audio;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Controls.Source;
using Celeste.Game.Gameplay.Effects.Slashes;
using Celeste.Game.Gameplay.Mechanisms.DashSwitches;
using Celeste.Game.Gameplay.Enemies.Seekers;
using Celeste.Game.Entities.Platforms.SwapBlocks;
using Celeste.Graphics.Animation;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Gameplay.Interaction.Springs;
using Celeste.Simulation.Gameplay.Walls.Concealment;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.Effects.Mirrors;
using Celeste.Simulation.Presentation.Player.Death;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Mods.Batteries.Modules;
using Celeste.Mods.Batteries.Runtime.Power;
using Celeste.Mods.Batteries.Components.Power;
using Celeste.Mods.Batteries.Entities.Gates;
using Celeste.Mods.Batteries.Entities.Switches;
using Celeste.Mods.Batteries.Entities.Platforms;
using Color = Celeste.Core.Display.PackedColor;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using Holdable = Celeste.Simulation.Gameplay.Holdables.HoldableState;
using HoldableCollider = Celeste.Simulation.Gameplay.Holdables.HoldableColliderState;
using Collision = Celeste.Physics.Collision.SourceCollisionHandler;
using CollisionData = Celeste.Physics.Collision.PixelCollision;
using EntityID = Celeste.Core.Maps.MapEntityId;
using VertexLight = Celeste.Simulation.Presentation.Lighting.VertexLightState;
using MirrorReflection = Celeste.Simulation.Presentation.Effects.Mirrors.MirrorReflectionState;
using Entity = Celeste.Scene.Entities.SceneEntityState;
using Player = Celeste.Simulation.RoomPlayerSession;
using Level = Celeste.Game.Execution.Levels.OriginalLevelSceneRoot;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Seeker = Celeste.Game.Gameplay.Enemies.Seekers.OriginalSeekerState;
using SeekerBarrier = Celeste.Simulation.Gameplay.Enemies.Seeker.SeekerBarrierState;
using DashSwitch = Celeste.Game.Gameplay.Mechanisms.DashSwitches.OriginalDashSwitchState;
using SwapBlock = Celeste.Game.Entities.Platforms.SwapBlocks.OriginalSwapBlockState;
using Solid = Celeste.Physics.Collision.ISourceEntityCollider;

namespace Celeste.Mods.Batteries.Entities.Holdables
{
    public partial class Battery
    {
        public override void Update()
        {
            base.Update();
            if (Charge > 0 && ((Level)Scene).OnInterval(fresh ? 0.5f : 0.1f))
            {
                bool collided = false;
                foreach (FakeWallState wall in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                             "Celeste.FakeWall"))
                {
                    if (wall.Collider.Collide(Collider))
                    {
                        collided = true;
                    }
                }

                if (!collided)
                {
                    Level.ParticlesFG.Emit(particle, Hold.IsHeld ? 2 : 1, Position + particleOffset, Vector2.One * 5f);
                }
            }

            if (dead)
            {
                return;
            }

            if (swatTimer > 0f)
            {
                swatTimer -= engine.DeltaTime;
            }

            hardVerticalHitSoundCooldown -= engine.DeltaTime;
            Depth = 100;
            if (((Level)Scene).OnInterval(0.5f) && Running && Charge > 0)
            {
                Charge -= DischargeRate / 2;
            }

            string newSprite;
            ParticleType newParticle = P_Low;
            if (DischargeRate == 0)
            {
                newSprite = "infinite";
                newParticle = P_Infinite;
            }
            else if (fresh && Charge == MaxCharge)
            {
                newSprite = "fresh";
                newParticle = P_Full;
            }
            else if (Charge > MaxCharge / 2)
            {
                newSprite = "full";
                newParticle = P_Full;
            }
            else if (Charge > MaxCharge / 5)
            {
                newSprite = "half";
                newParticle = P_Half;
            }
            else if (Charge > 0)
            {
                newSprite = "low";
                newParticle = P_Low;
            }
            else
            {
                newSprite = "empty";
            }

            if (sprite.CurrentAnimationID != newSprite)
            {
                sprite.Play(newSprite);
                particle = newParticle;
                if (newSprite == "empty")
                {
                    PlaySound("event:/classic/sfx0", Position);
                    Level.ParticlesFG.Emit(particle, 30, Position + particleOffset, Vector2.One * 10f);
                }
                else if (newSprite is not "infinite" and not "full" and not "fresh")
                {
                    PlaySound("event:/classic/sfx1", Position);
                }
            }

            if (Hold.IsHeld)
            {
                prevLiftSpeed = Vector2.Zero;
                module.LastHeld = this;
                if (!Running)
                {
                    Running = true;
                    fresh = false;
                }
            }
            else
            {
                CarryableActorMotion.AdvanceSpeed(this, engine, Hold, ref Speed, ref prevLiftSpeed, ref noGravityTimer);

                previousPosition = ExactPosition;
                MoveH(Speed.X * engine.DeltaTime, onCollideH);
                MoveV(Speed.Y * engine.DeltaTime, onCollideV);
                if (Right > Level.Bounds.Right)
                {
                    Right = Level.Bounds.Right;
                    CollisionData data = new()
                    {
                        Direction = Vector2.UnitX
                    };
                    OnCollideH(data);
                }
                else if (Left < Level.Bounds.Left)
                {
                    Left = Level.Bounds.Left;
                    CollisionData data = new()
                    {
                        Direction = -Vector2.UnitX
                    };
                    OnCollideH(data);
                }
                else if (Bottom > Level.Bounds.Bottom &&
                         ((IOriginalGameplayProcess)engine).SaveContext.CurrentSave.Assists.Invincible)
                {
                    Bottom = Level.Bounds.Bottom;
                    Speed.Y = -300f;
                    PlaySound("event:/game/general/assist_screenbottom", Position);
                }
                else if (Top > Level.Bounds.Bottom)
                {
                    RemoveSelf();
                }

                Player entity =
                    ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntity<Player>(typeof(Player).FullName);
                BatteryGate templeGate = CollideFirst<BatteryGate>(gateRegistryKey);
                if (templeGate != null && entity != null)
                {
                    templeGate.Collidable = false;
                    MoveH(Math.Sign(entity.X - X) * 32 * engine.DeltaTime);
                    templeGate.Collidable = true;
                }
            }

            if (!dead)
            {
                var tracker = ((ISourceScene)Scene).SourceScene.Components;
                foreach (BatteryCollider batteryCollider in tracker.GetTrackedComponents(
                             tracker.Catalog.Find(typeof(BatteryCollider)).Name))
                {
                    batteryCollider.Check(this);
                }

                Hold.CheckAgainstColliders();
                if (!ignoreBarriers)
                {
                    foreach (SeekerBarrier entity in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                                 "Celeste.SeekerBarrier"))
                    {
                        entity.Collidable = true;
                        bool flag = SourceEntityCollision.Check(this, entity);
                        entity.Collidable = false;
                        if (flag)
                        {
                            entity.OnReflectSeeker();
                            Die();
                        }
                    }
                }
            }

            if (hitSeeker != null && swatTimer <= 0f && !hitSeeker.Check(Hold))
            {
                hitSeeker = null;
            }
        }
    }
}
