// Copyright (c) 2022 Edith Boles and Communal Helper Team. Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using Celeste.Core.Display;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Processes;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.Batteries.Runtime.Particles
{
    public sealed class BatterySwitchParticles
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, BatterySwitchParticles> instances = new();
        public ParticleTypePresentation Signal { get; }

        private BatterySwitchParticles(ISourceEngineContext engine)
        {
            Signal = new ParticleTypePresentation(
                ((IOriginalGameplayProcess)engine).ParticleTypes.DashSwitch.P_PressAMirror)
            {
                Color = PackedColor.Aqua,
                ColorMode = ParticleTypePresentation.ColorModes.Choose,
                LifeMax = 0.6f,
                LifeMin = 0.3f,
                SpeedMultiplier = 0.1f,
                DirectionRange = (float)(Math.PI / 5)
            };
        }

        public static BatterySwitchParticles For(ISourceEngineContext engine) =>
            instances.GetValue(engine, static owner => new BatterySwitchParticles(owner));
    }
}
