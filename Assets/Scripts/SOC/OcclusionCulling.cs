//using System;
//using System.Collections.Generic;
//using System.IO;
//using Unity.Burst;
//using Unity.Collections;
//using Unity.Collections.LowLevel.Unsafe;
//using Unity.Jobs;
//using Unity.Jobs.LowLevel.Unsafe;
//using Unity.Mathematics;
//using UnityEngine;
//using UnityEngine.Profiling;

//public class OcclusionCulling : MonoBehaviour
//{
//    public Transform root;
//    private List<MeshFilter> _meshFilters;
//    private Camera _mainCamera;

//    private float _screenWidth;
//    private float _screenHeight;
//    private float _screenArea;
//    private float4x4 _screenMatrix;
//    private const int DEFAULT_CONTAINER_SIZE = 104629;
//    private const float OCCLUDER_SCREEN_THREASHOLD = 0.01f;

//    private JobHandle dependency;

//    private Matrix4x4 vpMatrix;

//    //Debug Property
//    [Header("Debug")]
//    public int occluderCount = 0;
//    public int occludeeCount = 0;
//    public bool needDebug = false;


//    //Frustum Culling
//    private int _objectCount = 0;
//    private Plane[] tempPlane;
//    private NativeArray<FrustumPlane> _frustumPlanes;
//    private NativeList<BoundsInfo> _inFrustumBounds;


//    //Select Occluder
//    private NativeList<int> _occluderMfIndex;
//    private NativeList<int> _occludeeMfIndex;


//    //Collect Vertex Info
//    private List<Vector3> _occluderVertexPosTempList; // 临时存储 模型节点的位置
//    private List<int> _triangleIndexVertexTemp;
//    //private VertexInfo[] _occluderVertexTempArray;
//    //private VertexInfo[] _occludeeBoundsVertexTempArray;
//    // 存储物体顶点的局部坐标
//    private NativeArray<VertexInfo> _occluderVertexInfo;
//    private NativeArray<VertexInfo> _occludeeBoundsVertexInfo;
//    // 存储遮挡者三角形索引信息
//    private NativeList<int4> _occluderTriangleIndex; // （v0,v1,v2,该mesh的顶点起始索引）
//    private int _occluderTriangleCount;
//    // 物体顶点M矩阵
//    private NativeList<float4x4> _occluderMatrixList;
//    private NativeList<float4x4> _occludeeMatrixList;
//    // 裁剪空间坐标
//    private NativeArray<float4> _occluderClipVertexInfo;
//    private NativeArray<float4> _occludeeClipVertexInfo;


//    //Collect Occluder Triangle Info
//    private NativeList<TriangleInfo> _occluderTriangleInfoList;


//    //Collect Occludee Bounds Info
//    private NativeArray<ScreenBoundsInfo> _occludeeBoundsInfoArray;

//    //Rasterize Occluder Triangle
//    private static int tileRow = Screen.height;
//    private static int tileCol = Screen.width / 32;
//    private static int tileCount = tileRow * tileCol;
//    private NativeArray<TileInfo> _tileNativeArray;

//    private NativeArray<bool> _objectsVisibility;
//    // Start is called before the first frame update

//    void Awake()
//    {
//        JobsUtility.JobWorkerCount = 4;
//        _mainCamera = Camera.main;
//        //Init();
//        _screenWidth = Screen.width;
//        _screenHeight = Screen.height;
//        _screenArea = _screenWidth * _screenHeight;
//        _screenMatrix = new float4x4(
//            new float4(_screenWidth * 0.5f, 0, 0, 0),
//            new float4(0, _screenHeight * 0.5f, 0, 0),
//            new float4(0, 0, 1, 0),
//            new float4(_screenWidth * 0.5f, _screenHeight * 0.5f, 0, 1));

//        _meshFilters = new List<MeshFilter>();
//        _frustumPlanes = new NativeArray<FrustumPlane>(6, Allocator.Persistent);
//        _inFrustumBounds = new NativeList<BoundsInfo>(Allocator.Persistent);
//        _objectsVisibility = new NativeArray<bool>(DEFAULT_CONTAINER_SIZE, Allocator.Persistent);

