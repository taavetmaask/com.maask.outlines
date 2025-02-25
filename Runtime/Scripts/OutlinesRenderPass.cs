using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Maask.Outlines
{
    public class OutlinesRenderPass : ScriptableRenderPass
    {
        private static readonly int COMPUTE_SOURCE_TEXTURE = Shader.PropertyToID("source_tex");
        private static readonly int COMPUTE_FLOOD_TEXTURE = Shader.PropertyToID("flood_tex");
        private static readonly int COMPUTE_FLOOD_READ_TEXTURE = Shader.PropertyToID("flood_tex_read");
        private static readonly int COMPUTE_SDF_TEXTURE = Shader.PropertyToID("sdf_tex");
        private static readonly int COMPUTE_RESOLUTION = Shader.PropertyToID("resolution");
        private static readonly int COMPUTE_STEP_SIZE = Shader.PropertyToID("step_size");
     
        private static readonly int OUTLINE_TEXTURE = Shader.PropertyToID("_OutlineTex");
        private static readonly int OUTLINE_THICKNESS = Shader.PropertyToID("_Thickness");
        private static readonly int OUTLINE_COLOR = Shader.PropertyToID("_Color");
        
        private readonly OutlineSettings _settings;
        
        private readonly Material _overrideMaterial;
        private readonly Material _outlineMaterial;

        private readonly ComputeShader _computeShader;
        
        public OutlinesRenderPass(OutlineSettings settings)
        {
            _settings = settings;
            _overrideMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _outlineMaterial = new Material(Shader.Find("Hidden/Maask/Outlines"));
            _computeShader = Object.Instantiate(Resources.Load<ComputeShader>("TextureToSdf"));
        }

        public void Clear()
        {
            if (Application.isPlaying)
            {
                Object.Destroy(_overrideMaterial);
                Object.Destroy(_outlineMaterial);
                Object.Destroy(_computeShader);
            }
            else
            {
                Object.DestroyImmediate(_overrideMaterial);
                Object.DestroyImmediate(_outlineMaterial);
                Object.DestroyImmediate(_computeShader);
            }
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_overrideMaterial == null || _outlineMaterial == null || _computeShader == null)
            {
                return;
            }
            
            var res = new Vector2Int(Screen.width, Screen.height) / 4;
            var resourceData = frameData.Get<UniversalResourceData>();
            
            var sourceTexture = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0),
                "Outline Source Texture",
                true
            );
            
            var floodTexture = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, 
                new RenderTextureDescriptor(res.x, res.y, GraphicsFormat.R32G32B32A32_UInt, 0)
                {
                    autoGenerateMips = false,
                    enableRandomWrite = true,
                }, 
                "Outline Flood Texture", 
                true
            );

            var sdfTexture = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                new RenderTextureDescriptor(res.x, res.y, GraphicsFormat.R16_SFloat, 0)
                {
                    enableRandomWrite = true,
                    autoGenerateMips = false,
                },
                "Outline Sdf Texture",
                true
            );
            
            if (resourceData.isActiveTargetBackBuffer)
            {
                return;
            }
            
            using (var builder = renderGraph.AddRasterRenderPass<RenderPassData>("Draw Outline Objects", out var passData))
            {
                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();
                
                var sortFlags = cameraData.defaultOpaqueSortFlags;
                var renderQueueRange = RenderQueueRange.opaque;
                var filterSettings = new FilteringSettings(renderQueueRange, ~0, _settings.LayerMask);
                var shadersToOverride = new ShaderTagId("UniversalForward");
                var drawSettings = RenderingUtils.CreateDrawingSettings(shadersToOverride, renderingData, cameraData, lightData, sortFlags);
                
                drawSettings.overrideMaterial = _overrideMaterial;
                
                var rendererListParameters = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
                
                passData.RendererListHandle = renderGraph.CreateRendererList(rendererListParameters);
                
                builder.AllowPassCulling(false);
                builder.SetRenderAttachment(sourceTexture, 0);
                builder.UseRendererList(passData.RendererListHandle);
                builder.SetRenderFunc((RenderPassData data, RasterGraphContext ctx) => ExecuteRenderPass(data, ctx));
            }

            using (var builder = renderGraph.AddComputePass<ComputePassData>("Compute SDF", out var passData))
            {
                passData.ComputeShader = _computeShader;
                passData.Resolution = res;
                passData.SourceTexture = sourceTexture;
                passData.FloodTexture = floodTexture;
                passData.SdfTexture = sdfTexture;
                
                builder.UseTexture(sourceTexture);
                builder.UseTexture(floodTexture, AccessFlags.ReadWrite);
                builder.UseTexture(sdfTexture, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(sdfTexture, OUTLINE_TEXTURE);
                builder.SetRenderFunc((ComputePassData data, ComputeGraphContext ctx) => ExecuteComputePass(data, ctx));
            }
            
            {
                _outlineMaterial.SetFloat(OUTLINE_THICKNESS, _settings.Thickness);
                _outlineMaterial.SetColor(OUTLINE_COLOR, _settings.Color);
                
                var blitParams = new RenderGraphUtils.BlitMaterialParameters(
                    sdfTexture, 
                    resourceData.activeColorTexture, 
                    _outlineMaterial, 
                    0
                );
                
                renderGraph.AddBlitPass(blitParams, "Outline Final Blit Pass");
            }
        }
        
        private class RenderPassData
        {
            public RendererListHandle RendererListHandle;
        }

        private static void ExecuteRenderPass(RenderPassData data, RasterGraphContext context)
        {
            context.cmd.DrawRendererList(data.RendererListHandle);
        }

        private class ComputePassData
        {
            public ComputeShader ComputeShader;
            public Vector2Int Resolution;
            public TextureHandle SourceTexture;
            public TextureHandle FloodTexture;
            public TextureHandle SdfTexture;
        }

        private static void ExecuteComputePass(ComputePassData data, ComputeGraphContext context)
        {
            var shader = data.ComputeShader;
            
            var initKernel = shader.FindKernel("init_texture");
            var floodKernel = shader.FindKernel("flood_texture");
            var sdfKernel = shader.FindKernel("sdf_texture");
            
            var groupThreadsX = Mathf.CeilToInt(data.Resolution.x / 8.0f);
            var groupThreadsY = Mathf.CeilToInt(data.Resolution.y / 8.0f);
            
            context.cmd.SetComputeIntParams(shader, COMPUTE_RESOLUTION, data.Resolution.x, data.Resolution.y);
            
            context.cmd.SetComputeTextureParam(shader, initKernel, COMPUTE_SOURCE_TEXTURE, data.SourceTexture);
            context.cmd.SetComputeTextureParam(shader, initKernel, COMPUTE_FLOOD_TEXTURE, data.FloodTexture);
            context.cmd.SetComputeTextureParam(shader, floodKernel, COMPUTE_FLOOD_TEXTURE, data.FloodTexture);
            context.cmd.SetComputeTextureParam(shader, sdfKernel, COMPUTE_FLOOD_READ_TEXTURE, data.FloodTexture);
            context.cmd.SetComputeTextureParam(shader, sdfKernel, COMPUTE_SDF_TEXTURE, data.SdfTexture);
            
            context.cmd.DispatchCompute(shader, initKernel, groupThreadsX, groupThreadsY, 1);
            
            var sizeMax = Mathf.Max(data.Resolution.x, data.Resolution.y);
            var stepMax = (int)Mathf.Log(Mathf.NextPowerOfTwo(sizeMax), 2);
        
            for (var n = stepMax; n >= 0; n--)
            {
                var stepSize = n > 0 ? (int)Mathf.Pow(2, n) : 1;
                context.cmd.SetComputeIntParam(shader, COMPUTE_STEP_SIZE, stepSize);
                context.cmd.DispatchCompute(shader, floodKernel, data.Resolution.x, data.Resolution.y, 1);
            }
          
            context.cmd.DispatchCompute(shader, sdfKernel, groupThreadsX, groupThreadsY, 1);
        }
    }
}

