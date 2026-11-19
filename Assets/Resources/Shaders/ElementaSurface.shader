// Собственный непрозрачный шейдер ELEMENTA.
// Почему свой: встроенный "Standard" Unity вырезает из билда, если на него нет
// ссылок из ассетов (материалы создаются кодом) — в игре всё становится розовым
// «error material». Шейдер, лежащий в папке Resources, всегда попадает в билд.
Shader "Elementa/Surface"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.35
        _Metallic ("Metallic", Range(0,1)) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _EmissionColor;
            half _Glossiness;
            half _Metallic;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wnrm : TEXCOORD1;
                float3 wpos : TEXCOORD2;
                fixed4 color : COLOR;
                UNITY_FOG_COORDS(3)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.wnrm = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed3 albedo = tex.rgb * _Color.rgb * i.color.rgb;

                float3 n = normalize(i.wnrm);
                // основной источник (направленный свет сцены)
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float ndl = saturate(dot(n, l));
                // мягкий «полу-ламберт», чтобы тени не были угольно-чёрными, как в 2D-играх
                float wrap = saturate(dot(n, l) * 0.5 + 0.5);
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * albedo * 1.15;
                fixed3 diffuse = _LightColor0.rgb * albedo * lerp(ndl, wrap, 0.45);

                // блик — даёт «дорогой» вид металлам и пластику
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.wpos);
                float3 h = normalize(l + viewDir);
                float spec = pow(saturate(dot(n, h)), lerp(8.0, 64.0, _Glossiness)) * lerp(0.15, 1.0, _Glossiness);
                fixed3 specColor = _LightColor0.rgb * spec * lerp(0.25, 1.0, _Metallic);

                fixed3 col = ambient + diffuse + specColor + _EmissionColor.rgb * albedo;
                fixed4 outCol = fixed4(col, _Color.a * tex.a);
                UNITY_APPLY_FOG(i.fogCoord, outCol);
                return outCol;
            }
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f
            {
                V2F_SHADOW_CASTER;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    Fallback Off
}
