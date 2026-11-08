#ifndef MAYHEM_SEE_THROUGH_INCLUDED
#define MAYHEM_SEE_THROUGH_INCLUDED

// Valores globais definidos pelo script SeeThroughController (Shader.SetGlobalVector).
// xy = centro no mundo, z = abertura (0 fechado .. 1 aberto), w = píxeis por unidade (grelha)
float4 _SeeThroughPos;
// x = raio, y = suavidade da borda, z = alpha mínimo no centro, w = escurecimento no centro
float4 _SeeThroughParams;

// Tudo o que vem das globais é lido no vertex shader e passado por interpoladores:
//   a = (delta.x, delta.y, raio, suavidade)   b = (abertura, alpha mínimo, escurecimento, píxeis por unidade)
void SeeThroughVertex(float2 positionWS, out float4 a, out float4 b)
{
    a = float4(positionWS - _SeeThroughPos.xy, _SeeThroughParams.x, _SeeThroughParams.y);
    b = float4(saturate(_SeeThroughPos.z), _SeeThroughParams.z, _SeeThroughParams.w, max(_SeeThroughPos.w, 1.0));
}

// Matriz de Bayer 4x4 (dithering ordenado, estilo retro)
float BayerThreshold(float2 cell)
{
    float2 m = cell - 4.0 * floor(cell / 4.0);
    int i = (int)m.x + (int)m.y * 4;
    const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    return (bayer[i] + 0.5) / 16.0;
}

// Máscara radial (como no Shaders Bible: 1 - smoothstep(raio - suavidade, raio, distância)),
// mas pixelizada: a distância é medida na grelha de píxeis do jogo e o gradiente é feito com
// dithering — cada píxel ou está "aberto" ou "fechado".
float4 ApplySeeThrough(float4 inputColor, float4 a, float4 b)
{
    float2 cell = floor(a.xy * b.w);
    float2 snapped = (cell + 0.5) / b.w;
    float inside = 1.0 - smoothstep(a.z - a.w, a.z, length(snapped));
    float k = inside * b.x;
    float on = step(BayerThreshold(cell), k);
    return float4(inputColor.rgb * lerp(1.0, b.z, on), inputColor.a * lerp(1.0, b.y, on));
}

#endif
