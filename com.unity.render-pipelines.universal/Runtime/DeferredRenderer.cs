using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Deferred renderer for Universal RP.
    /// This renderer is supported on all Universal RP supported platforms.
    /// In the default mode, lights volumes are rendered using stencil masks.
    /// </summary>
    public sealed class DeferredRenderer : ScriptableRenderer
    {
        public static readonly int k_GBufferSlicesCount = 3;
        public static readonly int k_DepthStencilBufferBits = 32;

        static readonly string k_CreateCameraTextures = "Create Camera Texture";

        ColorGradingLutPass m_ColorGradingLutPass;
        DepthOnlyPass m_DepthPrepass;
        MainLightShadowCasterPass m_MainLightShadowCasterPass;
        AdditionalLightsShadowCasterPass m_AdditionalLightsShadowCasterPass;
        ScreenSpaceShadowResolvePass m_ScreenSpaceShadowResolvePass;
        GBufferPass m_GBufferPass;
        TileDepthRangePass m_TileDepthRangePass;
        TileDepthRangePass m_TileDepthRangeExtraPass; // TODO use subpass API to hide this pass
        DeferredPass m_DeferredPass;
        DrawObjectsPass m_RenderOpaqueForwardOnlyPass;
        DrawSkyboxPass m_DrawSkyboxPass;
        CopyDepthPass m_CopyDepthPass;
        CopyColorPass m_CopyColorPass;
        TransparentSettingsPass m_TransparentSettingsPass;
        DrawObjectsPass m_RenderTransparentForwardPass;
        InvokeOnRenderObjectCallbackPass m_OnRenderObjectCallbackPass;
        PostProcessPass m_PostProcessPass;
        PostProcessPass m_FinalPostProcessPass;
        FinalBlitPass m_FinalBlitPass;
        CapturePass m_CapturePass;

#if UNITY_EDITOR
        SceneViewDepthCopyPass m_SceneViewDepthCopyPass;
#endif

        // Attachments are like "binding points", internally they identify the texture shader properties declared with the same names
        RenderTargetHandle m_ActiveCameraColorAttachment;
        RenderTargetHandle m_ActiveCameraDepthAttachment;
        RenderTargetHandle m_CameraColorTexture;
        RenderTargetHandle m_CameraDepthTexture;
        RenderTargetHandle m_CameraDepthAttachment;
        RenderTargetHandle[] m_GBufferAttachments = new RenderTargetHandle[k_GBufferSlicesCount];
        RenderTargetHandle m_OpaqueColor;
        RenderTargetHandle m_AfterPostProcessColor;
        RenderTargetHandle m_ColorGradingLut;
        RenderTargetHandle m_DepthInfoTexture;
        RenderTargetHandle m_TileDepthInfoTexture;

        ForwardLights m_ForwardLights; // Required for transparent pass
        DeferredLights m_DeferredLights;
        bool m_PreferDepthPrepass;
        StencilState m_DefaultStencilState;

        Material m_BlitMaterial;
        Material m_CopyDepthMaterial;
        Material m_SamplingMaterial;
        Material m_ScreenspaceShadowsMaterial;
        Material m_TileDepthInfoMaterial;
        Material m_TileDeferredMaterial;
        Material m_StencilDeferredMaterial;

        public DeferredRenderer(DeferredRendererData data) : base(data)
        {
            m_BlitMaterial = CoreUtils.CreateEngineMaterial(data.shaders.blitPS);
            m_CopyDepthMaterial = CoreUtils.CreateEngineMaterial(data.shaders.copyDepthPS);
            m_SamplingMaterial = CoreUtils.CreateEngineMaterial(data.shaders.samplingPS);
            m_ScreenspaceShadowsMaterial = CoreUtils.CreateEngineMaterial(data.shaders.screenSpaceShadowPS);
            m_TileDepthInfoMaterial = CoreUtils.CreateEngineMaterial(data.shaders.tileDepthInfoPS);
            m_TileDeferredMaterial = CoreUtils.CreateEngineMaterial(data.shaders.tileDeferredPS);
            m_StencilDeferredMaterial = CoreUtils.CreateEngineMaterial(data.shaders.stencilDeferredPS);

            StencilStateData stencilData = data.defaultStencilState;
            m_DefaultStencilState = StencilState.defaultValue;
            m_DefaultStencilState.enabled = stencilData.overrideStencilState;
            m_DefaultStencilState.SetCompareFunction(stencilData.stencilCompareFunction);
            m_DefaultStencilState.SetPassOperation(stencilData.passOperation);
            m_DefaultStencilState.SetFailOperation(stencilData.failOperation);
            m_DefaultStencilState.SetZFailOperation(stencilData.zFailOperation);

            m_ForwardLights = new ForwardLights();
            m_DeferredLights = new DeferredLights(m_TileDepthInfoMaterial, m_TileDeferredMaterial, m_StencilDeferredMaterial);
            m_DeferredLights.accurateGbufferNormals = data.accurateGbufferNormals;
            m_DeferredLights.tiledDeferredShading = false;

            m_PreferDepthPrepass = data.preferDepthPrepass;

            // Note: Since all custom render passes inject first and we have stable sort,
            // we inject the builtin passes in the before events.
            m_DepthPrepass = new DepthOnlyPass(RenderPassEvent.BeforeRenderingPrepasses, RenderQueueRange.opaque, data.opaqueLayerMask);
            m_MainLightShadowCasterPass = new MainLightShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);
            m_AdditionalLightsShadowCasterPass = new AdditionalLightsShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);
            m_ScreenSpaceShadowResolvePass = new ScreenSpaceShadowResolvePass(RenderPassEvent.BeforeRenderingPrepasses, m_ScreenspaceShadowsMaterial);
            m_ColorGradingLutPass = new ColorGradingLutPass(RenderPassEvent.BeforeRenderingPrepasses, data.postProcessData);
            m_GBufferPass = new GBufferPass(RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, m_DefaultStencilState, stencilData.stencilReference, m_DeferredLights);
            m_TileDepthRangePass = new TileDepthRangePass(RenderPassEvent.BeforeRenderingOpaques + 2, m_DeferredLights, 0);
            m_TileDepthRangeExtraPass = new TileDepthRangePass(RenderPassEvent.BeforeRenderingOpaques + 3, m_DeferredLights, 1);
            m_DeferredPass = new DeferredPass(RenderPassEvent.BeforeRenderingOpaques + 4, m_DeferredLights);
            // Forward only pass:
            // - If a material can be rendered either forward or deferred, then it should declare a UniversalForward and a UniversalGBuffer pass.
            // - If a material cannot be lit in deferred (unlit, bakedLit, special material such as hair, skin shader), then it should declare UniversalForwardOnly pass
            // - Legacy materials have unamed pass, which is implicitely renamed as SRPDefaultUnlit. In that case, they are considered forward-only too.
            // TO declare a material with unnamed pass and UniversalForward/UniversalForwardOnly pass is an ERROR, as the material will be rendered twice.
            m_RenderOpaqueForwardOnlyPass = new DrawObjectsPass("Render Opaques Forward Only", new ShaderTagId[] { new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("UniversalForwardOnly") }, true, RenderPassEvent.BeforeRenderingOpaques + 5, RenderQueueRange.opaque, data.opaqueLayerMask, m_DefaultStencilState, stencilData.stencilReference);
            m_CopyDepthPass = new CopyDepthPass(RenderPassEvent.AfterRenderingOpaques, m_CopyDepthMaterial);
            m_DrawSkyboxPass = new DrawSkyboxPass(RenderPassEvent.BeforeRenderingSkybox);
            m_CopyColorPass = new CopyColorPass(RenderPassEvent.BeforeRenderingTransparents, m_SamplingMaterial);
            m_TransparentSettingsPass = new TransparentSettingsPass(RenderPassEvent.BeforeRenderingTransparents, data.shadowTransparentReceive);
            m_RenderTransparentForwardPass = new DrawObjectsPass("Render Transparents", false, RenderPassEvent.BeforeRenderingTransparents, RenderQueueRange.transparent, data.transparentLayerMask, m_DefaultStencilState, stencilData.stencilReference);
            m_OnRenderObjectCallbackPass = new InvokeOnRenderObjectCallbackPass(RenderPassEvent.BeforeRenderingPostProcessing);
            m_PostProcessPass = new PostProcessPass(RenderPassEvent.BeforeRenderingPostProcessing, data.postProcessData, m_BlitMaterial);
            m_FinalPostProcessPass = new PostProcessPass(RenderPassEvent.AfterRendering + 1, data.postProcessData, m_BlitMaterial);
            m_CapturePass = new CapturePass(RenderPassEvent.AfterRendering);
            m_FinalBlitPass = new FinalBlitPass(RenderPassEvent.AfterRendering + 1, m_BlitMaterial);

