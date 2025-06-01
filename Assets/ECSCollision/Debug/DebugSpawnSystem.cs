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

                    Debug.LogWarning("������ ��ƼƼ�� �̹� �����ϸ� ������ ��� + �Ⱥ��� (�������� �ȵ�)");

                    var ecb = new EntityCommandBuffer(Allocator.TempJob);
                    // ������ ��ƼƼ�� �̹� �����ϸ� ������ ��� + ���� �ȵ�
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

                    //DoOnceEnable = true;//�۾��� ������ ����
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