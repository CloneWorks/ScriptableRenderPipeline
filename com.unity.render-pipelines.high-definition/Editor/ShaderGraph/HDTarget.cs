﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UIElements;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEditor.Graphing;
using UnityEditor.UIElements;
using static UnityEngine.Rendering.HighDefinition.HDMaterialProperties;

namespace UnityEditor.Rendering.HighDefinition.ShaderGraph
{
    enum AlphaMode
    {
        Alpha,
        Premultiply,
        Additive,
    }

    enum DistortionMode
    {
        Add,
        Multiply,
        Replace
    }

    enum DoubleSidedMode
    {
        Disabled,
        Enabled,
        FlippedNormals,
        MirroredNormals,
    }

    enum EmissionGIMode
    {
        Disabled,
        Realtime,
        Baked,
    }

    enum SpecularOcclusionMode
    {
        Off,
        FromAO,
        FromAOAndBentNormal,
        Custom
    }

    sealed class HDTarget : Target, ISerializationCallbackReceiver
    {
        // Constants
        const string kAssetGuid = "61d9843d4027e3e4a924953135f76f3c";
        
        // SubTarget
        List<SubTarget> m_SubTargets;
        List<string> m_SubTargetNames;
        int activeSubTargetIndex => m_SubTargets.IndexOf(m_ActiveSubTarget);

        // View
        PopupField<string> m_SubTargetField;
        TextField m_CustomGUIField;

        // TODO: Remove when Peter's serialization lands
        [SerializeField]
        SerializationHelper.JSONSerializedElement m_SerializedSubTarget;

        [SerializeField]
        SubTarget m_ActiveSubTarget;

        [SerializeField]
        SurfaceType m_SurfaceType;

        [SerializeField]
        AlphaMode m_AlphaMode;

        [SerializeField]
        HDRenderQueue.RenderQueueType m_RenderingPass = HDRenderQueue.RenderQueueType.Opaque;

        [SerializeField]
        bool m_TransparencyFog = true;

        [SerializeField]
        bool m_Distortion;

        [SerializeField]
        DistortionMode m_DistortionMode;

        [SerializeField]
        bool m_DistortionDepthTest = true;

        [SerializeField]
        bool m_AlphaTest;

        [SerializeField]
        int m_SortPriority;

        [SerializeField]
        DoubleSidedMode m_DoubleSidedMode;

        [SerializeField]
        bool m_AddPrecomputedVelocity = false;

        [SerializeField]
        bool m_ZWrite = true;

        [SerializeField]
        TransparentCullMode m_TransparentCullMode = TransparentCullMode.Back;

        [SerializeField]
        CompareFunction m_ZTest = CompareFunction.LessEqual;

        [SerializeField]
        bool m_DOTSInstancing = false;

        [SerializeField]
        string m_CustomEditorGUI;

        public HDTarget()
        {
            displayName = "HDRP";
            m_SubTargets = TargetUtils.GetSubTargets(this);
            m_SubTargetNames = m_SubTargets.Select(x => x.displayName).ToList();
        }

        public static string sharedTemplateDirectory => $"{HDUtils.GetHDRenderPipelinePath()}Editor/ShaderGraph/Templates";

        public SubTarget activeSubTarget
        {
            get => m_ActiveSubTarget;
            set => m_ActiveSubTarget = value;
        }

        public SurfaceType surfaceType
        {
            get => m_SurfaceType;
            set => m_SurfaceType = value;
        }

        public AlphaMode alphaMode
        {
            get => m_AlphaMode;
            set => m_AlphaMode = value;
        }

        public HDRenderQueue.RenderQueueType renderingPass
        {
            get => m_RenderingPass;
            set => m_RenderingPass = value;
        }

        public bool transparencyFog
        {
            get => m_TransparencyFog;
            set => m_TransparencyFog = value;
        }

        public bool distortion
        {
            get => m_Distortion;
            set => m_Distortion = value;
        }

        public DistortionMode distortionMode
        {
            get => m_DistortionMode;
            set => m_DistortionMode = value;
        }

        public bool distortionDepthTest
        {
            get => m_DistortionDepthTest;
            set => m_DistortionDepthTest = value;
        }

        public bool alphaTest
        {
            get => m_AlphaTest;
            set => m_AlphaTest = value;
        }

        public int sortPriority
        {
            get => m_SortPriority;
            set => m_SortPriority = value;
        }

        public DoubleSidedMode doubleSidedMode
        {
            get => m_DoubleSidedMode;
            set => m_DoubleSidedMode = value;
        }

        public bool addPrecomputedVelocity
        {
            get => m_AddPrecomputedVelocity;
            set => m_AddPrecomputedVelocity = value;
        }

        public bool zWrite
        {
            get => m_ZWrite;
            set => m_ZWrite = value;
        }

        public TransparentCullMode transparentCullMode
        {
            get => m_TransparentCullMode;
            set => m_TransparentCullMode = value;
        }

        public CompareFunction zTest
        {
            get => m_ZTest;
            set => m_ZTest = value;
        }

        public bool dotsInstancing
        {
            get => m_DOTSInstancing;
            set => m_DOTSInstancing = value;
        }

        public string customEditorGUI
        {
            get => m_CustomEditorGUI;
            set => m_CustomEditorGUI = value;
        }

        public override void Setup(ref TargetSetupContext context)
        {
            // Setup the Target
            context.AddAssetDependencyPath(AssetDatabase.GUIDToAssetPath(kAssetGuid));

            // Setup the active SubTarget
            TargetUtils.ProcessSubTargetList(ref m_ActiveSubTarget, ref m_SubTargets);
            m_ActiveSubTarget.Setup(ref context);

            // Override EditorGUI
            if(!string.IsNullOrEmpty(m_CustomEditorGUI))
            {
                context.SetDefaultShaderGUI(m_CustomEditorGUI);
            }
        }
        
        public override void GetFields(ref TargetFieldContext context)
        {
            // Core fields
            context.AddField(Fields.GraphVertex,            context.blocks.Contains(BlockFields.VertexDescription.Position) ||
                                                            context.blocks.Contains(BlockFields.VertexDescription.Normal) ||
                                                            context.blocks.Contains(BlockFields.VertexDescription.Tangent));
            context.AddField(Fields.GraphPixel);

            // Distortion
            context.AddField(HDFields.DistortionAdd,                distortionMode == DistortionMode.Add);
            context.AddField(HDFields.DistortionMultiply,           distortionMode == DistortionMode.Multiply);
            context.AddField(HDFields.DistortionReplace,            distortionMode == DistortionMode.Replace);
            context.AddField(HDFields.TransparentDistortion,        surfaceType != SurfaceType.Opaque && distortion);
            context.AddField(HDFields.DistortionDepthTest,          distortionDepthTest);

            // Misc
            context.AddField(Fields.AlphaTest,                      alphaTest && context.pass.pixelBlocks.Contains(BlockFields.SurfaceDescription.AlphaClipThreshold));
            context.AddField(HDFields.DoAlphaTest,                  alphaTest && context.pass.pixelBlocks.Contains(BlockFields.SurfaceDescription.AlphaClipThreshold));
            context.AddField(HDFields.AlphaFog,                     surfaceType != SurfaceType.Opaque && transparencyFog);
            context.AddField(Fields.VelocityPrecomputed,            addPrecomputedVelocity);

            // SubTarget fields
            m_ActiveSubTarget.GetFields(ref context);
        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            // Core blocks
            context.AddBlock(BlockFields.VertexDescription.Position);
            context.AddBlock(BlockFields.VertexDescription.Normal);
            context.AddBlock(BlockFields.VertexDescription.Tangent);
            context.AddBlock(BlockFields.SurfaceDescription.BaseColor);
            context.AddBlock(BlockFields.SurfaceDescription.Emission);
            context.AddBlock(BlockFields.SurfaceDescription.Alpha,              m_SurfaceType == SurfaceType.Transparent || m_AlphaTest);
            context.AddBlock(BlockFields.SurfaceDescription.AlphaClipThreshold, m_AlphaTest);
            context.AddBlock(HDBlockFields.SurfaceDescription.Distortion,       m_SurfaceType == SurfaceType.Transparent && m_Distortion);
            context.AddBlock(HDBlockFields.SurfaceDescription.DistortionBlur,   m_SurfaceType == SurfaceType.Transparent && m_Distortion);

            // SubTarget blocks
            m_ActiveSubTarget.GetActiveBlocks(ref context);
        }

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange)
        {
            // Core properties
            m_SubTargetField = new PopupField<string>(m_SubTargetNames, activeSubTargetIndex);
            context.AddProperty("Material", m_SubTargetField, (evt) =>
            {
                if (Equals(activeSubTargetIndex, m_SubTargetField.index))
                    return;

                m_ActiveSubTarget = m_SubTargets[m_SubTargetField.index];
                onChange();
            });

            // SubTarget properties
            m_ActiveSubTarget.GetPropertiesGUI(ref context, onChange);

            // Custom Editor GUI
            // Requires FocusOutEvent
            m_CustomGUIField = new TextField("") { value = customEditorGUI };
            m_CustomGUIField.RegisterCallback<FocusOutEvent>(s =>
            {
                if (Equals(customEditorGUI, m_CustomGUIField.value))
                    return;

                customEditorGUI = m_CustomGUIField.value;
                onChange();
            });
            context.AddProperty("Custom Editor GUI", m_CustomGUIField, (evt) => {});
        }