//        _occluderMfIndex = new NativeList<int>(DEFAULT_CONTAINER_SIZE / 3, Allocator.Persistent);
//        _occludeeMfIndex = new NativeList<int>(DEFAULT_CONTAINER_SIZE / 3, Allocator.Persistent);

//        _occluderVertexPosTempList = new List<Vector3>();
//        _triangleIndexVertexTemp = new List<int>();

//        _occluderVertexInfo = new NativeArray<VertexInfo>(DEFAULT_CONTAINER_SIZE, Allocator.Persistent);
//        _occludeeBoundsVertexInfo = new NativeArray<VertexInfo>(DEFAULT_CONTAINER_SIZE, Allocator.Persistent);

//        _occluderTriangleIndex = new NativeList<int4>(Allocator.Persistent);
//        _occluderMatrixList = new NativeList<float4x4>(Allocator.Persistent);
//        _occludeeMatrixList = new NativeList<float4x4>(Allocator.Persistent);

//        _occluderClipVertexInfo = new NativeArray<float4>(DEFAULT_CONTAINER_SIZE, Allocator.Persistent);
//        _occludeeClipVertexInfo = new NativeArray<float4>(DEFAULT_CONTAINER_SIZE, Allocator.Persistent);

//        _occluderTriangleInfoList = new NativeList<TriangleInfo>(DEFAULT_CONTAINER_SIZE / 3, Allocator.Persistent);
//        _occludeeBoundsInfoArray = new NativeArray<ScreenBoundsInfo>(DEFAULT_CONTAINER_SIZE / 3, Allocator.Persistent);

//        _tileNativeArray = new NativeArray<TileInfo>(tileCount, Allocator.Persistent);
//    }
//    private void OnDestroy()
//    {
//        dependency.Complete();
//        _frustumPlanes.Dispose();
//        _inFrustumBounds.Dispose();
//        _objectsVisibility.Dispose();
//        _occluderMfIndex.Dispose();
//        _occludeeMfIndex.Dispose();
//        _occluderTriangleIndex.Dispose();
//        _occluderMatrixList.Dispose();
//        _occludeeMatrixList.Dispose();
//        _occluderClipVertexInfo.Dispose();
//        _occludeeClipVertexInfo.Dispose();
//        _occluderTriangleInfoList.Dispose();
//        _occludeeBoundsInfoArray.Dispose();
//        _tileNativeArray.Dispose();

//        JobsUtility.ResetJobWorkerCount();
//    }

//    // Update is called once per frame
//    void Update()
//    {
//        dependency.Complete();

//        //缓存Camera的VP矩阵
//        vpMatrix = _mainCamera.projectionMatrix * _mainCamera.worldToCameraMatrix;

//        Profiler.BeginSample("Frustum Culling");
//        FrustumCulling(_mainCamera);
//        Profiler.EndSample();

//        Profiler.BeginSample("Select Occulder");
//        SelectOccluder();
//        Profiler.EndSample();

//        Profiler.BeginSample("Collect Vertex Info");
//        CollectVertexInfo();
//        Profiler.EndSample();

//        Profiler.BeginSample("Collect Occluder Triangle Info");
//        CollectOccluderTriangleInfo();
//        Profiler.EndSample();

//        Profiler.BeginSample("Collect Occludee Bounds Info");
//        CollectOccludeeBoundsInfo();
//        Profiler.EndSample();
//    }

//    #region Frustum Culling
//    private void FrustumCulling(Camera camera)
//    {
//        _meshFilters.Clear();
//        _inFrustumBounds.Clear();
//        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

//        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);

//        for (int i = 0; i < 6; i++)
//        {
//            _frustumPlanes[i] = new FrustumPlane(planes[i].normal, planes[i].distance);
//        }

//        foreach (var renderer in renderers)
//        {

