Shader "Hidden/Maask/Blur"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float vertical_blur;
        float horizontal_blur;
    
        float4 blur_vertical (const Varyings input) : SV_Target
        {
            const float blur_samples = 64;
            const float blur_samples_range = blur_samples / 2;
            const float blur_pixels = vertical_blur * _ScreenParams.y;

            float3 color = 0;
            
            for(float i = -blur_samples_range; i <= blur_samples_range; i++)
            {
                const float2 sample_offset = float2 (0, blur_pixels / _BlitTexture_TexelSize.w * (i / blur_samples_range));
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord + sample_offset).rgb;
            }
            
            return float4(color.rgb / (blur_samples + 1), 1);
        }

        float4 blur_horizontal (const Varyings input) : SV_Target
        {
            const float blur_samples = 64;
            const float blur_samples_range = blur_samples / 2;
            const float blur_pixels = horizontal_blur * _ScreenParams.x;
            
            float3 color = 0;

            for(float i = -blur_samples_range; i <= blur_samples_range; i++)
            {
                const float2 sample_offset =float2 (blur_pixels / _BlitTexture_TexelSize.z * (i / blur_samples_range), 0);
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord + sample_offset).rgb;
            }
            
            return float4(color / (blur_samples + 1), 1);
        }
    ENDHLSL
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off
        Pass
        {
            Name "BlurPassVertical"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment blur_vertical
            
            ENDHLSL
        }
        
        Pass
        {
            Name "BlurPassHorizontal"

            HLSLPROGRAM
            
            #pragma vertex Vert
            #pragma fragment blur_horizontal
            
            ENDHLSL
        }
    }
}