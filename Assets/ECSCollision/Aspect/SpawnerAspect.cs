using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreeEditor;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace EcsCollision
{
    public readonly partial struct SpawnerAspect : IAspect
    {
        public readonly Entity self;
        public readonly RefRW<ParticleSpawnerComponent> spawn;

        readonly RefRO<LocalTransform> transform;
        //public readonly RefRW<ParticleParameterComponent> particle;
        public readonly DynamicBuffer<ParticleSpawnAreaElement> spawnArea;

        public LocalTransform Transform
        {
            get => transform.ValueRO;
        }
        public ParticleSpawnerComponent SpawnData
        {
            get => spawn.ValueRO;
        }
        // public ParticleParameterComponent Particle
        // {
        //     get => particle.ValueRO;
        // }
        public int SpawnAreaCount
        {
            get => spawnArea.Length;
        }


        #region JobHandle

        /// <summary>
        /// 力茄等 傍埃郴 积己
        /// </summary>
        /// <param name="ecb"></param>
        /// <param name="randomSeed"></param>
        /// <param name="SpawnAmount"></param>
        /// <returns></returns>
        public SpawnJob SpawnParticle(EntityCommandBuffer ecb, ParticleParameterComponent manager, uint randomSeed, int SpawnAmount)
        {
            return new SpawnJob
            {
                ecb = ecb,
                particle = manager,
                particleSpawner = SpawnData,
                randomSeed = randomSeed,
                SpawnAmount = SpawnAmount,
                transform = Transform
            };
        }
        public IntiJob IntiParticle(EntityCommandBuffer ecb)
        {
            return new IntiJob()
            {
                ecb = ecb.AsParallelWriter(),
                particleSpawner = SpawnData,
                transform = Transform
            };
        }

        public bool EnableParticles(SystemBase systemBase, EntityCommandBuffer ecb,
            NativeArray<Entity> Disabled, NativeArray<ParticleSpawnAreaComponent> Area,
            ParticleParameterComponent manager,
            uint randomSeed, out EnableJob allEnable)
        {

            if (Disabled.Length > 0)
            {

                allEnable = new EnableJob
                {
                    ecb = ecb.AsParallelWriter(),
                    randomSeed = randomSeed,
                    disabled = Disabled,
                    SpawnerTrans = Transform,
                    AreaData = Area,
                    particleParameter = manager,
                    spawner = SpawnData
                };
                return true;
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
        [System.Obsolete]
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
                        randomSeed = randomSeed,
                        disabled = disabled,
                        SpawnerTrans = Transform
                    }.Schedule((ToEnableAmount > 0 ? ToEnableAmount : disabled.Length), innerloopBatchCount).Complete();
                    return true;
                }
            }

            return false;
        }
        #endregion//

        #region Job
        [BurstCompile, System.Obsolete]
        public partial struct DebugSpawnJob : IJob
        {
            public EntityCommandBuffer ecb;
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
                    var instance = ecb.Instantiate(spawnComponent.particle);

                    var position = new float3((i % size) * 1.2f + random.NextFloat(-0.1f, 0.1f) * spawnComponent.RandomPower,
                        0 + (i / size / size) * 1.2f,
                        ((i / size) % size) * 1.2f + random.NextFloat(-0.1f, 0.1f) * spawnComponent.RandomPower) + transform.Position;

                    var Ltrans = new LocalTransform
                    {
                        Position = position,
                        Rotation = quaternion.identity,
                        Scale = particle.ParticleRadius / 0.5f
                    };

                    ecb.SetComponent(instance, Ltrans);
                }
            }
        }

        [BurstCompile]
        public partial struct SpawnJob : IJobEntity
        {
            public EntityCommandBuffer ecb;
            public ParticleSpawnerComponent particleSpawner;

            [ReadOnly] public ParticleParameterComponent particle;
            [ReadOnly] public LocalTransform transform;
            public uint randomSeed;
            public int SpawnAmount;

            public void Execute(ParticleSpawnAreaComponent area)
            {
                var random = new Unity.Mathematics.Random(randomSeed);
                var MaxSpawnPoints = area.SpawnPoints.x * area.SpawnPoints.y * area.SpawnPoints.z;

                for (int x = 0, i = 0; x < area.SpawnPoints.x; x++)
                {
                    for (int y = 0; y < area.SpawnPoints.y; y++)
                    {
                        for (int z = 0; z < area.SpawnPoints.z; z++, i++)
                        {
                            if (i > SpawnAmount && SpawnAmount > 0)
                                return;
                            if (i > MaxSpawnPoints)
                                return;

                            var worldPos = float3.zero;

                            if (particleSpawner.SpawnRandomPoint)
                            {
                                float3 size = new float3(area.SpawnPoints.x, area.SpawnPoints.y, area.SpawnPoints.z);
                                worldPos = transform.Position + random.NextFloat3(size * -1f, size) * 0.25f;
                            }
                            else
                            {
                                worldPos = GetSpawnPoint(transform, area.LocalMinPos,
                                    particle.ParticleRadius, particleSpawner.SpawnBetweenSpace, x, y, z);
                                worldPos += random.NextFloat3(new float3(1, 1, 1) * -1f, new float3(1, 1, 1))
                                    * (particleSpawner.SpawnBetweenSpace * 0.5f);
                            }

                            var Ltrans = new LocalTransform
                            {
                                Position = worldPos,
                                Rotation = quaternion.identity,
                                Scale = particle.ParticleRadius / 0.5f
                            };

                            var instance = ecb.Instantiate(particleSpawner.ParticleObj);
                            ecb.SetComponent(instance, Ltrans);
                            ecb.SetComponent(instance, new FluidSimlationComponent
                            {
                                velocity = area.IntiVelocity//particleSpawner.IntiVelocity
                            });
                        }
                    }
                }
            }
        }
        [BurstCompile]
        public partial struct IntiJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            public ParticleSpawnerComponent particleSpawner;

            [ReadOnly] public LocalTransform transform;

            public void Execute(int index)
            {
                var instance = ecb.Instantiate(index, particleSpawner.ParticleObj);
                ecb.SetComponent(index, instance, transform);
                //entities[index] = instance;
                //entities.AddNoResize(instance);
                //ecb.SetEnabled(index, instance, false);
            }
        }

        [BurstCompile]
        public partial struct DisableAllJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            public NativeList<Entity>.ParallelWriter entities;
            public void Execute([EntityIndexInQuery] int index, Entity entity, FluidSimlationComponent fluid)
            {
                entities.AddNoResize(entity);
                ecb.SetEnabled(index, entity, false);
                ecb.SetComponentEnabled<FluidSimlationComponent>(index, entity, false);
            }
        }

        [BurstCompile]
        public partial struct DisableJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            //[System.Obsolete] public NativeList<Entity>.ParallelWriter entities;

            public NativeArray<Entity> targets;
            public void Execute(int index)
            {
                //entities.AddNoResize(targets[index]);
                ecb.SetEnabled(index, targets[index], false);
                ecb.SetComponentEnabled<FluidSimlationComponent>(index, targets[index], false);
            }
        }

        [BurstCompile]
        public partial struct DestroyJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            //public NativeList<Entity>.ParallelWriter entities;

            public NativeArray<Entity> targets;

            public void Execute(int index)
            {
                ecb.DestroyEntity(index, targets[index]);
            }
        }

        [BurstCompile]
        public partial struct EnableJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            public uint randomSeed;

            [ReadOnly] public NativeArray<Entity> disabled;
            [ReadOnly] public NativeArray<ParticleSpawnAreaComponent> AreaData;

            [ReadOnly] public LocalTransform SpawnerTrans;

            public ParticleParameterComponent particleParameter;
            public ParticleSpawnerComponent spawner;

            public void Execute(int index)
            {
                if (index >= disabled.Length)
                    return;
                if (disabled[index] == Entity.Null)
                    return;

                var random = new Unity.Mathematics.Random(randomSeed + (uint)index);
                var spawnTrans = SpawnerTrans;

                /*
                int size = Mathf.FloorToInt(Mathf.Pow(amount, 1 / 3f));

                var randomPos = new float3((index % size) * 1.2f + random.NextFloat(-0.1f, 0.1f),
                        0 + (index / size / size) * 1.2f,
                        ((index / size) % size) * 1.2f + random.NextFloat(-0.1f, 0.1f));
                */

                int SpawnAreaIndex = 0;
                int BorderSpawnAreaIndex = 0;
                for (int i = 0; i < AreaData.Length; i++)
                {
                    BorderSpawnAreaIndex += AreaData[i].SpawnPoints.x * AreaData[i].SpawnPoints.y * AreaData[i].SpawnPoints.z;
                    if (index >= BorderSpawnAreaIndex)
                    {
                        SpawnAreaIndex++;
                    }
                }
                if (SpawnAreaIndex >= AreaData.Length)
                    return;

                spawnTrans.Position = AreaData[SpawnAreaIndex].Bound.center;

                var Lpos = SpawnerAspect.GetSpawnPoint(spawnTrans, AreaData[SpawnAreaIndex].LocalMinPos,
                    particleParameter.ParticleRadius, spawner.SpawnBetweenSpace, AreaData[SpawnAreaIndex].SpawnPoints, index);
                spawnTrans.Position = Lpos + random.NextFloat3(new float3(1, 1, 1) * -1f, new float3(1, 1, 1)) * spawner.SpawnBetweenSpace * 0.5f;
                spawnTrans.Scale = particleParameter.ParticleRadius / 0.5f;

                ecb.SetEnabled(index, disabled[index], true);
                ecb.SetComponentEnabled<FluidSimlationComponent>(index, disabled[index], true);
                ecb.SetComponent(index, disabled[index], spawnTrans);
                ecb.SetComponent(index, disabled[index], new FluidSimlationComponent()
                {
                    position = spawnTrans.Position,
                    velocity = AreaData[SpawnAreaIndex].IntiVelocity
                });
            }
        }
        #endregion

        #region EntityQuery
        public NativeArray<Entity> GetActiveParticle(SystemBase systemBase, Allocator allocator = Allocator.Temp)
        {
            var particleQB = new EntityQueryBuilder(Allocator.Temp).WithAll<FluidSimlationComponent, LocalTransform>();
            return systemBase.GetEntityQuery(particleQB).ToEntityArray(allocator);
        }
        public int GetActiveParticleCount(SystemBase systemBase)
        {
            var particleQB = new EntityQueryBuilder(Allocator.Temp).WithAll<FluidSimlationComponent, LocalTransform>();
            return systemBase.GetEntityQuery(particleQB).CalculateEntityCount();
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


        // public float3 GetSpawnPoint(ParticleSpawnAreaComponent area, int index)
        // {
        //     return GetSpawnPoint(Transform, area.LocalMinPos, Particle.ParticleRadius, SpawnData.SpawnBetweenSpace, area.SpawnPoints, index);
        // }
        public static float3 GetSpawnPoint(LocalTransform transform, float3 MinPos, float radius, float between, int3 spawnPoints, int index)
        {
            int PosY = index / (spawnPoints.x * spawnPoints.z);
            int PosZ = index / spawnPoints.x % spawnPoints.z;
            int PosX = index % spawnPoints.x;

            return GetSpawnPoint(transform, MinPos, radius, between, PosX, PosY, PosZ);
        }

        public static float3 GetSpawnPoint(LocalTransform transform, float3 MinPos, float radius, float between, int LocalX, int LocalY, int LocalZ)
        {
            var Local = math.rotate(transform.Rotation, MinPos);
            Local += transform.Right() * LocalX * (radius * 2 + between);
            Local += transform.Up() * LocalY * (radius * 2 + between);
            Local += transform.Forward() * LocalZ * (radius * 2 + between);

            return transform.Position + Local;
        }

        public int GetSpawnPointAmount(NativeArray<ParticleSpawnAreaComponent> Area)
        {
            int count = 0;
            for (int i = 0; i < Area.Length; i++)
            {
                count += Area[i].SpawnPoints.x * Area[i].SpawnPoints.y * Area[i].SpawnPoints.z;
            }
            return count;
        }
        public bool SetMaxAmount(int Amount, NativeList<Entity> Spawned, NativeArray<Entity> Disabled)
        {
            if (Amount < Spawned.Length)
            {
                if (Spawned.Length - Amount < Disabled.Length)
                {
                    spawn.ValueRW.MaxAmount = Amount;
                    return true;
                }
            }
            else if (Amount > Spawned.Length)
            {
                spawn.ValueRW.MaxAmount = Amount;
                return true;
            }

            return false;
        }
    }

}