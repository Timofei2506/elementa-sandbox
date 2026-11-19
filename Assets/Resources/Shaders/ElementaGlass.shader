// Стеклянный UI-шейдер ELEMENTA.
// Читает размытый кадр сцены (глобальная текстура _ElementaBlurTex, её готовит GlassBackdrop),
// накладывает его под панель, подкрашивает цветом панели и добавляет блик по верхней кромке.
// Форма панели (скругления) берётся из спрайта, поэтому работают 9-slice и маски.
Shader "Elementa/Glass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurStrength ("Размытие", Range(0,1)) = 0.85
        _BlurBoost ("Яркость размытия", Range(0,2)) = 1.15
        _Highlight ("Блик сверху", Range(0,2)) = 0.10
        _GlowColor ("Цвет свечения", Color) = (0.55,0.85,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
            Name "GLASS"
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
                float4 screenPos     : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            sampler2D _ElementaBlurTex;
            float4 _ElementaBlurTex_TexelSize;
            half _BlurStrength;
            half _BlurBoost;
            half _Highlight;
            fixed4 _GlowColor;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // цвет панели (в нём же лежит альфа прозрачности)
                fixed4 tint = tex2D(_MainTex, i.texcoord) * i.color;

                // размытый кадр сцены под панелью
                float2 uvScreen = i.screenPos.xy / max(i.screenPos.w, 1e-5);
                fixed3 backdrop = tex2D(_ElementaBlurTex, uvScreen).rgb * _BlurBoost;

                // стекло: фон просвечивает, но подкрашивается цветом панели
                fixed3 glass = lerp(backdrop, backdrop * tint.rgb * 1.6 + tint.rgb * 0.25, 1 - _BlurStrength);
                glass = lerp(glass, backdrop, _BlurStrength * 0.35);

                // мягкий блик по верхней кромке — «отражение света на стекле»
                float2 uv = i.texcoord;
                float edgeTop = smoothstep(0.62, 1.0, uv.y);
                float edgeSide = smoothstep(0.82, 1.0, uv.x) + smoothstep(0.82, 1.0, 1.0 - uv.x);
                glass += _GlowColor.rgb * _Highlight * (edgeTop + edgeSide * 0.5);

                fixed4 outCol = fixed4(glass, tint.a);

                #ifdef UNITY_UI_CLIP_RECT
                outCol.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outCol.a - 0.001);
                #endif
                return outCol;
            }
            ENDCG
        }
    }
    Fallback "UI/Default"
}