//            Bounds bounds = renderer.bounds;
//            // SIMD 优化 考虑一个物体的包围盒和六个面同时计算
//            if (CullingTools.FrustumCulling(_frustumPlanes, bounds.center, bounds.extents.magnitude))
//            {
//                renderer.enabled = true;
//                _meshFilters.Add(renderer.gameObject.GetComponent<MeshFilter>());
//                _inFrustumBounds.Add(new BoundsInfo()
//                {
//                    center = bounds.center,
//                    extents = bounds.extents,
//                    radius = bounds.extents.magnitude,
//                });
//            }
//            else
//            {
//                renderer.enabled = false;
//            }
//        }
//        _objectCount = _meshFilters.Count;
//    }
//    #endregion

//    #region Select Occluder
//    private void SelectOccluder()
//    {
//        if (needDebug)
//        {
//            foreach (var mf in _meshFilters)
//            {
//                MeshRenderer renderer = mf.GetComponent<MeshRenderer>();
//                Material material = renderer.material;
//                material.color = Color.white;
//            }
//        }
//        _occluderMfIndex.Clear();
//        _occludeeMfIndex.Clear();
//        dependency = new OccluderCollectJob()
//        {
//            pMatrix = _mainCamera.projectionMatrix,
//            viewOrigin = new float4(_mainCamera.transform.position, 0),
//            screenArea = _screenArea,
//            screenHeight = _screenHeight,
//            screenWidth = _screenWidth,
//            occluderMfIndex = _occluderMfIndex.AsParallelWriter(),
//            occludeeMfIndex = _occludeeMfIndex.AsParallelWriter(),
//            bounds = _inFrustumBounds
//        }.Schedule(_objectCount, 16, dependency);
//        //Debug.Log(occludeeCnt + " " + occluderCnt);
//        dependency.Complete();
//        if (needDebug)
//        {
//            foreach (var idx in _occluderMfIndex)
//            {
//                MeshFilter mf = _meshFilters[idx];
//                MeshRenderer renderer = mf.GetComponent<MeshRenderer>();
//                Material material = renderer.material;
//                material.color = Color.red;
//            }
//        }

//    }

//    [BurstCompile]
//    private struct OccluderCollectJob : IJobParallelFor
//    {
//        [ReadOnly]
//        public float4x4 pMatrix;
//        [ReadOnly]
//        public float4 viewOrigin;
//        [ReadOnly]
//        public float screenArea;
//        [ReadOnly]
//        public float screenWidth;
//        [ReadOnly]
//        public float screenHeight;
//        [NativeDisableParallelForRestriction]
//        public NativeList<int>.ParallelWriter occluderMfIndex;
//        [NativeDisableParallelForRestriction]
//        public NativeList<int>.ParallelWriter occludeeMfIndex;
//        [ReadOnly]
//        public NativeList<BoundsInfo> bounds;
//        public void Execute(int index)
//        {
//            BoundsInfo boundsInfo = bounds[index];
//            float4 center = new float4(boundsInfo.center, 1);
//            float radius = boundsInfo.radius;
//            float dist = math.distance(center, viewOrigin);

//            float projScaleX = pMatrix[0].x; // 对应水平方向缩放因子
//            float projScaleY = pMatrix[1].y; // 对应垂直方向缩放因子

//            // 4. 计算屏幕空间缩放因子（考虑视口分辨率）
//            float screenScaleX = 0.5f * screenWidth * projScaleX;
//            float screenScaleY = 0.5f * screenHeight * projScaleY;
//            float screenMultiple = math.max(screenScaleX, screenScaleY);
//            float screenRadius = screenMultiple * radius / math.max(1.0f, dist);
//            float screenRatio = math.PI * screenRadius * screenRadius / screenArea;

//            if (screenRatio >= OCCLUDER_SCREEN_THREASHOLD)
//            {
//                occluderMfIndex.AddNoResize(index);
//            }
//            else
//            {
//                occludeeMfIndex.AddNoResize(index);
//            }
//        }
//    }
//    #endregion

//    #region Collect Vertex Info
//    private void CollectVertexInfo()
//    {
//        //清理数据
//        _occludeeMatrixList.Clear();
//        _occluderMatrixList.Clear();
//        _occluderTriangleIndex.Clear();

