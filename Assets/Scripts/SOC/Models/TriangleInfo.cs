using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;

/// <summary>
/// 遮挡物三角形
/// </summary>
public struct TriangleInfo
{
    //遵循 v0.y<v1.y<v2.y
    public float4 v0;
    public float4 v1;
    public float4 v2;

    public float maxDepth;
}

