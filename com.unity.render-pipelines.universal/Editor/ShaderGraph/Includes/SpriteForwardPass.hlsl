﻿#if ETC1_EXTERNAL_ALPHA
    TEXTURE2D(_AlphaTex); SAMPLER(sampler_AlphaTex);
    float _EnableAlphaTexture;
#endif

PackedVaryings vert(Attributes input)
{
    Varyings output = (Varyings)0;
    output = BuildVaryings(input);
    PackedVaryings packedOutput = PackVaryings(output);
    return packedOutput;
}

half4 frag(PackedVaryings packedInput) : SV_TARGET 
{    
    Varyings unpacked = UnpackVaryings(packedInput);
    UNITY_SETUP_INSTANCE_ID(unpacked);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(unpacked);

    // Fields required by feature blocks are not currently generated
    // unless the corresponding data block is present
    // Therefore we need to predefine all potential data values.
    // Required fields should be tracked properly and generated.
    half3 baseColor = half3(1, 1, 1);
    half alpha = 1;

    #if defined(FEATURES_GRAPH_PIXEL)
        SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(unpacked);
        SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);

        // Data is overriden if the corresponding data block is present.
        // Could use "$Tag.Field: value = surfaceDescription.Field" pattern
        // to avoid preprocessors if this was a template file.
        #ifdef SURFACEDESCRIPTION_BASECOLOR
            baseColor = surfaceDescription.BaseColor;
        #endif
        #ifdef SURFACEDESCRIPTION_ALPHA
            alpha = surfaceDescription.Alpha;
        #endif
    #endif

#if ETC1_EXTERNAL_ALPHA
    float4 alphaTex = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, unpacked.texCoord0.xy);
    alpha = lerp (alpha, alphaTex.r, _EnableAlphaTexture);
#endif

    half4 color = half4(baseColor, alpha);
    color *= unpacked.color;

    return color;
}
