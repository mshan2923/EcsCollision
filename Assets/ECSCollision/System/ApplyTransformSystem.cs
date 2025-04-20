using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace EcsCollision
{
    public partial class ApplyTransformSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var Parameter = SystemAPI.GetSingleton<ParticleParameterComponent>();
            //Parameter.ParticleRadius / 0.5f

            var ecb = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                                .CreateCommandBuffer(World.Unmanaged);
            new ApplyPosition
            {
                ecb = ecb.AsParallelWriter(),
                size = Parameter.ParticleRadius / 0.5f
            }.ScheduleParallel(Dependency).Complete();
        }

        [BurstCompile]
        partial struct ApplyPosition : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            public float size;
            public void Execute([EntityIndexInQuery] int index, Entity entity,
                                 in LocalTransform transform, in FluidSimlationComponent data)
            {
                // transform.Position = data.position;
                // transform.Scale = size;

                var trans = transform;
                trans.Position = data.position;
                trans.Scale = size;

                ecb.SetComponent(index, entity, trans);
            }
        }
    }

}