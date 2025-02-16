using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Maask.Outlines
{
    public class OutlinesRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private OutlineSettings _outlineSettings;
        [SerializeField] private RenderPassEvent _passEvent = RenderPassEvent.AfterRenderingSkybox;
        
        private Material _material;
        private OutlinesRenderPass _pass;

        public override void Create()
        {
            _material = new Material(Shader.Find("Hidden/Maask/Outlines"));
            _pass = new OutlinesRenderPass(_material, _outlineSettings)
            {
                renderPassEvent = _passEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_pass);
        }
    }
}