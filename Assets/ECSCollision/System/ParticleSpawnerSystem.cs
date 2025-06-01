using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


namespace EcsCollision
{
    //[UpdateAfter(typeof(EndInitializationEntityCommandBufferSystem))]
    public partial class ParticleSpawnerSystem : SystemBase
    {
        public Entity SpawnerEntity;
        public RefRW<ParticleSpawnerComponent> SpawnManager;
        public RefRW<ParticleParameterComponent> ParticleManager;
        public DynamicBuffer<ParticleSpawnAreaElement> SpawnAreaBuffer;

        public EntityQuery SpawnAreaQuery;

        SpawnerAspect spawnerAspect;

        NativeList<Entity> SpawnedParticle;
        bool DoOnceEnable;

        float IntervalCount = 0;

        bool ChangingMaxAmount = false;

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            ParticleManager = SystemAPI.GetSingletonRW<ParticleParameterComponent>();
            if (SystemAPI.TryGetSingletonRW<ParticleSpawnerComponent>(out var spawnerComponent))
            {
                SpawnManager = spawnerComponent;
                SpawnAreaQuery = GetEntityQuery(typeof(ParticleSpawnAreaComponent));

                SpawnerEntity = SystemAPI.GetSingletonEntity<ParticleSpawnerComponent>();
                spawnerAspect = SystemAPI.GetAspect<SpawnerAspect>(SpawnerEntity);

                SpawnAreaBuffer = SystemAPI.GetSingletonBuffer<ParticleSpawnAreaElement>();

                var areaEntity = SpawnAreaQuery.ToEntityArray(Allocator.Temp);
                var areaData = SpawnAreaQuery.ToComponentDataArray<ParticleSpawnAreaComponent>(Allocator.Temp);
                for (int i = 0; i < areaEntity.Length; i++)
                {
                    SpawnAreaBuffer.Add(new ParticleSpawnAreaElement { entity = areaEntity[i], SpawnArea = areaData[i] });
                }

                areaEntity.Dispose();
                areaData.Dispose();


                new CalculateSpawnPoint()
                {
                    spawnManager = spawnerComponent.ValueRO,
                    particleSize = ParticleManager.ValueRO.ParticleRadius
                }.ScheduleParallel(Dependency).Complete();
                // ParticleSpawnAreaElement 의 LocalMinPos , SpawnPoints 계산


                var ECB = new EntityCommandBuffer(Allocator.TempJob);
                SpawnedParticle = new NativeList<Entity>(spawnerAspect.SpawnData.MaxAmount, Allocator.Persistent);
                spawnerAspect.IntiParticle(ECB)
                    .Schedule(spawnerAspect.SpawnData.MaxAmount, 8, Dependency).Complete();
                ECB.Playback(EntityManager);
                ECB.Dispose();
                //여기서 리스트 하면 아직 스폰 안된 상태라서

                ECB = new EntityCommandBuffer(Allocator.TempJob);
                var disableHandle = new SpawnerAspect.DisableAllJob()
                {
                    ecb = ECB.AsParallelWriter(),
                    entities = SpawnedParticle.AsParallelWriter()
                }.ScheduleParallel(Dependency);

                Dependency = disableHandle;
                disableHandle.Complete();
                ECB.Playback(EntityManager);
                ECB.Dispose();



                ParticleSpawner.instance.OnChangeMaxSpawnAmount += OnChangeMaxSpawnAmount;
            }
            else
                Enabled = false;
        }
        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            SpawnedParticle.Dispose();
        }
        protected override void OnUpdate()
        {
            if (SpawnerEntity.Equals(Entity.Null))
            {
                Enabled = false;
                return;
            }

            spawnerAspect = SystemAPI.GetAspect<SpawnerAspect>(SpawnerEntity);

            {
                if (Input.GetMouseButton(0) && false)
                {
                    if (!DoOnceEnable)
                    {
                        DoOnceEnable = true;
                        /*
                        var ecb = new EntityCommandBuffer(Allocator.TempJob);

                        spawnerAspect.GetDisableParticle(this, SpawnedParticle, out var disabled);

                        Debug.Log($"S : {SpawnedParticle.Length} / D : {disabled.Length}");


                        if (spawnerAspect.EnableParticles(this, ecb, disabled, SpawnAreaQuery.ToComponentDataArray<ParticleSpawnAreaComponent>(Allocator.TempJob),
                            (uint)World.Time.ElapsedTime + 1u, out var enableHandler))
                        {
                            enableHandler.Schedule(50, 8, Dependency).Complete();
                            ecb.Playback(EntityManager);
                        }

                        ecb.Dispose();
                        */

                    }

                    //DoOnceEnable = true;//작업이 끝난후 적용
                }
                else
                {
                    DoOnceEnable = false;
                }
            }//Mouse Click Process (Disabled)


            IntervalCount += SystemAPI.Time.DeltaTime;

            {
                ParticleManager = SystemAPI.GetSingletonRW<ParticleParameterComponent>();
                if (!Mathf.Approximately(ParticleManager.ValueRO.ParticleRadius, ParticleParameter.instance.particleRadius))
                {
                    //var parameter = ParticleManager;
                    //parameter.ParticleRadius = ParticleSpawner.instance.ParticleRadius;
                    //EntityManager.SetComponentData(spawnerAspect.self, parameter);

                    ParticleManager.ValueRW.ParticleRadius = ParticleParameter.instance.particleRadius;

                    //Debug.Log($"Edited : {ParticleManager.ParticleRadius} / {ParticleSpawner.instance.ParticleRadius}");
                }//HasheDluidSimulation 에서 위치 적용할때 같이 하면 바로 적용

                if (ParticleParameter.instance.NeedUpdate)
                {
                    ParticleManager.ValueRW.ParticleRadius = ParticleParameter.instance.particleRadius;
                    ParticleManager.ValueRW.SmoothRadius = ParticleParameter.instance.smoothRadius;
                    ParticleManager.ValueRW.Gravity = ParticleParameter.instance.gravity;
                    ParticleManager.ValueRW.ParticleViscosity = ParticleParameter.instance.particleViscosity;
                    ParticleManager.ValueRW.ParticleDrag = ParticleParameter.instance.particleDrag;
                    ParticleManager.ValueRW.ParticlePush = ParticleParameter.instance.particlePush;
                    ParticleManager.ValueRW.SimulateLiquid = ParticleParameter.instance.SimulateLiquid;
                    //ParticleManager.ValueRW.DT = 1f / ParticleParameter.instance.MoveFPS;
                    ParticleManager.ValueRW.floorType = ParticleParameter.instance.floorType;
                    ParticleManager.ValueRW.floorHeight = ParticleParameter.instance.floorHeight;

                    ParticleParameter.instance.NeedUpdate = false;
                }

                if (spawnerAspect.SpawnData.SpawnPerSecond != ParticleSpawner.instance.SpawnPerSecond)
                {
                    spawnerAspect.spawn.ValueRW.SpawnPerSecond = ParticleSpawner.instance.SpawnPerSecond;
                }
            }//Apply Edit Vaule

            if (IntervalCount > spawnerAspect.SpawnData.SpawnInterval)
            {

                IntervalCount = 0;

                var ecb = new EntityCommandBuffer(Allocator.TempJob);

                spawnerAspect.GetDisableParticle(this, SpawnedParticle, out var disabled);

                int spawnCountforSec = Mathf.FloorToInt(1f / spawnerAspect.SpawnData.SpawnInterval);
                int spawnAmount = spawnerAspect.SpawnData.SpawnPerSecond / spawnCountforSec;

                //Debug.Log($"S : {SpawnedParticle.Length} / D : {disabled.Length} \n {disabled[0]}");
                //Debug.Log($"Active : {spawnerAspect.GetActiveParticle(this).Length} ,  Spawn Amount for Once : {spawnAmount}");

                var spawnArea = SpawnAreaQuery.ToComponentDataArray<ParticleSpawnAreaComponent>(Allocator.TempJob);

                // ParticleSpawer에 다시 보내기
                ParticleSpawner.instance.SpawnAmount = spawnerAspect.GetActiveParticleCount(this);
                ParticleSpawner.instance.SpawnAmountForSecond = Math.Min(Math.Min(spawnAmount, spawnerAspect.GetSpawnPointAmount(spawnArea)), disabled.Length);

                if (spawnerAspect.EnableParticles(this, ecb, disabled, spawnArea, ParticleManager.ValueRO,
                    (uint)World.Time.ElapsedTime + 1u, out var enableHandler))
                {
                    enableHandler.Schedule(spawnAmount, 8, Dependency).Complete();
                    ecb.Playback(EntityManager);
                }

                disabled.Dispose();
                spawnArea.Dispose();
                ecb.Dispose();
            }
        }

        public void OnChangeMaxSpawnAmount(int preAmount, int amount)
        {
            //Debug.Log($"Changed {preAmount} >> {amount}");

            var AddAmount = amount - preAmount;
            if (AddAmount < 0)
            {
                //Dependency.Complete();

                spawnerAspect.GetDisableParticle(this, SpawnedParticle, out var disabled);
                var active = spawnerAspect.GetActiveParticle(this);
                if (spawnerAspect.SetMaxAmount(amount, SpawnedParticle, disabled))
                {
                    var ECB = new EntityCommandBuffer(Allocator.TempJob);
                    new SpawnerAspect.DestroyJob()
                    {
                        ecb = ECB.AsParallelWriter(),
                        targets = disabled
                    }.Schedule(-AddAmount, 8, Dependency).Complete();

                    ECB.Playback(EntityManager);
                    ECB.Dispose();


                    var sliced = disabled.Slice(-AddAmount);
                    var appended = active.Concat(sliced);// Append 

                    SpawnedParticle.Dispose();
                    SpawnedParticle = new NativeList<Entity>(Allocator.Persistent);
                    SpawnedParticle.AddRange(new NativeArray<Entity>(appended.ToArray(), Allocator.Temp));

                    disabled.Dispose();
                    active.Dispose();
                }
            }
            else if (AddAmount > 0)
            {
                spawnerAspect.GetDisableParticle(this, SpawnedParticle, out var disabled);
                spawnerAspect.SetMaxAmount(ParticleSpawner.instance.MaxAmount, SpawnedParticle, disabled);

                var ECB = new EntityCommandBuffer(Allocator.TempJob);
                spawnerAspect.IntiParticle(ECB)
                    .Schedule(AddAmount, 8, Dependency).Complete();
                ECB.Playback(EntityManager);
                ECB.Dispose();
                disabled.Dispose();
                //여기서 리스트 하면 아직 스폰 안된 상태라서


                var activeParticle = spawnerAspect.GetActiveParticle(this);
                var spawnedArray = SpawnedParticle.ToArray(Allocator.Temp);
                var newEntity = new NativeArray<Entity>(activeParticle.Except(spawnedArray).ToArray(), Allocator.TempJob);

                //Debug.Log($"Added (A - E): {newEntity.Length}  / A : {activeParticle.Length} , L : {SpawnedParticle.Length}");

                ECB = new EntityCommandBuffer(Allocator.TempJob);
                new SpawnerAspect.DisableJob()
                {
                    ecb = ECB.AsParallelWriter(),
                    targets = newEntity
                }.Schedule(newEntity.Length, 8, Dependency).Complete();
                SpawnedParticle.AddRange(newEntity);

                ECB.Playback(EntityManager);
                ECB.Dispose();
                spawnedArray.Dispose();
                newEntity.Dispose();
            }
        }
    }

    public partial struct CalculateSpawnPoint : IJobEntity
    {
        public float particleSize;
        public ParticleSpawnerComponent spawnManager;
        public void Execute([EntityIndexInQuery] int index, ref ParticleSpawnAreaComponent spawnerArea)
        {
            float SpawnBetween = particleSize + spawnManager.SpawnBetweenSpace;
            var temp = spawnerArea.Bound.extents * 2 / SpawnBetween;
            int AmountX = Mathf.Max(Mathf.FloorToInt(temp.x) - 1, 1);
            int AmountY = Mathf.Max(Mathf.FloorToInt(temp.y) - 1, 1);
            int AmountZ = Mathf.Max(Mathf.FloorToInt(temp.z) - 1, 1);

            spawnerArea.SpawnPoints = new int3(AmountX, AmountY, AmountZ);

            Vector3 AreaSize = new((AmountX - 1) * SpawnBetween, (AmountY - 1) * SpawnBetween, (AmountZ - 1) * SpawnBetween);
            spawnerArea.LocalMinPos = -AreaSize * 0.5f;
        }
    }

    public partial struct EnableParticle : IJobParallelFor
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        //[NativeDisableUnsafePtrRestriction] public SpawnerAspect spawnerAspect;//========== 읽기 쓰기 동시에 한다고??

        public LocalTransform transform;
        public ParticleParameterComponent particleParameter;
        public ParticleSpawnerComponent spawner;

        public NativeArray<Entity> disabled;
        [ReadOnly] public NativeArray<ParticleSpawnAreaComponent> spawnArea;
        public void Execute(int index)
        {
            //var Lpos = spawnerAspect.GetSpawnPoint(spawnArea[0], index);
            var Lpos = SpawnerAspect.GetSpawnPoint(transform, spawnArea[0].LocalMinPos,
                particleParameter.ParticleRadius, spawner.SpawnBetweenSpace, spawnArea[0].SpawnPoints, index);

            ecb.SetEnabled(index, disabled[index], true);
            ecb.SetComponentEnabled<FluidSimlationComponent>(index, disabled[index], true);
            ecb.SetComponent(index, disabled[index], new LocalTransform
            {
                Position = Lpos,
                Rotation = Quaternion.identity,
                Scale = particleParameter.ParticleRadius / 0.5f
            });
            ecb.SetComponent(index, disabled[index], new FluidSimlationComponent() { position = Lpos });
        }
    }
}
