using System;
using System.Collections.Generic;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

public class OcclusionCulling : MonoBehaviour
{
    public Transform root;
    private List<MeshFilter> _meshFilters;
    private Camera _mainCamera;

    private float _screenWidth;
    private float _screenHeight;
    private float _screenArea;
    private float4x4 _screenMatrix;
    private const int DEFAULT_CONTAINER_SIZE = 104629;
    private const float OCCLUDER_SCREEN_THREASHOLD = 0.01f;

    private JobHandle dependency;

    private Matrix4x4 vpMatrix;

    #region Debug Property
    public int occluderCount = 0;
    #endregion

    #region Frustum Culling
    private int _objectCount = 0;
    private Plane[] tempPlane;
    private NativeArray<FrustumPlane> _frustumPlanes;
    private NativeList<BoundsInfo> _inFrustumBounds;
    #endregion

    #region Select Occluder
    private NativeList<int> _occluderMfIndex;
    private NativeList<int> _occludeeMfIndex;
    #endregion

    #region Collect Vertex Info
    private List<Vector3> _occluderVertexPosTempList; // 临时存储 模型节点的位置
    private List<int> _triangleIndexVertexTemp;
    //private VertexInfo[] _occluderVertexTempArray;
    //private VertexInfo[] _occludeeBoundsVertexTempArray;
    private NativeArray<VertexInfo> _occluderVerextInfo;
    private NativeArray<VertexInfo> _occludeeBoundsVertexInfo;
    private NativeList<int4> _occluderTriangleIndex; // （v0,v1,v2,该mesh的顶点起始索引）
    private int _occluderTriangleCount;
    private NativeList<float4x4> _occluderMatrixList;
    private NativeList<float4x4> _occludeeMatrixList;
    private NativeArray<float4> _occluderClipVertexInfo;
    private NativeArray<float4> _occludeeClipVertexInfo;
    #endregion

    #region Collect Triangle Info
    private NativeList<TriangleInfo> _occluderTriangleInfoList;
    #endregion


    private NativeArray<bool> _objectsVisibility;
    // Start is called before the first frame update

    void Awake()
    {
        _mainCamera = Camera.main;
        //Init();
        _screenWidth = Screen.width;
        _screenHeight = Screen.height;
        _screenArea = _screenWidth * _screenHeight;
        _screenMatrix = new float4x4(
            new float4(_screenWidth * 0.5f, 0, 0, _screenWidth * 0.5f),
            new float4(0, _screenHeight * 0.5f, 0, _screenHeight * 0.5f),
            new float4(0, 0, 1, 0), new float4(0, 0, 0, 1));

        _meshFilters = new List<MeshFilter>();
        _frustumPlanes = new NativeArray<FrustumPlane>(6, Allocator.Persistent);
        _inFrustumBounds = new NativeList<BoundsInfo>(Allocator.Persistent);
        _objectsVisibility = new NativeArray<bool>(DEFAULT_CONTAINER_SIZE, Allocator.Persistent);

        _occluderMfIndex = new NativeList<int>(DEFAULT_CONTAINER_SIZE / 3, Allocator.Persistent);
        _occludeeMfIndex = new NativeList<int>(DEFAULT_CONTAINER_SIZE / 3, Allocator.Persistent);

        _occluderVertexPosTempList = new List<Vector3>();
        _triangleIndexVertexTemp = new List<int>();

        _occluderVerextInfo = new NativeArray<VertexInfo>(DEFAULT_CONTAINER_SIZE, Allocator.Persistent);
        _occludeeBoundsVertexInfo = new NativeArray<VertexInfo>(DEFAULT_CONTAINER_SIZE, Allocator.Persistent);

        _occluderTriangleIndex = new NativeList<int4>(Allocator.Persistent);
        _occluderMatrixList = new NativeList<float4x4>(Allocator.Persistent);
        _occludeeMatrixList = new NativeList<float4x4>(Allocator.Persistent);

        _occluderClipVertexInfo = new NativeArray<float4>(DEFAULT_CONTAINER_SIZE, Allocator.Persistent);
        _occludeeClipVertexInfo = new NativeArray<float4>(DEFAULT_CONTAINER_SIZE, Allocator.Persistent);

        _occluderTriangleInfoList = new NativeList<TriangleInfo>(DEFAULT_CONTAINER_SIZE / 3, Allocator.Persistent);
    }
    private void OnDestroy()
    {
        dependency.Complete();
        _frustumPlanes.Dispose();
        _inFrustumBounds.Dispose();
        _objectsVisibility.Dispose();
        _occluderMfIndex.Dispose();
        _occludeeMfIndex.Dispose();
        _occluderTriangleIndex.Dispose();
        _occluderMatrixList.Dispose();
        _occludeeMatrixList.Dispose();
        _occluderClipVertexInfo.Dispose();
        _occludeeClipVertexInfo.Dispose();
        _occluderTriangleInfoList.Dispose();
    }

