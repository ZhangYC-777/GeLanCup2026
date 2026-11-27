// 对原始 32×32 地牢素材调色，在兜帽的暗部保留两点像素红光。
Shader "GeLan/HunterWraith"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip ("Flip", Vector) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "CanUseSpriteAtlas" = "False" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment HunterFrag
            #include "UnitySprites.cginc"

            fixed4 HunterFrag(v2f input) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(input.texcoord) * input.color;
                float2 pixel = floor(input.texcoord * 32.0);
                if (pixel.y == 25.0 && (pixel.x == 15.0 || pixel.x == 18.0))
                {
                    color.rgb = fixed3(0.95, 0.20, 0.25);
                }

                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