//        _occluderTriangleCount = 0;


//        Profiler.BeginSample("Collect Occluder Vertex");
//        // 记录当前顶点信息数组长度 用来作为偏移
//        int occulderVertexCount = 0;
//        // 记录当前模型的M矩阵索引
//        int matrixIndex = 0;
//        // 记录当前模型的顶点的开始索引
//        int modelStartIndex = 0;
//        occluderCount = _occluderMfIndex.Length;
//        for (int i = 0; i < _occluderMfIndex.Length; i++)
//        {
//            MeshFilter mf = _meshFilters[_occluderMfIndex[i]];

//            Mesh mesh = mf.mesh;
//            _occluderVertexPosTempList.Clear();
//            mesh.GetVertices(_occluderVertexPosTempList);

//            _triangleIndexVertexTemp.Clear();
//            mesh.GetTriangles(_triangleIndexVertexTemp, 0);
//            int triangleVertexCount = _triangleIndexVertexTemp.Count;
//            for (int j = 0; j < triangleVertexCount; j += 3)
//            {
//                _occluderTriangleIndex.Add(new int4(_triangleIndexVertexTemp[j], _triangleIndexVertexTemp[j + 1]
//                    , _triangleIndexVertexTemp[j + 2], modelStartIndex));
//            }

//            //计算当前三角形数量
//            _occluderTriangleCount += triangleVertexCount / 3;

//            int vertexCount = _occluderVertexPosTempList.Count;
//            for (int j = 0; j < vertexCount; j++)
//            {
//                Vector3 vertex = _occluderVertexPosTempList[j];
//                //Debug.Log(occulderVertexCount);
//                _occluderVertexInfo[occulderVertexCount++] = new VertexInfo()
//                {
//                    vertex = vertex,
//                    modelMatrixIndex = matrixIndex
//                };
//            }

//            modelStartIndex += vertexCount;

//            float4x4 mMatrix = mf.transform.localToWorldMatrix;
//            _occluderMatrixList.Add(mMatrix);
//            matrixIndex++;
//        }

//        // 调度完成 节点坐标获取作业
//        dependency = new VertexTransferJob()
//        {
//            vpMatrix = vpMatrix,
//            vertexInfos = _occluderVertexInfo,
//            modelMatrixList = _occluderMatrixList,
//            ClipVertexResult = _occluderClipVertexInfo
//        }.Schedule(occulderVertexCount, 16, dependency);

//        Profiler.EndSample();

//        occludeeCount = _occludeeMfIndex.Length;
//        Profiler.BeginSample("Collect Occludee Vertex");
//        //被遮挡物记录包围盒
//        int occludeeVertexCount = 0;
//        matrixIndex = 0;
//        for (int i = 0; i < _occludeeMfIndex.Length; i++)
//        {
//            MeshFilter mf = _meshFilters[_occludeeMfIndex[i]];
//            Bounds bounds = mf.mesh.bounds;
//            Vector3 max = bounds.max;
//            Vector3 min = bounds.min;
//            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
//            {
//                vertex = max,
//                modelMatrixIndex = matrixIndex,
//            };
//            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
//            {
//                vertex = new float3(bounds.min.x, bounds.max.y, bounds.max.z),
//                modelMatrixIndex = matrixIndex,
//            };
//            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
//            {
//                vertex = new float3(bounds.min.x, bounds.max.y, bounds.min.z),
//                modelMatrixIndex = matrixIndex,
//            };
//            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
//            {
//                vertex = new float3(bounds.max.x, bounds.max.y, bounds.min.z),
//                modelMatrixIndex = matrixIndex,
//            };
//            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
//            {
//                vertex = new float3(bounds.max.x, bounds.min.y, bounds.max.z),
//                modelMatrixIndex = matrixIndex,
//            };
//            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
//            {
//                vertex = new float3(bounds.min.x, bounds.min.y, bounds.max.z),
//                modelMatrixIndex = matrixIndex,
//            };
//            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
//            {
//                vertex = min,
//                modelMatrixIndex = matrixIndex,
//            };
//            _occludeeBoundsVertexInfo[occludeeVertexCount++] = new VertexInfo()
//            {
//                vertex = new float3(bounds.max.x, bounds.min.y, bounds.min.z),
//                modelMatrixIndex = matrixIndex,
//            };

