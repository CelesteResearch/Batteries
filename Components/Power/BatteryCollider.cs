// Copyright (c) 2022 Edith Boles and Communal Helper Team. Licensed under the MIT License.

using System;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Mods.Batteries.Entities.Holdables;

namespace Celeste.Mods.Batteries.Components.Power
{
    [SceneTracked(false)]
    [RuntimeTypeAlias("Celeste.Mod.Batteries.BatteryCollider")]
    public class BatteryCollider : SourceComponentState
    {
        public Action<Battery> OnCollide;
        public ColliderGeometry Collider;

        public BatteryCollider(Action<Battery> onCollide, ColliderGeometry collider = null)
            : base(active: false, visible: false)
        {
            OnCollide = onCollide;
            Collider = collider;
        }

        public override void DebugRender(ISourceRenderContext context, object camera)
        {
            if (Collider != null)
            {
                ColliderGeometry collider = ((ISourceEntityCollider)Entity).Collider;
                ((ISourceEntityCollider)Entity).Collider = Collider;
                Collider.Render(camera, PackedColor.HotPink, context);
                ((ISourceEntityCollider)Entity).Collider = collider;
            }
        }

        public bool Check(Battery battery)
        {
            ColliderGeometry collider = Collider;
            if (Collider == null)
            {
                if (SourceEntityCollision.Check(battery, (ISourceEntityCollider)Entity))
                {
                    OnCollide(battery);
                    return true;
                }

                return false;
            }

            ColliderGeometry collider2 = ((ISourceEntityCollider)Entity).Collider;
            ((ISourceEntityCollider)Entity).Collider = collider;
            bool flag = SourceEntityCollision.Check(battery, (ISourceEntityCollider)Entity);
            ((ISourceEntityCollider)Entity).Collider = collider2;
            if (flag)
            {
                OnCollide(battery);
                return true;
            }

            return false;
        }
    }
}