#if UNITY_EDITOR
            m_SceneViewDepthCopyPass = new SceneViewDepthCopyPass(RenderPassEvent.AfterRendering + 9, m_CopyDepthMaterial);
#endif

            // RenderTexture format depends on camera and pipeline (HDR, non HDR, etc)
            // Samples (MSAA) depend on camera and pipeline
            // string shaderProperty passed to Init() is use to refer to a texture from shader code
            m_CameraColorTexture.Init("_CameraColorTexture");
            m_CameraDepthTexture.Init("_CameraDepthTexture");
            m_CameraDepthAttachment.Init("_CameraDepthAttachment");

            m_GBufferAttachments[0].Init("_GBuffer0");
            m_GBufferAttachments[1].Init("_GBuffer1");
            m_GBufferAttachments[2].Init("_GBuffer2");

            m_OpaqueColor.Init("_CameraOpaqueTexture");
            m_AfterPostProcessColor.Init("_AfterPostProcessTexture");
            m_ColorGradingLut.Init("_InternalGradingLut");
            m_DepthInfoTexture.Init("_DepthInfoTexture");
            m_TileDepthInfoTexture.Init("_TileDepthInfoTexture");

            supportedRenderingFeatures = new RenderingFeatures()
            {
                cameraStacking = true,
                msaa = false,
            };
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            // always dispose unmanaged resources
            m_PostProcessPass.Cleanup();
            // m_FinalPostProcessPass.Cleanup() ?
            CoreUtils.Destroy(m_BlitMaterial);
            CoreUtils.Destroy(m_CopyDepthMaterial);
            CoreUtils.Destroy(m_SamplingMaterial);
            CoreUtils.Destroy(m_ScreenspaceShadowsMaterial);
            CoreUtils.Destroy(m_TileDepthInfoMaterial);
            CoreUtils.Destroy(m_TileDeferredMaterial);
            CoreUtils.Destroy(m_StencilDeferredMaterial);
        }

        /// <inheritdoc />
        public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            ref CameraData cameraData = ref renderingData.cameraData;
            RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            bool requiresDepthPrepass = cameraData.isSceneViewCamera || m_PreferDepthPrepass || m_DeferredLights.tiledDeferredShading;

            // TODO: There's an issue in multiview and depth copy pass. Atm forcing a depth prepass on XR until we have a proper fix.
            if (cameraData.isStereoEnabled && cameraData.requiresDepthTexture)
                requiresDepthPrepass = true;

            // Special path for depth only offscreen cameras. Only write opaques + transparents.
            bool isOffscreenDepthTexture = cameraData.targetTexture != null && cameraData.targetTexture.format == RenderTextureFormat.Depth;
            if (isOffscreenDepthTexture)
            {
                ConfigureCameraTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget);

                for (int i = 0; i < rendererFeatures.Count; ++i)
                {
                    if(rendererFeatures[i].isActive)
                        rendererFeatures[i].AddRenderPasses(this, ref renderingData);
                }

                if (requiresDepthPrepass)
                {
                    m_DepthPrepass.Setup(cameraTargetDescriptor, m_CameraDepthAttachment);
                    EnqueuePass(m_DepthPrepass);
                }

                // Deferred shading do not try to apply shadow because we don't render any
                // (m_MainLightShadowCasterPass and m_AdditionalLightsShadowCasterPass are not queued).
                EnqueueDeferred(ref renderingData, requiresDepthPrepass, false, false, context);

                EnqueuePass(m_DrawSkyboxPass);
                EnqueuePass(m_RenderTransparentForwardPass);
                return;
            }

            // Should apply post-processing after rendering this camera?
            bool applyPostProcessing = cameraData.postProcessEnabled;
            // There's at least a camera in the camera stack that applies post-processing
            bool anyPostProcessing = renderingData.postProcessingEnabled;


            // We generate color LUT in the base camera only. This allows us to not break render pass execution for overlay cameras.
            bool generateColorGradingLUT = anyPostProcessing && cameraData.renderType == CameraRenderType.Base;
            bool isStereoEnabled = cameraData.isStereoEnabled;

            bool mainLightShadows = m_MainLightShadowCasterPass.Setup(ref renderingData);
            bool additionalLightShadows = m_AdditionalLightsShadowCasterPass.Setup(ref renderingData);
            bool transparentsNeedSettingsPass = m_TransparentSettingsPass.Setup(ref renderingData);

            // The copying of depth should normally happen after rendering opaques only.
            // But if we only require it for post processing or the scene camera then we do it after rendering transparent objects
            m_CopyDepthPass.renderPassEvent = (!cameraData.requiresDepthTexture && (applyPostProcessing || cameraData.isSceneViewCamera)) ? RenderPassEvent.AfterRenderingTransparents : RenderPassEvent.AfterRenderingOpaques;

            // Configure all settings require to start a new camera stack (base camera only)
            if (cameraData.renderType == CameraRenderType.Base)
            {
                m_ActiveCameraColorAttachment = m_CameraColorTexture;
                m_ActiveCameraDepthAttachment = m_CameraDepthAttachment;

                CreateCameraRenderTarget(context, ref renderingData.cameraData, m_ActiveCameraColorAttachment, m_ActiveCameraDepthAttachment);

                if (Camera.main == camera && camera.cameraType == CameraType.Game && cameraData.targetTexture == null)
                    SetupBackbufferFormat(1, isStereoEnabled);
            }
            else
            {
                m_ActiveCameraColorAttachment = m_CameraColorTexture;
                m_ActiveCameraDepthAttachment = m_CameraDepthAttachment;
            }

            var m_CameraColorDescriptor = new AttachmentDescriptor(cameraTargetDescriptor.graphicsFormat);
            m_CameraColorDescriptor.ConfigureTarget(m_CameraColorTexture.Identifier(), true, true);
            var m_CameraDepthDescriptor = new AttachmentDescriptor(RenderTextureFormat.Depth);
            m_CameraDepthDescriptor.ConfigureTarget(m_CameraDepthAttachment.Identifier(), true, false);

            ConfigureCameraTarget(m_ActiveCameraColorAttachment.Identifier(),
                m_ActiveCameraDepthAttachment.Identifier(), m_CameraColorDescriptor, m_CameraDepthDescriptor);

            for (int i = 0; i < rendererFeatures.Count; ++i)
            {
                if (rendererFeatures[i].isActive)
                    rendererFeatures[i].AddRenderPasses(this, ref renderingData);
            }

            int count = activeRenderPassQueue.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                if (activeRenderPassQueue[i] == null)
                    activeRenderPassQueue.RemoveAt(i);
            }

            bool hasPassesAfterPostProcessing =
                activeRenderPassQueue.Find(x => x.renderPassEvent == RenderPassEvent.AfterRendering) != null;

            if (mainLightShadows)
                EnqueuePass(m_MainLightShadowCasterPass);

            if (additionalLightShadows)
                EnqueuePass(m_AdditionalLightsShadowCasterPass);

            if (requiresDepthPrepass)
            {
                m_DepthPrepass.Setup(cameraTargetDescriptor, m_CameraDepthAttachment);
                EnqueuePass(m_DepthPrepass);
            }

            if (generateColorGradingLUT)
            {
                m_ColorGradingLutPass.Setup(m_ColorGradingLut);
                EnqueuePass(m_ColorGradingLutPass);
            }

            #region RenderPass1

            EnqueueDeferred(ref renderingData, requiresDepthPrepass, mainLightShadows, additionalLightShadows, context);

            #endregion

            #region RenderPass2

            bool isOverlayCamera = cameraData.renderType == CameraRenderType.Overlay;

            if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null && !isOverlayCamera)
            {
                // Previous pass configured different CameraTargets, restore main color and depth to be used as targets by the DrawSkybox pass:
                m_DrawSkyboxPass.ConfigureTarget(m_CameraColorDescriptor, m_CameraDepthDescriptor);
                EnqueueRenderPass(m_DrawSkyboxPass, cameraTargetDescriptor);
            }

            // If a depth texture was created we necessarily need to copy it, otherwise we could have render it to a renderbuffer
            // This is inserted before m_DrawSkyboxPass or after m_TransparentSettingsPass ...
            if (!requiresDepthPrepass && renderingData.cameraData.requiresDepthTexture)
            {
                m_CopyDepthPass.Setup(m_CameraDepthAttachment, m_CameraDepthTexture);
                EnqueuePass(m_CopyDepthPass);
            }

            // This is useful for refraction effects (particle system).
            if (renderingData.cameraData.requiresOpaqueTexture)
            {
                // TODO: Downsampling method should be store in the renderer instead of in the asset.
                // We need to migrate this data to renderer. For now, we query the method in the active asset.
                Downsampling downsamplingMethod = Downsampling.None; // Ignoring this setting as otherwise it will break RenderPass
                m_CopyColorPass.Setup(m_CameraColorTexture.Identifier(), m_OpaqueColor, downsamplingMethod);

                var opaqueDescriptor = new AttachmentDescriptor(cameraTargetDescriptor.graphicsFormat);
                opaqueDescriptor.ConfigureTarget(m_OpaqueColor.Identifier(), false, true); //Seems like store is required, though used inside the renderPass

                m_CopyColorPass.ConfigureInputAttachment(m_CameraColorDescriptor);
                m_CopyColorPass.ConfigureTarget(opaqueDescriptor);

                EnqueueRenderPass(m_CopyColorPass, cameraTargetDescriptor);
            }

            if (transparentsNeedSettingsPass)
            {
                m_TransparentSettingsPass.ConfigureTarget(m_CameraColorDescriptor, m_CameraDepthDescriptor);
                EnqueueRenderPass(m_TransparentSettingsPass, cameraTargetDescriptor); // Only toggle shader keywords for shadow receivers
            }

            m_RenderTransparentForwardPass.ConfigureTarget(m_CameraColorDescriptor, m_CameraDepthDescriptor);
            EnqueueRenderPass(m_RenderTransparentForwardPass, cameraTargetDescriptor);

            m_OnRenderObjectCallbackPass.ConfigureTarget(m_CameraColorDescriptor, m_CameraDepthDescriptor);
            EnqueueRenderPass(m_OnRenderObjectCallbackPass, cameraTargetDescriptor);

            #endregion
            bool lastCameraInTheStack = cameraData.resolveFinalTarget;
            bool hasCaptureActions = renderingData.cameraData.captureActions != null && lastCameraInTheStack;

            bool applyFinalPostProcessing = anyPostProcessing && lastCameraInTheStack &&
                                     renderingData.cameraData.antialiasing == AntialiasingMode.FastApproximateAntialiasing;

            // When post-processing is enabled we can use the stack to resolve rendering to camera target (screen or RT).
            // However when there are render passes executing after post we avoid resolving to screen so rendering continues (before sRGBConvertion etc)
            bool dontResolvePostProcessingToCameraTarget = hasCaptureActions || hasPassesAfterPostProcessing || applyFinalPostProcessing;

            if (lastCameraInTheStack)
            {
                // Post-processing will resolve to final target. No need for final blit pass.
                if (applyPostProcessing)
                {
                    var destination = dontResolvePostProcessingToCameraTarget ? m_AfterPostProcessColor : RenderTargetHandle.CameraTarget;

                    // if resolving to screen we need to be able to perform sRGBConvertion in post-processing if necessary
                    bool doSRGBConvertion = !(dontResolvePostProcessingToCameraTarget || (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget));
                    m_PostProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, destination, m_ActiveCameraDepthAttachment, m_ColorGradingLut, applyFinalPostProcessing, doSRGBConvertion);
                    Debug.Assert(applyPostProcessing || doSRGBConvertion, "This will do unnecessary blit!");
                    EnqueuePass(m_PostProcessPass);
                }

                if (renderingData.cameraData.captureActions != null)
                {
                    m_CapturePass.Setup(m_ActiveCameraColorAttachment);
                    EnqueuePass(m_CapturePass);
                }

                // if we applied post-processing for this camera it means current active texture is m_AfterPostProcessColor
                var sourceForFinalPass = (applyPostProcessing) ? m_AfterPostProcessColor : m_ActiveCameraColorAttachment;

                // Do FXAA or any other final post-processing effect that might need to run after AA.
                if (applyFinalPostProcessing)
                {
                    m_FinalPostProcessPass.SetupFinalPass(sourceForFinalPass);
                    EnqueuePass(m_FinalPostProcessPass);
                }

                // if post-processing then we already resolved to camera target while doing post.
                // Also only do final blit if camera is not rendering to RT.
                bool cameraTargetResolved =
                    // final PP always blit to camera target
                    applyFinalPostProcessing ||
                    // no final PP but we have PP stack. In that case it blit unless there are render pass after PP
                    (applyPostProcessing && !hasPassesAfterPostProcessing) ||
                    // offscreen camera rendering to a texture, we don't need a blit pass to resolve to screen
                    m_ActiveCameraColorAttachment == RenderTargetHandle.CameraTarget;

                // We need final blit to resolve to screen
                if (!cameraTargetResolved)
                {
                    var blitAttachment = new AttachmentDescriptor(m_CameraColorDescriptor.graphicsFormat);
                    blitAttachment.ConfigureTarget((cameraData.targetTexture != null) ? new RenderTargetIdentifier(cameraData.targetTexture) : BuiltinRenderTextureType.CameraTarget, false, true);
                    m_FinalBlitPass.ConfigureTarget(blitAttachment);
                    m_FinalBlitPass.Setup(cameraTargetDescriptor, sourceForFinalPass);
                    EnqueueRenderPass(m_FinalBlitPass, cameraTargetDescriptor);
                }
            }

            // stay in RT so we resume rendering on stack after post-processing
            else if (applyPostProcessing)
            {
                m_PostProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, m_AfterPostProcessColor, m_ActiveCameraDepthAttachment, m_ColorGradingLut, false, false);
                EnqueuePass(m_PostProcessPass);
            }

