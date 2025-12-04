#ifndef __FORWARDPLUS_HLSL__
#define __FORWARDPLUS_HLSL__

// xy: Screen UV to tile coordinates.
// z: Tiles per row.
// w: Tile data size.
float4 _TileSettings;

StructuredBuffer<int> _TileLightIndices;

struct ForwardPlusTile
{
	int2 coordinates;

	int index;
	
	int GetTileDataSize()
	{
		return _TileSettings.w;
	}

	int GetHeaderIndex()
	{
		return index * GetTileDataSize();
	}

	int GetLightCount()
	{
		return _TileLightIndices[GetHeaderIndex()];
	}

	int GetLightIndex(int lightIndexInTile)
	{
		return _TileLightIndices[GetHeaderIndex() + 1 + lightIndexInTile];
	}

	bool IsMinimumEdgePixel(float2 screenUV)
	{
		float2 startUV = float2(coordinates) / _TileSettings.xy;
		return any((screenUV - startUV) < GetCameraTexInvSize());
	}
	
	int GetMaxLightsPerTile()
	{
		return GetTileDataSize() - 1;
	}

	int2 GetScreenSize()
	{
		return int2(round(GetCameraTexSize() / _TileSettings.xy));
	}
};

ForwardPlusTile GetForwardPlusTile(float2 screenUV)
{
	ForwardPlusTile tile;
	tile.coordinates = int2(screenUV * _TileSettings.xy);
	tile.index = tile.coordinates.y * _TileSettings.z +
		tile.coordinates.x;
	return tile;
}

#endif