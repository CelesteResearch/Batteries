// Copyright (c) 2022 Edith Boles and Communal Helper Team. Licensed under the MIT License.

using System.Linq;
using System.Xml;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Everest.Modules.Runtime;
using Celeste.Graphics.Animation;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Graphics.Content;
using Celeste.Mods.Batteries.Entities.Holdables;
using Celeste.Mods.Batteries.Runtime.Power;

namespace Celeste.Mods.Batteries.Modules
{
    public sealed class BatteriesModule : SourceEverestModule
    {
        private BatteryParticles particles;
        public SpriteBank Sprites { get; private set; }
        public Battery LastHeld;
        public BatteryParticles Particles => particles ??= new BatteryParticles(SourceEngine);

        public BatteriesModule(ISourceEngineContext engine) : base(engine)
        {
        }

        public static BatteriesModule For(ISourceEngineContext engine) =>
            ((ISourceEverestModulesEnvironment)engine).Modules.OfType<BatteriesModule>().Single();

        public override void Load()
        {
        }

        public override void Unload()
        {
        }

        public override void LoadContent(bool firstLoad)
        {
            var xml = new XmlDocument();
            using (var stream = ((IEverestContentRegistryEnvironment)SourceEngine).SourceContentRegistry
                   .Get("Graphics/BatteriesSprites").Stream)
                xml.Load(stream);
            Sprites = new SpriteBank(SourceEngine,
                ((ISourceGameGraphicsEnvironment)SourceEngine).GameGraphics.GameAtlas, xml);
        }
    }
}
