using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace EcsCollision
{
    [ExecuteAlways]
    public class ParticleSpawnArea : MonoBehaviour
    {
        public ColliderShape ColliderShape = ColliderShape.Box;
        [NonSerialized] public Bounds SpawnBounds;
        public float3 IntiVelocity;

#if UNITY_EDITOR
        // Start is called before the first frame update
        void Start()
        {

        }
        
        private void Update()
        {
            Vector3 origin = transform.position;
            float sizeX = transform.localScale.x * 0.5f;
            float sizeY = transform.localScale.y * 0.5f;
            float sizeZ = transform.localScale.z * 0.5f;

            {
                Debug.DrawLine(origin + new Vector3(sizeX, sizeY, sizeZ), origin + new Vector3(-sizeX, sizeY, sizeZ), Color.black, Time.deltaTime);
                Debug.DrawLine(origin + new Vector3(sizeX, sizeY, sizeZ), origin + new Vector3(sizeX, -sizeY, sizeZ), Color.black, Time.deltaTime);
                Debug.DrawLine(origin + new Vector3(sizeX, sizeY, sizeZ), origin + new Vector3(sizeX, sizeY, -sizeZ), Color.black, Time.deltaTime);

                Debug.DrawLine(origin + new Vector3(-sizeX, -sizeY, -sizeZ), origin + new Vector3(sizeX, -sizeY, -sizeZ), Color.black, Time.deltaTime);
                Debug.DrawLine(origin + new Vector3(-sizeX, -sizeY, -sizeZ), origin + new Vector3(-sizeX, sizeY, -sizeZ), Color.black, Time.deltaTime);
                Debug.DrawLine(origin + new Vector3(-sizeX, -sizeY, -sizeZ), origin + new Vector3(-sizeX, -sizeY, sizeZ), Color.black, Time.deltaTime);


                Debug.DrawLine(origin + new Vector3(sizeX, -sizeY, sizeZ), origin + new Vector3(-sizeX, -sizeY, sizeZ), Color.black, Time.deltaTime);
                Debug.DrawLine(origin + new Vector3(sizeX, -sizeY, sizeZ), origin + new Vector3(sizeX, -sizeY, -sizeZ), Color.black, Time.deltaTime);

                Debug.DrawLine(origin + new Vector3(-sizeX, sizeY, -sizeZ), origin + new Vector3(sizeX, sizeY, -sizeZ), Color.black, Time.deltaTime);
                Debug.DrawLine(origin + new Vector3(-sizeX, sizeY, -sizeZ), origin + new Vector3(-sizeX, sizeY, sizeZ), Color.black, Time.deltaTime);

                Debug.DrawLine(origin + new Vector3(-sizeX, sizeY, sizeZ), origin + new Vector3(-sizeX, -sizeY, sizeZ), Color.black, Time.deltaTime);
                Debug.DrawLine(origin + new Vector3(sizeX, sizeY, -sizeZ), origin + new Vector3(sizeX, -sizeY, -sizeZ), Color.black, Time.deltaTime);
            }//Draw Rectengle

            Debug.DrawLine(origin , origin + new Vector3(IntiVelocity.x, IntiVelocity.y, IntiVelocity.z), Color.red, Time.deltaTime);
        }
#endif
    }
    public struct ParticleSpawnAreaComponent : IComponentData
    {
        public ColliderShape shape;
        public Bounds Bound;
        public float3 LocalMinPos;
        public int3 SpawnPoints;
        public float3 IntiVelocity;
    }
    public class ParticleSpawnAreaBaker : Baker<ParticleSpawnArea>
    {
        public override void Bake(ParticleSpawnArea authoring)
        {
            AddComponent(GetEntity(authoring, TransformUsageFlags.Dynamic),
                new ParticleSpawnAreaComponent
                {
                    shape = authoring.ColliderShape,
                    Bound = new Bounds
                    {
                        center = authoring.transform.position,
                        extents = authoring.transform.localScale * 0.5f
                    },
                    LocalMinPos = float3.zero,
                    SpawnPoints = new int3(1,1,1),

                    IntiVelocity = authoring.IntiVelocity
                });
        }
    }

}