//            float4x4 mMatrix = mf.transform.localToWorldMatrix;
//            _occludeeMatrixList.Add(mMatrix);
//            matrixIndex++;
//        }

//        dependency = new VertexTransferJob()
//        {
//            vpMatrix = vpMatrix,
//            vertexInfos = _occludeeBoundsVertexInfo,
//            modelMatrixList = _occludeeMatrixList,
//            ClipVertexResult = _occludeeClipVertexInfo
//        }.Schedule(occludeeVertexCount, 16, dependency);
//        Profiler.EndSample();
//    }

//    [BurstCompile]
//    public struct VertexTransferJob : IJobParallelFor
//    {
//        [ReadOnly]
//        public float4x4 vpMatrix;
//        [ReadOnly]
//        public NativeArray<VertexInfo> vertexInfos;
//        [ReadOnly]
//        public NativeList<float4x4> modelMatrixList;
//        public NativeArray<float4> ClipVertexResult;
//        public void Execute(int index)
//        {
//            VertexInfo info = vertexInfos[index];
//            float3 vertex = info.vertex;
//            float4x4 mMatrix = modelMatrixList[info.modelMatrixIndex];
//            float4x4 mvpMatrix = math.mul(vpMatrix, mMatrix);
//            float4 result = math.mul(mvpMatrix, new float4(vertex.x, vertex.y, vertex.z, 1));
//            // Clip空间坐标获得
//            ClipVertexResult[index] = result;
//        }
//    }
//    #endregion

//    #region Collect Occluder Triangle Info
//    private void CollectOccluderTriangleInfo()
//    {
//        _occluderTriangleInfoList.Clear();
//        //Debug.Log(_occluderTriangleCount);
//        // 构建三角形信息
//        dependency = new OccluderTriangleJob()
//        {
//            screenMatrix = _screenMatrix,
//            triangleIndexes = _occluderTriangleIndex.AsArray(),
//            vertexClip = _occluderClipVertexInfo,
//            triangleInfoWriter = _occluderTriangleInfoList.AsParallelWriter()
//        }.Schedule(_occluderTriangleCount, 16, dependency);

//        //排序三角形
//        dependency = new OccluderTriangleDepthSortJob()
//        {
//            occulderTriangleInfo = _occluderTriangleInfoList
//        }.Schedule(dependency);
//    }

//    [BurstCompile]
//    private struct OccluderTriangleJob : IJobParallelFor
//    {
//        [ReadOnly]
//        public float4x4 screenMatrix;
//        [ReadOnly]
//        public NativeArray<int4> triangleIndexes;
//        [ReadOnly]
//        public NativeArray<float4> vertexClip;
//        [NativeDisableParallelForRestriction]
//        public NativeList<TriangleInfo>.ParallelWriter triangleInfoWriter;

//        public void Execute(int index)
//        {
//            int4 triangleIdx = triangleIndexes[index];
//            float4 v0 = vertexClip[triangleIdx.x + triangleIdx.w];
//            float4 v1 = vertexClip[triangleIdx.y + triangleIdx.w];
//            float4 v2 = vertexClip[triangleIdx.z + triangleIdx.w];

//            bool neadClip = CullingTools.TriangleClip(v0, v1, v2);
//            if (!neadClip)
//            {
//                //转换NDC坐标
//                float4 ndcV0 = v0 / v0.w;
//                float4 ndcV1 = v1 / v1.w;
//                float4 ndcV2 = v2 / v2.w;

//                //转换屏幕坐标
//                float4 screenV0 = math.mul(screenMatrix, ndcV0);
//                float4 screenV1 = math.mul(screenMatrix, ndcV1);
//                float4 screenV2 = math.mul(screenMatrix, ndcV2);
//                if (screenV0.y > screenV1.y)
//                {
//                    float4 tmp = screenV0;
//                    screenV0 = screenV1;
//                    screenV1 = tmp;
//                }

