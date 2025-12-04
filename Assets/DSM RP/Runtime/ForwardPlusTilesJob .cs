using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct ForwardPlusTilesJob : IJobFor
{
    [ReadOnly]
    public NativeArray<float4> lightBounds; // 光照范围
    [WriteOnly]
    public NativeArray<int> tileBufferData;

    public int otherLightCount;
    public int maxLightPreTile;
    public int tileDataSize;
    public int tilePreRow;
    public float2 tileScreenSize;
    
    public void Execute(int tileIndex)
    {
        int x = tileIndex % tilePreRow;
        int y = tileIndex / tilePreRow;
        float4 bound = new float4(x, y, x + 1, y + 1) * tileScreenSize.xyxy;

        int start = tileIndex * tileDataSize;
        int offset = start;
        int lightCount = 0; // 当前 tile 内的光源数量
        for(int i = 0; i < otherLightCount; i++)
        {
            float4 lightBound = lightBounds[i];
            // 判断是否在边界内
            if(math.all(new float4(lightBound.xy, bound.xy) <= new float4(bound.zw, lightBound.zw)))
            {
                tileBufferData[++offset] = i;
                if(++lightCount >= maxLightPreTile)
                {
                    break;
                }
            }
        }
        tileBufferData[start] = lightCount;
    }
}