using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Maask.Outlines
{
    public class OutlinesRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private OutlineSettings _outlineSettings;
        [SerializeField] private RenderPassEvent _event = RenderPassEvent.BeforeRenderingPostProcessing;
        
        private OutlinesRenderPass _pass;

        public override void Create()
        {
            _pass = new OutlinesRenderPass(_outlineSettings)
            {
                renderPassEvent = _event
            };
        }
        
        protected override void Dispose(bool disposing) 
        {
            _pass.Clear();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Game)
            {
                renderer.EnqueuePass(_pass);   
            }
        }
    }
}