using EcsCollision;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEditor;
using UnityEngine;

namespace EcsCollision
{
    public class ParticleSpawner : MonoBehaviour
    {

        public GameObject ParticleObj;
        [Expand.ReadOnly] public int maxAmount = 10000;
        public int MaxAmount
        {
            get { return maxAmount; }
            set
            {
                maxAmount = value;
            }
        }
        public int SpawnPerSecond = 1000;
        [Range(0.01f, 1)] public float SpawnInterval = 0.1f;

        public List<ParticleSpawnArea> SpawnAreas = new();

        /// <summary>
        /// 이 값 절반만큼 스폰시 항상 랜덤 - 완전히 같은위치에 겹치면 밀리지 않아서
        /// </summary>
        public float SpawnBetweenSpace = 0.1f;
        /// <summary>
        /// Not Use , 스폰 대상 + 위치 랜덤
        /// </summary>
        public bool SpawnRandomPoint;


        [Space(5)]
        public int SpawnAmount;
        [Expand.ReadOnly] public int SpawnAmountForSecond;


        //  싱글톤으로 (Baker에서 설정) 스폰공간 부족 알림 
        // 최대갯수에 도달했을때 추가 스폰 ...적용은 되긴 할껀데 확인 
        void Start()
        {

        }

    }

    [CustomEditor(typeof(ParticleSpawner))]
    public class ParticleSpawnerEditor : Editor
    {
        ParticleSpawner onwer;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (onwer == null)
                onwer = serializedObject.targetObject as ParticleSpawner;

            onwer.MaxAmount = EditorGUILayout.DelayedIntField("Spawn Max Amount", onwer.MaxAmount);
        }
    }

    public struct ParticleSpawnAreaElement : IBufferElementData
    {
        public Entity entity;
        public ParticleSpawnAreaComponent SpawnArea;
    }
    public struct ParticleSpawnerComponent : IComponentData
    {
        public Entity ParticleObj;
        public int MaxAmount;
        public int SpawnPerSecond;
        public float SpawnInterval;
        public float SpawnBetweenSpace;
        public bool SpawnRandomPoint;
    }
    public class ParticleSpawnerBaker : Baker<ParticleSpawner>
    {
        public override void Bake(ParticleSpawner authoring)
        {

            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddBuffer<ParticleSpawnAreaElement>(entity);
            AddComponent(entity,
                new ParticleSpawnerComponent
                {
                    ParticleObj = GetEntity(authoring.ParticleObj, TransformUsageFlags.Renderable),
                    MaxAmount = authoring.MaxAmount,
                    SpawnPerSecond = authoring.SpawnPerSecond,
                    SpawnInterval = authoring.SpawnInterval,
                    SpawnBetweenSpace = authoring.SpawnBetweenSpace,
                    SpawnRandomPoint = authoring.SpawnRandomPoint
                });
        }
    }

}