#if UNITY_EDITOR
            if (renderingData.cameraData.isSceneViewCamera)
            {
                // Scene view camera should always resolve target (not stacked)
                Assertions.Assert.IsTrue(lastCameraInTheStack, "Editor camera must resolve target upon finish rendering.");
                m_SceneViewDepthCopyPass.Setup(m_CameraDepthTexture);
                EnqueuePass(m_SceneViewDepthCopyPass);
            }
#endif
        }

        /// <inheritdoc />
        public override void SetupLights(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            m_ForwardLights.Setup(context, ref renderingData);

            // Perform per-tile light culling on CPU
            m_DeferredLights.SetupLights(context, ref renderingData);
        }

        /// <inheritdoc />
        public override void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters,
            ref CameraData cameraData)
        {
            // TODO: PerObjectCulling also affect reflection probes. Enabling it for now.
            // if (asset.additionalLightsRenderingMode == LightRenderingMode.Disabled ||
            //     asset.maxAdditionalLightsCount == 0)
            // {
            //     cullingParameters.cullingOptions |= CullingOptions.DisablePerObjectCulling;
            // }

            // We disable shadow casters if both shadow casting modes are turned off
            // or the shadow distance has been turned down to zero
            bool isShadowCastingDisabled = !UniversalRenderPipeline.asset.supportsMainLightShadows && !UniversalRenderPipeline.asset.supportsAdditionalLightShadows;
            bool isShadowDistanceZero = Mathf.Approximately(cameraData.maxShadowDistance, 0.0f);
            if (isShadowCastingDisabled || isShadowDistanceZero)
            {
                cullingParameters.cullingOptions &= ~CullingOptions.ShadowCasters;
            }

            // We set the number of maximum visible lights allowed and we add one for the mainlight...
            cullingParameters.maximumVisibleLights = 0xFFFF;
            cullingParameters.shadowDistance = cameraData.maxShadowDistance;
        }

        /// <inheritdoc />
        public override void FinishRendering(CommandBuffer cmd)
        {
            if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(m_ActiveCameraColorAttachment.id);
                m_ActiveCameraColorAttachment = RenderTargetHandle.CameraTarget;
            }

            if (m_ActiveCameraDepthAttachment != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(m_ActiveCameraDepthAttachment.id);
                m_ActiveCameraDepthAttachment = RenderTargetHandle.CameraTarget;
            }
        }

        void EnqueueDeferred(ref RenderingData renderingData, bool hasDepthPrepass, bool applyMainShadow, bool applyAdditionalShadow, ScriptableRenderContext context)
        {
#if UNITY_IOS && !UNITY_EDITOR
            int depthAsRT = 1;
#else
            int depthAsRT = 0;
#endif

            var desc = renderingData.cameraData.cameraTargetDescriptor;

            RenderTargetHandle[] gbufferColorAttachments = new RenderTargetHandle[k_GBufferSlicesCount + 1 + depthAsRT];
            for (int gbufferIndex = 0; gbufferIndex < k_GBufferSlicesCount; ++gbufferIndex)
                gbufferColorAttachments[gbufferIndex] = m_GBufferAttachments[gbufferIndex];
            gbufferColorAttachments[k_GBufferSlicesCount] = m_ActiveCameraColorAttachment; // the last slice is the lighting buffer created in DeferredRenderer.cs

            AttachmentDescriptor[] gbufferAttachmentDescriptors = new AttachmentDescriptor[k_GBufferSlicesCount + 1 + depthAsRT];

            gbufferAttachmentDescriptors[0] = new AttachmentDescriptor(GBufferPass.kGBufferFormats[0]);
            gbufferAttachmentDescriptors[1] = new AttachmentDescriptor(GBufferPass.kGBufferFormats[1]);
            gbufferAttachmentDescriptors[2] = new AttachmentDescriptor(GBufferPass.kGBufferFormats[2]);

            gbufferAttachmentDescriptors[3] = new AttachmentDescriptor(renderingData.cameraData.cameraTargetDescriptor.graphicsFormat);
            gbufferAttachmentDescriptors[3].ConfigureTarget(m_ActiveCameraColorAttachment.Identifier(), false, true);

#if UNITY_IOS && !UNITY_EDITOR
            gbufferAttachmentDescriptors[k_GBufferSlicesCount + 1] = new AttachmentDescriptor(GBufferPass.kGBufferFormats[4]);
#endif

            AttachmentDescriptor depthBufferAttachmentDescriptor = new AttachmentDescriptor(RenderTextureFormat.Depth);
            depthBufferAttachmentDescriptor.ConfigureTarget(m_CameraDepthAttachment.Identifier(), false, true);

            if (!hasDepthPrepass)
                depthBufferAttachmentDescriptor.ConfigureClear(Color.black, 1.0f, 0);

            m_GBufferPass.ConfigureTarget(gbufferAttachmentDescriptors, depthBufferAttachmentDescriptor);
            EnqueueRenderPass(m_GBufferPass, desc);

            m_DeferredLights.Setup(ref renderingData, applyAdditionalShadow ? m_AdditionalLightsShadowCasterPass : null, m_CameraDepthTexture, m_DepthInfoTexture, m_TileDepthInfoTexture, m_CameraDepthAttachment, gbufferColorAttachments);

            // Note: DeferredRender.Setup is called by UniversalRenderPipeline.RenderSingleCamera (overrides ScriptableRenderer.Setup).
            // At this point, we do not know if m_DeferredLights.m_Tilers[x].m_Tiles actually contain any indices of lights intersecting tiles (If there are no lights intersecting tiles, we could skip several following passes) : this information is computed in DeferredRender.SetupLights, which is called later by UniversalRenderPipeline.RenderSingleCamera (via ScriptableRenderer.Execute).
            // However HasTileLights uses m_HasTileVisLights which is calculated by CheckHasTileLights from all visibleLights. visibleLights is the list of lights that have passed camera culling, so we know they are in front of the camera. So we can assume m_DeferredLights.m_Tilers[x].m_Tiles will not be empty in that case.
            // m_DeferredLights.m_Tilers[x].m_Tiles could be empty if we implemented an algorithm accessing scene depth information on the CPU side, but this (access depth from CPU) will probably not happen.
            if (m_DeferredLights.HasTileLights())
            {
                // Compute for each tile a 32bits bitmask in which a raised bit means "this 1/32th depth slice contains geometry that could intersect with lights".
                // Per-tile bitmasks are obtained by merging together the per-pixel bitmasks computed for each individual pixel of the tile.
                EnqueuePass(m_TileDepthRangePass);

                // On some platform, splitting the bitmasks computation into two passes:
                //   1/ Compute bitmasks for individual or small blocks of pixels
                //   2/ merge those individual bitmasks into per-tile bitmasks
                // provides better performance that doing it in a single above pass.
                if (m_DeferredLights.HasTileDepthRangeExtraPass())
                    EnqueuePass(m_TileDepthRangeExtraPass);
            }

            m_DeferredPass.ConfigureTarget(gbufferAttachmentDescriptors[k_GBufferSlicesCount], depthBufferAttachmentDescriptor);
            m_DeferredPass.ConfigureInputAttachment(new[] {gbufferAttachmentDescriptors[0], gbufferAttachmentDescriptors[1], gbufferAttachmentDescriptors[2]
#if UNITY_IOS && !UNITY_EDITOR
                , gbufferAttachmentDescriptors[4]});
#else
                , depthBufferAttachmentDescriptor});
