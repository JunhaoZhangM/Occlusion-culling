using UnityEngine;
using Unity.Collections;

public class DepthRenderer : MonoBehaviour
{
    // 分辨率和 tile 尺寸（题目指定）
    public int textureWidth = 1920;
    public int textureHeight = 1080;
    public int tileWidth = 32;
    public int tileHeight = 1;

    private int tileColumns;  // tile 列数 (1920/32 = 60)
    private int tileRows;     // tile 行数 (1080/1 = 1080)
    private Texture2D depthTexture;
    private Color[] pixelBuffer;  // 用于批量设置像素的缓冲数组
    private bool needRender = true; // 是否需要渲染

    void Start()
    {
        // 计算 tile 网格的行列数量
        tileColumns = textureWidth / tileWidth;   // 60 列
        tileRows = textureHeight / tileHeight;    // 1080 行
        int totalTiles = tileColumns * tileRows;  // 总 tile 数量 60*1080 = 64800

        // 创建用于显示深度的贴图和像素缓冲区
        depthTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false);
        depthTexture.wrapMode = TextureWrapMode.Clamp;
        pixelBuffer = new Color[textureWidth * textureHeight];
    }

    public void UpdateGUI(NativeArray<TileInfo> _tileNativeArray,bool need)
    {
        needRender = need;
        if(!needRender) return; // 如果不需要渲染，直接返回
        // 每帧更新贴图像素：根据 _tileNativeArray 中的深度值设置灰度颜色
        int width = textureWidth;
        for (int tileIndex = 0; tileIndex < _tileNativeArray.Length; ++tileIndex)
        {
            // 获取当前 tile 的深度并转换为灰度(深度越大灰度值越高，限制在0~1范围内)
            float depth = _tileNativeArray[tileIndex].zMax0;
            if (depth < 0f) depth = 0f;
            if (depth > 1f) depth = 1f;
            Color gray = new Color(depth, depth, depth, 1f);

            // 计算当前 tile 对应的图像位置
            int tileX = tileIndex % tileColumns;   // 列索引（0 最左列）
            int tileY = tileIndex / tileColumns;   // 行索引（0 最下行）
            int pixelY = tileY * tileHeight;       // 转换为像素的 Y 坐标（由于 tileHeight=1，因此 pixelY 等于 tileY）
            int pixelXStart = tileX * tileWidth;   // 当前 tile 区块起始像素的 X 坐标

            // 将整块tile区域(32×1像素)设置为相同的灰度值
            for (int px = 0; px < tileWidth; ++px)
            {
                int pixelX = pixelXStart + px;
                int pixelIndex = pixelY * width + pixelX;
                pixelBuffer[pixelIndex] = gray;
            }
            // （注：tileHeight=1，如需处理更高的 tile，可增加一个内层 for 循环遍历 pixelY 到 pixelY+tileHeight-1）
        }

        // 将计算好的像素颜色数组应用到纹理上
        depthTexture.SetPixels(pixelBuffer);
        depthTexture.Apply();
    }

    void OnGUI()
    {
        if (!needRender) return;
        if (depthTexture != null)
        {
            // 将生成的深度灰度贴图绘制到屏幕上 (左下角为原点，铺满整个 1920×1080 区域)
            GUI.DrawTexture(new Rect(0, 0, textureWidth, textureHeight), depthTexture, ScaleMode.StretchToFill, false);
        }
    }
}
