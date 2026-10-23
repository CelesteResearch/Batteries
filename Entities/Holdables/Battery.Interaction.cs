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
        public void Reset()
        {
            if (Charge < MaxCharge * 0.80)
            {
                PlaySound("event:/classic/sfx16", Position);
                Level.ParticlesFG.Emit(P_Full, 30, Position + particleOffset, Vector2.One * 10f);
            }

            Charge = MaxCharge;
        }

        public void ExplodeLaunch(Vector2 from)
        {
            if (!Hold.IsHeld)
            {
                Speed = ((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(Center - from, 120f);
                OriginalSlashFxState.Burst(engine, Center, Vector2Geometry.Angle(Speed));
            }
        }

        public void Swat(HoldableCollider hc, int dir)
        {
            if (Hold.IsHeld && hitSeeker == null)
            {
                swatTimer = 0.1f;
                hitSeeker = hc;
                Hold.Holder.Swat(dir);
            }
        }

        public bool Dangerous(HoldableCollider holdableCollider)
        {
            return !Hold.IsHeld && Speed != Vector2.Zero && hitSeeker != holdableCollider;
        }

        public void HitSeeker(Seeker seeker)
        {
            PlaySound("event:/game/05_mirror_temple/crystaltheo_hit_side", Position);
            if (!Hold.IsHeld)
            {
                Speed = ((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(Center - seeker.Center, 120f);
            }
        }

        public void HitSpinner(Entity spinner)
        {
            if (spinnerHits < 10 && !Hold.IsHeld &&
                ((ISourceVectorEnvironment)engine).VectorArithmetic.Length(Speed) < 0.01f &&
                ((ISourceVectorEnvironment)engine).VectorArithmetic.Length(LiftSpeed) < 0.01f &&
                ((ISourceVectorEnvironment)engine).VectorArithmetic.Length(previousPosition - ExactPosition) < 0.01f &&
                OnGround())
            {
                spinnerHits++;
                int num = Math.Sign(X - spinner.X);
                if (num == 0)
                {
                    num = 1;
                }

                Speed.X = num * 120f;
                Speed.Y = -30f;
            }
        }

        public bool HitSpring(ISourceSpringEntity spring)
        {
            if (!Hold.IsHeld)
            {
                if (spring.Orientation == SpringOrientation.Floor && Speed.Y >= 0f)
                {
                    Speed.X *= 0.5f;
                    Speed.Y = -160f;
                    noGravityTimer = 0.15f;
                    return true;
                }
                else if (spring.Orientation == SpringOrientation.WallLeft && Speed.X <= 0f)
                {
                    MoveTowardsY(spring.CenterY + 5f, 4f);
                    Speed.X = 220f;
                    Speed.Y = -80f;
                    noGravityTimer = 0.1f;
                    return true;
                }
                else if (spring.Orientation == SpringOrientation.WallRight && Speed.X >= 0f)
                {
                    MoveTowardsY(spring.CenterY + 5f, 4f);
                    Speed.X = -220f;
                    Speed.Y = -80f;
                    noGravityTimer = 0.1f;
                    return true;
                }
            }

            return false;
        }

        public void Use()
        {
            if (oneUse)
            {
                ((Level)Scene).Session.SetFlag(FlagName);
            }
        }

        public void Die()
        {
            if (!dead)
            {
                if (Hold.IsHeld)
                {
                    Vector2 speed2 = Hold.Holder.Data.Motion.Normal.Speed;
                    Hold.Holder.Drop();
                    Speed = speed2 * 0.333f;
                    ((IOriginalGameInputProcessEnvironment)engine).InputSource.Rumble(RumbleStrength.Medium,
                        RumbleLength.Medium);
                }

                dead = true;
                PlaySound("event:/char/madeline/death", Position);
                Add(new DeathEffect(Color.ForestGreen, engine, Center - Position));
                sprite.Visible = false;
                Charge = 0;
                Depth = -1000000;
                Collidable = false;
                AllowPushing = false;
            }
        }
    }
}