//                if (screenV0.y > screenV2.y)
//                {
//                    float4 tmp = screenV0;
//                    screenV0 = screenV2;
//                    screenV2 = tmp;
//                }

//                //这里也可以记录中间节点的索引 省下3次float4赋值过程
//                if (screenV1.y > screenV2.y)
//                {
//                    float4 tmp = screenV1;
//                    screenV1 = screenV2;
//                    screenV2 = tmp;
//                }

//                float depth = screenV0.z;
//                if (screenV1.z > depth)
//                {
//                    depth = screenV1.z;
//                }

//                if (screenV2.z > depth)
//                {
//                    depth = screenV2.z;
//                }

//                triangleInfoWriter.AddNoResize(new TriangleInfo()
//                {
//                    v0 = screenV0,
//                    v1 = screenV1,
//                    v2 = screenV2,
//                    maxDepth = math.asuint(depth)
//                });
//            }
//        }
//    }

//    [BurstCompile]
//    private struct OccluderTriangleDepthSortJob : IJob
//    {
//        public NativeList<TriangleInfo> occulderTriangleInfo;

//        public unsafe void Execute()
//        {
//            NativeSortExtension.Sort((TriangleInfo*)occulderTriangleInfo.GetUnsafePtr(), occulderTriangleInfo.Length, new TriangleDepthCompare());
//        }
//    }

//    private struct TriangleDepthCompare : IComparer<TriangleInfo>
//    {
//        //false则由近及远排序
//        public bool ReverseZ;

//        public TriangleDepthCompare(bool ReverseZ = false)
//        {
//            this.ReverseZ = ReverseZ;
//        }

//        public int Compare(TriangleInfo x, TriangleInfo y)
//        {
//            int depthComparison = x.maxDepth.CompareTo(y.maxDepth);
//            if (ReverseZ)
//            {
//                depthComparison = -depthComparison;
//            }

//            return depthComparison;
//        }
//    }
//    #endregion

//    #region Collect Occludee Bounds Info
//    private void CollectOccludeeBoundsInfo()
//    {
//        int occludeeBoundsCount = _occludeeMfIndex.Length;
//        dependency = new OccludeeBoundsJob()
//        {
//            screenMatirx = _screenMatrix,
//            vertexes = _occludeeClipVertexInfo,
//            boundsInfo = _occludeeBoundsInfoArray,
//        }.Schedule(occludeeBoundsCount, 16, dependency);
//    }

//    [BurstCompile]
//    private struct OccludeeBoundsJob : IJobParallelFor
//    {
//        [ReadOnly]
//        public float4x4 screenMatirx;
//        [ReadOnly]
//        public NativeArray<float4> vertexes;
//        [NativeDisableParallelForRestriction]
//        public NativeArray<ScreenBoundsInfo> boundsInfo;
//        public void Execute(int index)
//        {
//            int start = index * 8;
//            int end = (index + 1) * 8;
//            float4 min = new float4(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue);
//            float4 max = new float4(float.MinValue, float.MinValue, float.MinValue, float.MinValue);
//            float minDepth = float.MaxValue;
//            for (int i = start; i < end; i++)
//            {
//                float4 vertex = vertexes[i];
//                float4 ndcResult = vertex / vertex.w;
//                float4 screenResult = math.mul(screenMatirx, ndcResult);

//                if (screenResult.x < min.x && screenResult.y < min.y)
//                {
//                    min = screenResult;
//                }

//                if (screenResult.x > max.x && screenResult.y > max.y)
//                {
//                    max = screenResult;
//                }

//                if (screenResult.z < minDepth)
//                {
//                    minDepth = screenResult.z;
//                }
//            }
//            boundsInfo[index] = new ScreenBoundsInfo()
//            {
//                min = min,
//                max = max,
//                minDepth = math.asuint(minDepth)
//            };
//        }
//    }
//    #endregion

