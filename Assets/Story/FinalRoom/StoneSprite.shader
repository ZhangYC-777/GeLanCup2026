// 把角色贴图变成石雕效果：先取亮度去色，再乘石头颜色，保留原来的明暗层次。
Shader "GeLan/StoneSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StoneTint ("Stone Colour", Color) = (0.62, 0.64, 0.68, 1)
        _Gray ("Stone Amount", Range(0,1)) = 1
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _StoneTint;
            float _Gray;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, i.texcoord);
                fixed grey = dot(sprite.rgb, fixed3(0.299, 0.587, 0.114));
                fixed3 stone = grey * _StoneTint.rgb * 1.25;
                sprite.rgb = lerp(sprite.rgb, stone, _Gray);
                sprite *= i.color;
                return sprite;
            }
            ENDCG
        }
    }
}
