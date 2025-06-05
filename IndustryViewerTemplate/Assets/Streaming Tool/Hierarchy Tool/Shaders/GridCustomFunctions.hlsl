void InfiniteGrid_float(float3 WorldPos, float3 CameraPos, float3 CameraForward, float BaseSize, float LineThickness, float FadeDistance, out float Alpha)
{
    float3 planeNormal = float3(0, 1, 0);
    float planeHeight = 0;
    float t = (planeHeight - CameraPos.y) / dot(CameraForward, planeNormal);
    float3 planeIntersection = CameraPos + t * CameraForward;
    float dist = distance(planeIntersection, CameraPos);

    float logDist = log10(max(dist, 1.0));
    float scale = pow(10.0, floor(logDist));
    float fadeScale = 1.0-frac(logDist);

    float2 coord = WorldPos.xz / (BaseSize * scale);
    float2 derivative = fwidth(coord);
    float2 grid = abs(frac(coord - 0.5) - 0.5);

    float2 lines1 = smoothstep(0, derivative * LineThickness, grid);
    float line1 = 1.0 - min(lines1.x, lines1.y);

    float2 coord2 = WorldPos.xz / (BaseSize * scale * 0.1);
    float2 derivative2 = fwidth(coord2);
    float2 grid2 = abs(frac(coord2 - 0.5) - 0.5);

    float2 lines2 = smoothstep(0, derivative2 * LineThickness, grid2);
    float line2 = 1.0 - min(lines2.x, lines2.y);

    Alpha = (line1) + (line2 * fadeScale);
}
