using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public static class CullingTools
{

    public static bool FrustumCulling(NativeArray<FrustumPlane> planes, float3 center, float radius = 1f)
    {
        for (int i = 0; i < 6; i++)
        {
            var frustumPlane = planes[i];
            float dis = math.dot(frustumPlane.normal, center) + frustumPlane.distanceToOrigin;
            if (dis < -radius)
            {
                return false;
            }
        }

        return true;
    }

    public static float CalculateScreenRatio(Matrix4x4 vpMatrix, float3 center,float3 extents,float screenArea)
    {
        // 计算包围盒的8个顶点的屏幕坐标（仅计算四个角的极值）
        float3 screenMin = vpMatrix.MultiplyPoint3x4(center - extents);
        float3 screenMax = vpMatrix.MultiplyPoint3x4(center + extents);

        // 屏幕空间的裁剪区域（归一化到 [0,1] 范围）
        float screenLeft = screenMin.x;
        float screenRight = screenMax.x;
        float screenBottom = screenMin.y;
        float screenTop = screenMax.y;

        // 计算投影区域的宽度和高度
        float width = screenRight - screenLeft;
        float height = screenTop - screenBottom;

        // 投影面积占比
        float ratio = (width * height) / screenArea;
        return ratio;
    }

    public static bool IsMaterialTransparent(Material mat)
    {
        if (mat == null)
        {
            return false;
        }

        return mat.renderQueue == (int)RenderQueue.Transparent;
    }

    public static bool TriangleClip(float4 v0, float4 v1, float4 v2)
    {
        float v0w = math.abs(v0.w);
        float v1w = math.abs(v1.w);
        float v2w = math.abs(v2.w);

        bool outLeft = v0.x < -v0w && v1.x < -v1w && v2.x < -v2w;
        bool outRight = v0.x > v0w && v1.x > v1w && v2.x > v2w;

        if (outLeft || outRight) return true;

        bool outTop = v0.y > v0w && v1.y > v1w && v2.y > v2w;
        bool outBottom = v0.y < -v0w && v1.y < -v1w && v2.y < -v2w;

        if (outTop || outBottom) return true;

        bool outNear = v0.z < -v0w && v1.z < -v1w && v2.z < -v2w;
        bool outFar = v0.z > v0w && v1.z > v1w && v2.z > v2w;

        if (outNear || outFar) return true;

        return false;
    }
}
