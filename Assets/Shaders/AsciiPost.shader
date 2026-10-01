// URP fullscreen ASCII post-process.
// Use with: Renderer Data > Add Renderer Feature > Full Screen Pass Renderer Feature
// Put this anywhere in Assets/ (e.g. Assets/Shaders/AsciiPost.shader)

Shader "Custom/Ascii Post"
{
    Properties
    {
        [NoScaleOffset] _AsciiAtlas ("ASCII Atlas (ramp + 4 edge glyphs)", 2D) = "white" {}
        _CharCount    ("Ramp Character Count", Float) = 10
        _CellSize     ("Cell Size (px)", Float) = 8
        _Gamma        ("Luminance Gamma", Range(0.2, 3)) = 1
        _EdgeStrength ("Edge Strength", Range(0, 4)) = 1
        _EdgeThreshold("Edge Threshold", Range(0, 1)) = 0.15
        _ColorBlend   ("Scene Color Blend", Range(0, 1)) = 1
        _Tint         ("Monochrome Tint", Color) = (0.55, 1.0, 0.65, 1)
        _Saturation   ("Saturation", Range(0, 2)) = 1

        [Header(Bright Cell Background)]
        _BgThreshold  ("Background Threshold", Range(0, 1)) = 0.55
        _BgSoftness   ("Background Softness", Range(0.001, 0.5)) = 0.08
        _BgShade      ("Background Shade (of cell color)", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ASCII"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_AsciiAtlas);
            SAMPLER(sampler_AsciiAtlas);

            CBUFFER_START(UnityPerMaterial)
                float  _CharCount;
                float  _CellSize;
                float  _Gamma;
                float  _EdgeStrength;
                float  _EdgeThreshold;
                float  _ColorBlend;
                float4 _Tint;
                float  _Saturation;
                float  _BgThreshold;
                float  _BgSoftness;
                float  _BgShade;
            CBUFFER_END

            #define EDGE_GLYPH_COUNT 4

            float3 SampleSrc(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
            }

            // offset01 is a position inside the cell, in 0..1
            float2 CellUV(float2 cellId, float2 offset01, float2 screen)
            {
                return ((cellId + offset01) * _CellSize) / screen;
            }

            // Average color of a cell, 4x4 taps. Used for the cell we are actually drawing.
            float3 CellColor(float2 cellId, float2 screen)
            {
                float3 sum = 0;
                [unroll]
                for (int y = 0; y < 4; y++)
                {
                    [unroll]
                    for (int x = 0; x < 4; x++)
                    {
                        float2 o = (float2(x, y) + 0.5) * 0.25;
                        sum += SampleSrc(CellUV(cellId, o, screen));
                    }
                }
                return sum * (1.0 / 16.0);
            }

            // Cheaper 2x2 luminance, used for the 3x3 Sobel neighborhood.
            float CellLum(float2 cellId, float2 screen)
            {
                float sum = 0;
                [unroll]
                for (int y = 0; y < 2; y++)
                {
                    [unroll]
                    for (int x = 0; x < 2; x++)
                    {
                        float2 o = (float2(x, y) + 0.5) * 0.5;
                        sum += Luminance(SampleSrc(CellUV(cellId, o, screen)));
                    }
                }
                return sum * 0.25;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screen = _ScreenParams.xy;
                float2 pix    = input.texcoord * screen;

                float2 cellId  = floor(pix / _CellSize);
                float2 inCell  = frac(pix / _CellSize);

                float3 cellCol = CellColor(cellId, screen);
                float  lum     = saturate(Luminance(cellCol));
                lum = pow(lum, _Gamma);

                // --- density glyph ---
                int rampCount = max(1, (int)_CharCount);
                int index = (int)floor(lum * rampCount);
                index = clamp(index, 0, rampCount - 1);

                // --- Sobel on cell luminance -> directional glyph ---
                float l[9];
                int i = 0;
                [unroll]
                for (int sy = -1; sy <= 1; sy++)
                {
                    [unroll]
                    for (int sx = -1; sx <= 1; sx++)
                    {
                        l[i++] = CellLum(cellId + float2(sx, sy), screen);
                    }
                }

                float gx = -l[0] + l[2] - 2.0 * l[3] + 2.0 * l[5] - l[6] + l[8];
                float gy = -l[0] - 2.0 * l[1] - l[2] + l[6] + 2.0 * l[7] + l[8];
                float mag = length(float2(gx, gy)) * _EdgeStrength;

                if (mag > _EdgeThreshold)
                {
                    // Edge runs perpendicular to the gradient.
                    float a = atan2(gy, gx) + PI * 0.5;
                    a = a - PI * floor(a / PI);              // wrap into [0, PI)
                    int bin = ((int)floor(a / (PI * 0.25) + 0.5)) % EDGE_GLYPH_COUNT;
                    index = rampCount + bin;                 // atlas order: dash, slash, pipe, backslash
                }

                // --- sample the atlas ---
                float totalTiles = rampCount + EDGE_GLYPH_COUNT;
                float2 atlasUV = float2((index + inCell.x) / totalTiles, inCell.y);
                float glyph = SAMPLE_TEXTURE2D(_AsciiAtlas, sampler_AsciiAtlas, atlasUV).r;

                // --- color ---
                float3 gray = Luminance(cellCol).xxx;
                float3 sat  = lerp(gray, cellCol, _Saturation);
                float3 col  = lerp(_Tint.rgb, sat, _ColorBlend);

                // --- background fill for bright cells ---
                // Above _BgThreshold, fill the whole cell with a dimmer shade of its
                // own color so dense/bright glyphs read as a solid block instead of
                // floating characters on empty black.
                float bgMask = smoothstep(_BgThreshold - _BgSoftness, _BgThreshold + _BgSoftness, lum);
                float3 bg = col * _BgShade * bgMask;

                float3 finalColor = lerp(bg, col, glyph);
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
