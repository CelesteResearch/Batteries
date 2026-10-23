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
using Celeste.Graphics.Rendering;
using Celeste.Scene.Components.Shaking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Platforms.Collision;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Mods.Batteries.Modules;

namespace Celeste.Mods.Batteries.Entities.Gates
{
    [SceneTracked(false)]
    [RuntimeTypeAlias("Celeste.Mod.Batteries.BatteryGate")]
    [CustomEntity("batteries/battery_gate")]
    public class BatteryGate : SourceSolidEntityState
    {
        public MapEntityId entityID;
        private readonly ISourceEngineContext engine;
        public bool ClaimedByASwitch;
        public bool bound;
        public bool closes;
        public int openWith;

        private const int OpenHeight = 4;
        private readonly bool vertical;
        private readonly int closedSize;
        private readonly Sprite sprite;
        private readonly ShakerComponentState shaker;
        private float drawHeight;
        private float drawHeightMoveSpeed;

        public BatteryGate(Vector2 position, bool vertical, int? openWith, bool closes, MapEntityId id,
            ISourceSolidWorld world)
            : base(position, vertical ? 15f : 48f, vertical ? 48f : 15f, true, world)
        {
            engine = world.EngineContext;
            this.vertical = vertical;
            this.closes = closes;
            bound = openWith is not (null or < 0);
            if (bound)
            {
                this.openWith = (int)openWith;
            }

            closedSize = 48;
            entityID = id;
            Add(sprite = BatteriesModule.For(engine).Sprites.Create("battery_gate"));

            if (vertical)
            {
                sprite.X = (Collider.Width - 1) / 2;
            }
            else
            {
                sprite.Y = (Collider.Height + 1) / 2;
                sprite.Rotation = 3 * (float)Math.PI / 2;
            }

            sprite.Play("idle");
            Add(shaker = new ShakerComponentState(engine, on: false));
            if (closes)
            {
                StartOpen();
            }

            Depth = -9000;
        }

        public BatteryGate(EntityData data, Vector2 offset, MapEntityId id, ISourceSolidWorldFactory factory)
            : this(data.Position + offset, data.Bool("vertical"), data.Int("switchId", -1), data.Bool("closes", false),
                id, factory.Create(data.Position + offset, true))
        {
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            drawHeight = Math.Max(4f, vertical ? Height : Width);
        }

        public override void Update()
        {
            base.Update();
            float num = Math.Max(4f, vertical ? Height : Width);
            if (drawHeight != num)
            {
                drawHeight = ScalarMath.Approach(drawHeight, num, drawHeightMoveSpeed * engine.DeltaTime,
                    ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);
            }
        }

        public override void Render(ISourceRenderContext context)
        {
            if (vertical)
            {
                Vector2 value = new(Math.Sign(shaker.Value.X), 0f);
                SourcePrimitiveDrawing.Rect(context, this, X + 2, Y - 1, 11f, 2f, PackedColor.Black);
                sprite.DrawSubrect(context, Vector2.Zero + value,
                    new IntRect(0, (int)(sprite.Height - drawHeight), (int)sprite.Width, (int)drawHeight),
                    ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);
            }
            else
            {
                Vector2 value = new(0f, Math.Sign(shaker.Value.Y));
                SourcePrimitiveDrawing.Rect(context, this, X - 1, Y + 2, 2f, 11f, PackedColor.Black);
                sprite.DrawSubrect(context, Vector2.Zero + value,
                    new IntRect(0, (int)(sprite.Height - drawHeight), (int)sprite.Width, (int)drawHeight),
                    ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);
            }
        }

        public void SwitchOpen()
        {
            sprite.Play("open");
            SourceAlarmState.Set(engine.AlarmPool, OwnedComponents, 0.2f, delegate
            {
                shaker.ShakeFor(0.2f, removeOnFinish: false);
                SourceAlarmState.Set(engine.AlarmPool, OwnedComponents, 0.2f, closes ? Close : Open);
            });
        }

        public void Open()
        {
            ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play("event:/game/05_mirror_temple/gate_main_open",
                Position);
            drawHeightMoveSpeed = 200f;
            drawHeight = vertical ? Height : Width;
            shaker.ShakeFor(0.2f, removeOnFinish: false);
            SetHeight(OpenHeight);
            sprite.Play("open");
        }

        public void StartOpen()
        {
            SetHeight(OpenHeight);
            drawHeight = 4f;
        }

        public void StartClosed()
        {
            SetHeight(closedSize);
            drawHeight = Math.Max(4f, vertical ? Height : Width);
        }

        public void Close()
        {
            ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play("event:/game/05_mirror_temple/gate_main_close",
                Position);
            drawHeightMoveSpeed = 400f;
            drawHeight = Math.Max(4f, vertical ? Height : Width);
            shaker.ShakeFor(0.2f, removeOnFinish: false);
            SetHeight(closedSize);
            sprite.Play("hit");
        }

        private void SetHeight(int height)
        {
            if (vertical) ClosingSolidResize.SetHeight(this, height);
            else ClosingSolidResize.SetWidth(this, height);
        }

        public static BatteryGate Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
            => new(data, offset, new MapEntityId(room.Name, data.ID),
                ((ISourcePlatformCollisionScene)level).SolidWorldFactory);
    }
}
