using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Samples.Boids;
using Unity.Physics;

namespace EcsCollision
{
    public partial class HashedFluidSimlationSystem : SystemBase
    {
        private static readonly int[] cellOffsetTable =
        {
            1, 1, 1, 1, 1, 0, 1, 1, -1, 1, 0, 1, 1, 0, 0, 1, 0, -1, 1, -1, 1, 1, -1, 0, 1, -1, -1,
            0, 1, 1, 0, 1, 0, 0, 1, -1, 0, 0, 1, 0, 0, 0, 0, 0, -1, 0, -1, 1, 0, -1, 0, 0, -1, -1,
            -1, 1, 1, -1, 1, 0, -1, 1, -1, -1, 0, 1, -1, 0, 0, -1, 0, -1, -1, -1, 1, -1, -1, 0, -1, -1, -1
        };

        #region Job

        [BurstCompile]
        partial struct PositionSetup : IJobEntity
        {
            public void Execute([EntityIndexInQuery] int index, in LocalTransform transform, ref FluidSimlationComponent data)
            {
                data.position = transform.Position;
            }
        }//처음에 스폰된 위치 적용

        [BurstCompile]
        private struct HashPositions : IJobParallelFor
        {
            //#pragma warning disable 0649
            [ReadOnly] public float cellRadius;

            //public NativeArray<LocalTransform> positions;
            [ReadOnly] public NativeArray<FluidSimlationComponent> particleData;

            public NativeParallelMultiHashMap<int, int>.ParallelWriter hashMap;
            //#pragma warning restore 0649

            public void Execute(int index)
            {
                float3 position = particleData[index].position;
                    //positions[index].Position;

                int hash = GridHash.Hash(position, cellRadius);
                hashMap.Add(hash, index);

                //positions[index] = new LocalTransform { Position = position, Rotation = quaternion.identity, Scale = 1 };
            }
        }
        [BurstCompile]
        private struct MergeParticles : Samples.Boids.IJobNativeMultiHashMapMergedSharedKeyIndices
        {
            public NativeArray<int> particleIndices;

            //Merge : 병합 
            // 키가 생길때
            public void ExecuteFirst(int index)
            {
                particleIndices[index] = index;
            }
            // 키가 있을때 , cellIndex == firstIndex
            public void ExecuteNext(int cellIndex, int index)
            {
                particleIndices[index] = cellIndex;
            }

            //#pragma warning restore 0649
            //FIXME - 
        }//딕셔러리에 처음 삽입 OR 삽입 될때

        [BurstCompile]
        partial struct ResetAcc : IJobEntity
        {
            [WriteOnly] public NativeArray<FluidSimlationComponent> particleData;
            public ParticleParameterComponent parameter;
            public Vector3 AccVaule;

            public void Execute([EntityIndexInQuery] int index, in FluidSimlationComponent data)
            {
                var temp = data;
                temp.acc = parameter.Gravity + AccVaule;

                particleData[index] = temp;
                //FluidSimlationComponent값을 계산 끝나고 적용되니
            }
        }//Acc 초기화

        [BurstCompile]
        private struct ComputePressure : IJobParallelFor
        {
            [ReadOnly] public NativeParallelMultiHashMap<int, int> hashMap;
            [ReadOnly] public NativeArray<int> cellOffsetTable;
            [ReadOnly] public NativeArray<FluidSimlationComponent> particleData;

            [ReadOnly] public ParticleParameterComponent parameter;
            public float DT;

            public NativeArray<Vector3> pressureDir;
            public NativeArray<Vector3> pressureVel;
            public NativeArray<float> moveRes;

