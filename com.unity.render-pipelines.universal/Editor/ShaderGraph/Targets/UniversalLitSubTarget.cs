﻿using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.ShaderGraph;
using UnityEngine.Rendering;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    sealed class UniversalLitSubTarget : SubTarget<UniversalTarget>
    {
        const string kAssetGuid = "d6c78107b64145745805d963de80cc17";

        [SerializeField]
        WorkflowMode m_WorkflowMode = WorkflowMode.Metallic;

        [SerializeField]
        NormalDropOffSpace m_NormalDropOffSpace = NormalDropOffSpace.Tangent;

        public UniversalLitSubTarget()
        {
            displayName = "Lit";
        }

        public WorkflowMode workflowMode
        {
            get => m_WorkflowMode;
            set => m_WorkflowMode = value;
        }

        public NormalDropOffSpace normalDropOffSpace
        {
            get => m_NormalDropOffSpace;
            set => m_NormalDropOffSpace = value;
        }

        public override void Setup(ref TargetSetupContext context)
        {
            context.AddAssetDependencyPath(AssetDatabase.GUIDToAssetPath(kAssetGuid));
            context.SetDefaultShaderGUI("ShaderGraph.PBRMasterGUI"); // TODO: This should be owned by URP

            // Process SubShaders
            SubShaderDescriptor[] subShaders = { SubShaders.Lit, SubShaders.LitDOTS };
            for(int i = 0; i < subShaders.Length; i++)
            {
                // Update Render State
                subShaders[i].renderType = target.renderType;
                subShaders[i].renderQueue = target.renderQueue;

                // Add
                context.AddSubShader(subShaders[i]);
            }
        }

        public override void GetFields(ref TargetFieldContext context)
        {
            // Surface Type & Blend Mode
            // These must be set per SubTarget as Sprite SubTargets override them
            context.AddField(Fields.SurfaceOpaque,       target.surfaceType == SurfaceType.Opaque);
            context.AddField(Fields.SurfaceTransparent,  target.surfaceType != SurfaceType.Opaque);
            context.AddField(Fields.BlendAdd,            target.surfaceType != SurfaceType.Opaque && target.alphaMode == AlphaMode.Additive);
            context.AddField(Fields.BlendAlpha,          target.surfaceType != SurfaceType.Opaque && target.alphaMode == AlphaMode.Alpha);
            context.AddField(Fields.BlendMultiply,       target.surfaceType != SurfaceType.Opaque && target.alphaMode == AlphaMode.Multiply);
            context.AddField(Fields.BlendPremultiply,    target.surfaceType != SurfaceType.Opaque && target.alphaMode == AlphaMode.Premultiply);

            // Lit
            context.AddField(Fields.NormalDropOffOS,     normalDropOffSpace == NormalDropOffSpace.Object);
            context.AddField(Fields.NormalDropOffTS,     normalDropOffSpace == NormalDropOffSpace.Tangent);
            context.AddField(Fields.NormalDropOffWS,     normalDropOffSpace == NormalDropOffSpace.World);
            context.AddField(Fields.SpecularSetup,       workflowMode == WorkflowMode.Specular);
            context.AddField(Fields.Normal,              context.blocks.Contains(BlockFields.SurfaceDescription.NormalTS));
        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            context.AddBlock(BlockFields.SurfaceDescription.Smoothness);
            context.AddBlock(BlockFields.SurfaceDescription.NormalTS);
            context.AddBlock(BlockFields.SurfaceDescription.Emission);
            context.AddBlock(BlockFields.SurfaceDescription.Occlusion);
            context.AddBlock(BlockFields.SurfaceDescription.Specular,           workflowMode == WorkflowMode.Specular);
            context.AddBlock(BlockFields.SurfaceDescription.Metallic,           workflowMode == WorkflowMode.Metallic);
            context.AddBlock(BlockFields.SurfaceDescription.Alpha,              target.surfaceType == SurfaceType.Transparent || target.alphaClip);
            context.AddBlock(BlockFields.SurfaceDescription.AlphaClipThreshold, target.alphaClip);
        }

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange)
        {
            context.AddProperty("Workflow", new EnumField(WorkflowMode.Metallic) { value = workflowMode }, (evt) =>
            {
                if (Equals(workflowMode, evt.newValue))
                    return;

                workflowMode = (WorkflowMode)evt.newValue;
                onChange();
            });

            context.AddProperty("Surface", new EnumField(SurfaceType.Opaque) { value = target.surfaceType }, (evt) =>
            {
                if (Equals(target.surfaceType, evt.newValue))
                    return;
                
                target.surfaceType = (SurfaceType)evt.newValue;
                onChange();
            });

            context.AddProperty("Blend", new EnumField(AlphaMode.Alpha) { value = target.alphaMode }, target.surfaceType == SurfaceType.Transparent, (evt) =>
            {
                if (Equals(target.alphaMode, evt.newValue))
                    return;

                target.alphaMode = (AlphaMode)evt.newValue;
                onChange();
            });

            context.AddProperty("Alpha Clip", new Toggle() { value = target.alphaClip }, (evt) =>
            {
                if (Equals(target.alphaClip, evt.newValue))
                    return;
                
                target.alphaClip = evt.newValue;
                onChange();
            });

            context.AddProperty("Two Sided", new Toggle() { value = target.twoSided }, (evt) =>
            {
                if (Equals(target.twoSided, evt.newValue))
                    return;
                
                target.twoSided = evt.newValue;
                onChange();
            });

            context.AddProperty("Fragment Normal Space", new EnumField(NormalDropOffSpace.Tangent) { value = normalDropOffSpace }, (evt) =>
            {
                if (Equals(normalDropOffSpace, evt.newValue))
                    return;

                normalDropOffSpace = (NormalDropOffSpace)evt.newValue;
                onChange();
            });
        }

