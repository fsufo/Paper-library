Shader "Custom/InstancedItem"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 color : TEXCOORD0;
            };

            // 必须与 C# 和 Compute Shader 中的结构体保持一致
            struct ItemData
            {
                float2 position;
                float2 velocity;
                float4 color;
                int isActive;
                int price;
                int itemID;
                int padding; // 对齐
            };

            StructuredBuffer<ItemData> items;
            float4 _Color;

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                ItemData data = items[instanceID];

                // 1. 活性检查
                // 如果未激活，将顶点缩放到0（隐藏），避免渲染开销
                if (data.isActive == 0)
                {
                    o.vertex = 0;
                    o.color = float3(0,0,0);
                    return o;
                }

                // 2. 位置计算
                // 将 2D 模拟位置转换为 3D 世界坐标 (y=0.5 略高于地面)
                float3 worldPos = float3(data.position.x, 0.5, data.position.y);
                float4 finalPos = float4(worldPos + v.vertex.xyz, 1.0);

                o.vertex = UnityObjectToClipPos(finalPos);
                
                // 3. 颜色逻辑
                // 速度极小时显示为白色，表示静止/阻塞状态
                o.color = data.color.rgb;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float alpha = 0.5;
                return fixed4(i.color, alpha); // 固定 0.5 透明度
            }
            ENDCG
        }
    }
}