            public void Execute(int index)
            {
                // Cache
                //int particleCount = particlesPosition.Length;
                var position = particleData[index].position;
                //float density = 0.0f;
                int i, hash, j;
                int3 gridOffset;
                int3 gridPosition = GridHash.Quantize(position, parameter.ParticleRadius);
                bool found;

                int collisionCount = 0;
                pressureVel[index] = Vector3.zero;

                // Find neighbors
                for (int oi = 0; oi < 27; oi++)
                {
                    i = oi * 3;
                    gridOffset = new int3(cellOffsetTable[i], cellOffsetTable[i + 1], cellOffsetTable[i + 2]);
                    hash = GridHash.Hash(gridPosition + gridOffset);
                    NativeParallelMultiHashMapIterator<int> iterator;
                    found = hashMap.TryGetFirstValue(hash, out j, out iterator);

                    while (found)
                    {
                        collisionCount++;

                        // Neighbor found, get density
                        var rij = particleData[j].position - position;//position - particleData[j].position;
                        float r2 = math.lengthsq(rij);

                        float r = parameter.ParticleRadius + parameter.SmoothRadius;
                        if (r2 < 4 * r * r)
                        {
                            //density += settings.mass * (315.0f / (64.0f * PI * math.pow(settings.smoothingRadius, 9.0f)))
                            //  * math.pow(settings.smoothingRadiusSq - r2, 3.0f);

                            pressureDir[index] += rij;

                            if (float.IsNaN(particleData[j].velocity.x) || float.IsNaN(particleData[j].velocity.y) || float.IsNaN(particleData[j].velocity.z))
                            {

                            }else
                            {
                                pressureVel[index] += particleData[j].velocity;
                            }

                            moveRes[index] += Mathf.Clamp01(Vector3.Dot(rij.normalized, (particleData[index].velocity + particleData[index].acc * DT)));
                        }

                        // Next neighbor
                        found = hashMap.TryGetNextValue(out j, ref iterator);
                    }
                }

                //Debug.Log($"{index} : {pressureVel[index]} / {collisionCount}");

                if (collisionCount > 1)
                    pressureVel[index] /= collisionCount;
                else
                    pressureVel[index] = Vector3.zero;

            }
        }

        [BurstCompile]
        struct ComputeFloorCollision : IJobParallelFor
        {
            public NativeArray<FluidSimlationComponent> particleData;
            public NativeArray<Entity> particleEntity;

            public ParticleParameterComponent parameter;

            public NativeArray<LocalTransform> collisionTransform;
            public NativeArray<CollisionComponent> collisions;

            public EntityCommandBuffer.ParallelWriter ecb;

            public void Execute(int index)
            {
                var particle = particleData[index];//

                switch (parameter.floorType)
                {
                    case FloorType.Collision:
                        {
                            //var particle = particleData[index];//

                            if (particleData[index].position.y <= parameter.floorHeight + parameter.ParticleRadius)
                            {
                                if (particleData[index].isGround == false)
                                {
                                    particle.velocity = Vector3.Reflect(particle.velocity, Vector3.up)
                                        * (1 - parameter.ParticleViscosity);
                                    //----------------------------------------------- 반사된 방향을 다시 반사해서 바로 멈추나?
                                }
                                var AccSpeed = particle.acc.magnitude;
                                particle.acc.y = 0;
                                particle.acc = particle.acc.normalized * AccSpeed;

                                particle.isGround = true;
                                // 아래로 내려가지 않도록 y 방향 제거
                            }
                            else
                            {
                                particle.isGround = false;
                            }

                            particleData[index] = particle;
                            break;
                        }
                    case FloorType.Disable:
                        {
                            if (particleData[index].position.y <= parameter.floorHeight + parameter.ParticleRadius)
                            {
                                //ecb.SetEnabled(index, particleEntity[index], false);
                                ecb.SetComponentEnabled<FluidSimlationComponent>(index, particleEntity[index], false);
                            }
                            break;
                        }
                    case FloorType.Kill:
                        {
                            if (particleData[index].position.y <= parameter.floorHeight + parameter.ParticleRadius)
                            {
                                ecb.DestroyEntity(index, particleEntity[index]);
                            }
                            break;
                        }
                    case FloorType.None:
                    default:
                        return;
                }
                
            }
        }

        [BurstCompile]
        struct ComputeObstacleCollision : IJobParallelFor
        {
            public NativeArray<FluidSimlationComponent> particleData;
            public NativeArray<Entity> particleEntity;

