Shader "Hidden/GeLanCup/PuzzleRoomVisibility"
{
    Properties
    {
        _MainTex ("Scene", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _ViewOrigin;
            float4 _ViewRight;
            float4 _ViewUp;
            float4 _RoomBounds;
            float4 _DoorwayBounds;
            float4 _RoomLight;
            float4 _FocusPoint;
            half _Blackout;
            float4 _BeamOrigin;
            float4 _BeamDirection;
            float4 _AccentLight;
            half _LightingScale;

            half Reveal(float2 world, float4 region)
            {
                float2 edges = min(world - region.xy, region.zw - world);
                return smoothstep(0.0, 0.24, min(edges.x, edges.y));
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                if (_Blackout > 0.5)
                    return fixed4(0, 0, 0, 1);

                float2 viewport = input.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0.0)
                    viewport.y = 1.0 - viewport.y;
                #endif

                float2 world = _ViewOrigin.xy + _ViewRight.xy * viewport.x + _ViewUp.xy * viewport.y;
                half visible = max(Reveal(world, _RoomBounds), Reveal(world, _DoorwayBounds));
                float radius = _RoomLight.z;
                half nearby = 1.0 - smoothstep(radius * 0.25, max(radius, 0.001), distance(world, _FocusPoint.xy));
                half brightness = lerp(_RoomLight.x, _RoomLight.y, nearby);
                float2 fromLamp = world - _BeamOrigin.xy;
                float distanceToLamp = max(length(fromLamp), 0.001);
                half beam = smoothstep(0.67, 0.93, dot(fromLamp / distanceToLamp, _BeamDirection.xy)) *
                    (1.0 - smoothstep(3.2, 6.4, distanceToLamp)) * _BeamOrigin.z;
                brightness = max(brightness, beam);
                fixed4 pixel = tex2D(_MainTex, input.uv);
                half3 shadow = half3(0.022, 0.020, 0.033);
                half3 litPixel = lerp(shadow, pixel.rgb, brightness);
                half accent = (1.0 - smoothstep(0.0, max(_AccentLight.z, 0.001),
                    distance(world, _AccentLight.xy))) * _AccentLight.w;
                litPixel += accent * pixel.rgb * half3(1.0, 0.19, 0.15);
                pixel.rgb = lerp(shadow, litPixel, visible * _LightingScale);
                return pixel;
            }
            ENDCG
        }
    }
}
