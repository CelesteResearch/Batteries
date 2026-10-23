// Copyright (c) 2022 Edith Boles and Communal Helper Team. Licensed under the MIT License.

using System;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Components.Timing;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Maps;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Simulation;
using Celeste.Physics.Collision.Shapes;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Mods.Batteries.Entities.Holdables;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Platforms.Collision;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Mods.Batteries.Modules;

namespace Celeste.Mods.Batteries.Entities.Platforms
{
    [CustomEntity("batteries/recharge_platform")]
    [RuntimeTypeAlias("Celeste.Mod.Batteries.RechargePlatform")]
    public class RechargePlatform : SourceSolidEntityState
    {
        private readonly ColliderGeometry nc;
        private readonly BatteriesModule module;

        public RechargePlatform(Vector2 Position, ISourceSolidWorld world)
            : base(Position, 24, 4, true, world)
        {
            module = BatteriesModule.For(world.EngineContext);
            Add(module.Sprites.Create("recharge_platform"));
            Collider.BottomCenter = Vector2.Zero;
            Add(new PlayerColliderComponentState(OnPlayerCollide, nc = Collider.Clone()));
            nc.Width += 2;
            nc.Height += 3;
            nc.CenterX -= 1;
            nc.CenterY -= 3;
        }

        public RechargePlatform(EntityData data, Vector2 offset, ISourceSolidWorldFactory worlds)
            : this(data.Position + offset, worlds.Create(data.Position + offset, true))
        {
        }

        public void OnPlayerCollide(RoomPlayerSession player)
        {
            if ((Math.Abs(player.Data.Motion.Normal.Speed.X) > 0f || HasPlayerClimbing()) &&
                Math.Abs(player.Position.Y - Position.Y) < 0.5)
            {
                player.Y = Position.Y - 4;
                player.X += Math.Sign(Position.X - player.Position.X);
            }

            if (module.LastHeld != null && module.LastHeld.Hold.IsHeld)
            {
                module.LastHeld.Reset();
            }
        }

        public static RechargePlatform Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
            => new(data, offset, ((ISourcePlatformCollisionScene)level).SolidWorldFactory);
    }
}
