// Pixel-art sloshing ink for the HUD ink jar.
// Use on a UI Image whose Sprite is the jar's INTERIOR MASK (white inside the hollow, transparent elsewhere).
// Ink only draws on mask pixels, so it can never spill out of the jar. Values are driven by InkJarUI.cs.

Shader "UI/Ink Jar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Interior Mask (sprite)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _InkColor     ("Ink Color", Color) = (0.08, 0.06, 0.18, 1)
        _SurfaceColor ("Surface (top row) Color", Color) = (0.3, 0.25, 0.55, 1)

        _PixelGrid  ("Pixel Grid", Range(16, 128)) = 32
        _Fill       ("Fill (0-1)", Range(0, 1)) = 1
        _FillBottom ("Interior Bottom (0-1)", Range(0, 1)) = 0.1
        _FillTop    ("Interior Top (0-1)", Range(0, 1)) = 0.8
        _Tilt       ("Tilt", Range(-0.5, 0.5)) = 0
        _WaveAmp    ("Wave Amplitude", Range(0, 0.1)) = 0.01
        _WaveFreq   ("Wave Frequency", Float) = 1.5
        _WavePhase  ("Wave Phase", Float) = 0

        // --- Standard UI properties (needed for Mask / RectMask2D / canvas rendering) ---
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _InkColor;
            fixed4 _SurfaceColor;
            float _PixelGrid;
            float _Fill;
            float _FillBottom;
            float _FillTop;
            float _Tilt;
            float _WaveAmp;
            float _WaveFreq;
            float _WavePhase;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Snap to the centre of the pixel-art grid cell, so every edge moves in whole pixels.
                float grid = max(1.0, floor(_PixelGrid));
                float2 cell = (floor(IN.texcoord * grid) + 0.5) / grid;

                // 1) Only draw inside the jar's hollow.
                float inside = step(0.5, tex2D(_MainTex, cell).a);

                // 2) Liquid surface height at this column: fill level, tilted by slosh, plus a gentle wave.
                float baseHeight = lerp(_FillBottom, _FillTop, saturate(_Fill));
                float surface = baseHeight
                              + _Tilt * (cell.x - 0.5)
                              + _WaveAmp * sin(cell.x * _WaveFreq * 6.2831853 + _WavePhase);

                float isInk = step(cell.y, surface) * step(0.0001, _Fill);

                // 3) Top row of ink gets the lighter surface colour.
                float isSurface = isInk * step(surface, cell.y + 1.0 / grid);

                fixed4 col = lerp(_InkColor, _SurfaceColor, isSurface);
                col *= IN.color;
                col.a *= inside * isInk;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
