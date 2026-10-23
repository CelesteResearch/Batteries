// Copyright (c) 2022 Edith Boles and Communal Helper Team. Licensed under the MIT License.

using Celeste.Core.Display;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Processes;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.Batteries.Runtime.Power
{
    public sealed class BatteryParticles
    {
        public readonly ParticleTypePresentation Infinite, Full, Half, Low;

        public BatteryParticles(ISourceEngineContext engine)
        {
            var glow = ((IOriginalGameplayProcess)engine).ParticleTypes.Refill.P_Glow;
            Infinite = new ParticleTypePresentation(glow);
            Full = new ParticleTypePresentation(glow);
            Half = new ParticleTypePresentation(glow);
            Low = new ParticleTypePresentation(glow);
        }

        public void Load()
        {
            Infinite.Color = PackedColor.Cyan;
            Infinite.ColorMode = ParticleTypePresentation.ColorModes.Static;
            Full.Color = PackedColor.Lime;
            Full.ColorMode = ParticleTypePresentation.ColorModes.Static;
            Half.Color = PackedColor.LightGoldenrodYellow;
            Half.ColorMode = ParticleTypePresentation.ColorModes.Static;
            Low.Color = PackedColor.OrangeRed;
            Low.ColorMode = ParticleTypePresentation.ColorModes.Static;
        }
    }
}
