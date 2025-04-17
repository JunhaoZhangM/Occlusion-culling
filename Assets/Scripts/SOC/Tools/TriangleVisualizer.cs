//using System.Collections.Generic;
//using Unity.Collections;
//using Unity.Mathematics;
//using UnityEngine;

//public class TriangleVisualizer : MonoBehaviour
//{

//    // ��������б����� elsewhere ��䣩
//    public List<TriangleInfo> occluderTriangleInfoList;

//    // ÿ��������Ƴɶ�����Ļ��
//    // Gizmo С��İ뾶
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

//    // �� Scene ��ͼ�ͣ����� Gizmos ��ģ�Game ��ͼ�ж��ᱻ����
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

//    // ����Ļ���� (x,y,z) �� �������� �� ������
//    void DrawVertexWorld(Camera cam, float4 screenPos)
//    {
//        // 1) �����һ����ȣ�ndc.z ͨ����[-1,1]��[0,1]��
//        //    �����ȷ�� ndc.z ��Χ [0,1]�����п��Լ򻯳� float zNorm = screenPos.z;
//        float zNorm = (screenPos.z + 1f) * 0.5f;

//        // 2) ��ֵ������Ľ�ƽ�桪Զƽ�����
//        float worldDist = Mathf.Lerp(cam.nearClipPlane, cam.farClipPlane, zNorm);

//        // 3) ���� ScreenPoint (�������� + �������)
//        Vector3 sp = new Vector3(screenPos.x, screenPos.y, worldDist);

//        // 4) ת����������
//        Vector3 wp = cam.ScreenToWorldPoint(sp);

//        Debug.Log(sp + " " + wp);

//        // 5) ��һ��С��
//        Gizmos.DrawSphere(wp, sphereRadius);
//    }
//}