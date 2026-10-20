// Calm gradient sky for the login scene: top, horizon and bottom colors,
// plus a soft colored glow toward one direction. Cheap enough for Quest.
Shader "Nexora/Gradient Skybox"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.02, 0.04, 0.10, 1)
        _HorizonColor ("Horizon Color", Color) = (0.08, 0.22, 0.34, 1)
        _BottomColor ("Bottom Color", Color) = (0.01, 0.02, 0.05, 1)
        _HorizonSharpness ("Horizon Sharpness", Range(0.5, 8)) = 2.5
        _GlowColor ("Glow Color", Color) = (0.85, 0.18, 0.27, 1)
        _GlowDirection ("Glow Direction", Vector) = (0, 0.05, 1, 0)
        _GlowSize ("Glow Size", Range(1, 64)) = 6
        _GlowStrength ("Glow Strength", Range(0, 2)) = 0.45
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _HorizonColor;
            fixed4 _BottomColor;
            half _HorizonSharpness;
            fixed4 _GlowColor;
            float4 _GlowDirection;
            half _GlowSize;
            half _GlowStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);

                // Above the horizon blend to the top color, below it to the bottom color.
                half up = pow(saturate(dir.y), 1.0 / _HorizonSharpness);
                half down = pow(saturate(-dir.y), 1.0 / _HorizonSharpness);
                fixed3 col = lerp(_HorizonColor.rgb, _TopColor.rgb, up);
                col = lerp(col, _BottomColor.rgb, down);

                // Soft glow toward the chosen direction.
                half glow = pow(saturate(dot(dir, normalize(_GlowDirection.xyz))), _GlowSize);
                col += _GlowColor.rgb * glow * _GlowStrength;

                return fixed4(col, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
