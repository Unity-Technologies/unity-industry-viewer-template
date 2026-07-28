#ifndef COINCIDENCE_BIAS_INCLUDED
#define COINCIDENCE_BIAS_INCLUDED

// EXPERIMENTAL viewer-side mitigation for coincident-surface z-fighting on streamed CAD geometry.
//
// The Unity Cloud DataStreaming package renders every streamed element as its own GameObject with its
// own object-to-world matrix (GOBaseRenderer). We use that: each element gets a tiny, STABLE, per-object
// push along the view direction. Two coincident surfaces from DIFFERENT elements (the common BIM case:
// slab vs finish, abutting walls from different trades) then resolve to a consistent depth winner
// instead of flickering per-frame as the camera moves.
//
// Limitations:
//  * Two coincident faces inside the SAME element (same matrix) get the same push -> not separated.
//  * Render-only. Picking/measurement use stage raycasts against source data, so accuracy is unaffected.
//  * Bias is the max separation distance in METRES. 0.0003 (0.3 mm) is invisible at building scale.
//    For tiny Manufacturing parts, pass a smaller value (e.g. 0.00003).

void CoincidenceBias_float(float3 In, float Bias, out float3 Out)
{
    // Off (feature disabled / slider at 0): skip the hash + matrix work entirely. Bias is driven by the
    // "_Bias" global uniform, so this is a COHERENT branch -- every vertex takes the same path with no
    // divergence cost -- which makes the disabled state effectively free.
    if (Bias <= 0.0)
    {
        Out = In;
        return;
    }

    // Per-object seed from the object's world translation (differs per streamed element).
    float3 objWorld = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
    float seed = frac(sin(dot(objWorld, float3(12.9898, 78.233, 37.719))) * 43758.5453);
    float signedSeed = seed - 0.5; // stable per element, symmetric around zero

    // This vertex in world space.
    float3 worldPos = mul(UNITY_MATRIX_M, float4(In, 1.0)).xyz;

    // Push a hair toward/away from camera; separating along view direction biases depth directly.
    float3 toCam = _WorldSpaceCameraPos.xyz - worldPos;
    toCam /= max(length(toCam), 1e-5);
    worldPos += toCam * (signedSeed * Bias);

    // Back to object space (the Vertex Position block expects object space).
    Out = mul(UNITY_MATRIX_I_M, float4(worldPos, 1.0)).xyz;
}

#endif // COINCIDENCE_BIAS_INCLUDED