            [ReadOnly]  public NativeArray<LocalTransform> collisionTransform;
            [ReadOnly]  public NativeArray<CollisionComponent> collisions;

            public ParticleParameterComponent parameter;

            public EntityCommandBuffer.ParallelWriter ecb;

            public void Execute(int index)
            {
                var particle = particleData[index];

                for (int i = 0; i < collisions.Length; i++)
                {
                    float3 offset = particle.position;
                    offset -= collisionTransform[i].Position;
                    offset += parameter.ParticleRadius;

                    if (Vector3.SqrMagnitude(offset) > Vector3.SqrMagnitude(collisions[i].WorldSize * 0.5f))
                    {
                        continue;
                    }

                    Vector3 dir = Vector3.zero;
                    var IsCollision = collisions[i].IsCollisionFormSphere(collisionTransform[i],
                            collisions[i].WorldSize * 0.5f, particle.position, parameter.ParticleRadius, out dir);

                    if (IsCollision)
                    {
                        if (collisions[i].colliderEvent != ColliderEvent.AccelerationTrigger)
                        {
                            particle.force += (1 - parameter.ParticleViscosity) * parameter.ParticlePush * dir;
                            particle.velocity += dir;
                        }

                        switch (collisions[i].colliderEvent)
                        {
                            case ColliderEvent.Collision:
                                break;
                            case ColliderEvent.DisableTrigger:
                                ecb.SetEnabled(index, particleEntity[index], false);
                                ecb.SetComponentEnabled<FluidSimlationComponent>(index, particleEntity[index], false);
                                break;
                            case ColliderEvent.KillTrigger:
                                ecb.DestroyEntity(index, particleEntity[index]);
                                break;
                            case ColliderEvent.AccelerationTrigger:
                                particle.acc += collisions[i].AccVelocity;
                                break;
                        }
                    }
                }

                particleData[index] = particle;

            }
        }

        [BurstCompile]
        struct ComputeCollision : IJobParallelFor
        {
            public NativeArray<FluidSimlationComponent> particleData;
            public NativeArray<Vector3> pressureDir;
            public NativeArray<Vector3> pressureVel;
            public NativeArray<float> moveRes;
            public float Amount;

            public ParticleParameterComponent parameter;
            public float DT;

            public void Execute(int index)
            {
                {
                    var temp = particleData[index];

                    if (Mathf.Approximately(pressureDir[index].sqrMagnitude, 0))
                    {
                        if (particleData[index].isGround)
                        {
                            temp.velocity *= 1 - (parameter.ParticleViscosity * DT);
                        }
                    }
                    else
                    {
                        float CollisionAcc = Mathf.Max(1 - parameter.ParticleDrag, 0);
                        if (particleData[index].isGround)
                        {
                            if (moveRes[index] >= 0)
                            {
                                var reflected = Vector3.Reflect(temp.velocity, pressureDir[index].normalized) * CollisionAcc;
                                reflected.y = Mathf.Max(reflected.y, 0);

                                temp.velocity = reflected.normalized * reflected.magnitude;
                            }
                            else
                            {
                                var reflected = Vector3.Reflect(-temp.velocity, pressureDir[index].normalized) * CollisionAcc;
                                reflected.y = Mathf.Max(reflected.y, 0);

                                temp.velocity = reflected.normalized * reflected.magnitude;
                            }
                        }
                        else
                        {
                            if (Mathf.Abs(moveRes[index]) > 0.1f)//없으면 계속 튀김
                            {
                                var reflected = Vector3.Reflect(temp.velocity.normalized, pressureDir[index].normalized)
                                    * ((temp.velocity + temp.acc).magnitude * (1 - parameter.ParticleDrag) * parameter.ParticlePush);

                                temp.velocity = (temp.velocity * parameter.SimulateLiquid) + (reflected * (1 - parameter.SimulateLiquid));

                                temp.position -= pressureDir[index] * DT * parameter.ParticlePush * 10;
                            }
                        }
                    }

                    particleData[index] = temp;

                }//
            }
        }

