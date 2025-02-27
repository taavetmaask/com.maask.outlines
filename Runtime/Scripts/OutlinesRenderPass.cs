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
        
        private static readonly int BLUR_HORIZONTAL = Shader.PropertyToID("horizontal_blur");
        private static readonly int BLUR_VERTICAL = Shader.PropertyToID("vertical_blur");
        private static readonly int BLUR_TEXTURE = Shader.PropertyToID("_BlurTex");
        
        private readonly OutlineSettings _settings;
        
        private readonly Material _overrideMaterial;
        private readonly Material _outlineMaterial;
        private readonly Material _blurMaterial;

        public OutlinesRenderPass(OutlineSettings settings)
        {
            _settings = settings;
            _overrideMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _outlineMaterial = new Material(Shader.Find("Hidden/Maask/Outlines"));
            _blurMaterial = new Material(Shader.Find("Hidden/Maask/Blur"));
        }

        public void Clear()
        {
            if (Application.isPlaying)
            {
                Object.Destroy(_overrideMaterial);
                Object.Destroy(_outlineMaterial);
                Object.Destroy(_blurMaterial);
            }
            else
            {
                Object.DestroyImmediate(_overrideMaterial);
                Object.DestroyImmediate(_outlineMaterial);
                Object.DestroyImmediate(_blurMaterial);
            }
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_overrideMaterial == null || _outlineMaterial == null || _blurMaterial == null)
            {
                return;
            }
            
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var textureProperties = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);
            
            if (resourceData.isActiveTargetBackBuffer)
            {
                return;
            }
            
            var outlineRender =  UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureProperties, "Outline Objects Texture", false);
            var blurRender = UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureProperties, "Outline Blur Texture", false);
            var temp = UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureProperties, "Outline Blur Temp", false);
            
            if (!outlineRender.IsValid() || !blurRender.IsValid() || !temp.IsValid())
            {
                return;
            }
            
            using (var builder = renderGraph.AddRasterRenderPass<RenderPassData>("Draw Outline Objects", out var passData))
            {
                var renderingData = frameData.Get<UniversalRenderingData>();
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
                builder.SetRenderAttachment(outlineRender, 0);
                builder.UseRendererList(passData.RendererListHandle);
                builder.SetGlobalTextureAfterPass(outlineRender, OUTLINE_TEXTURE);
                builder.SetRenderFunc((RenderPassData data, RasterGraphContext context) => ExecuteRenderPass(data, context));
            }
            
            using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>(passName, out var passData))
            {
                passData.SourceTexture = outlineRender;
                
                builder.AllowPassCulling(false);
                builder.UseTexture(outlineRender);
                builder.SetRenderAttachment(blurRender, 0);
                builder.SetGlobalTextureAfterPass(blurRender, BLUR_TEXTURE);
                builder.SetRenderFunc((CopyPassData data, RasterGraphContext context) => CopyRenderFunc(data, context));
            }
            
            _blurMaterial.SetFloat(BLUR_HORIZONTAL, _settings.Thickness / Screen.width);
            _blurMaterial.SetFloat(BLUR_VERTICAL, _settings.Thickness / Screen.height);
            
            {
                var paraVertical = new RenderGraphUtils.BlitMaterialParameters(blurRender, temp, _blurMaterial, 0);
                renderGraph.AddBlitPass(paraVertical, "Outline Blur Horizontal Pass");
            }
            
            {
                var paraHorizontal = new RenderGraphUtils.BlitMaterialParameters(temp, blurRender, _blurMaterial, 1);
                renderGraph.AddBlitPass(paraHorizontal, "Outline Blur Vertical Pass");
            }
            
            {
                _outlineMaterial.SetFloat(OUTLINE_THICKNESS, _settings.Thickness);
                _outlineMaterial.SetColor(OUTLINE_COLOR, _settings.Color);
            
                var blitParams = new RenderGraphUtils.BlitMaterialParameters(blurRender, resourceData.activeColorTexture, _outlineMaterial, 0);
                
                renderGraph.AddBlitPass(blitParams, "Outline Final Pass");    
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

        private class CopyPassData
        {
            public TextureHandle SourceTexture;
        }

        private static void CopyRenderFunc(CopyPassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, data.SourceTexture, new Vector4(1, 1, 0, 0), 0, false);
        }
    }
}