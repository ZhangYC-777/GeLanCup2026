Shader "Hidden/GeLanCup/BreadTrialFlashlight"
{
    Properties
    {
        _MainTex ("Maze View", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _View;
            float2 _Facing;
            float2 _Runner;
            float _Danger;
            float _RayLimits[96];

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0)
                    uv.y = 1.0 - uv.y;
                #endif
                float2 offset = _View.xy + (uv - 0.5) * _View.zw - _Runner;
                float distanceToRunner = max(length(offset), 0.0001);
                float2 direction = offset / distanceToRunner;
                float angle = frac(atan2(offset.y, offset.x) / 6.2831853 + 1.0) * 96.0;
                int ray = min((int)angle, 95);
                float obstruction = lerp(_RayLimits[ray], _RayLimits[(ray + 1) % 96], frac(angle));
                float beyondWall = 1.0 - smoothstep(obstruction + 0.06, obstruction + 0.42, distanceToRunner);
                float aroundRunner = 1.0 - smoothstep(0.60, 1.8, distanceToRunner);
                float cone = smoothstep(0.79, 0.90, dot(direction, _Facing));
                cone *= 1.0 - smoothstep(7.5, 12.5, distanceToRunner);
                float light = max(aroundRunner, cone) * beyondWall;
                float3 image = tex2D(_MainTex, input.uv).rgb * lerp(0.022, 1.0, light);
                float edges = smoothstep(0.33, 0.65, length(uv - 0.5));
                return fixed4(image + float3(0.16, 0.018, 0.022) * edges * _Danger, 1);
            }
            ENDCG
        }
    }
}