        public override void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            // Trunk currently relies on checking material property "_EmissionColor" to allow emissive GI. If it doesn't find that property, or it is black, GI is forced off.
            // ShaderGraph doesn't use this property, so currently it inserts a dummy color (white). This dummy color may be removed entirely once the following PR has been merged in trunk: Pull request #74105
            // The user will then need to explicitly disable emissive GI if it is not needed.
            // To be able to automatically disable emission based on the ShaderGraph config when emission is black,
            // we will need a more general way to communicate this to the engine (not directly tied to a material property).
            collector.AddShaderProperty(new ColorShaderProperty()
            {
                overrideReferenceName = "_EmissionColor",
                hidden = true,
                value = new Color(1.0f, 1.0f, 1.0f, 1.0f)
            });

            // ShaderGraph only property used to send the RenderQueueType to the material
            collector.AddShaderProperty(new Vector1ShaderProperty
            {
                overrideReferenceName = "_RenderQueueType",
                hidden = true,
                value = (int)renderingPass,
            });

            // See SG-ADDITIONALVELOCITY-NOTE
            if (addPrecomputedVelocity)
            {
                collector.AddShaderProperty(new BooleanShaderProperty
                {
                    value  = true,
                    hidden = true,
                    overrideReferenceName = kAddPrecomputedVelocity,
                });
            }

            // Core properties
            HDSubShaderUtilities.AddDoubleSidedProperty(collector, doubleSidedMode);

            // SubTarget properties
            m_ActiveSubTarget.CollectShaderProperties(collector, generationMode);
        }

        public override void ProcessPreviewMaterial(Material material)
        {
            // Fixup the material settings:
            material.SetFloat(kSurfaceType, (int)surfaceType);
            material.SetFloat(kDoubleSidedEnable, doubleSidedMode != DoubleSidedMode.Disabled ? 1.0f : 0.0f);
            material.SetFloat(kDoubleSidedNormalMode, (int)doubleSidedMode);
            material.SetFloat(kAlphaCutoffEnabled, alphaTest ? 1 : 0);
            material.SetFloat(kBlendMode, (int)HDSubShaderUtilities.ConvertAlphaModeToBlendMode(alphaMode));
            material.SetFloat(kEnableFogOnTransparent, transparencyFog ? 1.0f : 0.0f);
            material.SetFloat(kZTestTransparent, (int)zTest);
            material.SetFloat(kTransparentCullMode, (int)transparentCullMode);
            material.SetFloat(kZWrite, zWrite ? 1.0f : 0.0f);

            // No sorting priority for shader graph preview
            material.renderQueue = (int)HDRenderQueue.ChangeType(renderingPass, offset: 0, alphaTest: alphaTest);

            HDUnlitGUI.SetupMaterialKeywordsAndPass(material);
        }

        public bool ChangeRenderingPass(HDRenderQueue.RenderQueueType value)
        {
            switch (value)
            {
                case HDRenderQueue.RenderQueueType.Overlay:
                case HDRenderQueue.RenderQueueType.Unknown:
                case HDRenderQueue.RenderQueueType.Background:
                    throw new ArgumentException("Unexpected kind of RenderQueue, was " + value);
                default:
                    break;
            };
            return UpdateRenderingPassValue(value);
        }

        public bool UpdateRenderingPassValue(HDRenderQueue.RenderQueueType value)
        {
            switch (surfaceType)
            {
                case SurfaceType.Opaque:
                    value = HDRenderQueue.GetOpaqueEquivalent(value);
                    break;
                case SurfaceType.Transparent:
                    value = HDRenderQueue.GetTransparentEquivalent(value);
                    break;
                default:
                    throw new ArgumentException("Unknown SurfaceType");
            }

            if (Equals(renderingPass, value))
                return false;

            renderingPass = value;
            return true;
        }

        // TODO: Remove this
#region Serialization
        public void OnBeforeSerialize()
        {
            m_SerializedSubTarget = SerializationHelper.Serialize<SubTarget>(m_ActiveSubTarget);
        }

        public void OnAfterDeserialize()
        {
            // Deserialize the SubTarget
            m_ActiveSubTarget = SerializationHelper.Deserialize<SubTarget>(m_SerializedSubTarget, GraphUtil.GetLegacyTypeRemapping());
            m_ActiveSubTarget.target = this;
        }
#endregion
    }

#region BlockMasks
    static class CoreBlockMasks
    {
        public static BlockFieldDescriptor[] Vertex = new BlockFieldDescriptor[]
        {
            BlockFields.VertexDescription.Position,
            BlockFields.VertexDescription.Normal,
            BlockFields.VertexDescription.Tangent,
        };
    }
#endregion

#region StructCollections
    static class CoreStructCollections
    {
        public static StructCollection Default = new StructCollection
        {
            { HDStructs.AttributesMesh },
            { HDStructs.VaryingsMeshToPS },
            { Structs.SurfaceDescriptionInputs },
            { Structs.VertexDescriptionInputs },
        };
    }
#endregion