#region SubShader
        static class SubShaders
        {
            public static SubShaderDescriptor Lit = new SubShaderDescriptor()
            {
                pipelineTag = UniversalTarget.kPipelineTag,
                generatesPreview = true,
                passes = new PassCollection
                {
                    { LitPasses.Forward },
                    { CorePasses.ShadowCaster },
                    { CorePasses.DepthOnly },
                    { LitPasses.Meta },
                    { LitPasses._2D },
                },
            };

            public static SubShaderDescriptor LitDOTS
            {
                get
                {
                    var forward = LitPasses.Forward;
                    var shadowCaster = CorePasses.ShadowCaster;
                    var depthOnly = CorePasses.DepthOnly;
                    var meta = LitPasses.Meta;
                    var _2d = LitPasses._2D;

                    forward.pragmas = CorePragmas.DOTSForward;
                    shadowCaster.pragmas = CorePragmas.DOTSInstanced;
                    depthOnly.pragmas = CorePragmas.DOTSInstanced;
                    meta.pragmas = CorePragmas.DOTSDefault;
                    _2d.pragmas = CorePragmas.DOTSDefault;
                    
                    return new SubShaderDescriptor()
                    {
                        pipelineTag = UniversalTarget.kPipelineTag,
                        generatesPreview = true,
                        passes = new PassCollection
                        {
                            { forward },
                            { shadowCaster },
                            { depthOnly },
                            { meta },
                            { _2d },
                        },
                    };
                }
            }
        }
#endregion