        [BurstCompile]
        partial struct AddPosition : IJobEntity
        {
            public NativeArray<FluidSimlationComponent> particleData;
            public ParticleParameterComponent parameter;
            public NativeArray<Vector3> pressureVel;

            public float DT;

            public void Execute([EntityIndexInQuery] int index, ref FluidSimlationComponent data)//, in LocalTransform transform
            {
                var acc = particleData[index].acc;

                if (particleData[index].isGround)
                {
                    //data.acc -= parameter.Gravity; //=========== 계속 Acc가 쌓임
                    acc -= parameter.Gravity;
                }
                data.velocity = particleData[index].velocity + acc * DT;
                //if ()

                if (float.IsNaN(particleData[index].velocity.x) || float.IsNaN(particleData[index].velocity.y) || float.IsNaN(particleData[index].velocity.z))
                {
                    //이동을 파업했어....
                }
                else
                {
                    
                    var force = particleData[index].force;
                    if (force.sqrMagnitude > 0.0001f)
                    {
                        
                        if (math.dot(particleData[index].velocity.normalized, force.normalized) < 0)
                        {
                            force = math.reflect(particleData[index].velocity, force.normalized);
                            data.velocity = force * (1 - parameter.ParticleViscosity);//잘못된 방향으로 들어가기도 해서

                        }
                        else
                        {
                            force = force.normalized * particleData[index].velocity.magnitude;
                            data.velocity = force * (1 - parameter.ParticleViscosity);//안정적인데 , 전체적으로 느려지고 굴러 내려가는것도 느려져서
                        }
                    }
                    else
                    {
                        force = particleData[index].velocity;
                    }

                    if (! Mathf.Approximately(pressureVel[index].sqrMagnitude, 0))
                    {
                        Custom.Math.CollisionSphereReflect(force, pressureVel[index], out var vec0, out var vec1);

                        if ((force - pressureVel[index]).sqrMagnitude < (vec0 - vec1).sqrMagnitude)
                        {
                            force = vec0;
                            data.velocity = force * (1 - parameter.ParticleViscosity);
                        }

                    }

                    data.position = particleData[index].position + force * DT;
                }

                data.acc = Vector3.zero;
                data.force = Vector3.zero;
                data.isGround = particleData[index].isGround;

            }
        }
        [BurstCompile]
        partial struct ApplyPosition : IJobEntity
        {
            public float size;
            public void Execute([EntityIndexInQuery] int index, ref LocalTransform transform, in FluidSimlationComponent data)
            {
                transform.Position = data.position;
                transform.Scale = size;
            }
        }

        #endregion

        private EntityQuery ParticleGroup;
        private EntityQuery ObstacleGroup;

        ParticleParameterComponent Parameter;
        JobHandle PositionSetupHandle;
        bool isReady = false;
        float timer = 0;

        int DebuggingIndex = 1;

        protected override void OnCreate()
        {
            ParticleGroup = GetEntityQuery(typeof(FluidSimlationComponent), typeof(LocalTransform));
            ObstacleGroup = GetEntityQuery(typeof(CollisionComponent), typeof(LocalTransform));
        }
        protected override void OnStartRunning()
        {
            if (SystemAPI.HasSingleton<ParticleParameterComponent>())
                Parameter = SystemAPI.GetSingleton<ParticleParameterComponent>();
            else
                Enabled = false;

            isReady = false;
            
        }