#region FieldDependencies
    static class CoreFieldDependencies
    {
        public static DependencyCollection Varying = new DependencyCollection
        {
            //Standard Varying Dependencies
            new FieldDependency(HDStructFields.VaryingsMeshToPS.positionRWS,                         HDStructFields.AttributesMesh.positionOS),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.normalWS,                            HDStructFields.AttributesMesh.normalOS),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.tangentWS,                           HDStructFields.AttributesMesh.tangentOS),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.texCoord0,                           HDStructFields.AttributesMesh.uv0),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.texCoord1,                           HDStructFields.AttributesMesh.uv1),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.texCoord2,                           HDStructFields.AttributesMesh.uv2),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.texCoord3,                           HDStructFields.AttributesMesh.uv3),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.color,                               HDStructFields.AttributesMesh.color),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.instanceID,                          HDStructFields.AttributesMesh.instanceID),
        };

        public static DependencyCollection Tessellation = new DependencyCollection
        {
            //Tessellation Varying Dependencies
            new FieldDependency(HDStructFields.VaryingsMeshToPS.positionRWS,                         HDStructFields.VaryingsMeshToDS.positionRWS),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.normalWS,                            HDStructFields.VaryingsMeshToDS.normalWS),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.tangentWS,                           HDStructFields.VaryingsMeshToDS.tangentWS),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.texCoord0,                           HDStructFields.VaryingsMeshToDS.texCoord0),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.texCoord1,                           HDStructFields.VaryingsMeshToDS.texCoord1),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.texCoord2,                           HDStructFields.VaryingsMeshToDS.texCoord2),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.texCoord3,                           HDStructFields.VaryingsMeshToDS.texCoord3),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.color,                               HDStructFields.VaryingsMeshToDS.color),
            new FieldDependency(HDStructFields.VaryingsMeshToPS.instanceID,                          HDStructFields.VaryingsMeshToDS.instanceID),

            //Tessellation Varying Dependencies, TODO: Why is this loop created?
            new FieldDependency(HDStructFields.VaryingsMeshToDS.tangentWS,                           HDStructFields.VaryingsMeshToPS.tangentWS),
            new FieldDependency(HDStructFields.VaryingsMeshToDS.texCoord0,                           HDStructFields.VaryingsMeshToPS.texCoord0),
            new FieldDependency(HDStructFields.VaryingsMeshToDS.texCoord1,                           HDStructFields.VaryingsMeshToPS.texCoord1),
            new FieldDependency(HDStructFields.VaryingsMeshToDS.texCoord2,                           HDStructFields.VaryingsMeshToPS.texCoord2),
            new FieldDependency(HDStructFields.VaryingsMeshToDS.texCoord3,                           HDStructFields.VaryingsMeshToPS.texCoord3),
            new FieldDependency(HDStructFields.VaryingsMeshToDS.color,                               HDStructFields.VaryingsMeshToPS.color),
            new FieldDependency(HDStructFields.VaryingsMeshToDS.instanceID,                          HDStructFields.VaryingsMeshToPS.instanceID),
        };

        public static DependencyCollection FragInput = new DependencyCollection
        {
            //FragInput dependencies
            new FieldDependency(HDStructFields.FragInputs.positionRWS,                               HDStructFields.VaryingsMeshToPS.positionRWS),
            new FieldDependency(HDStructFields.FragInputs.tangentToWorld,                            HDStructFields.VaryingsMeshToPS.tangentWS),
            new FieldDependency(HDStructFields.FragInputs.tangentToWorld,                            HDStructFields.VaryingsMeshToPS.normalWS),
            new FieldDependency(HDStructFields.FragInputs.texCoord0,                                 HDStructFields.VaryingsMeshToPS.texCoord0),
            new FieldDependency(HDStructFields.FragInputs.texCoord1,                                 HDStructFields.VaryingsMeshToPS.texCoord1),
            new FieldDependency(HDStructFields.FragInputs.texCoord2,                                 HDStructFields.VaryingsMeshToPS.texCoord2),
            new FieldDependency(HDStructFields.FragInputs.texCoord3,                                 HDStructFields.VaryingsMeshToPS.texCoord3),
            new FieldDependency(HDStructFields.FragInputs.color,                                     HDStructFields.VaryingsMeshToPS.color),
            new FieldDependency(HDStructFields.FragInputs.IsFrontFace,                               HDStructFields.VaryingsMeshToPS.cullFace),
        };

        public static DependencyCollection VertexDescription = new DependencyCollection
        {
            //Vertex Description Dependencies
            new FieldDependency(StructFields.VertexDescriptionInputs.ObjectSpaceNormal,              HDStructFields.AttributesMesh.normalOS),
            new FieldDependency(StructFields.VertexDescriptionInputs.WorldSpaceNormal,               HDStructFields.AttributesMesh.normalOS),
            new FieldDependency(StructFields.VertexDescriptionInputs.ViewSpaceNormal,                StructFields.VertexDescriptionInputs.WorldSpaceNormal),

            new FieldDependency(StructFields.VertexDescriptionInputs.ObjectSpaceTangent,             HDStructFields.AttributesMesh.tangentOS),
            new FieldDependency(StructFields.VertexDescriptionInputs.WorldSpaceTangent,              HDStructFields.AttributesMesh.tangentOS),
            new FieldDependency(StructFields.VertexDescriptionInputs.ViewSpaceTangent,               StructFields.VertexDescriptionInputs.WorldSpaceTangent),

            new FieldDependency(StructFields.VertexDescriptionInputs.ObjectSpaceBiTangent,           HDStructFields.AttributesMesh.normalOS),
            new FieldDependency(StructFields.VertexDescriptionInputs.ObjectSpaceBiTangent,           HDStructFields.AttributesMesh.tangentOS),
            new FieldDependency(StructFields.VertexDescriptionInputs.WorldSpaceBiTangent,            StructFields.VertexDescriptionInputs.ObjectSpaceBiTangent),
            new FieldDependency(StructFields.VertexDescriptionInputs.ViewSpaceBiTangent,             StructFields.VertexDescriptionInputs.WorldSpaceBiTangent),

            new FieldDependency(StructFields.VertexDescriptionInputs.ObjectSpacePosition,            HDStructFields.AttributesMesh.positionOS),
            new FieldDependency(StructFields.VertexDescriptionInputs.WorldSpacePosition,             HDStructFields.AttributesMesh.positionOS),
            new FieldDependency(StructFields.VertexDescriptionInputs.AbsoluteWorldSpacePosition,     HDStructFields.AttributesMesh.positionOS),
            new FieldDependency(StructFields.VertexDescriptionInputs.ViewSpacePosition,              StructFields.VertexDescriptionInputs.WorldSpacePosition),

            new FieldDependency(StructFields.VertexDescriptionInputs.WorldSpaceViewDirection,        StructFields.VertexDescriptionInputs.WorldSpacePosition),
            new FieldDependency(StructFields.VertexDescriptionInputs.ObjectSpaceViewDirection,       StructFields.VertexDescriptionInputs.WorldSpaceViewDirection),
            new FieldDependency(StructFields.VertexDescriptionInputs.ViewSpaceViewDirection,         StructFields.VertexDescriptionInputs.WorldSpaceViewDirection),
            new FieldDependency(StructFields.VertexDescriptionInputs.TangentSpaceViewDirection,      StructFields.VertexDescriptionInputs.WorldSpaceViewDirection),
            new FieldDependency(StructFields.VertexDescriptionInputs.TangentSpaceViewDirection,      StructFields.VertexDescriptionInputs.WorldSpaceTangent),
            new FieldDependency(StructFields.VertexDescriptionInputs.TangentSpaceViewDirection,      StructFields.VertexDescriptionInputs.WorldSpaceBiTangent),
            new FieldDependency(StructFields.VertexDescriptionInputs.TangentSpaceViewDirection,      StructFields.VertexDescriptionInputs.WorldSpaceNormal),

            new FieldDependency(StructFields.VertexDescriptionInputs.ScreenPosition,                 StructFields.VertexDescriptionInputs.WorldSpacePosition),
            new FieldDependency(StructFields.VertexDescriptionInputs.uv0,                            HDStructFields.AttributesMesh.uv0),
            new FieldDependency(StructFields.VertexDescriptionInputs.uv1,                            HDStructFields.AttributesMesh.uv1),
            new FieldDependency(StructFields.VertexDescriptionInputs.uv2,                            HDStructFields.AttributesMesh.uv2),
            new FieldDependency(StructFields.VertexDescriptionInputs.uv3,                            HDStructFields.AttributesMesh.uv3),
            new FieldDependency(StructFields.VertexDescriptionInputs.VertexColor,                    HDStructFields.AttributesMesh.color),

            new FieldDependency(StructFields.VertexDescriptionInputs.BoneWeights,                   HDStructFields.AttributesMesh.weights),
            new FieldDependency(StructFields.VertexDescriptionInputs.BoneIndices,                   HDStructFields.AttributesMesh.indices),
        };

        public static DependencyCollection SurfaceDescription = new DependencyCollection
        {
            //Surface Description Dependencies
            new FieldDependency(StructFields.SurfaceDescriptionInputs.WorldSpaceNormal,              HDStructFields.FragInputs.tangentToWorld),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.ObjectSpaceNormal,             StructFields.SurfaceDescriptionInputs.WorldSpaceNormal),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.ViewSpaceNormal,               StructFields.SurfaceDescriptionInputs.WorldSpaceNormal),

            new FieldDependency(StructFields.SurfaceDescriptionInputs.WorldSpaceTangent,             HDStructFields.FragInputs.tangentToWorld),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.ObjectSpaceTangent,            StructFields.SurfaceDescriptionInputs.WorldSpaceTangent),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.ViewSpaceTangent,              StructFields.SurfaceDescriptionInputs.WorldSpaceTangent),

            new FieldDependency(StructFields.SurfaceDescriptionInputs.WorldSpaceBiTangent,           HDStructFields.FragInputs.tangentToWorld),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.ObjectSpaceBiTangent,          StructFields.SurfaceDescriptionInputs.WorldSpaceBiTangent),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.ViewSpaceBiTangent,            StructFields.SurfaceDescriptionInputs.WorldSpaceBiTangent),

            new FieldDependency(StructFields.SurfaceDescriptionInputs.WorldSpacePosition,            HDStructFields.FragInputs.positionRWS),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.AbsoluteWorldSpacePosition,    HDStructFields.FragInputs.positionRWS),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.ObjectSpacePosition,           HDStructFields.FragInputs.positionRWS),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.ViewSpacePosition,             HDStructFields.FragInputs.positionRWS),

            new FieldDependency(StructFields.SurfaceDescriptionInputs.WorldSpaceViewDirection,       HDStructFields.FragInputs.positionRWS),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.ObjectSpaceViewDirection,      StructFields.SurfaceDescriptionInputs.WorldSpaceViewDirection),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.ViewSpaceViewDirection,        StructFields.SurfaceDescriptionInputs.WorldSpaceViewDirection),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.TangentSpaceViewDirection,     StructFields.SurfaceDescriptionInputs.WorldSpaceViewDirection),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.TangentSpaceViewDirection,     StructFields.SurfaceDescriptionInputs.WorldSpaceTangent),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.TangentSpaceViewDirection,     StructFields.SurfaceDescriptionInputs.WorldSpaceBiTangent),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.TangentSpaceViewDirection,     StructFields.SurfaceDescriptionInputs.WorldSpaceNormal),

            new FieldDependency(StructFields.SurfaceDescriptionInputs.ScreenPosition,                StructFields.SurfaceDescriptionInputs.WorldSpacePosition),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.uv0,                           HDStructFields.FragInputs.texCoord0),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.uv1,                           HDStructFields.FragInputs.texCoord1),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.uv2,                           HDStructFields.FragInputs.texCoord2),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.uv3,                           HDStructFields.FragInputs.texCoord3),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.VertexColor,                   HDStructFields.FragInputs.color),
            new FieldDependency(StructFields.SurfaceDescriptionInputs.FaceSign,                      HDStructFields.FragInputs.IsFrontFace),

            new FieldDependency(HDFields.DepthOffset,                                                HDStructFields.FragInputs.positionRWS),
        };

        public static DependencyCollection Default = new DependencyCollection
        {
            { Varying },
            { Tessellation },
            { FragInput },
            { VertexDescription },
            { SurfaceDescription },
        };
    }