#endif
            EnqueueRenderPass(m_DeferredPass, desc);

            // Must explicitely set correct depth target to the transparent pass (it will bind a different depth target otherwise).
            m_RenderOpaqueForwardOnlyPass.ConfigureTarget(m_GBufferPass.colorAttachmentDescriptors[k_GBufferSlicesCount], depthBufferAttachmentDescriptor);
            EnqueueRenderPass(m_RenderOpaqueForwardOnlyPass, desc);
        }

        void CreateCameraRenderTarget(ScriptableRenderContext context, ref CameraData cameraData, RenderTargetHandle colorTarget, RenderTargetHandle depthTarget)
        {
            CommandBuffer cmd = CommandBufferPool.Get(k_CreateCameraTextures);
            var descriptor = cameraData.cameraTargetDescriptor;
            int msaaSamples = descriptor.msaaSamples;
            if (colorTarget != RenderTargetHandle.CameraTarget)
            {
                bool useDepthRenderBuffer = depthTarget == RenderTargetHandle.CameraTarget;
                var colorDescriptor = descriptor; // Camera decides if HDR format is needed. ScriptableRenderPipelineCore.cs decides between FP16 and R11G11B10.
                colorDescriptor.depthBufferBits = (useDepthRenderBuffer) ? k_DepthStencilBufferBits : 0;
                cmd.GetTemporaryRT(colorTarget.id, colorDescriptor, FilterMode.Bilinear);
            }

            if (depthTarget != RenderTargetHandle.CameraTarget)
            {
                var depthDescriptor = descriptor;
                depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                depthDescriptor.depthBufferBits = k_DepthStencilBufferBits;
                depthDescriptor.bindMS = msaaSamples > 1 && !SystemInfo.supportsMultisampleAutoResolve && (SystemInfo.supportsMultisampledTextures != 0);
                cmd.GetTemporaryRT(depthTarget.id, depthDescriptor, FilterMode.Point);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
