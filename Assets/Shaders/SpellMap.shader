// The Eye spell map: a glowing ink map in a circle, centred on the player, rotated so the way they face is up.
// Use on a square RawImage on the overlay Canvas (above the ASCII filter). Values are driven by SpellMap.cs.
//
// _MainTex = the baked MapLayout texture (R = floor, G = wall). _RevealTex = MapExplorer's explored area.

Shader "UI/Spell Map"
{
    Properties
    {
        [PerRendererData] _MainTex ("Layout (R floor, G wall)", 2D) = "black" {}
        _RevealTex ("Explored Area", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Colours)]
        _InkColor    ("Wall Ink", Color) = (0.75, 0.55, 1.0, 1)
        _FloorColor  ("Floor Wash", Color) = (0.45, 0.3, 0.8, 0.35)
        _BackColor   ("Background", Color) = (0.1, 0.05, 0.2, 0.35)
        _RingColor   ("Edge Ring", Color) = (0.8, 0.65, 1.0, 0.9)
        _PlayerColor ("Player Marker", Color) = (1, 1, 1, 1)

        [Header(Ink)]
        _EdgeRaggedness ("Ragged Ink Edges", Range(0, 1)) = 0.35
        _NoiseScale     ("Ink Noise Scale (per metre)", Float) = 0.6

        [Header(Driven by SpellMap.cs)]
        _PlayerPos  ("Player Position (XZ)", Vector) = (0,0,0,0)
        _Yaw        ("Player Yaw (radians)", Float) = 0
        _ViewRadius ("View Radius (metres)", Float) = 15
        _WorldMin   ("Layout World Min (XZ)", Vector) = (0,0,0,0)
        _WorldSize  ("Layout World Size (XZ)", Vector) = (100,100,0,0)
        _Draw       ("Draw In (0-1)", Range(0, 1)) = 1
        _Smudge     ("Smudge Away (0-1)", Range(0, 1)) = 0

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
            #pragma target 3.0

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
            sampler2D _RevealTex;
            fixed4 _Color;
            fixed4 _InkColor;
            fixed4 _FloorColor;
            fixed4 _BackColor;
            fixed4 _RingColor;
            fixed4 _PlayerColor;
            float _EdgeRaggedness;
            float _NoiseScale;
            float4 _PlayerPos;
            float _Yaw;
            float _ViewRadius;
            float4 _WorldMin;
            float4 _WorldSize;
            float _Draw;
            float _Smudge;
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

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Smooth value noise, 0..1. Gives the ink its uneven, bleeding edges.
            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // -1..1 across the image, (0,0) = the player, +y = the way they're facing.
                float2 p = IN.texcoord * 2.0 - 1.0;

                // Smudge: smear the picture downwards and sideways in uneven streaks as it fades.
                float streak = Noise(float2(p.x * 9.0, 3.7));
                p += float2((Noise(p * 4.0) - 0.5) * 0.3, streak * 0.6) * _Smudge;
                float r = length(p);

                // Local map position -> world X/Z, rotated by the player's yaw.
                float s, c;
                sincos(_Yaw, s, c);
                float2 forward = float2(s, c);
                float2 right = float2(c, -s);
                float2 world = _PlayerPos.xy + (p.x * right + p.y * forward) * _ViewRadius;
                float2 uv = (world - _WorldMin.xy) / _WorldSize.xy;
                float inBounds = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);

                fixed4 rooms = tex2D(_MainTex, uv);
                float explored = tex2D(_RevealTex, uv).r;

                // Explored edge in world space so it doesn't swim as the player turns.
                float worldNoise = Noise(world * _NoiseScale);
                float known = smoothstep(0.35, 0.6, explored + (worldNoise - 0.5) * _EdgeRaggedness) * inBounds;

                // Draw-in: ink spreads out from the player to the rim.
                float drawNoise = Noise(p * 5.0 + 11.0);
                float drawFront = _Draw * 1.3;
                float drawn = 1.0 - smoothstep(drawFront - 0.2, drawFront, r + (drawNoise - 0.5) * 0.25);

                float circle = 1.0 - smoothstep(0.95, 1.0, r);
                float ring = smoothstep(0.9, 0.95, r) * (1.0 - smoothstep(0.98, 1.0, r));

                // Layer it up: background disc, floor wash, wall ink, rim, player marker.
                fixed4 col = _BackColor;
                col.a *= circle;
                col = lerp(col, fixed4(_FloorColor.rgb, max(col.a, _FloorColor.a)), rooms.r * known * circle);
                col = lerp(col, _InkColor, rooms.g * known * circle);
                col = lerp(col, _RingColor, ring * _RingColor.a);

                float marker = 1.0 - smoothstep(0.025, 0.035, length(p));
                float tick = step(abs(p.x), 0.008) * step(0.0, p.y) * step(p.y, 0.09);
                col = lerp(col, _PlayerColor, saturate(marker + tick) * _PlayerColor.a);

                // Fade away unevenly while smudging.
                float smudgeFade = 1.0 - smoothstep(0.2, 1.0, _Smudge + (worldNoise - 0.5) * 0.5);

                col *= IN.color;
                col.a *= drawn * smudgeFade;

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
