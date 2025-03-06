using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;

public struct BoundsInfo
{
    public float3 center;
    public float3 extents;
    public float radius;
}

public struct ScreenBoundsInfo
{
    public float4 min;
    public float4 max;
    public float minDepth;
}

