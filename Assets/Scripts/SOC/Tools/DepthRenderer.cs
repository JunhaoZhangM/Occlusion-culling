using UnityEngine;
using Unity.Collections;

public class DepthRenderer : MonoBehaviour
{
    // �ֱ��ʺ� tile �ߴ磨��Ŀָ����
    public int textureWidth = 1920;
    public int textureHeight = 1080;
    public int tileWidth = 32;
    public int tileHeight = 1;

    private int tileColumns;  // tile ���� (1920/32 = 60)
    private int tileRows;     // tile ���� (1080/1 = 1080)
    private Texture2D depthTexture;
    private Color[] pixelBuffer;  // ���������������صĻ�������
    private bool needRender = true; // �Ƿ���Ҫ��Ⱦ

    void Start()
    {
        // ���� tile �������������
        tileColumns = textureWidth / tileWidth;   // 60 ��
        tileRows = textureHeight / tileHeight;    // 1080 ��
        int totalTiles = tileColumns * tileRows;  // �� tile ���� 60*1080 = 64800

        // ����������ʾ��ȵ���ͼ�����ػ�����
        depthTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false);
        depthTexture.wrapMode = TextureWrapMode.Clamp;
        pixelBuffer = new Color[textureWidth * textureHeight];
    }

    public void UpdateGUI(NativeArray<TileInfo> _tileNativeArray,bool need)
    {
        needRender = need;
        if(!needRender) return; // �������Ҫ��Ⱦ��ֱ�ӷ���
        // ÿ֡������ͼ���أ����� _tileNativeArray �е����ֵ���ûҶ���ɫ
        int width = textureWidth;
        for (int tileIndex = 0; tileIndex < _tileNativeArray.Length; ++tileIndex)
        {
            // ��ȡ��ǰ tile ����Ȳ�ת��Ϊ�Ҷ�(���Խ��Ҷ�ֵԽ�ߣ�������0~1��Χ��)
            float depth = _tileNativeArray[tileIndex].zMax0;
            if (depth < 0f) depth = 0f;
            if (depth > 1f) depth = 1f;
            Color gray = new Color(depth, depth, depth, 1f);

            // ���㵱ǰ tile ��Ӧ��ͼ��λ��
            int tileX = tileIndex % tileColumns;   // ��������0 �����У�
            int tileY = tileIndex / tileColumns;   // ��������0 �����У�
            int pixelY = tileY * tileHeight;       // ת��Ϊ���ص� Y ���꣨���� tileHeight=1����� pixelY ���� tileY��
            int pixelXStart = tileX * tileWidth;   // ��ǰ tile ������ʼ���ص� X ����

            // ������tile����(32��1����)����Ϊ��ͬ�ĻҶ�ֵ
            for (int px = 0; px < tileWidth; ++px)
            {
                int pixelX = pixelXStart + px;
                int pixelIndex = pixelY * width + pixelX;
                pixelBuffer[pixelIndex] = gray;
            }
            // ��ע��tileHeight=1�����账����ߵ� tile��������һ���ڲ� for ѭ������ pixelY �� pixelY+tileHeight-1��
        }

        // ������õ�������ɫ����Ӧ�õ�������
        depthTexture.SetPixels(pixelBuffer);
        depthTexture.Apply();
    }

    void OnGUI()
    {
        if (!needRender) return;
        if (depthTexture != null)
        {
            // �����ɵ���ȻҶ���ͼ���Ƶ���Ļ�� (���½�Ϊԭ�㣬�������� 1920��1080 ����)
            GUI.DrawTexture(new Rect(0, 0, textureWidth, textureHeight), depthTexture, ScaleMode.StretchToFill, false);
        }
    }
}
