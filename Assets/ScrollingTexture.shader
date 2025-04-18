Shader "Custom/ScrollingTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScrollSpeed ("Scroll Speed", Range(0.1, 10)) = 1
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _ScrollSpeed;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate scrolling based on time and scroll speed
                float offset = _Time.y * _ScrollSpeed;
                // Repeat the texture
                float2 uv = i.uv;
                uv.x = frac(uv.x + offset);

                // Sample texture
                fixed4 col = tex2D(_MainTex, uv);
                
                // Apply fog
                #ifdef UNITY_FOG_COORDS
                UNITY_APPLY_FOG(i.fogCoord, col);
                #endif
                
                return col;
            }
            ENDCG
        }
    }
}