#endregion

#region RequiredFields
    static class CoreRequiredFields
    {
        public static FieldCollection Meta = new FieldCollection()
        {
            HDStructFields.AttributesMesh.normalOS,
            HDStructFields.AttributesMesh.tangentOS,
            HDStructFields.AttributesMesh.uv0,
            HDStructFields.AttributesMesh.uv1,
            HDStructFields.AttributesMesh.color,
            HDStructFields.AttributesMesh.uv2,
        };

        public static FieldCollection PositionRWS = new FieldCollection()
        {
            HDStructFields.VaryingsMeshToPS.positionRWS,
        };

        public static FieldCollection LitMinimal = new FieldCollection()
        {
            HDStructFields.FragInputs.tangentToWorld,
            HDStructFields.FragInputs.positionRWS,
            HDStructFields.FragInputs.texCoord1,
            HDStructFields.FragInputs.texCoord2,
        };

        public static FieldCollection LitFull = new FieldCollection()
        {
            HDStructFields.AttributesMesh.normalOS,
            HDStructFields.AttributesMesh.tangentOS,
            HDStructFields.AttributesMesh.uv0,
            HDStructFields.AttributesMesh.uv1,
            HDStructFields.AttributesMesh.color,
            HDStructFields.AttributesMesh.uv2,
            HDStructFields.AttributesMesh.uv3,
            HDStructFields.FragInputs.tangentToWorld,
            HDStructFields.FragInputs.positionRWS,
            HDStructFields.FragInputs.texCoord1,
            HDStructFields.FragInputs.texCoord2,
            HDStructFields.FragInputs.texCoord3,
            HDStructFields.FragInputs.color,
        };
    }
#endregion

