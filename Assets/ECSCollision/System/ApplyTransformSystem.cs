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
            //var Parameter = SystemAPI.GetSingleton<ParticleParameterComponent>();
            if (SystemAPI.TryGetSingleton<ParticleParameterComponent>(out var Parameter) == false)
            {
                Enabled = false;
                return;
            }
            //Parameter.ParticleRadius / 0.5f

            var ecb = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                                .CreateCommandBuffer(World.Unmanaged);
            new ApplyPosition
            {
                ecb = ecb.AsParallelWriter(),
                size = Parameter.ParticleRadius / 0.5f,
                delta = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(Dependency).Complete();
        }

        [BurstCompile]
        partial struct ApplyPosition : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            public float size;
            public float delta;
            public void Execute([EntityIndexInQuery] int index, Entity entity,
                                 in LocalTransform transform, in FluidSimlationComponent data)
            {

                var trans = transform;
                trans.Position = data.position;

                if (Vector3.SqrMagnitude(data.velocity) > 0.01f)
                {
                    trans.Rotation *= Quaternion.LookRotation(data.velocity);

                    trans.Rotation = Quaternion.Lerp(transform.Rotation, trans.Rotation, delta);
                }

                trans.Scale = size;

                ecb.SetComponent(index, entity, trans);
            }
        }
    }

}