﻿using UnityEngine;
using UnityEditor.ShaderGraph.Internal;

namespace UnityEditor.ShaderGraph
{
    internal static class BlockFields
    {
        [GenerateBlocks]
        public struct VertexDescription
        {
            public static string name = "VertexDescription";
            public static BlockFieldDescriptor Position      = new BlockFieldDescriptor(VertexDescription.name, "Position", "VERTEXDESCRIPTION_POSITION",
                new PositionControl(CoordinateSpace.Object), ShaderStage.Vertex);
            public static BlockFieldDescriptor Normal        = new BlockFieldDescriptor(VertexDescription.name, "Normal", "VERTEXDESCRIPTION_NORMAL",
                new NormalControl(CoordinateSpace.Object), ShaderStage.Vertex);
            public static BlockFieldDescriptor Tangent       = new BlockFieldDescriptor(VertexDescription.name, "Tangent", "VERTEXDESCRIPTION_TANGENT",
                new TangentControl(CoordinateSpace.Object), ShaderStage.Vertex);
        }

        [GenerateBlocks]
        public struct SurfaceDescription
        {
            public static string name = "SurfaceDescription";
            public static BlockFieldDescriptor BaseColor     = new BlockFieldDescriptor(SurfaceDescription.name, "BaseColor", "SURFACEDESCRIPTION_BASECOLOR",
                new ColorControl(UnityEngine.Color.grey, false), ShaderStage.Fragment);
            public static BlockFieldDescriptor Normal        = new BlockFieldDescriptor(SurfaceDescription.name, "Normal", "SURFACEDESCRIPTION_NORMAL",
                new NormalControl(CoordinateSpace.Tangent), ShaderStage.Fragment);
            public static BlockFieldDescriptor Metallic      = new BlockFieldDescriptor(SurfaceDescription.name, "Metallic", "SURFACEDESCRIPTION_METALLIC", 
                new FloatControl(0.0f), ShaderStage.Fragment);
            public static BlockFieldDescriptor Specular      = new BlockFieldDescriptor(SurfaceDescription.name, "Specular", "SURFACEDESCRIPTION_SPECULAR",
                new ColorControl(UnityEngine.Color.grey, false), ShaderStage.Fragment);
            public static BlockFieldDescriptor Smoothness    = new BlockFieldDescriptor(SurfaceDescription.name, "Smoothness", "SURFACEDESCRIPTION_SMOOTHNESS",
                new FloatControl(0.5f), ShaderStage.Fragment);
            public static BlockFieldDescriptor Occlusion     = new BlockFieldDescriptor(SurfaceDescription.name, "Occlusion", "SURFACEDESCRIPTION_OCCLUSION",
                new FloatControl(1.0f), ShaderStage.Fragment);
            public static BlockFieldDescriptor Emission      = new BlockFieldDescriptor(SurfaceDescription.name, "Emission", "SURFACEDESCRIPTION_EMISSION",
                new ColorControl(UnityEngine.Color.black, true), ShaderStage.Fragment);
            public static BlockFieldDescriptor Alpha         = new BlockFieldDescriptor(SurfaceDescription.name, "Alpha", "SURFACEDESCRIPTION_ALPHA",
                new FloatControl(1.0f), ShaderStage.Fragment);
            public static BlockFieldDescriptor AlphaClipThreshold = new BlockFieldDescriptor(SurfaceDescription.name, "AlphaClipThreshold", "SURFACEDESCRIPTION_ALPHACLIPTHRESHOLD",
                new FloatControl(0.5f), ShaderStage.Fragment);
            public static BlockFieldDescriptor SpriteMask = new BlockFieldDescriptor(SurfaceDescription.name, "SpriteMask", "SURFACEDESCRIPTION_SPRITEMASK",
                new ColorRGBAControl(new Color(1, 1, 1, 1)), ShaderStage.Fragment);
        }
    }
}