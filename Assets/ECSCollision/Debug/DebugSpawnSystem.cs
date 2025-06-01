using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;
using Unity.VisualScripting;
using static UnityEngine.EventSystems.EventTrigger;
using System.Linq;
using System.Text;
using static UnityEngine.ParticleSystem;

namespace EcsCollision
{
    public partial class DebugSpawnSystem : SystemBase
    {
        BeginInitializationEntityCommandBufferSystem IntiECB;

        bool IsSpawn = false;
        bool DoOnceEnable = false;

        NativeList<Entity> SpawnedParticle;

        DebugSpawnerAspect spawnerAspect;

        protected override void OnCreate()
        {
            base.OnCreate();
            IntiECB = World.GetOrCreateSystemManaged<BeginInitializationEntityCommandBufferSystem>();

        }
        protected override void OnStartRunning()
        {
            base.OnStartRunning();

        }
        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            if (SpawnedParticle.IsCreated)
                SpawnedParticle.Dispose();
        }
        protected override void OnUpdate()
        {
            if (SystemAPI.TryGetSingletonEntity<DebugSpawnerComponent>(out var spanwerEntity))
            {
                var manager = SystemAPI.GetSingleton<DebugSpawnerComponent>();
                spawnerAspect = SystemAPI.GetAspect<DebugSpawnerAspect>(spanwerEntity);

                if (!IsSpawn)
                {
                    IsSpawn = true;

                    Debug.LogWarning("스폰할 엔티티가 이미 존재하면 프레임 드랍 + 안보임 (랜더링만 안됨)");

                    var ecb = new EntityCommandBuffer(Allocator.TempJob);
                    // 스폰할 엔티티가 이미 존재하면 프레임 드랍 + 스폰 안됨
                    //spawnerAspect.SpawnParticle(ecb, 4632u).Schedule(Dependency).Complete();
                    //IntiECB.AddJobHandleForProducer(Dependency);

                    new DebugSpawnerAspect.SpawnJob()
                    {
                        ecb = ecb.AsParallelWriter(),
                        particle = spawnerAspect.Particle,
                        randomSeed = 23424u,
                        spawnComponent = spawnerAspect.SpawnData,
                        transform = spawnerAspect.Transform,
                    }.Schedule(Dependency).Complete();

                    ecb.Playback(EntityManager);
                    ecb.Dispose();

                    return;
                }

                using var particle = GetEntityQuery(typeof(FluidSimlationComponent), typeof(LocalTransform)).ToEntityArray(Allocator.TempJob);
                //spawnerAspect.GetActiveParticle(this);

                if (!SpawnedParticle.IsCreated && IsSpawn)
                {
                    SpawnedParticle = new NativeList<Entity>(manager.Amount, Allocator.Persistent);
                    SpawnedParticle.AddRange(particle);
                }

                if (Input.GetMouseButton(0))
                {
                    if (!DoOnceEnable)
                    {
                        var ecb = new EntityCommandBuffer(Allocator.TempJob);
                        DoOnceEnable = true;

                        spawnerAspect.EnableParticles(this, ecb, SpawnedParticle, (uint)World.Time.ElapsedTime, -1, 8);
                        ecb.Playback(EntityManager);
                        ecb.Dispose();
                    }

                    //DoOnceEnable = true;//작업이 끝난후 적용
                }
                else
                {
                    DoOnceEnable = false;
                }
            }
            else
            {
                Enabled = false;
            }
        }
    }

}