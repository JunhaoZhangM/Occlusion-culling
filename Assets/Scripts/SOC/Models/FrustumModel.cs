using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;

public struct FrustumPlane
{
    public float3 normal;
    public float distanceToOrigin;

    public FrustumPlane(float3 normal, float distanceToOrigin)
    {
        this.normal = normal;
        this.distanceToOrigin = distanceToOrigin;
    }
}

public struct InFrustumObject
{
    public float3 center;
    public float3 extents;
    public int index;
}

