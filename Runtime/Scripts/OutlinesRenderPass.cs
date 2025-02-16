using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Maask.Outlines
{
    public class OutlinesRenderPass : ScriptableRenderPass
    {
        private static readonly int OUTLINE_TEXTURE = Shader.PropertyToID("_OutlineTex");
        private static readonly int OUTLINE_THICKNESS = Shader.PropertyToID("_Thickness");
        private static readonly int OUTLINE_COLOR = Shader.PropertyToID("_Color");
        
        private readonly Material _material;
        private readonly OutlineSettings _settings;
        private readonly Material _overrideMaterial;

        public OutlinesRenderPass(Material material, OutlineSettings settings)
        {
            _material = material;
            _settings = settings;
            _overrideMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            _material.SetFloat(OUTLINE_THICKNESS, _settings.Thickness);
            _material.SetColor(OUTLINE_COLOR, _settings.Color);
            
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
                
                var textureProperties = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);
                var texture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureProperties, "Outline Objects Texture", false);

                builder.AllowPassCulling(false);
                builder.SetRenderAttachment(texture, 0);
                builder.UseRendererList(passData.RendererListHandle);
                builder.SetGlobalTextureAfterPass(texture, OUTLINE_TEXTURE);
                builder.SetRenderFunc((RenderPassData data, RasterGraphContext context) => ExecuteRenderPass(data, context));
            }

            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var src = resourceData.activeColorTexture;
            
                if (!src.IsValid()) return;

                var blitParams = new RenderGraphUtils.BlitMaterialParameters(src, src, _material, 0);
                renderGraph.AddBlitPass(blitParams, "Outline Pass");    
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
    }
}


// private class OutlinePassData
// {
//     internal Material Material;
//     internal TextureHandle InputTexture;
// }
//
// private static void ExecuteOutlinePass(OutlinePassData data, RasterGraphContext context)
// {
//     
// }
/*
var resourcesData = frameData.Get<UniversalResourceData>();
   var cameraData = frameData.Get<UniversalCameraData>();

   TextureHandle source, destination;

   using (var builder = renderGraph.AddRasterRenderPass<RenderPassData>("Outline Objects Render Pass", out var passData))
   {
       // var targetDesc = renderGraph.GetTextureDesc(resourcesData.cameraColor);
       // targetDesc.name = "_OutlineTargetRenderPass";
       // targetDesc.clearBuffer = true;
       // _renderTextureDescriptor.width = cameraData.cameraTargetDescriptor.width;
       // _renderTextureDescriptor.height = cameraData.cameraTargetDescriptor.height;
       // _renderTextureDescriptor.depthBufferBits = 0;
       // destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, _renderTextureDescriptor, "Outline Render Texture", true);

       var targetDesc = renderGraph.GetTextureDesc(resourcesData.cameraColor);
       targetDesc.name = "_OutlineTargetRenderPass";
       targetDesc.clearBuffer = true;
       
       destination = renderGraph.CreateTexture(targetDesc);

       // destination = resourcesData.activeColorTexture;
       
       var renderingData = frameData.Get<UniversalRenderingData>();
       var lightData = frameData.Get<UniversalLightData>();
       
       var sortFlags = cameraData.defaultOpaqueSortFlags;
       var renderQueueRange = RenderQueueRange.all;
       var filterSettings = new FilteringSettings(renderQueueRange, -1, _settings.LayerMask);
       var shadersToOverride = new ShaderTagId("UniversalForward");
   
       var drawSettings = RenderingUtils.CreateDrawingSettings(shadersToOverride, renderingData, cameraData, lightData, sortFlags);
      
       drawSettings.overrideMaterial = _overrideMaterial;
   
       var rendererListParameters = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
   
       passData.RendererListHandle = renderGraph.CreateRendererList(rendererListParameters);
       
       builder.UseRendererList(passData.RendererListHandle);
       builder.SetRenderAttachment(destination, 0);
       builder.SetRenderAttachmentDepth(resourcesData.activeDepthTexture);
       builder.SetRenderFunc((RenderPassData data, RasterGraphContext context) => ExecuteRenderPass(data, context));

       source = destination;
   }
   
   renderGraph.AddCopyPass(source, resourcesData.activeColorTexture);

   // destination = resourcesData.activeColorTexture;
   //
   // var paraVertical = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);
   // renderGraph.AddBlitPass(paraVertical, "Outline Blit Pass");

   // using (var builder = renderGraph.AddRasterRenderPass<OutlinePassData>("Outline Blit Pass", out var passData))
   // {
   //     destination = resourcesData.activeColorTexture;
   //
   //     passData.Material = _material;
   //     passData.InputTexture = source;
   //     
   //     builder.SetRenderAttachment(destination, 0);
   //     builder.SetRenderFunc((OutlinePassData data, RasterGraphContext context) =>
   //     {
   //         ExecuteOutlinePass(data, context);
   //     });    
   // }
   // var resourceData = frameData.Get<UniversalResourceData>();
   // var cameraData = frameData.Get<UniversalCameraData>();
   // Debug.Log(_material.GetPassName(0));
   // if (resourceData.isActiveTargetBackBuffer)
   //     return;
   //
   // _renderTextureDescriptor.width = cameraData.cameraTargetDescriptor.width;
   // _renderTextureDescriptor.height = cameraData.cameraTargetDescriptor.height;
   // _renderTextureDescriptor.depthBufferBits = 0;
   //
   // var src = UniversalRenderer.CreateRenderGraphTexture(renderGraph, _renderTextureDescriptor, "Outline Render Texture", true);
   // var dst = UniversalRenderer.CreateRenderGraphTexture(renderGraph, _renderTextureDescriptor, "Outline Texture", false);
   // using (var builder = renderGraph.AddRasterRenderPass<PassData>("Outline Render Pass", out var passData))
   // {
   //     var renderingData = frameData.Get<UniversalRenderingData>();
   //     var lightData = frameData.Get<UniversalLightData>();
   //     
   //     var sortFlags = cameraData.defaultOpaqueSortFlags;
   //     var renderQueueRange = RenderQueueRange.all;
   //     var filterSettings = new FilteringSettings(renderQueueRange, -1, _settings.LayerMask);
   //     var shadersToOverride = new ShaderTagId("UniversalForward");
   //     
   //     var drawSettings = RenderingUtils.CreateDrawingSettings(shadersToOverride, renderingData, cameraData, lightData, sortFlags);
   //     
   //     drawSettings.overrideMaterial = _overrideMaterial;
   //     
   //     var rendererListParameters = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
   //     
   //     passData.RendererListHandle = renderGraph.CreateRendererList(rendererListParameters);
   //     
   //     builder.UseRendererList(passData.RendererListHandle);
   //     builder.SetRenderAttachment(src, 0);
   //     // builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
   // }
   //
   // if (!src.IsValid() || !dst.IsValid())
   //     return;
   // RenderGraphUtils.BlitMaterialParameters paraVertical = new(src, dst, _material, 0);
   // renderGraph.AddBlitPass(paraVertical, k_VerticalPassName);
*/