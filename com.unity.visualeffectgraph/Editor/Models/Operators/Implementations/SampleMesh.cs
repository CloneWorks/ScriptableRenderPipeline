using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.VFX.Operator
{
    [VFXInfo(category = "Sampling", experimental = true)]
    class SampleMesh : VFXOperator
    {
        override public string name { get { return "Sample Mesh"; } }

        public class InputProperties
        {
            [Tooltip("Sets the mesh to sample from.")]
            public Mesh mesh = VFXResources.defaultResources.mesh;
            [Tooltip("The vertex index to read from.")]
            public uint vertex = 0u;
        }

        //public enum PlacementMode
        //{
        //    Vertex,
        //    Edge,
        //    Surface
        //};

        [Flags]
        public enum VertexAttributeFlag
        {
            None = 0,
            Position = 1 << 0,
            Normal = 1 << 1,
            Tangent = 1 << 2,
            Color = 1 << 3,
            TexCoord0 = 1 << 4,
            TexCoord1 = 1 << 5,
            TexCoord2 = 1 << 6,
            TexCoord3 = 1 << 7,
            TexCoord4 = 1 << 8,
            TexCoord5 = 1 << 9,
            TexCoord6 = 1 << 10,
            TexCoord7 = 1 << 11,
            BlendWeight = 1 << 12,
            BlendIndices = 1 << 13
        }

        [VFXSetting(VFXSettingAttribute.VisibleFlags.InInspector), SerializeField, Tooltip("Outputs the result of the specified mesh sampling.")]
        private VertexAttributeFlag output = VertexAttributeFlag.Position | VertexAttributeFlag.Color | VertexAttributeFlag.TexCoord0;

        [VFXSetting, SerializeField, Tooltip("Change how the out of bounds are handled while fetching with the custom vertex index.")]
        private VFXOperatorUtility.SequentialAddressingMode adressingMode = VFXOperatorUtility.SequentialAddressingMode.Wrap;

        private bool HasOutput(VertexAttributeFlag flag)
        {
            return (output & flag) == flag;
        }

        private IEnumerable<VertexAttributeFlag> GetOutputVertexAttributes()
        {
            var vertexAttributes = Enum.GetValues(typeof(VertexAttributeFlag)).Cast<VertexAttributeFlag>();
            foreach (var vertexAttribute in vertexAttributes)
                if (vertexAttribute != VertexAttributeFlag.None && HasOutput(vertexAttribute))
                    yield return vertexAttribute;
        }

        private static Type GetOutputType(VertexAttributeFlag attribute)
        {
            switch (attribute)
            {
                case VertexAttributeFlag.Position: return typeof(Vector3);
                case VertexAttributeFlag.Normal: return typeof(Vector3);
                case VertexAttributeFlag.Tangent: return typeof(Vector4);
                case VertexAttributeFlag.Color: return typeof(Vector4);
                case VertexAttributeFlag.TexCoord0:
                case VertexAttributeFlag.TexCoord1:
                case VertexAttributeFlag.TexCoord2:
                case VertexAttributeFlag.TexCoord3:
                case VertexAttributeFlag.TexCoord4:
                case VertexAttributeFlag.TexCoord5:
                case VertexAttributeFlag.TexCoord6:
                case VertexAttributeFlag.TexCoord7:
#if UNITY_2020_2_OR_NEWER
                    return typeof(Vector4);
#else
                    return typeof(Vector2);
#endif
                case VertexAttributeFlag.BlendWeight: return typeof(Vector4);
                case VertexAttributeFlag.BlendIndices: return typeof(Vector4);
                default: throw new InvalidOperationException("Unexpected attribute : " + attribute);
            }
        }

        private static VertexAttribute GetActualVertexAttribute(VertexAttributeFlag attribute)
        {
            switch (attribute)
            {
                case VertexAttributeFlag.Position: return VertexAttribute.Position;
                case VertexAttributeFlag.Normal: return VertexAttribute.Normal;
                case VertexAttributeFlag.Tangent: return VertexAttribute.Tangent;
                case VertexAttributeFlag.Color: return VertexAttribute.Color;
                case VertexAttributeFlag.TexCoord0: return VertexAttribute.TexCoord0;
                case VertexAttributeFlag.TexCoord1: return VertexAttribute.TexCoord1;
                case VertexAttributeFlag.TexCoord2: return VertexAttribute.TexCoord2;
                case VertexAttributeFlag.TexCoord3: return VertexAttribute.TexCoord3;
                case VertexAttributeFlag.TexCoord4: return VertexAttribute.TexCoord4;
                case VertexAttributeFlag.TexCoord5: return VertexAttribute.TexCoord5;
                case VertexAttributeFlag.TexCoord6: return VertexAttribute.TexCoord6;
                case VertexAttributeFlag.TexCoord7: return VertexAttribute.TexCoord7;
                case VertexAttributeFlag.BlendWeight: return VertexAttribute.BlendWeight;
                case VertexAttributeFlag.BlendIndices: return VertexAttribute.BlendIndices;
                default: throw new InvalidOperationException("Unexpected attribute : " + attribute);
            }
        }

        protected override IEnumerable<VFXPropertyWithValue> outputProperties
        {
            get
            {
                foreach (var vertexAttribute in GetOutputVertexAttributes())
                {
                    var outputType = GetOutputType(vertexAttribute);
                    yield return new VFXPropertyWithValue(new VFXProperty(outputType, vertexAttribute.ToString()));
                }
            }
        }

        protected override sealed VFXExpression[] BuildExpression(VFXExpression[] inputExpression)
        {
            var mesh = inputExpression[0];

            VFXExpression meshVertexStride = new VFXExpressionMeshVertexStride(mesh);
            VFXExpression meshVertexCount = new VFXExpressionMeshVertexCount(mesh);
            VFXExpression vertexIndex = VFXOperatorUtility.ApplyAddressingMode(inputExpression[1], meshVertexCount, adressingMode);

            var outputExpressions = new List<VFXExpression>();
            foreach (var vertexAttribute in GetOutputVertexAttributes())
            {
                var channelIndex = VFXValue.Constant<uint>((uint)GetActualVertexAttribute(vertexAttribute));
                var meshChannelOffset = new VFXExpressionMeshChannelOffset(mesh, channelIndex);

                var outputType = GetOutputType(vertexAttribute);
                VFXExpression sampled = null;

#if UNITY_2020_2_OR_NEWER
                var meshChannelFormatAndDimension = new VFXExpressionMeshChannelFormatAndDimension(mesh, channelIndex);
                var vertexOffset = vertexIndex * meshVertexStride + meshChannelOffset;
                if (vertexAttribute == VertexAttributeFlag.Color)
                    sampled = new VFXExpressionSampleMeshColor(mesh, vertexOffset, meshChannelFormatAndDimension);
                else if (outputType == typeof(float))
                    sampled = new VFXExpressionSampleMeshFloat(mesh, vertexOffset, meshChannelFormatAndDimension);
                else if (outputType == typeof(Vector2))
                    sampled = new VFXExpressionSampleMeshFloat2(mesh, vertexOffset, meshChannelFormatAndDimension);
                else if (outputType == typeof(Vector3))
                    sampled = new VFXExpressionSampleMeshFloat3(mesh, vertexOffset, meshChannelFormatAndDimension);
                else
                    sampled = new VFXExpressionSampleMeshFloat4(mesh, vertexOffset, meshChannelFormatAndDimension);
#else
                if (vertexAttribute == VertexAttributeFlag.Color)
                    sampled = new VFXExpressionSampleMeshColor(mesh, vertexIndex, meshChannelOffset, meshVertexStride);
                else if (outputType == typeof(float))
                    sampled = new VFXExpressionSampleMeshFloat(mesh, vertexIndex, meshChannelOffset, meshVertexStride);
                else if (outputType == typeof(Vector2))
                    sampled = new VFXExpressionSampleMeshFloat2(mesh, vertexIndex, meshChannelOffset, meshVertexStride);
                else if (outputType == typeof(Vector3))
                    sampled = new VFXExpressionSampleMeshFloat3(mesh, vertexIndex, meshChannelOffset, meshVertexStride);
                else
                    sampled = new VFXExpressionSampleMeshFloat4(mesh, vertexIndex, meshChannelOffset, meshVertexStride);
#endif
                outputExpressions.Add(sampled);
            }
            return outputExpressions.ToArray();
        }
    }
}