#region RenderStates
    static class CoreRenderStates
    {
        public static class Uniforms
        {
            public static readonly string srcBlend = "[_SrcBlend]";
            public static readonly string dstBlend = "[_DstBlend]";
            public static readonly string alphaSrcBlend = "[_AlphaSrcBlend]";
            public static readonly string alphaDstBlend = "[_AlphaDstBlend]";
            public static readonly string cullMode = "[_CullMode]";
            public static readonly string cullModeForward = "[_CullModeForward]";
            public static readonly string zTestDepthEqualForOpaque = "[_ZTestDepthEqualForOpaque]";
            public static readonly string zTestTransparent = "[_ZTestTransparent]";
            public static readonly string zTestGBuffer = "[_ZTestGBuffer]";
            public static readonly string zWrite = "[_ZWrite]";
            public static readonly string zClip = "[_ZClip]";
            public static readonly string stencilWriteMaskDepth = "[_StencilWriteMaskDepth]";
            public static readonly string stencilRefDepth = "[_StencilRefDepth]";
            public static readonly string stencilWriteMaskMV = "[_StencilWriteMaskMV]";
            public static readonly string stencilRefMV = "[_StencilRefMV]";
            public static readonly string stencilWriteMask = "[_StencilWriteMask]";
            public static readonly string stencilRef = "[_StencilRef]";
            public static readonly string stencilWriteMaskGBuffer = "[_StencilWriteMaskGBuffer]";
            public static readonly string stencilRefGBuffer = "[_StencilRefGBuffer]";
            public static readonly string stencilRefDistortionVec = "[_StencilRefDistortionVec]";
            public static readonly string stencilWriteMaskDistortionVec = "[_StencilWriteMaskDistortionVec]";
        }

        public static RenderStateCollection Meta = new RenderStateCollection
        {
            { RenderState.Cull(Cull.Off) },
        };

        public static RenderStateCollection ShadowCaster = new RenderStateCollection
        {
            { RenderState.Cull(Uniforms.cullMode) },
            { RenderState.ZWrite(ZWrite.On) },
            { RenderState.ZClip(Uniforms.zClip) },
            { RenderState.ColorMask("ColorMask 0") },
        };

        public static RenderStateCollection BlendShadowCaster = new RenderStateCollection
        {
            { RenderState.Blend(Blend.One, Blend.Zero) },
            { RenderState.Cull(Uniforms.cullMode) },
            { RenderState.ZWrite(ZWrite.On) },
            { RenderState.ZClip(Uniforms.zClip) },
            { RenderState.ColorMask("ColorMask 0") },
        };

        public static RenderStateCollection SceneSelection = new RenderStateCollection
        {
            { RenderState.ColorMask("ColorMask 0") },
        };

        public static RenderStateCollection DepthOnly = new RenderStateCollection
        {
            { RenderState.Cull(Uniforms.cullMode) },
            { RenderState.ZWrite(ZWrite.On) },
            { RenderState.Stencil(new StencilDescriptor()
            {
                WriteMask = Uniforms.stencilWriteMaskDepth,
                Ref = Uniforms.stencilRefDepth,
                Comp = "Always",
                Pass = "Replace",
            }) },
        };

        public static RenderStateCollection MotionVectors = new RenderStateCollection
        {
            { RenderState.Cull(Uniforms.cullMode) },
            { RenderState.ZWrite(ZWrite.On) },
            { RenderState.Stencil(new StencilDescriptor()
            {
                WriteMask = Uniforms.stencilWriteMaskMV,
                Ref = Uniforms.stencilRefMV,
                Comp = "Always",
                Pass = "Replace",
            }) },
        };

        public static RenderStateCollection TransparentBackface = new RenderStateCollection
        {
            { RenderState.Blend(Uniforms.srcBlend, Uniforms.dstBlend, Uniforms.alphaSrcBlend, Uniforms.alphaDstBlend) },
            { RenderState.Cull(Cull.Front) },
            { RenderState.ZWrite(Uniforms.zWrite) },
            { RenderState.ZTest(Uniforms.zTestTransparent) },
            { RenderState.ColorMask("ColorMask [_ColorMaskTransparentVel] 1") },
        };

        public static RenderStateCollection TransparentDepthPrePostPass = new RenderStateCollection
        {
            { RenderState.Blend(Blend.One, Blend.Zero) },
            { RenderState.Cull(Uniforms.cullMode) },
            { RenderState.ZWrite(ZWrite.On) },
            { RenderState.ColorMask("ColorMask 0") },
        };

        public static RenderStateCollection Forward = new RenderStateCollection
        {
            { RenderState.Blend(Uniforms.srcBlend, Uniforms.dstBlend, Uniforms.alphaSrcBlend, Uniforms.alphaDstBlend) },
            { RenderState.Cull(Uniforms.cullModeForward) },
            { RenderState.ZWrite(Uniforms.zWrite) },
            { RenderState.ZTest(Uniforms.zTestDepthEqualForOpaque), new FieldCondition[] {
                new FieldCondition(Fields.SurfaceOpaque, true),
                new FieldCondition(Fields.AlphaTest, false)
            } },
            { RenderState.ZTest(Uniforms.zTestDepthEqualForOpaque), new FieldCondition[] {
                new FieldCondition(Fields.SurfaceOpaque, false),
            } },
            { RenderState.ZTest(ZTest.Equal), new FieldCondition[] {
                new FieldCondition(Fields.SurfaceOpaque, true),
                new FieldCondition(Fields.AlphaTest, true)
            } },
            { RenderState.Stencil(new StencilDescriptor()
            {
                WriteMask = Uniforms.stencilWriteMask,
                Ref = Uniforms.stencilRef,
                Comp = "Always",
                Pass = "Replace",
            }) },
        };

        public static RenderStateCollection ForwardColorMask = new RenderStateCollection
        {
            { RenderState.Blend(Uniforms.srcBlend, Uniforms.dstBlend, Uniforms.alphaSrcBlend, Uniforms.alphaDstBlend) },
            { RenderState.Cull(Uniforms.cullModeForward) },
            { RenderState.ZWrite(Uniforms.zWrite) },
            { RenderState.ZTest(Uniforms.zTestDepthEqualForOpaque), new FieldCondition[] {
                new FieldCondition(Fields.SurfaceOpaque, true),
                new FieldCondition(Fields.AlphaTest, false)
            } },
            { RenderState.ZTest(Uniforms.zTestDepthEqualForOpaque), new FieldCondition[] {
                new FieldCondition(Fields.SurfaceOpaque, false),
            } },
            { RenderState.ZTest(ZTest.Equal), new FieldCondition[] {
                new FieldCondition(Fields.SurfaceOpaque, true),
                new FieldCondition(Fields.AlphaTest, true)
            } },
            { RenderState.ColorMask("ColorMask [_ColorMaskTransparentVel] 1") },
            { RenderState.Stencil(new StencilDescriptor()
            {
                WriteMask = Uniforms.stencilWriteMask,
                Ref = Uniforms.stencilRef,
                Comp = "Always",
                Pass = "Replace",
            }) },
        };
    }
#endregion

#region Pragmas
    static class CorePragmas
    {
        public static PragmaCollection Basic = new PragmaCollection
        {
            { Pragma.Target(ShaderModel.Target45) },
            { Pragma.Vertex("Vert") },
            { Pragma.Fragment("Frag") },
            { Pragma.OnlyRenderers(new Platform[] {Platform.D3D11, Platform.Playstation, Platform.XboxOne, Platform.Vulkan, Platform.Metal, Platform.Switch}) },
        };

        public static PragmaCollection InstancedRenderingLayer = new PragmaCollection
        {
            { Basic },
            { Pragma.MultiCompileInstancing },
            { Pragma.InstancingOptions(InstancingOptions.RenderingLayer) },
        };

        public static PragmaCollection InstancedRenderingLayerEditorSync = new PragmaCollection
        {
            { Basic },
            { Pragma.MultiCompileInstancing },
            { Pragma.InstancingOptions(InstancingOptions.RenderingLayer) },
            { Pragma.EditorSyncCompilation },
        };

        public static PragmaCollection DotsInstancedInV2Only = new PragmaCollection
        {
            { Basic },
            { Pragma.MultiCompileInstancing },
            #if ENABLE_HYBRID_RENDERER_V2
            { Pragma.DOTSInstancing },
            { Pragma.InstancingOptions(InstancingOptions.NoLodFade) },
            #endif
        };

        public static PragmaCollection DotsInstancedInV2OnlyEditorSync = new PragmaCollection
        {
            { Basic },
            { Pragma.MultiCompileInstancing },
            { Pragma.EditorSyncCompilation },
            #if ENABLE_HYBRID_RENDERER_V2
            { Pragma.DOTSInstancing },
            { Pragma.InstancingOptions(InstancingOptions.NoLodFade) },
            #endif
        };

        public static PragmaCollection DotsInstancedInV1AndV2 = new PragmaCollection
        {
            { Basic },
            { Pragma.MultiCompileInstancing },
            // Hybrid Renderer V2 requires a completely different set of pragmas from Hybrid V1
            #if ENABLE_HYBRID_RENDERER_V2
            { Pragma.DOTSInstancing },
            { Pragma.InstancingOptions(InstancingOptions.NoLodFade) },
            { Pragma.InstancingOptions(InstancingOptions.RenderingLayer) },
            #else
            { Pragma.InstancingOptions(InstancingOptions.NoLightProbe), new FieldCondition(HDFields.DotsInstancing, true) },
            { Pragma.InstancingOptions(InstancingOptions.NoLightProbe), new FieldCondition(HDFields.DotsProperties, true) },
            { Pragma.InstancingOptions(InstancingOptions.NoLodFade),    new FieldCondition(HDFields.DotsInstancing, true) },
            { Pragma.InstancingOptions(InstancingOptions.NoLodFade),    new FieldCondition(HDFields.DotsProperties, true) },
            { Pragma.InstancingOptions(InstancingOptions.RenderingLayer), new FieldCondition[]
            {
                new FieldCondition(HDFields.DotsInstancing, false),
                new FieldCondition(HDFields.DotsProperties, false),
            } },
            #endif
        };

        public static PragmaCollection DotsInstancedInV1AndV2EditorSync = new PragmaCollection
        {
            { Basic },
            { Pragma.MultiCompileInstancing },
            { Pragma.EditorSyncCompilation },
            // Hybrid Renderer V2 requires a completely different set of pragmas from Hybrid V1
            #if ENABLE_HYBRID_RENDERER_V2
            { Pragma.DOTSInstancing },
            { Pragma.InstancingOptions(InstancingOptions.NoLodFade) },
            { Pragma.InstancingOptions(InstancingOptions.RenderingLayer) },
            #else
            { Pragma.InstancingOptions(InstancingOptions.NoLightProbe), new FieldCondition(HDFields.DotsInstancing, true) },
            { Pragma.InstancingOptions(InstancingOptions.NoLightProbe), new FieldCondition(HDFields.DotsProperties, true) },
            { Pragma.InstancingOptions(InstancingOptions.NoLodFade),    new FieldCondition(HDFields.DotsInstancing, true) },
            { Pragma.InstancingOptions(InstancingOptions.NoLodFade),    new FieldCondition(HDFields.DotsProperties, true) },
            { Pragma.InstancingOptions(InstancingOptions.RenderingLayer), new FieldCondition[]
            {
                new FieldCondition(HDFields.DotsInstancing, false),
                new FieldCondition(HDFields.DotsProperties, false),
            } },
            #endif
        };

        public static PragmaCollection RaytracingBasic = new PragmaCollection
        {
            { Pragma.Target(ShaderModel.Target50) },
            { Pragma.Raytracing("test") },
            { Pragma.OnlyRenderers(new Platform[] {Platform.D3D11}) },
        };
    }