/*
// using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>(passName, out var passData))
// {
//     source = passData.SourceTexture = destination;
//     destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureProperties, "Outline Blur Texture", false);
//     
//     builder.AllowPassCulling(false);
//     builder.UseTexture(source);
//     builder.SetRenderAttachment(destination, 0);
//     builder.SetGlobalTextureAfterPass(destination, BLUR_TEXTURE);
//     builder.SetRenderFunc((CopyPassData data, RasterGraphContext context) => CopyRenderFunc(data, context));
// }
// _blurMaterial.SetFloat(BLUR_HORIZONTAL, _settings.Blur / Screen.width);
// _blurMaterial.SetFloat(BLUR_VERTICAL, _settings.Blur / Screen.height);
//
// var temp = UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureProperties, "Temp", false);
//
// {
//     source = destination;
//     RenderGraphUtils.BlitMaterialParameters paraVertical = new(source, temp, _blurMaterial, 0);
//     renderGraph.AddBlitPass(paraVertical, "Outline Blur Horizontal Pass");
// }
//
// {
//     RenderGraphUtils.BlitMaterialParameters paraHorizontal = new(temp, destination, _blurMaterial, 1);
//     renderGraph.AddBlitPass(paraHorizontal, "Outline Blur Vertical Pass");
// }
*/
// private class CopyPassData
// {
//     public TextureHandle SourceTexture;
// }
//
// private static void CopyRenderFunc(CopyPassData data, RasterGraphContext context)
// {
//     Blitter.BlitTexture(context.cmd, data.SourceTexture, new Vector4(1, 1, 0, 0), 0, false);
// }