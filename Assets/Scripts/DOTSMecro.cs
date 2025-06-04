using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DOTS
{
    public static class DOTSMecro
    {
        #region  GetEntityQuery
        /// <summary>
        /// No Generate GC
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="entityManager"></param>
        /// <returns></returns>
        public static EntityQuery GetEntityQuery<T, V, K>(EntityManager entityManager)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp);
            var query = builder.WithAll<T, V, K>().Build(entityManager);
            builder.Dispose();
            return query;
        }
        /// <summary>
        /// No Generate GC
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="entityManager"></param>
        /// <returns></returns>
        public static EntityQuery GetEntityQuery<T, V>(EntityManager entityManager)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp);
            var query = builder.WithAll<T, V>().Build(entityManager);
            builder.Dispose();
            return query;
        }
        /// <summary>
        /// No Generate GC
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entityManager"></param>
        /// <returns></returns>
        public static EntityQuery GetEntityQuery<T>(EntityManager entityManager)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp);
            var query = builder.WithAll<T>().Build(entityManager);
            builder.Dispose();
            return query;
        }
        #endregion



        #region Disabled
        /// <summary>
        /// 쿼리 순서대로 결과 리턴
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entityManager"></param>
        /// <param name="query"></param>
        /// <param name="Job"></param>
        /// <param name="chunks">Use EntityQuery.ToArchetypeChunkArray()</param>
        /// <param name="Result"></param>
        [System.Obsolete]
        public static NativeArray<T> GetSharedComponentDataArray<T>(EntityManager entityManager,
            NativeArray<ArchetypeChunk> chunks, out GetSharedComponentsJob<T> Job)
         where T : unmanaged, ISharedComponentData
        {
            int amount = chunks.Length;//query.CalculateEntityCount();
            var sharedComponentTypeHandle = entityManager.GetSharedComponentTypeHandle<T>();
            var Value = new NativeArray<T>(amount, Allocator.TempJob);
            Job = new GetSharedComponentsJob<T>
            {
                Chunks = chunks.AsReadOnly(),
                typeHandle = sharedComponentTypeHandle,
                values = Value
            };
            return Value;
        }
        [System.Obsolete]
        public static NativeArray<T> GetSharedComponentDataArray<T>(EntityManager entityManager,
            NativeArray<ArchetypeChunk> chunks, JobHandle handle)
         where T : unmanaged, ISharedComponentData
        {
            int amount = chunks.Length;//query.CalculateEntityCount();
            var sharedComponentTypeHandle = entityManager.GetSharedComponentTypeHandle<T>();
            var Value = new NativeArray<T>(amount, Allocator.TempJob);
            new GetSharedComponentsJob<T>
            {
                Chunks = chunks.AsReadOnly(),
                typeHandle = sharedComponentTypeHandle,
                values = Value
            }.Schedule(chunks.Length, JobsUtility.MaxJobThreadCount, handle).Complete();
            return Value;
        }

        [BurstCompile]
        [System.Obsolete]
        public struct GetSharedComponentsJob<T> : IJobParallelFor where T : unmanaged, ISharedComponentData
        {
            public NativeArray<ArchetypeChunk>.ReadOnly Chunks;
            public SharedComponentTypeHandle<T> typeHandle;

            [WriteOnly] public NativeArray<T> values;

            public void Execute(int index)
            {
                values[index] = Chunks[index].GetSharedComponent<T>(typeHandle);
            }
        }
        [System.Obsolete]
        public static NativeList<int> FindItemsInQuery<T>(EntityManager entityManager,
            NativeArray<ArchetypeChunk> chunks, NativeParallelHashSet<T> items, JobHandle depency)
            where T : unmanaged, ISharedComponentData, IEquatable<T>
        {
            int amount = items.Count();//chunks.Length
            var sharedComponentTypeHandle = entityManager.GetSharedComponentTypeHandle<T>();
            var Value = new NativeList<int>(amount, Allocator.TempJob);

            new FindSharedComponentJob<T>
            {
                Chunks = chunks.AsReadOnly(),
                typeHandle = sharedComponentTypeHandle,
                items = items.AsReadOnly(),
                values = Value.AsParallelWriter()
            }.Schedule(chunks.Length, JobsUtility.MaxJobThreadCount, depency).Complete();

            return Value;
        }
        /// <summary>
        /// 쿼리중 공유 컴포넌트인 특정 대상을 찾아 쿼리 인덱스로 리턴
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="chunks">Use EntityQuery.ToArchetypeChunkArray()</param>
        /// <returns></returns>
        [System.Obsolete]
        public static NativeList<int> FindItemsInQuery<T>(EntityManager entityManager,
            NativeArray<ArchetypeChunk> chunks, NativeParallelHashSet<T> items, out FindSharedComponentJob<T> job)
            where T : unmanaged, ISharedComponentData, IEquatable<T>
        {
            int amount = items.Count();//chunks.Length
            var sharedComponentTypeHandle = entityManager.GetSharedComponentTypeHandle<T>();
            var Value = new NativeList<int>(amount, Allocator.TempJob);

            job = new FindSharedComponentJob<T>
            {
                Chunks = chunks.AsReadOnly(),
                typeHandle = sharedComponentTypeHandle,
                items = items.AsReadOnly(),
                values = Value.AsParallelWriter()
            };

            return Value;
        }

        [BurstCompile]
        [System.Obsolete]
        public struct FindSharedComponentJob<T> : IJobParallelFor where T : unmanaged, ISharedComponentData, IEquatable<T>
        {
            public NativeArray<ArchetypeChunk>.ReadOnly Chunks;
            public SharedComponentTypeHandle<T> typeHandle;
            public NativeParallelHashSet<T>.ReadOnly items;
            public NativeList<int>.ParallelWriter values;
            public void Execute(int index)
            {
                var element = Chunks[index].GetSharedComponent<T>(typeHandle);

                if (items.Contains(element))
                {
                    values.AddNoResize(index);
                }
            }
        }

        [System.Obsolete]
        public static NativeList<int> FindItemsInQuery<T>(
            NativeArray<T> QueryItem, NativeParallelHashSet<T> item, JobHandle depency)
             where T : unmanaged, IComponentData, IEquatable<T>
        {
            var Value = new NativeList<int>(QueryItem.Length, Allocator.TempJob);
            new FindJob<T>
            {
                queryItem = QueryItem.AsReadOnly(),
                items = item.AsReadOnly(),
                values = Value.AsParallelWriter()
            }.Schedule(QueryItem.Length, JobsUtility.MaxJobThreadCount, depency).Complete();

            return Value;
        }
        [System.Obsolete]
        public struct FindJob<T> : IJobParallelFor where T : unmanaged, IComponentData, IEquatable<T>
        {
            public NativeArray<T>.ReadOnly queryItem;
            public NativeParallelHashSet<T>.ReadOnly items;
            public NativeList<int>.ParallelWriter values;
            public void Execute(int index)
            {
                if (items.Contains(queryItem[index]))
                {
                    values.AddNoResize(index);
                }
            }
        }

        [System.Obsolete]
        public static NativeList<int> FindExceptItem<T>(EntityManager entityManager, int QuerAmount,
            NativeArray<ArchetypeChunk> chunks, NativeParallelHashSet<T> items, out FindExceptSharedComponentJob<T> Job)
            where T : unmanaged, ISharedComponentData, IEquatable<T>
        {
            int amount = QuerAmount - items.Count();//chunks.Length
            var sharedComponentTypeHandle = entityManager.GetSharedComponentTypeHandle<T>();
            var Value = new NativeList<int>(amount, Allocator.TempJob);

            Job = new FindExceptSharedComponentJob<T>
            {
                Chunks = chunks.AsReadOnly(),
                typeHandle = sharedComponentTypeHandle,
                items = items.AsReadOnly(),
                values = Value.AsParallelWriter()
            };

            return Value;
        }
        [BurstCompile]
        [System.Obsolete]
        public struct FindExceptSharedComponentJob<T> : IJobParallelFor where T : unmanaged, ISharedComponentData, IEquatable<T>
        {
            public NativeArray<ArchetypeChunk>.ReadOnly Chunks;
            public SharedComponentTypeHandle<T> typeHandle;
            public NativeParallelHashSet<T>.ReadOnly items;
            public NativeList<int>.ParallelWriter values;
            public void Execute(int index)
            {
                var element = Chunks[index].GetSharedComponent<T>(typeHandle);

                if (items.Contains(element) == false)
                {
                    values.AddNoResize(index);
                }
            }
        }
        #endregion FindSharedComponent

        #region Query
        public static NativeList<int> FindExceptItem<T>(EntityQuery query, in NativeArray<Entity> queryEntities, T target) where T : unmanaged, ISharedComponentData
        {
            query.SetSharedComponentFilter(target);
            using var FindUnit = query.ToEntityArray(Allocator.TempJob);
            query.ResetFilter();

            int TartgetAmount = queryEntities.Length - FindUnit.Length;
            using var FindEntitySet = DOTSMecro.ConvetHashSet(FindUnit);
            var TargetUnitIndex = new NativeList<int>(TartgetAmount, Allocator.TempJob);

            new GetExceptUnit
            {
                Finder = FindEntitySet.AsReadOnly(),
                unitEntity = queryEntities.AsReadOnly(),
                TargetEntity = TargetUnitIndex.AsParallelWriter()
            }.Schedule(queryEntities.Length, JobsUtility.MaxJobThreadCount).Complete();

            return TargetUnitIndex;
        }
        public static NativeList<int> FindExceptItem<T>(EntityQuery query, in NativeArray<Entity> queryEntities, T target, out GetExceptUnit job)
             where T : unmanaged, ISharedComponentData
        {
            query.SetSharedComponentFilter(target);
            using var FindUnit = query.ToEntityArray(Allocator.TempJob);
            query.ResetFilter();

            int TartgetAmount = queryEntities.Length - FindUnit.Length;
            using var FindEntitySet = DOTSMecro.ConvetHashSet(FindUnit);
            var TargetUnitIndex = new NativeList<int>(TartgetAmount, Allocator.TempJob);

            job = new GetExceptUnit
            {
                Finder = FindEntitySet.AsReadOnly(),
                unitEntity = queryEntities.AsReadOnly(),
                TargetEntity = TargetUnitIndex.AsParallelWriter()
            };

            return TargetUnitIndex;
        }
        public struct GetExceptUnit : IJobParallelFor
        {
            public NativeParallelHashSet<Entity>.ReadOnly Finder;
            public NativeArray<Entity>.ReadOnly unitEntity;

            public NativeList<int>.ParallelWriter TargetEntity;
            public void Execute(int index)
            {
                if (Finder.Contains(unitEntity[index]) == false)
                {
                    TargetEntity.AddNoResize(index);
                }
            }
        }
        #endregion Query

        #region Convert
        public static NativeParallelHashSet<T> ConvetHashSet<T>(NativeArray<T> values, JobHandle Depency, out ConverHashSetJob<T> convertJob)
            where T : unmanaged, IEquatable<T>
        {

            using NativeParallelHashSet<T> writer = new(values.Length, Allocator.TempJob);
            convertJob = new ConverHashSetJob<T>
            {
                Values = values.AsReadOnly(),
                writer = writer.AsParallelWriter()
            };
            return writer;
        }
        public static NativeParallelHashSet<T> ConvetHashSet<T>(NativeArray<T> values) where T : unmanaged, IEquatable<T>
        {

            NativeParallelHashSet<T> writer = new(values.Length, Allocator.TempJob);
            new ConverHashSetJob<T>
            {
                Values = values.AsReadOnly(),
                writer = writer.AsParallelWriter()
            }.Schedule(values.Length, JobsUtility.MaxJobThreadCount).Complete();
            return writer;
        }
        public struct ConverHashSetJob<T> : IJobParallelFor where T : unmanaged, IEquatable<T>
        {
            public NativeArray<T>.ReadOnly Values;
            public NativeParallelHashSet<T>.ParallelWriter writer;
            public void Execute(int index)
            {
                writer.Add(Values[index]);
            }
        }
        public static NativeParallelHashSet<float3> ConvetPositionHashSet(NativeArray<LocalTransform> values)
        {
            using NativeParallelHashSet<float3> writer = new(values.Length, Allocator.TempJob);
            new ConverPositionHashSetJob
            {
                Values = values.AsReadOnly(),
                writer = writer.AsParallelWriter()
            }.Schedule(values.Length, JobsUtility.MaxJobThreadCount).Complete();

            return writer;
        }
        public struct ConverPositionHashSetJob : IJobParallelFor
        {
            public NativeArray<LocalTransform>.ReadOnly Values;
            public NativeParallelHashSet<float3>.ParallelWriter writer;
            public void Execute(int index)
            {
                writer.Add(Values[index].Position);
            }
        }
        #endregion Convert

        #region singleton

        public static T GetSingleton<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            using var builder = new EntityQueryBuilder(Allocator.Temp);
            builder.WithAll<T>();

            return entityManager.CreateEntityQuery(builder).GetSingleton<T>();
        }
        public static RefRW<T> GetSingletonRW<T>(EntityManager entityManager) where T : unmanaged, IComponentData
        {
            using var builder = new EntityQueryBuilder(Allocator.Temp);
            builder.WithAllRW<T>();

            return entityManager.CreateEntityQuery(builder).GetSingletonRW<T>();
        }
        public static bool TrySingleton<T>(EntityManager entityManager, out T value) where T : unmanaged, IComponentData
        {
            using var builder = new EntityQueryBuilder(Allocator.Temp);
            builder.WithAllRW<T>();
            bool b = entityManager.CreateEntityQuery(builder).TryGetSingleton<T>(out value);
            return b;
        }
        public static bool TrySingletonRW<T>(EntityManager entityManager, out RefRW<T> refRW) where T : unmanaged, IComponentData
        {
            using var builder = new EntityQueryBuilder(Allocator.Temp);
            builder.WithAllRW<T>();
            bool b = entityManager.CreateEntityQuery(builder).TryGetSingletonRW<T>(out refRW);
            return b;
        }
        #endregion singleton
    }

}