#endregion

#region Keywords
    static class CoreKeywords
    {
        public static KeywordCollection HDBase = new KeywordCollection
        {
            { CoreKeywordDescriptors.LodFadeCrossfade, new FieldCondition(Fields.LodCrossFade, true) },
            { CoreKeywordDescriptors.SurfaceTypeTransparent },
            { CoreKeywordDescriptors.BlendMode },
            { CoreKeywordDescriptors.DoubleSided, new FieldCondition(HDFields.SubShader.Unlit, false) },
            { CoreKeywordDescriptors.FogOnTransparent },
            { CoreKeywordDescriptors.AlphaTest, new FieldCondition(Fields.AlphaTest, true) },
        };

        public static KeywordCollection Lightmaps = new KeywordCollection
        {
            { CoreKeywordDescriptors.Lightmap },
            { CoreKeywordDescriptors.DirectionalLightmapCombined },
            { CoreKeywordDescriptors.DynamicLightmap },
        };

        public static KeywordCollection WriteMsaaDepth = new KeywordCollection
        {
            { CoreKeywordDescriptors.WriteMsaaDepth },
        };

        public static KeywordCollection DebugDisplay = new KeywordCollection
        {
            { CoreKeywordDescriptors.DebugDisplay },
        };

        public static KeywordCollection DepthMotionVectorsNoNormal = new KeywordCollection
        {
            { HDBase },
            { CoreKeywordDescriptors.WriteMsaaDepth },
        };

        public static KeywordCollection Forward = new KeywordCollection
        {
            { HDBase },
            { CoreKeywordDescriptors.DebugDisplay },
            { Lightmaps },
            { CoreKeywordDescriptors.ShadowsShadowmask },
            { CoreKeywordDescriptors.Shadow },
            { CoreKeywordDescriptors.Decals },
            { CoreKeywordDescriptors.LightList, new FieldCondition(Fields.SurfaceOpaque, true) },
        };

        public static KeywordCollection RaytracingIndirect = new KeywordCollection
        {
            { HDBase },
            { CoreKeywordDescriptors.DiffuseLightingOnly },
            { Lightmaps },
        };

        public static KeywordCollection RaytracingGBufferForward = new KeywordCollection
        {
            { HDBase },
            { Lightmaps },
        };
    }
#endregion

#region Defines
    static class CoreDefines
    {
        public static DefineCollection SceneSelection = new DefineCollection
        {
            { CoreKeywordDescriptors.SceneSelectionPass, 1 },
        };

        public static DefineCollection DepthMotionVectors = new DefineCollection
        {
            { RayTracingNode.GetRayTracingKeyword(), 0 },
            { CoreKeywordDescriptors.WriteNormalBuffer, 1 },
        };

        public static DefineCollection ShaderGraphRaytracingHigh = new DefineCollection
        {
            { RayTracingNode.GetRayTracingKeyword(), 0 },
        };

        public static DefineCollection TransparentDepthPrepass = new DefineCollection
        {
            { RayTracingNode.GetRayTracingKeyword(), 0 },
            { CoreKeywordDescriptors.WriteNormalBufferDefine, 1, new FieldCondition(HDFields.DisableSSRTransparent, false) },
        };

        public static DefineCollection Forward = new DefineCollection
        {
            { CoreKeywordDescriptors.HasLightloop, 1 },
            { CoreKeywordDescriptors.LightList, 1, new FieldCondition(Fields.SurfaceTransparent, true) },
            { RayTracingNode.GetRayTracingKeyword(), 0, new FieldCondition(Fields.SurfaceTransparent, true) },
        };
    }
#endregion

