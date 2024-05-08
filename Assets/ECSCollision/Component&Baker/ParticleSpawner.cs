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
        public static ParticleSpawner instance;

        public GameObject ParticleObj;
        [Expand.ReadOnly] public int maxAmount = 10000;
        public int MaxAmount
        {
            get { return maxAmount; }
            set 
            {
                if (OnChangeMaxSpawnAmount != null && maxAmount != value)
                {
                    OnChangeMaxSpawnAmount(maxAmount, value);
                }
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


        public delegate void DelegateSMaxpawnAmount(int preAmount, int amount);
        public DelegateSMaxpawnAmount OnChangeMaxSpawnAmount;

        //  싱글톤으로 (Baker에서 설정) 스폰공간 부족 알림 
        // 최대갯수에 도달했을때 추가 스폰 ...적용은 되긴 할껀데 확인 
        void Start()
        {

        }

    }

    [CustomEditor(typeof(ParticleSpawner))]
    public class ParticleSpawnerEditor : Editor
    {
        ParticleSpawner target;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (target == null)
                target = serializedObject.targetObject as ParticleSpawner;

            target.MaxAmount = EditorGUILayout.DelayedIntField("Spawn Max Amount", target.MaxAmount);
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
            ParticleSpawner.instance = authoring;

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
