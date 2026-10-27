// Tilt-shift blur for the diorama look: a sharp horizontal band across the garden, blurring
// smoothly towards the top of the screen (sky and hills, i.e. far away) and a little towards the
// bottom edge. Screen height stands in for depth because the camera is fixed and angled.
Shader "Hidden/LivingDiorama/TiltShift"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "TiltShift"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _FocusCenter;    // screen height (0 = bottom, 1 = top) of the middle of the sharp band
            float _FocusHalfWidth; // half the height of the sharp band
            float _Falloff;        // how far past the band the blur reaches full strength
            float _BlurRadius;     // full-strength blur radius in pixels at 900 px screen height
            float _NearBlurScale;  // strength below the band relative to above it

            // Poisson disk: evenly spread sample offsets inside the unit circle.
            static const int TapCount = 12;
            static const float2 Disk[TapCount] =
            {
                float2(-0.326, -0.406), float2(-0.840, -0.074), float2(-0.696,  0.457),
                float2(-0.203,  0.621), float2( 0.962, -0.195), float2( 0.473, -0.480),
                float2( 0.519,  0.767), float2( 0.185, -0.893), float2( 0.507,  0.064),
                float2( 0.896,  0.412), float2(-0.322, -0.933), float2(-0.792, -0.598)
            };

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                float offset = uv.y - _FocusCenter;
                float amount = smoothstep(0.0, 1.0, (abs(offset) - _FocusHalfWidth) / max(_Falloff, 1e-4));
                if (offset < 0.0) amount *= _NearBlurScale;

                half4 centre = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (amount <= 0.001) return centre;

                // Radius in UV units, independent of resolution and aspect ratio.
                float radiusY = amount * _BlurRadius / 900.0;
                float2 radius = float2(radiusY * _BlitTexture_TexelSize.w / _BlitTexture_TexelSize.z, radiusY);

                half4 sum = centre;
                [unroll]
                for (int i = 0; i < TapCount; i++)
                    sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + Disk[i] * radius);
                return sum / (TapCount + 1);
            }
            ENDHLSL
        }
    }
}