#region Passes
        static class LitPasses
        {
            public static PassDescriptor Forward = new PassDescriptor
            {
                // Definition
                displayName = "Universal Forward",
                referenceName = "SHADERPASS_FORWARD",
                lightMode = "UniversalForward",
                useInPreview = true,

                // Template
                passTemplatePath = GenerationUtils.GetDefaultTemplatePath("PassMesh.template"),
                sharedTemplateDirectory = GenerationUtils.GetDefaultSharedTemplateDirectory(),

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentLit,

                // Fields
                structs = CoreStructCollections.Default,
                requiredFields = LitRequiredFields.Forward,
                fieldDependencies = CoreFieldDependencies.Default,

                // Conditional State
                renderStates = CoreRenderStates.Default,
                pragmas = CorePragmas.Forward,
                keywords = LitKeywords.Forward,
                includes = LitIncludes.Forward,
            };

            public static PassDescriptor Meta = new PassDescriptor()
            {
                // Definition
                displayName = "Meta",
                referenceName = "SHADERPASS_META",
                lightMode = "Meta",

                // Template
                passTemplatePath = GenerationUtils.GetDefaultTemplatePath("PassMesh.template"),
                sharedTemplateDirectory = GenerationUtils.GetDefaultSharedTemplateDirectory(),

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = LitBlockMasks.FragmentMeta,

                // Fields
                structs = CoreStructCollections.Default,
                requiredFields = LitRequiredFields.Meta,
                fieldDependencies = CoreFieldDependencies.Default,

                // Conditional State
                renderStates = CoreRenderStates.ShadowCasterMeta,
                pragmas = CorePragmas.Default,
                keywords = LitKeywords.Meta,
                includes = LitIncludes.Meta,
            };

            public static PassDescriptor _2D = new PassDescriptor()
            {
                // Definition
                referenceName = "SHADERPASS_2D",
                lightMode = "Universal2D",

                // Template
                passTemplatePath = GenerationUtils.GetDefaultTemplatePath("PassMesh.template"),
                sharedTemplateDirectory = GenerationUtils.GetDefaultSharedTemplateDirectory(),

                // Port Mask
                vertexBlocks = CoreBlockMasks.Vertex,
                pixelBlocks = CoreBlockMasks.FragmentColorAlpha,

                // Fields
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,

                // Conditional State
                renderStates = CoreRenderStates.Default,
                pragmas = CorePragmas.Instanced,
                includes = LitIncludes._2D,
            };
        }
#endregion

#region PortMasks
        static class LitBlockMasks
        {
            public static BlockFieldDescriptor[] FragmentLit = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Specular,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Occlusion,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
            };

            public static BlockFieldDescriptor[] FragmentMeta = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
            };
        }
#endregion

#region RequiredFields
        static class LitRequiredFields
        {
            public static FieldCollection Forward = new FieldCollection()
            {
                StructFields.Attributes.uv1,                            // needed for meta vertex position
                StructFields.Varyings.positionWS,
                StructFields.Varyings.normalWS,
                StructFields.Varyings.tangentWS,                        // needed for vertex lighting
                StructFields.Varyings.viewDirectionWS,
                UniversalStructFields.Varyings.lightmapUV,
                UniversalStructFields.Varyings.sh,
                UniversalStructFields.Varyings.fogFactorAndVertexLight, // fog and vertex lighting, vert input is dependency
                UniversalStructFields.Varyings.shadowCoord,             // shadow coord, vert input is dependency
            };

            public static FieldCollection Meta = new FieldCollection()
            {
                StructFields.Attributes.uv1,                            // needed for meta vertex position
                StructFields.Attributes.uv2,                            //needed for meta vertex position
            };
        }
#endregion

#region Keywords
        static class LitKeywords
        {
            public static KeywordCollection Forward = new KeywordCollection
            {
                { CoreKeywordDescriptors.Lightmap },
                { CoreKeywordDescriptors.DirectionalLightmapCombined },
                { CoreKeywordDescriptors.MainLightShadows },
                { CoreKeywordDescriptors.MainLightShadowsCascade },
                { CoreKeywordDescriptors.AdditionalLights },
                { CoreKeywordDescriptors.AdditionalLightShadows },
                { CoreKeywordDescriptors.ShadowsSoft },
                { CoreKeywordDescriptors.MixedLightingSubtractive },
            };

            public static KeywordCollection Meta = new KeywordCollection
            {
                { CoreKeywordDescriptors.SmoothnessChannel },
            };
        }
#endregion

#region Includes
        static class LitIncludes
        {
            const string kShadows = "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl";
            const string kMetaInput = "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl";
            const string kForwardPass = "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl";
            const string kLightingMetaPass = "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl";
            const string k2DPass = "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBR2DPass.hlsl";

            public static IncludeCollection Forward = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                { kShadows, IncludeLocation.Pregraph },
                { CoreIncludes.ShaderGraphPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kForwardPass, IncludeLocation.Postgraph },
            };

            public static IncludeCollection Meta = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                { CoreIncludes.ShaderGraphPregraph },
                { kMetaInput, IncludeLocation.Pregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kLightingMetaPass, IncludeLocation.Postgraph },
            };

            public static IncludeCollection _2D = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                { CoreIncludes.ShaderGraphPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { k2DPass, IncludeLocation.Postgraph },
            };
        }
#endregion
    }
}
