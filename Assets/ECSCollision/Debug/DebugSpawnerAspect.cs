using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace EcsCollision
{
    public readonly partial struct DebugSpawnerAspect : IAspect
    {
        public readonly Entity self;
        [System.Obsolete] readonly RefRW<DebugSpawnerComponent> spawn;

        readonly RefRO<LocalTransform> transform;
        readonly RefRO<ParticleParameterComponent> particle;
        public LocalTransform Transform
        {
            get => transform.ValueRO;
        }
        public DebugSpawnerComponent SpawnData => spawn.ValueRW;
        public ParticleParameterComponent Particle
        {
            get => particle.ValueRO;
        }

        #region JobHandle
        [System.Obsolete]
        public SpawnJob SpawnParticle(EntityCommandBuffer ecb, uint randomSeed)
        {
            return new SpawnJob
            {
                ecb = ecb.AsParallelWriter(),
                particle = Particle,
                spawnComponent = SpawnData,
                transform = Transform,
                randomSeed = randomSeed
            };
        }
        public bool EnableParticles(SystemBase systemBase, EntityCommandBuffer ecb,
            NativeList<Entity> SpawnedParticle, NativeArray<Entity> Disabled, uint randomSeed, out EnableJob allEnable)
        {
            if (GetDisableParticle(systemBase, SpawnedParticle, out var disabled))
            {
                if (disabled.Length > 0)
                {
                    allEnable = new EnableJob
                    {
                        ecb = ecb.AsParallelWriter(),
                        amount = disabled.Length,
                        randomSeed = randomSeed,
                        disabled = Disabled,
                        SpawnerTrans = Transform
                    };
                    return true;
                }
            }

            allEnable = default;
            return false;
        }

        /// <summary>
        /// if ToEnableAmount is less than 0 , Spawn All Disabled Particle
        /// </summary>
        /// <param name="systemBase"></param>
        /// <param name="ecb"></param>
        /// <param name="SpawnedParticle"></param>
        /// <param name="randomSeed"></param>
        /// <param name="ToEnableAmount"></param>
        /// <param name="innerloopBatchCount"></param>
        /// <returns></returns>
        public bool EnableParticles(SystemBase systemBase, EntityCommandBuffer ecb,
            NativeList<Entity> SpawnedParticle, uint randomSeed, int ToEnableAmount, int innerloopBatchCount)
        {
            if (GetDisableParticle(systemBase, SpawnedParticle, out var disabled))
            {
                if (disabled.Length > 0 && ToEnableAmount <= disabled.Length)
                {
                    new EnableJob
                    {
                        ecb = ecb.AsParallelWriter(),
                        amount = disabled.Length,
                        randomSeed = randomSeed,
                        disabled = disabled,
                        SpawnerTrans = Transform
                    }.Schedule((ToEnableAmount > 0 ? ToEnableAmount : disabled.Length), innerloopBatchCount).Complete();
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region Job
        [BurstCompile, System.Obsolete]
        public partial struct SpawnJob : IJob
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            [ReadOnly] public ParticleParameterComponent particle;
            [ReadOnly] public DebugSpawnerComponent spawnComponent;
            [ReadOnly] public LocalTransform transform;
            public uint randomSeed;

            public void Execute()
            {
                var random = new Unity.Mathematics.Random(randomSeed);
                int size = Mathf.FloorToInt(Mathf.Pow(spawnComponent.Amount, 1 / 3f));

                for (int i = 0; i < spawnComponent.Amount; i++)
                {
                    var instance = ecb.Instantiate(i, spawnComponent.particle);

                    var position = new float3((i % size) * 1.2f + random.NextFloat(-0.1f, 0.1f) * spawnComponent.RandomPower,
                        0 + (i / size / size) * 1.2f,
                        ((i / size) % size) * 1.2f + random.NextFloat(-0.1f, 0.1f) * spawnComponent.RandomPower) + transform.Position;

                    var Ltrans = new LocalTransform
                    {
                        Position = position,
                        Rotation = quaternion.identity,
                        Scale = particle.ParticleRadius / 0.5f
                    };

                    ecb.SetComponent(i, instance, Ltrans);
                }
            }
        }

        [BurstCompile]
        public partial struct EnableJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            public float amount;
            public uint randomSeed;

            [ReadOnly] public NativeArray<Entity> disabled;

            [ReadOnly] public LocalTransform SpawnerTrans;

            public void Execute(int index)
            {
                if (amount <= 0)
                    return;

                var random = new Unity.Mathematics.Random(randomSeed + (uint)index);
                int size = Mathf.FloorToInt(Mathf.Pow(amount, 1 / 3f));

                if (size <= 0)
                    Debug.Log($"amount : {amount} , index : {index}");

                var randomPos = new float3((index % size) * 1.2f + random.NextFloat(-0.1f, 0.1f),
                        0 + (index / size / size) * 1.2f,
                        ((index / size) % size) * 1.2f + random.NextFloat(-0.1f, 0.1f));

                //SpawnerTrans.Position += randomPos;//설마 안된 이유가 값 변경된게 누적되서??
                var spawnTrans = SpawnerTrans;
                spawnTrans.Position += randomPos;

                ecb.SetEnabled(index, disabled[index], true);
                ecb.SetComponentEnabled<FluidSimlationComponent>(index, disabled[index], true);
                ecb.SetComponent(index, disabled[index], spawnTrans);
                ecb.SetComponent(index, disabled[index], new FluidSimlationComponent() { position = spawnTrans.Position });
            }
        }
        #endregion

        #region EntityQuery
        public NativeArray<Entity> GetActiveParticle(SystemBase systemBase)
        {
            using var particleQB = new EntityQueryBuilder(Allocator.TempJob).WithAll<FluidSimlationComponent, LocalTransform>();
            return systemBase.GetEntityQuery(particleQB).ToEntityArray(Allocator.TempJob);
        }
        public bool GetDisableParticle(SystemBase systemBase, NativeList<Entity> SpawnedParticle, out NativeArray<Entity> Disabled)
        {
            if (!SpawnedParticle.IsCreated)
            {
                Disabled = default;
                return false;
            }

            var activeParticle = GetActiveParticle(systemBase);
            var spawnedArray = SpawnedParticle.ToArray(Allocator.Temp);

            Disabled = new NativeArray<Entity>(spawnedArray.Except(activeParticle).ToArray(), Allocator.TempJob);

            return true;
        }
        #endregion
    }

}