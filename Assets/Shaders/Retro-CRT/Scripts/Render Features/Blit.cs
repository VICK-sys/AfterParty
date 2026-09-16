using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class Blit : ScriptableRendererFeature
{
    [System.Serializable]
    public class BlitSettings
    {
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;
        public Material blitMaterial;
        public int blitMaterialPassIndex;
        public bool setInverseViewMatrix;
        public Target srcType = Target.CameraColor;
        public string srcTextureId = "_CameraColorTexture";
        public RenderTexture srcTextureObject;
        public Target dstType = Target.CameraColor;
        public string dstTextureId = "_BlitPassTexture";
        public RenderTexture dstTextureObject;
    }

    public enum Target
    {
        CameraColor,
        TextureID,
        RenderTextureObject
    }

    public BlitSettings settings = new BlitSettings();
    private BlitPass blitPass;
    
    public override void Create()
    {
        blitPass?.Dispose();
        blitPass = new BlitPass(settings, name);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.blitMaterial == null)
            return;

        if (settings.srcType == Target.RenderTextureObject && settings.srcTextureObject == null)
            return;

        if (settings.dstType == Target.RenderTextureObject && settings.dstTextureObject == null)
            return;

        blitPass.renderPassEvent = settings.Event;
        renderer.EnqueuePass(blitPass);
    }

    protected override void Dispose(bool disposing)
    {
        blitPass?.Dispose();
    }

    private sealed class BlitPass : ScriptableRenderPass
    {
        private sealed class PassData
        {
            public TextureHandle source;
            public TextureHandle destination;
            public TextureHandle temporary;
            public bool globalSource;
            public int sourceId;
            public Material material;
            public int materialPass;
            public bool setInverseViewMatrix;
            public Matrix4x4 inverseViewMatrix;
        }

        private readonly BlitSettings settings;
        private readonly string passName;
        private RTHandle sourceHandle;
        private RTHandle destinationHandle;

        public BlitPass(BlitSettings settings, string passName)
        {
            this.settings = settings;
            this.passName = passName;
            requiresIntermediateTexture = true;
        }

        private static bool IsCameraTarget(Target type, string textureId)
        {
            return type == Target.CameraColor ||
                   (type == Target.TextureID &&
                    (textureId == "_CameraColorTexture" || textureId == "_AfterPostProcessTexture"));
        }

        private static TextureHandle ImportTexture(RenderGraph renderGraph, RenderTexture texture, ref RTHandle handle)
        {
            if (handle == null || handle.rt != texture)
            {
                handle?.Release();
                handle = RTHandles.Alloc(texture);
            }

            return renderGraph.ImportTexture(handle);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            bool cameraSource = IsCameraTarget(settings.srcType, settings.srcTextureId);
            bool cameraDestination = IsCameraTarget(settings.dstType, settings.dstTextureId);
            bool globalSource = settings.srcType == Target.TextureID && !cameraSource;
            bool globalDestination = settings.dstType == Target.TextureID && !cameraDestination;

            TextureHandle source = TextureHandle.nullHandle;
            if (cameraSource)
                source = resources.activeColorTexture;
            else if (!globalSource)
                source = ImportTexture(renderGraph, settings.srcTextureObject, ref sourceHandle);

            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            TextureHandle temporary = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, passName + " Temporary", false);
            TextureHandle destination;
            if (cameraDestination)
                destination = resources.activeColorTexture;
            else if (globalDestination)
                destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, settings.dstTextureId, false);
            else
                destination = ImportTexture(renderGraph, settings.dstTextureObject, ref destinationHandle);

            using (var builder = renderGraph.AddUnsafePass<PassData>(passName, out var data))
            {
                data.source = source;
                data.destination = destination;
                data.temporary = temporary;
                data.globalSource = globalSource;
                data.sourceId = globalSource ? Shader.PropertyToID(settings.srcTextureId) : 0;
                data.material = settings.blitMaterial;
                data.materialPass = Mathf.Clamp(settings.blitMaterialPassIndex, -1, settings.blitMaterial.passCount - 1);
                data.setInverseViewMatrix = settings.setInverseViewMatrix;
                data.inverseViewMatrix = cameraData.camera.cameraToWorldMatrix;

                if (!globalSource && source.Equals(destination))
                    builder.UseTexture(destination, AccessFlags.ReadWrite);
                else
                {
                    if (!globalSource)
                        builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(destination, AccessFlags.Write);
                }

                builder.UseTexture(temporary, AccessFlags.ReadWrite);
                builder.UseAllGlobalTextures(globalSource);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                if (globalDestination)
                    builder.SetGlobalTextureAfterPass(destination, Shader.PropertyToID(settings.dstTextureId));

                builder.SetRenderFunc((PassData pass, UnsafeGraphContext context) =>
                {
                    CommandBuffer command = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    if (pass.setInverseViewMatrix)
                        command.SetGlobalMatrix("_InverseView", pass.inverseViewMatrix);

                    RTHandle temporaryTarget = pass.temporary;
                    RTHandle destinationTarget = pass.destination;
                    RenderTargetIdentifier sourceTarget = pass.globalSource
                        ? new RenderTargetIdentifier(pass.sourceId)
                        : ((RTHandle)pass.source).nameID;
                    command.Blit(sourceTarget, temporaryTarget.nameID, pass.material, pass.materialPass);
                    command.Blit(temporaryTarget.nameID, destinationTarget.nameID);
                });
            }
        }
        
        public void Dispose()
        {
            sourceHandle?.Release();
            destinationHandle?.Release();
            sourceHandle = null;
            destinationHandle = null;
        }
    }
}