#region Includes
    static class CoreIncludes
    {
        // CorePregraph
        const string kCommon = "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl";
        const string kShaderVariables = "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl";
        const string kFragInputs = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl";
        const string kShaderPass = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl";
        const string kMaterial = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl";
        const string kDebugDisplay = "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplay.hlsl";

        // CoreUtility
        const string kBuiltInUtilities = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl";
        const string kMaterialUtilities = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl";

        // Pregraph Raytracing
        const string kRaytracingMacros = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/Raytracing/Shaders/RaytracingMacros.hlsl";
        const string kShaderVariablesRaytracing = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/Raytracing/Shaders/ShaderVariablesRaytracing.hlsl";
        const string kShaderVariablesRaytracingLightLoop = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/Raytracing/Shaders/ShaderVariablesRaytracingLightLoop.hlsl";
        const string kRaytracingIntersection = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/Raytracing/Shaders/RaytracingIntersection.hlsl";
        const string kRaytracingIntersectionGBuffer = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/Raytracing/Shaders/Deferred/RaytracingIntersectonGBuffer.hlsl";
        const string kRaytracingIntersectionSubSurface = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/Raytracing/Shaders/SubSurface/RayTracingIntersectionSubSurface.hlsl";
        const string kLitRaytracing = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitRaytracing.hlsl";
        const string kUnlitRaytracing = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/UnlitRaytracing.hlsl";
        const string kFabricRaytracing = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Fabric/FabricRaytracing.hlsl";
        const string kStackLitRaytracing = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/StackLit/StackLitRaytracing.hlsl";
        const string kHairRaytracing = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Hair/HairRaytracing.hlsl";
        const string kRaytracingLightLoop = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/Raytracing/Shaders/RaytracingLightLoop.hlsl";
        const string kRaytracingCommon = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/Raytracing/Shaders/RaytracingCommon.hlsl";
        const string kNormalBuffer = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl";

        // Postgraph Raytracing
        const string kPassRaytracingIndirect = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassRaytracingIndirect.hlsl";
        const string kPassRaytracingVisbility = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassRaytracingVisibility.hlsl";
        const string kPassRaytracingForward = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassRaytracingForward.hlsl";
        const string kPassRaytracingGBuffer = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderpassRaytracingGBuffer.hlsl";
        const string kPassPathTracing = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassPathTracing.hlsl";
        const string kPassRaytracingSubSurface = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderpassRaytracingSubSurface.hlsl";

        // Public Pregraph Function
        public const string kCommonLighting = "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl";
        public const string kShadowContext = "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Shadow/HDShadowContext.hlsl";
        public const string kHDShadow = "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/HDShadow.hlsl";
        public const string kLightLoopDef = "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoopDef.hlsl";
        public const string kPunctualLightCommon = "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/PunctualLightCommon.hlsl";
        public const string kHDShadowLoop = "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/HDShadowLoop.hlsl";
        public const string kNormalSurfaceGradient = "Packages/com.unity.render-pipelines.core/ShaderLibrary/NormalSurfaceGradient.hlsl";
        public const string kLighting = "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Lighting.hlsl";
        public const string kLightLoop = "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoop.hlsl";
        
        // Public Pregraph Material
        public const string kUnlit = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl";
        public const string kLit = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl";
        public const string kFabric = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Fabric/Fabric.hlsl";
        public const string kHair = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Hair/Hair.hlsl";
        public const string kStackLit = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/StackLit/StackLit.hlsl";

        // Public Pregraph Misc
        public const string kShaderGraphFunctions = "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderGraphFunctions.hlsl";
        public const string kDecalUtilities = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Decal/DecalUtilities.hlsl";

        // Public Postgraph Pass
        public const string kPassLightTransport = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassLightTransport.hlsl";
        public const string kPassDepthOnly = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl";
        public const string kPassMotionVectors = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassMotionVectors.hlsl";
        public const string kDisortionVectors = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDistortion.hlsl";
        public const string kPassForward = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForward.hlsl";
        public const string kStandardLit = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/StandardLit/StandardLit.hlsl";

        public static IncludeCollection CorePregraph = new IncludeCollection
        {
            { kCommon, IncludeLocation.Pregraph },
            { kShaderVariables, IncludeLocation.Pregraph },
            { kFragInputs, IncludeLocation.Pregraph },
            { kShaderPass, IncludeLocation.Pregraph },
            { kDebugDisplay, IncludeLocation.Pregraph },            
            { kMaterial, IncludeLocation.Pregraph },
        };

        public static IncludeCollection CoreUtility = new IncludeCollection
        {
            { kBuiltInUtilities, IncludeLocation.Pregraph },
            { kMaterialUtilities, IncludeLocation.Pregraph },
        };

        public static IncludeCollection Raytracing = new IncludeCollection
        {
            // Pregraph includes
            { kCommon, IncludeLocation.Pregraph },
            { kFragInputs, IncludeLocation.Pregraph },
            { kShaderPass, IncludeLocation.Pregraph },

            // Ray Tracing macros should be included before shader variables to guarantee that the macros are overriden
            { kRaytracingMacros, IncludeLocation.Pregraph },
            { kShaderVariables, IncludeLocation.Pregraph },
            { kMaterial, IncludeLocation.Pregraph },
            { kShaderVariablesRaytracing, IncludeLocation.Pregraph },
            { kShaderVariablesRaytracingLightLoop, IncludeLocation.Pregraph },

            // We want the gbuffer payload only if we are in the gbuffer pass 
            { kRaytracingIntersectionGBuffer, IncludeLocation.Pregraph, new FieldCondition(HDFields.ShaderPass.RayTracingGBuffer, true)},

            // We want the sub-surface payload if we are in the subsurface sub shader and this not an unlit
            { kRaytracingIntersectionSubSurface, IncludeLocation.Pregraph, new FieldCondition[]{
                new FieldCondition(HDFields.ShaderPass.RaytracingSubSurface, true),
                new FieldCondition(HDFields.SubShader.Unlit, false) }},

            // We want the generic payload if this is not a gbuffer or a subsurface subshader
            { kRaytracingIntersection, IncludeLocation.Pregraph, new FieldCondition[]{
                new FieldCondition(HDFields.ShaderPass.RayTracingGBuffer, false),
                new FieldCondition(HDFields.ShaderPass.RaytracingSubSurface, false) }},

            // We want to have the lighting include if this is an indirect sub-shader, a forward one or the path tracing (and this is not an unlit)
            { kLighting, IncludeLocation.Pregraph, new FieldCondition[]{
                new FieldCondition(HDFields.ShaderPass.RayTracingGBuffer, false),
                new FieldCondition(HDFields.ShaderPass.RaytracingVisibility, false),
                new FieldCondition(HDFields.ShaderPass.RaytracingSubSurface, false),
                new FieldCondition(HDFields.SubShader.Unlit, false) }},
            { kLightLoopDef, IncludeLocation.Pregraph, new FieldCondition[]{
                new FieldCondition(HDFields.ShaderPass.RayTracingGBuffer, false),
                new FieldCondition(HDFields.ShaderPass.RaytracingVisibility, false),
                new FieldCondition(HDFields.ShaderPass.RaytracingSubSurface, false),
                new FieldCondition(HDFields.SubShader.Unlit, false) }},

            // Each material has a specific hlsl file that should be included pre-graph and holds the lighting model
            { kLit, IncludeLocation.Pregraph, new FieldCondition(HDFields.SubShader.Lit, true)},
            { kFabric, IncludeLocation.Pregraph, new FieldCondition(HDFields.SubShader.Fabric, true)},
            { kUnlit, IncludeLocation.Pregraph, new FieldCondition(HDFields.SubShader.Unlit, true )},
            { kStackLit, IncludeLocation.Pregraph, new FieldCondition(HDFields.SubShader.StackLit, true )},
            { kHair, IncludeLocation.Pregraph, new FieldCondition(HDFields.SubShader.Hair, true )},

            // We want to have the normal buffer include if this is a gbuffer and unlit shader
            { kNormalBuffer, IncludeLocation.Pregraph, new FieldCondition[]{
                new FieldCondition(HDFields.ShaderPass.RayTracingGBuffer, true),
                new FieldCondition(HDFields.SubShader.Unlit, true) }},
                
            // If this is the gbuffer sub-shader, we want the standard lit data
            { kStandardLit, IncludeLocation.Pregraph,
                new FieldCondition(HDFields.ShaderPass.RayTracingGBuffer, true)},

            // We need to then include the ray tracing missing bits for the lighting models (based on which lighting model)
            { kLitRaytracing, IncludeLocation.Pregraph, new FieldCondition(HDFields.SubShader.Lit, true)},
            { kFabricRaytracing, IncludeLocation.Pregraph, new FieldCondition(HDFields.SubShader.Fabric, true)},
            { kStackLitRaytracing, IncludeLocation.Pregraph, new FieldCondition(HDFields.SubShader.StackLit, true)},
            { kUnlitRaytracing, IncludeLocation.Pregraph, new FieldCondition(HDFields.SubShader.Unlit, true )},
            { kHairRaytracing, IncludeLocation.Pregraph, new FieldCondition(HDFields.SubShader.Hair, true )},

            // We want to have the ray tracing light loop if this is an indirect sub-shader or a forward one and it is not the unlit shader
            { kRaytracingLightLoop, IncludeLocation.Pregraph, new FieldCondition[]{
                new FieldCondition(HDFields.ShaderPass.RayTracingGBuffer, false),
                new FieldCondition(HDFields.ShaderPass.RaytracingVisibility, false),
                new FieldCondition(HDFields.ShaderPass.RaytracingSubSurface, false),
                new FieldCondition(HDFields.ShaderPass.RaytracingPathTracing, false),
                new FieldCondition(HDFields.SubShader.Unlit, false) }},

            { CoreUtility },
            { kRaytracingCommon, IncludeLocation.Pregraph },
            { kShaderGraphFunctions, IncludeLocation.Pregraph },

            // post graph includes
            // The shader passes should always be post graph and are a 1 to 1 mapping to the shader pass name
            { kPassRaytracingIndirect, IncludeLocation.Postgraph, new FieldCondition(HDFields.ShaderPass.RaytracingIndirect, true) },
            { kPassRaytracingVisbility, IncludeLocation.Postgraph, new FieldCondition(HDFields.ShaderPass.RaytracingVisibility, true) },
            { kPassRaytracingForward, IncludeLocation.Postgraph, new FieldCondition(HDFields.ShaderPass.RaytracingForward, true) },
            { kPassRaytracingGBuffer, IncludeLocation.Postgraph, new FieldCondition(HDFields.ShaderPass.RayTracingGBuffer, true) },
            { kPassPathTracing, IncludeLocation.Postgraph, new FieldCondition(HDFields.ShaderPass.RaytracingPathTracing, true) },
            { kPassRaytracingSubSurface, IncludeLocation.Postgraph,  new FieldCondition(HDFields.ShaderPass.RaytracingSubSurface, true) }
        };
    }
