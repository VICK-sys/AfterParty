using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class VanillaWeekendRainFeature : ScriptableRendererFeature
{
    private RainPass pass;

    public override void Create() => pass = new RainPass();

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var rain = renderingData.cameraData.camera.GetComponent<VanillaWeekendRain>();
        if (rain == null || !rain.isActiveAndEnabled || rain.Material == null) return;
        pass.material = rain.Material;
        renderer.EnqueuePass(pass);
    }

    private sealed class RainPass : ScriptableRenderPass
    {
        public Material material;

        private sealed class Data
        {
            public TextureHandle source;
            public TextureHandle temporary;
            public Material material;
        }

        public RainPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var camera = frameData.Get<UniversalCameraData>();
            var descriptor = camera.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            var temporary = UniversalRenderer.CreateRenderGraphTexture(graph, descriptor, "Weekend Rain", false);
            using (var builder = graph.AddUnsafePass<Data>("Weekend Rain", out var data))
            {
                data.source = resources.activeColorTexture;
                data.temporary = temporary;
                data.material = material;
                builder.UseTexture(data.source, AccessFlags.ReadWrite);
                builder.UseTexture(temporary, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((Data value, UnsafeGraphContext context) =>
                {
                    var command = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    command.Blit(((RTHandle)value.source).nameID, ((RTHandle)value.temporary).nameID, value.material);
                    command.Blit(((RTHandle)value.temporary).nameID, ((RTHandle)value.source).nameID);
                });
            }
        }
    }
}
