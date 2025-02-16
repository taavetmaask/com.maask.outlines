using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Maask.Outlines
{
    public class OutlinesRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private OutlineSettings _outlineSettings;
        
        private OutlinesRenderPass _pass;

        public override void Create()
        {
            _pass = new OutlinesRenderPass(_outlineSettings)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }
        
        protected override void Dispose(bool disposing)
        {
            _pass.Clear();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_pass);
        }
    }
}