    // Update is called once per frame
    void Update()
    {
        dependency.Complete();

        //缓存Camera的VP矩阵
        vpMatrix = _mainCamera.projectionMatrix * _mainCamera.worldToCameraMatrix;

        Profiler.BeginSample("Frustum Culling");
        FrustumCulling(_mainCamera);
        Profiler.EndSample();

        Profiler.BeginSample("Select Occulder");
        SelectOccluder();
        Profiler.EndSample();

        Profiler.BeginSample("Collect Vertex Info");
        CollectVertexInfo();
        Profiler.EndSample();

        Profiler.BeginSample("Collect Occluder Triangle Info");
        CollectOccluderTriangleInfo();
        Profiler.EndSample();

    }

    #region Frustum Culling
    private void FrustumCulling(Camera camera)
    {
        _meshFilters.Clear();
        _inFrustumBounds.Clear();
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);

        for (int i = 0; i < 6; i++)
        {
            _frustumPlanes[i] = new FrustumPlane(planes[i].normal, planes[i].distance);
        }

        foreach (var renderer in renderers)
        {

            Bounds bounds = renderer.bounds;
            // SIMD 优化 考虑一个物体的包围盒和六个面同时计算
            if (CullingTools.FrustumCulling(_frustumPlanes, bounds.center, bounds.extents.magnitude))
            {
                renderer.enabled = true;
                _meshFilters.Add(renderer.gameObject.GetComponent<MeshFilter>());
                _inFrustumBounds.Add(new BoundsInfo()
                {
                    center = bounds.center,
                    extents = bounds.extents
                });
            }
            else
            {
                renderer.enabled = false;
            }
        }
        _objectCount = _meshFilters.Count;
    }
    /// <summary>
    /// 视锥剔除主函数
    /// </summary>
    /// <param name="camera"></param>
    //private void FrustumCullingSIMD(Camera camera)
    //{
    //    _meshFilters = new List<MeshFilter>(root.GetComponentsInChildren<MeshFilter>());

    //    // 获得视锥平面
    //    Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);

    //    for (int i = 0; i < 6; i++)
    //    {
    //        // 构建 SIMD加速数据结构
    //        _frustumPlanes[i] = new FrustumPlane(planes[i].normal, planes[i].distance);
    //    }

    //    objectCount = _meshFilters.Count;
    //    for (int i = 0; i < objectCount; i++)
    //    {
    //        Bounds bounds = _meshFilters[i].mesh.bounds;

    //        // 将 bounds 转化成 世界 bounds
    //        Vector3 worldCenter = _meshFilters[i].transform.TransformPoint(bounds.center);
    //        Vector3 worldExtents = _meshFilters[i].transform.TransformVector(bounds.extents);

    //        _worldBounds[i] = new FrustumBounds(worldCenter, worldExtents);
    //    }

    //    dependency = new FrustumVisibilityJob()
    //    {
    //        boundses = _worldBounds,
    //        frustumPlane = _frustumPlanes,
    //        objectsFrustumVibility = _objectsVisibility
    //    }.Schedule(objectCount, 10, dependency);

    //    dependency.Complete();

    //    // 根据结果 先进行 视锥剔除
    //    for (int i = 0; i < objectCount; i++)
    //    {
    //        _meshFilters[i].GetComponent<MeshRenderer>().enabled = _objectsVisibility[i];
    //    }
    //}

    //public struct FrustumVisibilityJob : IJobParallelFor
    //{
    //    public NativeArray<FrustumBounds> boundses;
    //    public NativeArray<FrustumPlane> frustumPlane;
    //    public NativeArray<bool> objectsFrustumVibility;

    //    public void Execute(int index)
    //    {
    //        FrustumBounds aabb = boundses[index];
    //        foreach (var plane in frustumPlane)
    //        {
    //            // 平面法向量方向的半轴长度
    //            float maxExtent = Mathf.Max(
    //                math.abs(aabb.extents.x * plane.normal.x),
    //                math.abs(aabb.extents.y * plane.normal.y),
    //                math.abs(aabb.extents.z * plane.normal.z)
    //            );

    //            // 计算中心点到平面的距离
    //            float centerDistance = math.dot(plane.normal, aabb.center) + plane.distanceToOrigin;

    //            // 如果中心距离 - 半轴长度 < 0，则整个AABB在平面外侧
    //            if (centerDistance < -maxExtent)
    //                objectsFrustumVibility[index] = false; 
    //                return;
    //        }
    //        objectsFrustumVibility[index] = true;
    //    }
    //}
    #endregion

    #region Select Occluder
    private void SelectOccluder()
    {
        _occluderMfIndex.Clear();
        _occludeeMfIndex.Clear();
        //for (int i = 0; i < _objectCount; i++)
        //{
        //    Bounds bounds = _inFrustumBounds[i];
        //    // 可能的优化 提前计算Camera的VP矩阵缓存
        //    float screenRatio = CullingTools.CalculateScreenRatio(_mainCamera, bounds, _screenArea);
        //    //Debug.Log(screenRatio);
        //    if (screenRatio >= OCCLUDER_SCREEN_THREASHOLD)
        //    {
        //        _occluderMfIndex.Add(i);
        //    }
        //    else
        //    {
        //        _occludeeMfIndex.Add(i);
        //    }
        //}
        dependency = new OccluderCollectJob()
        {
            vpMatrix = vpMatrix,
            screenArea = _screenArea,
            occluderMfIndex = _occluderMfIndex.AsParallelWriter(),
            occludeeMfIndex = _occludeeMfIndex.AsParallelWriter(),
            bounds = _inFrustumBounds
        }.Schedule(_objectCount, 16, dependency);
        //Debug.Log(occludeeCnt + " " + occluderCnt);
        dependency.Complete();
    }

    [BurstCompile]
    private struct OccluderCollectJob : IJobParallelFor
    {
        [ReadOnly]
        public Matrix4x4 vpMatrix;
        [ReadOnly]
        public float screenArea;
        [NativeDisableParallelForRestriction]
        public NativeList<int>.ParallelWriter occluderMfIndex;
        [NativeDisableParallelForRestriction]
        public NativeList<int>.ParallelWriter occludeeMfIndex;
        [ReadOnly]
        public NativeList<BoundsInfo> bounds;
        public void Execute(int index)
        {
            BoundsInfo boundsInfo = bounds[index];
            float3 center = boundsInfo.center;
            float3 ext = boundsInfo.extents;

            // 计算屏幕坐标
            float4 screenMin4 = math.mul(vpMatrix, new float4(center - ext, 1));
            float4 screenMax4 = math.mul(vpMatrix, new float4(center + ext, 1));
            screenMin4 /= screenMin4.w;
            screenMax4 /= screenMax4.w;

            // 提取屏幕边界（原始跨度）
            float screenLeft = Math.Min(screenMin4.x, screenMax4.x);
            float screenRight = Math.Max(screenMin4.x, screenMax4.x);
            float screenBottom = Math.Min(screenMin4.y, screenMax4.y);
            float screenTop = Math.Max(screenMin4.y, screenMax4.y);

            // 计算有效宽度和高度
            float width = Math.Max(0f, screenRight - screenLeft);
            float height = Math.Max(0f, screenTop - screenBottom);

            // 投影面积占比
            float screenRatio = width * height;

            if (screenRatio >= OCCLUDER_SCREEN_THREASHOLD)
            {
                occluderMfIndex.AddNoResize(index);
            }
            else
            {
                occludeeMfIndex.AddNoResize(index);
            }
        }
    }
    #endregion

    #region Collect Vertex Info
    private void CollectVertexInfo()
    {
        //清理数据
        _occludeeMatrixList.Clear();
        _occluderMatrixList.Clear();
        _occluderTriangleIndex.Clear();

        _occluderTriangleCount = 0;


        Profiler.BeginSample("Collect Occluder Vertex");
        // 记录当前顶点信息数组长度 用来作为偏移
        int occulderVertexCount = 0;
        // 记录当前模型的M矩阵索引
        int matrixIndex = 0;
        // 记录当前模型的顶点的开始索引
        int modelStartIndex = 0;
        occluderCount = _occluderMfIndex.Length;
        for (int i = 0; i < _occluderMfIndex.Length; i++)
        {
            MeshFilter mf = _meshFilters[_occluderMfIndex[i]];

            Mesh mesh = mf.mesh;
            _occluderVertexPosTempList.Clear();
            mesh.GetVertices(_occluderVertexPosTempList);

            _triangleIndexVertexTemp.Clear();
            mesh.GetTriangles(_triangleIndexVertexTemp, 0);
            int triangleVertexCount = _triangleIndexVertexTemp.Count;
            for (int j = 0; j < triangleVertexCount; j += 3)
            {
                _occluderTriangleIndex.Add(new int4(_triangleIndexVertexTemp[j], _triangleIndexVertexTemp[j + 1]
                    , _triangleIndexVertexTemp[j + 2], modelStartIndex));
            }

            //计算当前三角形数量
            _occluderTriangleCount += triangleVertexCount / 3;

            int vertexCount = _occluderVertexPosTempList.Count;
            for (int j = 0; j < vertexCount; j++)
            {
                Vector3 vertex = _occluderVertexPosTempList[j];
                //Debug.Log(occulderVertexCount);
                _occluderVerextInfo[occulderVertexCount++] = new VertexInfo()
                {
                    vertex = vertex,
                    modelMatrixIndex = matrixIndex
                };
            }

            modelStartIndex += vertexCount;

            float4x4 mMatrix = mf.transform.localToWorldMatrix;
            _occluderMatrixList.Add(mMatrix);
            matrixIndex++;
        }

        // 调度完成 节点坐标获取作业
        dependency = new VertexTransferJob()
        {
            vpMatrix = vpMatrix,
            vertexInfos = _occluderVerextInfo,
            modelMatrixList = _occluderMatrixList,
            ClipVertexResult = _occluderClipVertexInfo
        }.Schedule(occulderVertexCount, 16, dependency);

        Profiler.EndSample();

        Profiler.BeginSample("Collect Occludee Vertex");
        //被遮挡物记录包围盒
        int occludeeVertexCount = 0;
        matrixIndex = 0;
        for (int i = 0; i < _occludeeMfIndex.Length; i++)
        {
            MeshFilter mf = _meshFilters[_occludeeMfIndex[i]];
            Bounds bounds = mf.mesh.bounds;
            Vector3 max = bounds.max;
            Vector3 min = bounds.min;
            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
            {
                vertex = max,
                modelMatrixIndex = matrixIndex,
            };
            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
            {
                vertex = new float3(bounds.min.x, bounds.max.y, bounds.max.z),
                modelMatrixIndex = matrixIndex,
            };
            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
            {
                vertex = new float3(bounds.min.x, bounds.max.y, bounds.min.z),
                modelMatrixIndex = matrixIndex,
            };
            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
            {
                vertex = new float3(bounds.max.x, bounds.max.y, bounds.min.z),
                modelMatrixIndex = matrixIndex,
            };
            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
            {
                vertex = new float3(bounds.max.x, bounds.min.y, bounds.max.z),
                modelMatrixIndex = matrixIndex,
            };
            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
            {
                vertex = new float3(bounds.min.x, bounds.min.y, bounds.max.z),
                modelMatrixIndex = matrixIndex,
            };
            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
            {
                vertex = min,
                modelMatrixIndex = matrixIndex,
            };
            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
            {
                vertex = new float3(bounds.max.x, bounds.min.y, bounds.min.z),
                modelMatrixIndex = matrixIndex,
            };

            float4x4 mMatrix = mf.transform.localToWorldMatrix;
            _occludeeMatrixList.Add(mMatrix);
            matrixIndex++;
        }

        dependency = new VertexTransferJob()
        {
            vpMatrix = vpMatrix,
            vertexInfos = _occludeeBoundsVertexInfo,
            modelMatrixList = _occludeeMatrixList,
            ClipVertexResult = _occludeeClipVertexInfo
        }.Schedule(occludeeVertexCount, 16, dependency);
        Profiler.EndSample();
    }

    [BurstCompile]
    public struct VertexTransferJob : IJobParallelFor
    {
        [ReadOnly]
        public float4x4 vpMatrix;
        [ReadOnly]
        public NativeArray<VertexInfo> vertexInfos;
        [ReadOnly]
        public NativeList<float4x4> modelMatrixList;
        public NativeArray<float4> ClipVertexResult;
        public void Execute(int index)
        {
            VertexInfo info = vertexInfos[index];
            float3 vertex = info.vertex;
            float4x4 mMatrix = modelMatrixList[info.modelMatrixIndex];
            float4x4 mvpMatrix = math.mul(vpMatrix, mMatrix);
            float4 result = math.mul(mvpMatrix, new float4(vertex.x, vertex.y, vertex.z, 1));
            // Clip空间坐标获得
            ClipVertexResult[index] = result;
        }
    }
    #endregion

    #region Collect Occluder Triangle Info
    private void CollectOccluderTriangleInfo()
    {
        _occluderTriangleInfoList.Clear();
        //Debug.Log(_occluderTriangleCount);
        // 构建三角形信息
        dependency = new OccluderTriangleJob()
        {
            screenMatrix = _screenMatrix,
            triangleIndexes = _occluderTriangleIndex.AsArray(),
            vertexClip = _occluderClipVertexInfo,
            triangleInfoWriter = _occluderTriangleInfoList.AsParallelWriter()
        }.Schedule(_occluderTriangleCount, 16, dependency);

        //排序三角形
        dependency = new OccluderTriangleDepthSortJob()
        {
            occulderTriangleInfo = _occluderTriangleInfoList
        }.Schedule(dependency);
    }

    [BurstCompile]
    private struct OccluderTriangleJob : IJobParallelFor
    {
        [ReadOnly]
        public Matrix4x4 screenMatrix;
        [ReadOnly]
        public NativeArray<int4> triangleIndexes;
        [ReadOnly]
        public NativeArray<float4> vertexClip;
        [NativeDisableParallelForRestriction]
        public NativeList<TriangleInfo>.ParallelWriter triangleInfoWriter;

        public void Execute(int index)
        {
            int4 triangleIdx = triangleIndexes[index];
            float4 v0 = vertexClip[triangleIdx.x + triangleIdx.w];
            float4 v1 = vertexClip[triangleIdx.y + triangleIdx.w];
            float4 v2 = vertexClip[triangleIdx.z + triangleIdx.w];

            bool neadClip = CullingTools.TriangleClip(v0, v1, v2);

            if (!neadClip)
            {
                //转换NDC坐标
                float4 ndcV0 = v0 / v0.w;
                float4 ndcV1 = v1 / v1.w;
                float4 ndcV2 = v2 / v2.w;

                //转换屏幕坐标
                float4 screenV0 = math.mul(screenMatrix, ndcV0);
                float4 screenV1 = math.mul(screenMatrix, ndcV1);
                float4 screenV2 = math.mul(screenMatrix, ndcV2);

                if (screenV0.y > screenV1.y)
                {
                    float4 tmp = screenV0;
                    screenV0 = screenV1;
                    screenV1 = tmp;
                }

                if (screenV0.y > screenV2.y)
                {
                    float4 tmp = screenV0;
                    screenV0 = screenV2;
                    screenV2 = tmp;
                }

                //这里也可以记录中间节点的索引 省下3次float4赋值过程
                if (screenV1.y > screenV2.y)
                {
                    float4 tmp = screenV1;
                    screenV1 = screenV2;
                    screenV2 = tmp;
                }

                float depth = screenV0.z;
                if (screenV1.z > depth)
                {
                    depth = screenV1.z;
                }

                if (screenV2.z > depth)
                {
                    depth = screenV2.z;
                }

                triangleInfoWriter.AddNoResize(new TriangleInfo()
                {
                    v0 = screenV0,
                    v1 = screenV1,
                    v2 = screenV2,
                    maxDepth = depth
                });
            }
        }
    }

    [BurstCompile]
    private struct OccluderTriangleDepthSortJob : IJob
    {
        public NativeList<TriangleInfo> occulderTriangleInfo;

        public unsafe void Execute()
        {
            NativeSortExtension.Sort((TriangleInfo*)occulderTriangleInfo.GetUnsafePtr(), occulderTriangleInfo.Length, new TriangleDepthCompare());
        }
    }

    private struct TriangleDepthCompare : IComparer<TriangleInfo>
    {
        //false则由近及远排序
        public bool ReverseZ;

        public TriangleDepthCompare(bool ReverseZ = false)
        {
            this.ReverseZ = ReverseZ;
        }

        public int Compare(TriangleInfo x, TriangleInfo y)
        {
            int depthComparison = x.maxDepth.CompareTo(y.maxDepth);
            if (ReverseZ)
            {
                depthComparison = -depthComparison;
            }

            return depthComparison;
        }
    }
    #endregion

    #region Collect Occluder Bounds Info
    private void CollectOccluderBoundsInfo()
    {

    }
    #endregion
}