#endregion

#region KeywordDescriptors
    static class CoreKeywordDescriptors
    {
        public static KeywordDescriptor WriteNormalBufferDefine = new KeywordDescriptor()
        {
            displayName = "Write Normal Buffer",
            referenceName = "WRITE_NORMAL_BUFFER",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.Predefined,
            scope = KeywordScope.Global,
        };
        
        public static KeywordDescriptor WriteNormalBuffer = new KeywordDescriptor()
        {
            displayName = "Write Normal Buffer",
            referenceName = "WRITE_NORMAL_BUFFER",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static KeywordDescriptor WriteMsaaDepth = new KeywordDescriptor()
        {
            displayName = "Write MSAA Depth",
            referenceName = "WRITE_MSAA_DEPTH",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static KeywordDescriptor DebugDisplay = new KeywordDescriptor()
        {
            displayName = "Debug Display",
            referenceName = "DEBUG_DISPLAY",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static KeywordDescriptor Lightmap = new KeywordDescriptor()
        {
            displayName = "Lightmap",
            referenceName = "LIGHTMAP_ON",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static KeywordDescriptor DirectionalLightmapCombined = new KeywordDescriptor()
        {
            displayName = "Directional Lightmap Combined",
            referenceName = "DIRLIGHTMAP_COMBINED",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static KeywordDescriptor DynamicLightmap = new KeywordDescriptor()
        {
            displayName = "Dynamic Lightmap",
            referenceName = "DYNAMICLIGHTMAP_ON",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static KeywordDescriptor ShadowsShadowmask = new KeywordDescriptor()
        {
            displayName = "Shadows Shadowmask",
            referenceName = "SHADOWS_SHADOWMASK",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static KeywordDescriptor DiffuseLightingOnly = new KeywordDescriptor()
        {
            displayName = "Diffuse Lighting Only",
            referenceName = "DIFFUSE_LIGHTING_ONLY",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static KeywordDescriptor LightLayers = new KeywordDescriptor()
        {
            displayName = "Light Layers",
            referenceName = "LIGHT_LAYERS",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static KeywordDescriptor Decals = new KeywordDescriptor()
        {
            displayName = "Decals",
            referenceName = "DECALS",
            type = KeywordType.Enum,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
            entries = new KeywordEntry[]
            {
                new KeywordEntry() { displayName = "Off", referenceName = "OFF" },
                new KeywordEntry() { displayName = "3RT", referenceName = "3RT" },
                new KeywordEntry() { displayName = "4RT", referenceName = "4RT" },
            }
        };

        public static KeywordDescriptor LodFadeCrossfade = new KeywordDescriptor()
        {
            displayName = "LOD Fade Crossfade",
            referenceName = "LOD_FADE_CROSSFADE",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static KeywordDescriptor HasLightloop = new KeywordDescriptor()
        {
            displayName = "Has Lightloop",
            referenceName = "HAS_LIGHTLOOP",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static KeywordDescriptor LightList = new KeywordDescriptor()
        {
            displayName = "Light List",
            referenceName = "USE",
            type = KeywordType.Enum,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
            entries = new KeywordEntry[]
            {
                new KeywordEntry() { displayName = "FPTL", referenceName = "FPTL_LIGHTLIST" },
                new KeywordEntry() { displayName = "Clustered", referenceName = "CLUSTERED_LIGHTLIST" },
            }
        };

        public static KeywordDescriptor Shadow = new KeywordDescriptor()
        {
            displayName = "Shadow",
            referenceName = "SHADOW",
            type = KeywordType.Enum,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
            entries = new KeywordEntry[]
            {
                new KeywordEntry() { displayName = "Low", referenceName = "LOW" },
                new KeywordEntry() { displayName = "Medium", referenceName = "MEDIUM" },
                new KeywordEntry() { displayName = "High", referenceName = "HIGH" },
            }
        };

        public static KeywordDescriptor SurfaceTypeTransparent = new KeywordDescriptor()
        {
            displayName = "Surface Type Transparent",
            referenceName = "_SURFACE_TYPE_TRANSPARENT",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.ShaderFeature,
            scope = KeywordScope.Global,
        };

        public static KeywordDescriptor DoubleSided = new KeywordDescriptor()
        {
            displayName = "Double Sided",
            referenceName = "_DOUBLESIDED_ON",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.ShaderFeature,
            scope = KeywordScope.Local,
        };

        public static KeywordDescriptor BlendMode = new KeywordDescriptor()
        {
            displayName = "Blend Mode",
            referenceName = "_BLENDMODE",
            type = KeywordType.Enum,
            definition = KeywordDefinition.ShaderFeature,
            scope = KeywordScope.Local,
            entries = new KeywordEntry[]
            {
                new KeywordEntry() { displayName = "Off", referenceName = "OFF" },
                new KeywordEntry() { displayName = "Alpha", referenceName = "ALPHA" },
                new KeywordEntry() { displayName = "Add", referenceName = "ADD" },
                new KeywordEntry() { displayName = "PreMultiply", referenceName = "PRE_MULTIPLY" },
            }
        };

        public static KeywordDescriptor FogOnTransparent = new KeywordDescriptor()
        {
            displayName = "Enable Fog On Transparent",
            referenceName = "_ENABLE_FOG_ON_TRANSPARENT",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.ShaderFeature,
            scope = KeywordScope.Local,
        };

        public static KeywordDescriptor SceneSelectionPass = new KeywordDescriptor()
        {
            displayName = "Scene Selection Pass",
            referenceName = "SCENESELECTIONPASS",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.ShaderFeature,
            scope = KeywordScope.Local,
        };

        public static KeywordDescriptor TransparentDepthPrepass = new KeywordDescriptor()
        {
            displayName = "Transparent Depth Prepass",
            referenceName = "CUTOFF_TRANSPARENT_DEPTH_PREPASS",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.ShaderFeature,
            scope = KeywordScope.Local,
        };

        public static KeywordDescriptor TransparentDepthPostpass = new KeywordDescriptor()
        {
            displayName = "Transparent Depth Postpass",
            referenceName = "CUTOFF_TRANSPARENT_DEPTH_POSTPASS",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.ShaderFeature,
            scope = KeywordScope.Local,
        };

        public static KeywordDescriptor SkipRasterizedShadows = new KeywordDescriptor()
        {
            displayName = "Skip Rasterized Shadows",
            referenceName = "SKIP_RASTERIZED_SHADOWS",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static KeywordDescriptor AlphaTest = new KeywordDescriptor()
        {
            displayName = "Alpha Test",
            referenceName = "_ALPHATEST_ON",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.ShaderFeature,
            scope = KeywordScope.Local
        };
    }
#endregion
}