        protected override void OnUpdate()
        {
            if (isReady == false)
            {
                PositionSetup PositionSetupJob = new PositionSetup
                {
                    //particleData = ParticleGroup.ToComponentDataArray<FluidSimlationComponent>(Allocator.TempJob)
                };
                PositionSetupHandle = PositionSetupJob.ScheduleParallel(ParticleGroup, Dependency);
                PositionSetupHandle.Complete();

                var tempData = ParticleGroup.ToComponentDataArray<FluidSimlationComponent>(Allocator.Temp);
                if (tempData.Length <= 0)
                {
                    tempData.Dispose();
                    return;
                }

                isReady = true;


                tempData.Dispose();
                return;
            }// 스폰된 위치 정보를 FluidSimlationComponent 에게 줌

            if (timer > SystemAPI.Time.DeltaTime)
            {
                timer = 0;
                return;
            }
            else
            {
                timer += SystemAPI.Time.DeltaTime;
            }

            #region 초기화

            Parameter = SystemAPI.GetSingleton<ParticleParameterComponent>();
            NativeArray<FluidSimlationComponent> particleData =
                ParticleGroup.ToComponentDataArray<FluidSimlationComponent>(Allocator.TempJob);
            var particleEntity = ParticleGroup.ToEntityArray(Allocator.TempJob);

            int particleCount = particleData.Length;

            NativeParallelMultiHashMap<int, int> hashMap = new NativeParallelMultiHashMap<int, int>(particleCount, Allocator.TempJob);

            var particleDir = new NativeArray<Vector3>(particleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var particleVel = new NativeArray<Vector3>(particleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var particleMoveRes = new NativeArray<float>(particleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            ///NativeArray<int> particleIndices = new NativeArray<int>(particleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<int> cellOffsetTableNative = new NativeArray<int>(cellOffsetTable, Allocator.TempJob);

            var obstacleTransform = ObstacleGroup.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            var obstacleTypeData = ObstacleGroup.ToComponentDataArray<CollisionComponent>(Allocator.TempJob);

            
            var floorECB = new EntityCommandBuffer(Allocator.TempJob);
            var collisionECB = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(EntityManager.WorldUnmanaged);//new EntityCommandBuffer(Allocator.TempJob);

            #endregion

            #region 설정

            var particleDirJob = new MemsetNativeArray<Vector3> { Source = particleDir, Value = Vector3.zero };
            JobHandle particleDirJobHandle = particleDirJob.Schedule(particleCount, 64);
            var particleMoveResJob = new MemsetNativeArray<float> { Source = particleMoveRes, Value = 0 };
            JobHandle particleMoveResJobHandle = particleMoveResJob.Schedule(particleCount, 64);

            JobHandle SetupMergedHandle = JobHandle.CombineDependencies(PositionSetupHandle, particleDirJobHandle, particleMoveResJobHandle);

            ///MemsetNativeArray<int> particleIndicesJob = new MemsetNativeArray<int> { Source = particleIndices, Value = 0 };
            ///JobHandle particleIndicesJobHandle = particleIndicesJob.Schedule(particleCount, 64);
            //----------> particleIndices : 해당영역에 첫번째 파티클 / 딱히 쓰는데 없는데

            //-----

            
            ResetAcc ResetAccJob = new ResetAcc
            {
                particleData = particleData,
                parameter = Parameter,
                AccVaule = Vector3.zero
            };
            JobHandle ResetAccHandle = ResetAccJob.ScheduleParallel(ParticleGroup, SetupMergedHandle);

            // Put positions into a hashMap
            HashPositions hashPositionsJob = new HashPositions
            {
                //positions = particlesPosition,
                particleData = particleData,
                hashMap = hashMap.AsParallelWriter(),
                cellRadius = Parameter.ParticleRadius
            };

            //particlePosition 이 완료되고 실행
            JobHandle hashPositionsJobHandle = hashPositionsJob.Schedule(particleCount, 64, ResetAccHandle);
            //이걸쓰는 job이 hashPositionJob 과 particleIndicesJob 끝나야 실행되게
            ///JobHandle mergedPositionIndicesJobHandle = JobHandle.CombineDependencies(hashPositionsJobHandle, particleIndicesJobHandle);

            ///MergeParticles mergeParticlesJob = new MergeParticles
            ///{
            ///     particleIndices = particleIndices
            ///};

            //이 작업의 목적은 각 입자에 hashMap 버킷의 ID를 부여하는 것입니다.
            ///JobHandle mergeParticlesJobHandle = mergeParticlesJob.Schedule(hashMap, 64, mergedPositionIndicesJobHandle);
            ///mergeParticlesJobHandle.Complete();

            // 입자간 충돌 사전 작업완료
            #endregion

            #region Calculation Job
            //computePressureJob + computeDensityPressureJob

            ComputePressure computePressureJob = new ComputePressure
            {
                hashMap = hashMap,
                cellOffsetTable = cellOffsetTableNative,
                particleData = particleData,
                parameter = Parameter,
                DT = SystemAPI.Time.DeltaTime,
                pressureDir = particleDir,
                pressureVel = particleVel,
                moveRes = particleMoveRes
            };
            JobHandle computePressureJobHandle = computePressureJob.Schedule(particleCount, 64, hashPositionsJobHandle);// mergeParticlesJobHandle);

            ComputeFloorCollision FloorCollisionJob = new ComputeFloorCollision
            {
                particleData = particleData,
                parameter = Parameter,

                collisions = obstacleTypeData,
                collisionTransform = obstacleTransform,

                particleEntity = particleEntity,
                ecb = floorECB.AsParallelWriter()
            };
            JobHandle FloorCollisionHandle = FloorCollisionJob.Schedule(particleCount, 64, computePressureJobHandle);

            var groundCollision = new ComputeObstacleCollision
            {
                parameter = Parameter,
                particleData = particleData,

                collisions = obstacleTypeData,
                collisionTransform = obstacleTransform,

                particleEntity = particleEntity,
                ecb = collisionECB.AsParallelWriter()
            };
            JobHandle ObstacleCollisionHandle = groundCollision.Schedule(particleCount, 64, FloorCollisionHandle);


            ComputeCollision ComputeCollisionJob = new ComputeCollision
            {
                particleData = particleData,
                pressureDir = particleDir,
                pressureVel = particleVel,
                moveRes = particleMoveRes,
                Amount = particleCount,
                parameter = Parameter,
                DT = SystemAPI.Time.DeltaTime
            };
            JobHandle ComputeCollisionHandle = ComputeCollisionJob.Schedule(particleCount, 64, ObstacleCollisionHandle);
            //ComputeCollisionHandle.Complete();

            Debugging(particleData, "ComputeCollisionJob");//=================

            AddPosition AddPositionJob = new AddPosition
            {
                particleData = particleData,
                pressureVel = particleVel,
                parameter = Parameter,
                DT = SystemAPI.Time.DeltaTime
            };
            JobHandle AddPositionHandle = AddPositionJob.ScheduleParallel(ParticleGroup, ComputeCollisionHandle);
            AddPositionHandle.Complete();// ------ 없으면 에러

            Debugging(particleData, "AddPositionJob");

            ApplyPosition ApplyPositionJob = new() 
            {
                size = Parameter.ParticleRadius / 0.5f
            };
            //JobHandle ApplyPositionHandle = ApplyPositionJob.ScheduleParallel(ParticleGroup, AddPositionHandle);
            ApplyPositionJob.ScheduleParallel(ParticleGroup);

            #endregion

            //Dependency = AddPositionHandle;

            {
                particleData.Dispose();
                particleDir.Dispose();
                particleVel.Dispose();
                particleMoveRes.Dispose();
                particleEntity.Dispose();

                obstacleTransform.Dispose();
                obstacleTypeData.Dispose();

                hashMap.Dispose();
                //particleIndices.Dispose();
                cellOffsetTableNative.Dispose();

                floorECB.Playback(EntityManager);
                floorECB.Dispose();
                //collisionECB.Playback(EntityManager);
                //collisionECB.Dispose();
            }
        }

        public void Debugging(NativeArray<FluidSimlationComponent> ParameterData , string comment)
        {
            //DebuggingIndex
            //ParameterData[0].position
            if (DebuggingIndex < ParameterData.Length)
            {
                //Debug.Log(DebuggingIndex + " | " + comment + " : Pos :" + ParameterData[DebuggingIndex].position
                //    + " / velo :" +  ParameterData[DebuggingIndex].velocity + " / Is Ground : " + ParameterData[DebuggingIndex].isGround
                //     + " / velo sqrLength : " + ParameterData[DebuggingIndex].velocity.sqrMagnitude);
            }
        }
    }
}

