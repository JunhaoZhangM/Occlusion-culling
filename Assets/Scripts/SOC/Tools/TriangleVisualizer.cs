//using System.Collections.Generic;
//using Unity.Collections;
//using Unity.Mathematics;
//using UnityEngine;

//public class TriangleVisualizer : MonoBehaviour
//{

//    // 你的三角列表（由你 elsewhere 填充）
//    public List<TriangleInfo> occluderTriangleInfoList;

//    // 每个顶点绘制成多大的屏幕点
//    // Gizmo 小球的半径
//    public float sphereRadius = 1f;
//    private void Awake()
//    {
//        occluderTriangleInfoList = new List<TriangleInfo>();
//    }
//    public void DrawTriangle(NativeList<TriangleInfo> v)
//    {
//        occluderTriangleInfoList.Clear();
//        for(int i = 0; i < v.Length; i++){
//            occluderTriangleInfoList.Add(v[i]);
//        }
//    }

//    // 在 Scene 视图和（开启 Gizmos 后的）Game 视图中都会被调用
//    void OnDrawGizmos()
//    {
        

//        Camera cam = Camera.current ?? Camera.main;
//        if (cam == null) return;

//        Gizmos.color = Color.red;

//        for (int i = 0; i < occluderTriangleInfoList.Count; i++)
//        {
//            var tri = occluderTriangleInfoList[i];
//            DrawVertexWorld(cam, tri.v0);
//            DrawVertexWorld(cam, tri.v1);
//            DrawVertexWorld(cam, tri.v2);
//        }
//    }

//    // 将屏幕坐标 (x,y,z) → 世界坐标 → 画个球
//    void DrawVertexWorld(Camera cam, float4 screenPos)
//    {
//        // 1) 计算归一化深度（ndc.z 通常在[-1,1]或[0,1]）
//        //    如果你确认 ndc.z 范围 [0,1]，这行可以简化成 float zNorm = screenPos.z;
//        float zNorm = (screenPos.z + 1f) * 0.5f;

//        // 2) 插值到相机的近平面—远平面距离
//        float worldDist = Mathf.Lerp(cam.nearClipPlane, cam.farClipPlane, zNorm);

//        // 3) 构造 ScreenPoint (像素坐标 + 世界距离)
//        Vector3 sp = new Vector3(screenPos.x, screenPos.y, worldDist);

//        // 4) 转到世界坐标
//        Vector3 wp = cam.ScreenToWorldPoint(sp);

//        Debug.Log(sp + " " + wp);

//        // 5) 画一个小球
//        Gizmos.DrawSphere(wp, sphereRadius);
//    }
//}