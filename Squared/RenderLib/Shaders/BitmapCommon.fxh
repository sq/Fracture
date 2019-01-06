#ifndef MIP_BIAS
#define MIP_BIAS 0
#endif

#define ENABLE_DITHERING

float4 TransformPosition (float4 position, float offset) {
    // Transform to view space, then offset by half a pixel to align texels with screen pixels
#ifdef FNA
    // ... Except for OpenGL, who don't need no half pixels
    float4 modelViewPos = mul(position, Viewport.ModelView);
#else
    float4 modelViewPos = mul(position, Viewport.ModelView) - float4(offset, offset, 0, 0);
#endif
    // Finally project after offsetting
    return mul(modelViewPos, Viewport.Projection);
}

uniform const float2 BitmapTextureSize, BitmapTextureSize2;
uniform const float2 HalfTexel, HalfTexel2;

Texture2D BitmapTexture : register(t0);

sampler TextureSampler : register(s0) {
    Texture = (BitmapTexture);
    MipLODBias = MIP_BIAS;
};

Texture2D SecondTexture : register(t1);

sampler TextureSampler2 : register(s1) {
    Texture = (SecondTexture);
};

static const float2 Corners[] = {
    {0, 0},
    {1, 0},
    {1, 1},
    {0, 1}
};

inline float2 ComputeRegionSize (
    in float4 texRgn : POSITION1
) {
    return texRgn.zw - texRgn.xy;
}

inline float2 ComputeCorner (
    in int2 cornerIndex : BLENDINDICES0,
    in float2 regionSize
) {
    float2 corner = Corners[cornerIndex.x];
    return corner * regionSize;
}

inline float2 ComputeTexCoord (
    in int2 cornerIndex,
    in float2 corner,
    in float4 texRgn,
    out float4 newTexRgn
) {
    float2 texTL = min(texRgn.xy, texRgn.zw);
    float2 texBR = max(texRgn.xy, texRgn.zw);
    newTexRgn = float4(texTL.x, texTL.y, texBR.x, texBR.y);
    return clamp(
        texRgn.xy + corner, texTL, texBR
    );
}

inline float2 ComputeRotatedCorner (
    in float2 corner,
    in float4 texRgn : POSITION1,
    in float4 scaleOrigin : POSITION2, // scalex, scaley, originx, originy
    in float rotation : POSITION3
) {
    float2 regionSize = abs(texRgn.zw - texRgn.xy);

    corner = abs(corner);
    corner -= (scaleOrigin.zw * regionSize);
    float2 sinCos, rotatedCorner;
    corner *= scaleOrigin.xy;
    corner *= BitmapTextureSize;
    sincos(rotation, sinCos.x, sinCos.y);
    return float2(
        (sinCos.y * corner.x) - (sinCos.x * corner.y),
        (sinCos.x * corner.x) + (sinCos.y * corner.y)
    );
}

void ScreenSpaceVertexShader (
    in float3 position : POSITION0, // x, y
    in float4 texRgn1 : POSITION1, // x1, y1, x2, y2
    in float4 texRgn2 : POSITION2, // x1, y1, x2, y2
    in float4 scaleOrigin : POSITION3, // scalex, scaley, originx, originy
    in float rotation : POSITION4,
    inout float4 multiplyColor : COLOR0,
    inout float4 addColor : COLOR1,
    in int2 cornerIndex : BLENDINDICES0, // 0-3
    out float2 texCoord1 : TEXCOORD0,
    out float4 newTexRgn1 : TEXCOORD1,
    out float2 texCoord2 : TEXCOORD2,
    out float4 newTexRgn2 : TEXCOORD3,
    out float4 result : POSITION0
) {
    float2 regionSize = ComputeRegionSize(texRgn1);
    float2 corner = ComputeCorner(cornerIndex, regionSize);
    texCoord1 = ComputeTexCoord(cornerIndex, corner, texRgn1, newTexRgn1);
    texCoord2 = ComputeTexCoord(cornerIndex, corner, texRgn2, newTexRgn2);
    float2 rotatedCorner = ComputeRotatedCorner(corner, texRgn1, scaleOrigin, rotation);
    
    position.xy += rotatedCorner;

    result = TransformPosition(float4(position.xy, position.z, 1), 0.5);
}

void WorldSpaceVertexShader (
    in float3 position : POSITION0, // x, y
    in float4 texRgn1 : POSITION1, // x1, y1, x2, y2
    in float4 texRgn2 : POSITION2, // x1, y1, x2, y2
    in float4 scaleOrigin : POSITION3, // scalex, scaley, originx, originy
    in float rotation : POSITION4,
    inout float4 multiplyColor : COLOR0,
    inout float4 addColor : COLOR1,
    in int2 cornerIndex : BLENDINDICES0, // 0-3
    out float2 texCoord1 : TEXCOORD0,
    out float4 newTexRgn1 : TEXCOORD1,
    out float2 texCoord2 : TEXCOORD2,
    out float4 newTexRgn2 : TEXCOORD3,
    out float4 result : POSITION0
) {
    float2 regionSize = ComputeRegionSize(texRgn1);
    float2 corner = ComputeCorner(cornerIndex, regionSize);
    texCoord1 = ComputeTexCoord(cornerIndex, corner, texRgn1, newTexRgn1);
    texCoord2 = ComputeTexCoord(cornerIndex, corner, texRgn2, newTexRgn2);
    float2 rotatedCorner = ComputeRotatedCorner(corner, texRgn1, scaleOrigin, rotation);
    
    position.xy += rotatedCorner - Viewport.Position.xy;
    
    result = TransformPosition(float4(position.xy * Viewport.Scale.xy, position.z, 1), 0.5);
}

// Approximations from http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html

float3 SRGBToLinear (float3 srgb) {
  return srgb * (srgb * (srgb * 0.305306011 + 0.682171111) + 0.012522878);
}

float3 LinearToSRGB (float3 rgb) {
  float3 S1 = sqrt(rgb);
  float3 S2 = sqrt(S1);
  float3 S3 = sqrt(S2);
  return 0.662002687 * S1 + 0.684122060 * S2 - 0.323583601 * S3 - 0.0225411470 * rgb;
}