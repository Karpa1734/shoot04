// 📄 DanmakuAuraShader.shader 【四角アセット完全救済・真円加算オーラ最終決定版】
Shader "Custom/DanmakuAuraShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _AuraColor ("Aura Color", Color) = (1, 1, 1, 1)      // スキルデータの imageColor
        _AuraRange ("Aura Size", Range(0.01, 0.5)) = 0.45    // 💡真円オーラの半径サイズ（中心0.0〜端0.5）
        _AuraFalloff ("Aura Softness", Range(0.01, 0.5)) = 0.2 // 💡フチのボケ足の滑らかさ
        _AuraIntensity ("Aura Intensity", Range(0, 10)) = 4.5 // オーラの発光強度
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off Lighting Off ZWrite Off
        
        // 🎯 弾本体はクッキリ透過描画、真後ろのオーラ成分のみを背景に加算結合
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _AuraColor;
            float _AuraRange;
            float _AuraFalloff;
            float _AuraIntensity;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 1. 弾幕本来のテクスチャカラー（ドット絵の芯）
                fixed4 mainColor = tex2D(_MainTex, IN.texcoord);

                // 2. 🔮【アセット非依存型・真円極座標エミッションマスク】
                // 💡 元スプライトのアルファが四角か丸かに関わらず、
                // 💡 UV座標の中心点 (0.5, 0.5) からの絶対距離を算出し、完全な幾何学的「真円」を動的トレース！
                float2 uvOffset = IN.texcoord - float2(0.5, 0.5);
                float distFromCenter = length(uvOffset);
                
                // 💡 板ポリゴンの枠線（端）で光が絶対にパツンと切れないよう、
                //    中心から外側へ向かって非常に綺麗にフェードアウトする減衰曲線を smoothstep でロック。
                float auraMask = smoothstep(_AuraRange, _AuraRange - _AuraFalloff, distFromCenter);

                // 3. 弾のドット絵の実体がある中央部分は、オーラを綺麗にカットして本体の元のグラフィックスを100%最前面へ
                float pureAura = saturate(auraMask - (mainColor.a * 0.3f)); // ほんのり内側にも光を滲ませる絶妙のブレンド

                // 4. キャラクターのイメージカラーと輝度（Intensity）を結合してオーラ発光RGBを生成
                fixed4 auraColorFinal = _AuraColor * pureAura * _AuraIntensity;

                // =========================================================================
                // 🎯【透過本体 ＆ 下層真円オーラの非破壊合成マトリクス】
                // =========================================================================
                fixed4 finalOutput;
                
                // 💡 RGB: 本体のドット絵（不透明部分）を無傷で残し、その直下（背景側）にだけ完全な「真円オーラ」を純粋加算！
                finalOutput.rgb = (mainColor.rgb * mainColor.a) + (_AuraColor.rgb * pureAura * _AuraIntensity * (1.0 - mainColor.a));
                
                // 💡 Alpha: 通常ブレンドにより本体はクッキリ、外側は四角形にならず綺麗な円形としてフェードアウト
                finalOutput.a = saturate(mainColor.a + pureAura * _AuraColor.a);

                return finalOutput * IN.color;
            }
            ENDCG
        }
    }
}