//    #region Rasterize Occluder Triangle

//    private struct TileResetJob : IJobParallelFor
//    {
//        [NativeDisableParallelForRestriction]
//        public NativeArray<TileInfo> tileArray;
//        public void Execute(int index)
//        {
//            tileArray[index] = new TileInfo()
//            {
//                zMax0 = uint.MaxValue,
//                zMax1 = 0,
//                mask = 0
//            };
//        }
//    }
//    private struct OccluderTriangleRasterizeJob : IJobParallelFor
//    {
//        [ReadOnly]
//        private NativeArray<TriangleInfo> triangles;
//        [NativeDisableParallelForRestriction]
//        private NativeArray<TileInfo> tileArray;
//        public void Execute(int index)
//        {
//            TriangleInfo triangle = triangles[index];
//            float4 lowestVertex = triangle.v0;
//            float4 middleVertex = triangle.v1;
//            float4 highestVertex = triangle.v2;
//            float minx = math.min(lowestVertex.x, middleVertex.x);
//            minx = math.min(minx, highestVertex.x);
//            minx = math.max(0, minx); //防止越界
//            float maxx = math.max(lowestVertex.x, middleVertex.x);
//            maxx = math.max(maxx, highestVertex.x);
//            maxx = math.min(32 * tileCol, maxx); //防止越界
//            int startCol = math.max(0, (int)minx / 32);
//            int endCol = (int)maxx / 32;
//            int startRow = math.max(0, (int)lowestVertex.y);
//            int endRow = (int)highestVertex.y;
//            int midRow = (int)(middleVertex.y);
//            midRow = math.min(midRow, tileRow); //防止越界
//            endRow = math.min(endRow, tileRow); //防止越界
//            float v0v1Slope = triangle.CalculateSlope(lowestVertex, middleVertex);
//            float v0v2Slope = triangle.CalculateSlope(lowestVertex, highestVertex);
//            float leftSlope = v0v1Slope;
//            float rightSlope = v0v2Slope;
//            if (leftSlope > rightSlope)
//            {
//                leftSlope = v0v2Slope;
//                rightSlope = v0v1Slope;
//            }
//            float leftX = lowestVertex.x;
//            float rightX = lowestVertex.x;
//            //取倒数，因为我们的tile是32*1的
//            //每增加一行正好增加1个y值，所以我们的斜率是x/y
//            leftSlope = 1 / leftSlope;
//            rightSlope = 1 / rightSlope;
//            int rowStartX = startCol * 32;
//            for (int i = startRow; i < midRow; i++)
//            {
//                leftX += leftSlope;
//                rightX += rightSlope;
//                int curX = rowStartX;
//                for (int j = startCol; j < endCol; j++)
//                {
//                    uint leftEvent = ~0u >> math.max(0, ((int)leftX - curX));
//                    uint rightEvent = ~0u >> math.max(0, (curX - (int)rightX));
//                    uint triMask = leftEvent & rightEvent;
//                    UpdateHiZBuffer(i, j, triMask, triangle.maxDepth);
//                    curX += 32;
//                }
//            }
//        }

//        // 1.需要加非常多的原子操作
//        // 2.修改操作：为每个tile遍历所有三角形
//        // 3.放弃Mask，直接使用最大深度值进行更新,那么每次只需要锁住一个Z值就可以
//        private void UpdateHiZBuffer(int row, int col, uint triMask, uint triDepth)
//        {
//            TileInfo tile = tileArray[row * tileCol + col];
//            uint dist1t = tile.zMax1 - triDepth;
//            uint dist01 = tile.zMax0 - tile.zMax1;

//            if (dist1t > dist01)
//            {
//                tile.zMax1 = 0;
//                tile.mask = 0;
//            }

//            tile.zMax1 = math.max(tile.zMax1, triDepth);

//            tile.mask |= triMask;
//            if (tile.mask == ~0u)
//            {
//                tile.zMax0 = tile.zMax1;
//                tile.zMax1 = 0;
//                tile.mask = 0;
//            }
//        }
//    }
//    #endregion
//}
