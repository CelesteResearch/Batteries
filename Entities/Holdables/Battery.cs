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
    [SceneTracked]
    [CustomEntity("batteries/battery")]
    [RuntimeTypeAlias("Celeste.Mod.Batteries.Battery")]
    public partial class Battery : SourceActorEntityState, ISourceEngineContextEntity
    {
        private readonly ISourceEngineContext engine;
        private readonly string gateRegistryKey;
        private readonly BatteriesModule module;
        private readonly BatteryParticles particles;
        private ParticleType P_Infinite => particles.Infinite;
        private ParticleType P_Full => particles.Full;
        private ParticleType P_Half => particles.Half;
        private ParticleType P_Low => particles.Low;
        ISourceEngineContext ISourceEngineContextEntity.EngineContext => engine;

        public Vector2 Speed;
        public Holdable Hold;
        public int onlyFits;
        public bool Running = false;
        public int MaxCharge;
        public int Charge;
        public int DischargeRate;

        private static readonly Vector2 particleOffset = new(0, -8);
        private readonly Sprite sprite;
        private readonly Collision onCollideH;
        private readonly Collision onCollideV;
        private readonly bool ignoreBarriers;
        private readonly bool oneUse;
        private bool dead;
        private Level Level;
        private float noGravityTimer;
        private Vector2 prevLiftSpeed;
        private Vector2 previousPosition;
        private HoldableCollider hitSeeker;
        private float swatTimer;
        private float hardVerticalHitSoundCooldown = 0f;
        private ParticleType particle;
        private bool fresh;
        private EntityID id;
        private int spinnerHits = 0;

        public Battery(Vector2 position, int initalCharge, int maxCharge, int dischargeRate, bool oneUse,
            bool ignoreBarriers, int onlyFits, EntityID id, ISourceEngineContext engine)
            : base(new PixelActor(null, position), engine,
                ((ISourcePlatformTargetMotionEnvironment)engine).TargetMotionPolicy)
        {
            this.engine = engine;
            gateRegistryKey = ((Celeste.Scene.Tracking.ISourceSceneCatalogEnvironment)engine).SceneCatalog
                .Find(typeof(BatteryGate)).RegistryKey;
            module = BatteriesModule.For(engine);
            particles = module.Particles;
            previousPosition = position;
            this.id = id;
            fresh = true;
            Depth = 100;
            Collider = new Hitbox(8f, 10f, -4f, -10f);
            Add(sprite = module.Sprites.Create("battery"));
            sprite.Scale.X = -1f;
            Add(Hold = new Holdable(engine, 0.1f));
            Hold.PickupCollider = new Hitbox(16f, 22f, -8f, -16f);
            Hold.SlowFall = false;
            Hold.SlowRun = false;
            Hold.OnPickup = OnPickup;
            Hold.OnRelease = OnRelease;
            Hold.DangerousCheck = value => Dangerous((HoldableCollider)value);
            Hold.OnHitSeeker = value => HitSeeker((Seeker)value);
            Hold.OnSwat = (value, dir) => Swat((HoldableCollider)value, dir);
            Hold.OnHitSpring = value => HitSpring((ISourceSpringEntity)value);
            Hold.OnHitSpinner = value => HitSpinner((Entity)value);
            Hold.SpeedGetter = () => Speed;
            onCollideH = OnCollideH;
            onCollideV = OnCollideV;
            LiftSpeedGraceTime = 0.1f;
            Add(new VertexLight(engine, Collider.Center, Color.White, 1f, 32, 64));
            Add(new MirrorReflection());
            particle = P_Full;
            MaxCharge = maxCharge;
            Charge = initalCharge;
            DischargeRate = dischargeRate;
            this.oneUse = oneUse;
            this.ignoreBarriers = ignoreBarriers;
            this.onlyFits = onlyFits;
            LoadParticles();
        }

        public Battery(EntityData e, Vector2 offset, EntityID id, ISourceEngineContext engine)
            : this(e.Position + offset, e.Int("initalCharge", 500),
                e.Int("maxCharge", 500), e.Int("dischargeRate", 80),
                e.Bool("oneUse"), e.Bool("ignoreBarriers", false),
                e.Int("onlyFits", -1), id, engine)
        {
        }

        private string FlagName => GetFlagName(id);

        public static string GetFlagName(EntityID id) => "battery_" + id.Key;

        private void LoadParticles() => particles.Load();

        public static Battery Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
            => new(data, offset, new MapEntityId(room.Name, data.ID), level.EngineContext);

        public override void Added(object scene)
        {
            base.Added(scene);
            Level = (Level)Scene;
            if (Level.Session.GetFlag(FlagName))
            {
                RemoveSelf();
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
            }
        }

        public override bool IsRiding(Solid solid)
        {
            return Speed.Y == 0f && base.IsRiding(solid);
        }

        protected override void OnSquish(CollisionData data)
        {
            if (!TrySquishWiggle(data) &&
                !((IOriginalGameplayProcess)engine).SaveContext.CurrentSave.Assists.Invincible)
            {
                Die();
            }
        }

        private void PlaySound(string path, Vector2 position, string parameter = null, float value = 0f)
        {
            var process = (IOriginalGameplayProcess)engine;
            if (parameter == null) process.AudioSession.Play(path, position, process.SaveContext);
            else process.AudioSession.Play(path, position, process.SaveContext, parameter, value);
        }
    }
}
