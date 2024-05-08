using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.VisualScripting;

namespace EcsCollision
{
    public class DebugSpawner : MonoBehaviour
    {
        public GameObject particleObj;
        public int Amount;
        public float RandomPower;

        public void Start()
        {
            
        }//�̰� �߰��ϸ� �ν����Ϳ��� ��Ȱ��ȭ ����
    }
    public struct DebugSpawnerComponent : IComponentData
    {
        public Entity particle;
        public int Amount;
        public float RandomPower;
    }
    public class DebugSpawnerBake : Baker<DebugSpawner>
    {
        public override void Bake(DebugSpawner authoring)
        {
            AddComponent(GetEntity(authoring, TransformUsageFlags.Dynamic),
                new DebugSpawnerComponent
            {
                particle = GetEntity(authoring.particleObj, TransformUsageFlags.Renderable),
                Amount = authoring.Amount,
                RandomPower = authoring.RandomPower
            });
        }
    }

}