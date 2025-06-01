using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace EcsCollision
{
    public enum ColliderShape { Sphere, Box, Plane };
    public enum ColliderEvent { Collision, DisableTrigger, KillTrigger, AccelerationTrigger };
    public class CollisionObstacleAuthoring : MonoBehaviour
    {
        public ColliderShape colliderType;
        public ColliderEvent colliderEvent = ColliderEvent.Collision;
        public Vector3 AccVelocity;
    }



    public struct FluidCollider : IComponentData { }
    public struct FluidTrigger : IComponentData { }
    public struct CollisionComponent : IComponentData
    {
        public ColliderShape collidershape;
        public ColliderEvent colliderEvent;
        public float3 WorldSize;
        public Vector3 AccVelocity;

        /// <summary>
        /// Checking Collision Sphere to (Sphere , Box , Plane) 
        /// </summary>
        /// <param name="transform">Collider Transform</param>
        /// <param name="targetRadius">Particle Radius</param>
        /// <param name="targetPos">Particle Position</param>
        /// <param name="dir">Collision Normal Direction</param>
        /// <returns></returns>
        [System.Obsolete("Use IsCollisionFormSphere()")]
        public bool IsCollisionSphere(LocalTransform transform, float targetRadius, float3 targetPos, out float3 dir, out float dis)
        {
            if (collidershape == ColliderShape.Plane || collidershape == ColliderShape.Box)
            {
                var maxPoint = transform.Right() * WorldSize.x
                                + transform.Forward() * WorldSize.z;
                if (collidershape != ColliderShape.Plane)
                    maxPoint += transform.Up() * WorldSize.y;
                maxPoint *= 0.5f;

                var ostProjectX = math.project(transform.Position, transform.Right());
                var ostAreaProjectX = math.project(transform.Position + maxPoint, transform.Right());
                var particleProjectX = math.project(targetPos, transform.Right());
                //var projectXDis = math.distance(particleProjectX, ostProjectX);
                var disX = math.distance(particleProjectX, ostProjectX) - math.distance(ostAreaProjectX, ostProjectX) - targetRadius;
                if (disX >= 0)//(projectXDis - targetRadius >= math.distance(ostAreaProjectX, ostProjectX))
                {
                    dis = disX;
                    dir = float3.zero;
                    return false;
                }

                var ostProjectZ = math.project(transform.Position, transform.Forward());
                var ostAreaProjectZ = math.project(transform.Position + maxPoint, transform.Forward());
                var particleProjectZ = math.project(targetPos, transform.Forward());
                //var projectZDis = math.distance(particleProjectZ, ostProjectZ);
                var disZ = math.distance(particleProjectZ, ostProjectZ) - math.distance(ostAreaProjectZ, ostProjectZ) - targetRadius;
                if (disZ >= 0)//(projectZDis - targetRadius >= math.distance(ostAreaProjectZ, ostProjectZ))
                {
                    dis = disZ;
                    dir = float3.zero;
                    return false;
                }

                if (collidershape == ColliderShape.Box)
                {
                    var ostProjectY = math.project(transform.Position, transform.Up());
                    var ostAreaProjectY = math.project(transform.Position + maxPoint, transform.Up());
                    var particleProjectY = math.project(targetPos, transform.Up());
                    //var projectYDis = math.distance(particleProjectY, ostProjectY);
                    var disY = math.distance(particleProjectY, ostProjectY) - math.distance(ostAreaProjectY, ostProjectY) - targetRadius;

                    if (disY >= 0)//(projectYDis - targetRadius >= math.distance(ostAreaProjectY, ostProjectY))
                    {
                        dis = disY;
                        dir = float3.zero;
                        return false;
                    }
                    else
                    {
                        var disToMaxX = math.distancesq(particleProjectX, ostAreaProjectX);
                        var disToMaxY = math.distancesq(particleProjectY, ostAreaProjectY);
                        var disToMaxZ = math.distancesq(particleProjectZ, ostAreaProjectZ);

                        //Debug.Log($"{targetRadius} => {disX} , {disY} , {disZ} / {disToMaxX} , {disToMaxY} , {disToMaxZ}");//=================== 항상 음수 , 절댓값 이 targetRadius 보다 작은 방향을 지정

                        dis = 0;
                        var tDir = float3.zero;

                        if (-disX < targetRadius)
                        {
                            dis += -disX;
                            tDir += transform.Right() *
                                    ((math.dot(transform.Right(), math.normalize(particleProjectX - ostAreaProjectX)) > 0)
                                    ? 1 : -1) * (targetRadius - disX);

                            Debug.Log($"P : {particleProjectX} , O : {ostProjectX} , MO : {ostAreaProjectX} / " +
                                $"{math.distance(particleProjectX, ostProjectX) - math.distance(ostAreaProjectX, ostProjectX)}");

                            Debug.DrawLine(particleProjectX, ostProjectX, Color.green, 10f);
                            Debug.DrawLine(ostAreaProjectX + new float3(0, 0.1f, 0), ostProjectX - (ostAreaProjectX - ostProjectX) + new float3(0, 0.1f, 0), Color.red, 10f);
                        }

                        if (-disY < targetRadius)
                        {
                            dis += -disY;
                            tDir += transform.Up() *
                                    ((math.dot(transform.Up(), math.normalize(particleProjectY - ostAreaProjectY)) > 0)
                                    ? 1 : -1) * (targetRadius - disY);
                        }
                        if (-disZ < targetRadius)
                        {
                            dis += -disZ;
                            tDir += transform.Forward() *
                                    ((math.dot(transform.Forward(), math.normalize(particleProjectZ - ostAreaProjectZ)) > 0)
                                    ? 1 : -1) * (targetRadius - disZ);
                        }
                        dir = math.normalize(tDir);

                        {
                            /*
                            if (disToMaxX < disToMaxY && disToMaxX < disToMaxZ)
                            {
                                dis = targetRadius - math.sqrt(disToMaxX);
                                dir = transform.Right() *
                                    ((math.dot(transform.Right(), math.normalize(particleProjectX - ostAreaProjectX)) > 0)
                                    ? 1 : -1);
                            }
                            else if (disToMaxY < disToMaxX && disToMaxY < disToMaxZ)
                            {
                                dis = targetRadius - math.sqrt(disToMaxY);
                                dir = transform.Up() *
                                    ((math.dot(transform.Up(), math.normalize(particleProjectY - ostAreaProjectY)) > 0)
                                    ? 1 : -1);
                            }
                            else
                            {
                                dis = targetRadius - math.sqrt(disToMaxZ);
                                dir = transform.Forward() *
                                    ((math.dot(transform.Forward(), math.normalize(particleProjectZ - ostAreaProjectZ)) > 0)
                                    ? 1 : -1);
                            }
                            */
                        }//Legacy

                        return true;
                    }//Calculate Box Normal ---===================  Need Fix (Project 했을때 범위 밖에 있는거  OR 영역 범위 대비 % 으로)
                    //  지금은 영역 범위에 가까운거 , 조건이 맞으면 방향값을 더하기 --> 대각선 방향으로 예외 처리
                }
                else
                {
                    var ostProjectY = math.project(transform.Position, transform.Up());
                    var particleProjectY = math.project(targetPos, transform.Up());
                    var disPlane = math.distance(ostProjectY, particleProjectY);
                    if (disPlane > targetRadius)
                    {
                        dis = disPlane - targetRadius;
                        dir = float3.zero;
                        return false;
                    }
                    else
                    {
                        dis = targetRadius - disPlane;
                        dir = (math.dot(transform.Up(), math.normalize(particleProjectY - ostProjectY)) > 0)
                            ? transform.Up() : -transform.Up();
                        return true;
                    }
                }//Plane
            }
            else
            {
                float disTarget = math.distance(targetPos, transform.Position);
                float size = WorldSize.x * 0.5f;

                if (disTarget - targetRadius > size)
                {
                    dis = disTarget - (targetRadius + size);
                    dir = Vector3.zero;
                    return false;
                }
                else
                {
                    dis = (targetRadius + size) - disTarget;
                    dir = math.normalize(targetPos - transform.Position);
                    return true;
                }
            }
        }
        public bool IsCollisionFormSphere(LocalTransform transform, float3 BoxExtent, float3 targetPos, float targetRadius, out Vector3 dir)
        {
            if (collidershape == ColliderShape.Plane || collidershape == ColliderShape.Box)
            {
                var result = Custom.Math.CalculationBoxNormalOnhitSphere(transform, BoxExtent, targetPos, targetRadius, out Vector3 Ldir);

                dir = Ldir;
                return result;
            }
            else
            {
                float disTarget = math.distance(targetPos, transform.Position);
                float size = WorldSize.x * 0.5f;

                if (disTarget - targetRadius > size)
                {
                    dir = math.normalize(targetPos - transform.Position) * (disTarget - (targetRadius + size));
                    return false;
                }
                else
                {
                    dir = math.normalize(targetPos - transform.Position) * ((targetRadius + size) - disTarget);
                    return true;
                }
            }
        }

        public Vector3 GetRotatedBoxSize(LocalTransform transform)
        {
            Vector3 extents = WorldSize * 0.5f;
            Vector3[] points = new Vector3[]
            {
                transform.Right() * extents.x + transform.Up() * extents.y + transform.Forward() * extents.z,//ppp
                transform.Right() * extents.x + transform.Up() * extents.y - transform.Forward() * extents.z,//ppm
                transform.Right() * extents.x - transform.Up() * extents.y + transform.Forward() * extents.z,//pmp
                transform.Right() * extents.x - transform.Up() * extents.y - transform.Forward() * extents.z,//pmm
                Vector3.zero, Vector3.zero ,Vector3.zero, Vector3.zero
            };
            points[4] = -points[0];
            points[5] = -points[1];
            points[6] = -points[2];
            points[7] = -points[3];

            return new Vector3
            {
                x = points.Max(t => t.x) - points.Min(t => t.x),
                y = points.Max(t => t.y) - points.Min(t => t.y),
                z = points.Max(t => t.z) - points.Min(t => t.z)
            };
        }
    }
    public class CollisionBaker : Baker<CollisionObstacleAuthoring>
    {
        public override void Bake(CollisionObstacleAuthoring authoring)
        {
            if (authoring.TryGetComponent<MeshCollider>(out var meshCollider))
            {
                var size = meshCollider.sharedMesh.bounds.size;
                size.x *= authoring.transform.localScale.x;
                size.y *= authoring.transform.localScale.y;
                size.z *= authoring.transform.localScale.z;

                bool IsEllipse = false;
                if (authoring.colliderType == ColliderShape.Sphere)
                {
                    IsEllipse = !Mathf.Approximately(size.x, size.y) || !Mathf.Approximately(size.x, size.z);
                }

                AddComponent(GetEntity(authoring, TransformUsageFlags.Dynamic),
                    new CollisionComponent
                    {
                        collidershape = IsEllipse ? ColliderShape.Box : authoring.colliderType,
                        colliderEvent = authoring.colliderEvent,
                        WorldSize = size,
                        AccVelocity = authoring.AccVelocity
                    });
            }
            else if (authoring.TryGetComponent<SphereCollider>(out var sphereCollider))
            {
                var size = sphereCollider.radius * 2f * Mathf.Max(authoring.transform.localScale.x, authoring.transform.localScale.y, authoring.transform.localScale.z);

                AddComponent(GetEntity(authoring, TransformUsageFlags.Dynamic),
                    new CollisionComponent
                    {
                        collidershape = ColliderShape.Sphere,
                        colliderEvent = authoring.colliderEvent,
                        WorldSize = new float3(1, 1, 1) * size,
                        AccVelocity = authoring.AccVelocity
                    });
            }
            else if (authoring.TryGetComponent<BoxCollider>(out var boxCollider))
            {
                var size = boxCollider.size;
                size.x *= authoring.transform.localScale.x;
                size.y *= authoring.transform.localScale.y;
                size.z *= authoring.transform.localScale.z;

                AddComponent(GetEntity(authoring, TransformUsageFlags.Dynamic),
                    new CollisionComponent
                    {
                        collidershape = ColliderShape.Box,
                        colliderEvent = authoring.colliderEvent,
                        WorldSize = size,
                        AccVelocity = authoring.AccVelocity
                    });
            }


            if (authoring.colliderEvent == ColliderEvent.Collision)
            {
                AddComponent<FluidCollider>(GetEntity(authoring, TransformUsageFlags.Dynamic));
            }
            else
            {
                AddComponent<FluidTrigger>(GetEntity(authoring, TransformUsageFlags.Dynamic));
            }

        }
    }

}
