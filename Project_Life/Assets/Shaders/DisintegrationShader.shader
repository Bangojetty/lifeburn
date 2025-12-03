Shader "Custom/DisintegrationShader"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _Cutoff ("Dissolve Amount", Range(0,1)) = 0
        _EdgeColor ("Edge Color", Color) = (1,0.5,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard alpha:fade

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _NoiseTex;
        float _Cutoff;
        fixed4 _EdgeColor;

        struct Input
        {
            float2 uv_MainTex;
        };
        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex);
            float noise = tex2D (_NoiseTex, IN.uv_MainTex).r;
            float dissolve = smoothstep(_Cutoff - 0.05, _Cutoff, noise);
            
            o.Albedo = c.rgb;
            o.Alpha = dissolve;

            if (dissolve < 0.1)
                o.Emission = _EdgeColor.rgb * (1- dissolve);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
