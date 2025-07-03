Shader "Custom/MatrixRain"
{
    Properties
    {
        _MainTex ("Symbol Atlas (RGBA)", 2D) = "white" {}
        _Color   ("Symbol Color", Color) = (0,1,0,1)
        _Speed   ("Base Fall Speed", Float) = 1.0
        _SpeedVariation ("Speed Variation", Range(0, 1)) = 0.5
        _PhaseVariation ("Phase Variation", Range(0, 1)) = 0.5
        _Tile    ("Tiling (X,Y)", Vector) = (32,32,0,0)
        _AtlasSize ("Atlas Size (X,Y)", Vector) = (8,8,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Color;
            float  _Speed;
            float  _SpeedVariation;
            float  _PhaseVariation;
            float4 _Tile;
            float4 _AtlasSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            float rand(float n)
            {
                return frac(sin(n * 52.3456) * 789.2345);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = IN.uv * _Tile.xy;
                float column = floor(IN.uv.x * _Tile.x);
                
                float speedRand = rand(column * 1.234);
                float phase = rand(column * 9.876) * _PhaseVariation;
                
                uv.y += _Time.y * (_Speed + speedRand * _SpeedVariation) + phase;
                
                float row = floor(uv.y);
                float2 tiledUV = frac(uv);

                // Выбор случайного символа из атласа
                float symbolIndex = rand(column * 1000 + row);
                int symbolX = int(symbolIndex * _AtlasSize.x) % int(_AtlasSize.x);
                int symbolY = int((symbolIndex * _AtlasSize.y * _AtlasSize.x)) % int(_AtlasSize.y);
                
                // Расчет UV для конкретного символа в атласе
                float2 atlasUV = tiledUV / _AtlasSize.xy;
                atlasUV.x += float(symbolX) / _AtlasSize.x;
                atlasUV.y += float(symbolY) / _AtlasSize.y;
                
                // Выборка цвета символа
                half4 symbol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, atlasUV);
                
                // Порог видимости
                float threshold = rand(column * 1000 + row);
                
                // Градиент для хвоста
                float tail = smoothstep(0.0, 1.0, frac(uv.y));
                threshold = lerp(threshold, 1.0, tail * 0.3);

                float alpha = symbol.a;
                if (alpha > threshold)
                {
                    return half4(_Color.rgb * symbol.rgb, alpha);
                }
                return half4(0,0,0,0);
            }
            ENDHLSL
        }
    }
}