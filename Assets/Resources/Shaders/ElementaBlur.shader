// Двухпроходное размытие для стеклянного интерфейса (горизонталь, затем вертикаль).
// Используется только внутри GlassBackdrop, наружу не торчит.
Shader "Hidden/Elementa/Blur"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
        _BlurDir ("Direction", Vector) = (0.001, 0, 0, 0)
        _Spread ("Разброс", Range(0.5, 4)) = 1.6
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Fog { Mode Off }
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _BlurDir;
            half _Spread;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 dir = _BlurDir.xy * _Spread;
                // 9 отсчётов гауссом — дешёво и достаточно мягко
                fixed4 sum = tex2D(_MainTex, i.uv) * 0.227027;
                sum += tex2D(_MainTex, i.uv + dir * 1.384615) * 0.316216;
                sum += tex2D(_MainTex, i.uv - dir * 1.384615) * 0.316216;
                sum += tex2D(_MainTex, i.uv + dir * 3.230769) * 0.070270;
                sum += tex2D(_MainTex, i.uv - dir * 3.230769) * 0.070270;
                return sum;
            }
            ENDCG
        }
    }
    Fallback Off
}
