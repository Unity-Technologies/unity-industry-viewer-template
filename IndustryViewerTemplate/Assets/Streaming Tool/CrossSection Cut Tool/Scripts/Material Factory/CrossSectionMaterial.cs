using Unity.Cloud.DataStreaming.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Industry.Viewer.Streaming.CrossSection
{
    public class CrossSectionMaterial : IStreamingMaterial
    {
        const string k_DoubleSidedOn = "_DOUBLESIDED_ON";
        static readonly int k_DoubleSidedEnable = Shader.PropertyToID("_DoubleSidedEnable");
        
        public Material UnityMaterial { get; }

        public CrossSectionMaterial(Material material)
        {
            UnityMaterial = material;
        }

        public void Dispose()
        {
            // The factory clones a fresh Material per streaming material and the streamer
            // disposes this wrapper on every tile unload — without destroying the clone here
            // it leaks (~4 Materials per tile load under churn). Same pattern as the SDK's
            // own SimpleMaterial.Dispose.
            if (Application.isPlaying)
                Object.Destroy(UnityMaterial);
            else
                Object.DestroyImmediate(UnityMaterial);
        }

        public void SetAlbedoColor(Color color)
        {
            UnityMaterial.SetColor("_BaseColor", color);
        }

        public void SetAlbedoTexture(Texture texture, Vector4 textureSt, float textureRotation)
        {
            UnityMaterial.SetTexture("_BaseColorTex", texture);
            UnityMaterial.SetVector("_BaseColorTex_ST", textureSt);
            UnityMaterial.SetFloat("_BaseColorTex_R", textureRotation);
        }

        public void SetAlphaCutoff(float alphaCutoff)
        {
            UnityMaterial.SetFloat("_AlphaCutoff", alphaCutoff);
        }

        public void SetEnableAlphaClip(bool enableAlphaClip)
        {
            //Will not set AlphaClip on the material, as AlphaClip is always enabled for cross-section materials.
        }

        public void SetEnableDoubleSided(bool enableDoubleSided)
        {
            SetDoubleSided(enableDoubleSided);
        }
        
        void SetDoubleSided(bool enable)
        {
            if (enable)
            {
                UnityMaterial.EnableKeyword(k_DoubleSidedOn);
                SetDoubleSidedProperties(true, (int)CullMode.Off, 0, new Vector4(-1, -1, -1, 0)); // Flip vector of (1, 1, 1, 0)
            }
            else
            {
                UnityMaterial.DisableKeyword(k_DoubleSidedOn);
                SetDoubleSidedProperties(false, (int)CullMode.Back, 1, new Vector4(1, 1, -1, 0)); // Mirror vector of (1, 1, 1, 0)
            }
        }
        
        void SetDoubleSidedProperties(bool doubleSidedEnable, int cull, int doubleSidedNormalMode, Vector4 doubleSidedConstants)
        {
            UnityMaterial.SetFloat(k_DoubleSidedEnable, doubleSidedEnable ? 1 : 0);
            UnityMaterial.doubleSidedGI = doubleSidedEnable;

#if URP_AVAILABLE
            UnityMaterial.SetFloat(k_Cull, cull);
#endif
#if HDRP_AVAILABLE
            UnityMaterial.SetFloat(k_CullMode, cull);
            UnityMaterial.SetFloat(k_CullModeForward, cull);

            UnityMaterial.SetFloat(k_DoubleSidedNormalMode, doubleSidedNormalMode); // Flip: 0, Mirror: 1, None: 2
            UnityMaterial.SetVector(k_DoubleSidedConstants, doubleSidedConstants);
#endif
        }

        public void SetClearcoatFactor(float factor)
        {
            UnityMaterial.SetFloat("_ClearcoatFactor", factor);
        }

        public void SetClearcoatNormalTexture(Texture texture, Vector4 textureSt, float textureRotation)
        {
            UnityMaterial.SetTexture("_ClearcoatNormalTex", texture);
            UnityMaterial.SetVector("_ClearcoatNormalTex_ST", textureSt);
            UnityMaterial.SetFloat("_ClearcoatNormalTex_R", textureRotation);
        }

        public void SetClearcoatRoughnessFactor(float factor)
        {
            UnityMaterial.SetFloat("_ClearcoatRoughnessFactor", factor);
        }

        public void SetClearcoatRoughnessTexture(Texture texture, Vector4 textureSt, float textureRotation)
        {
            UnityMaterial.SetTexture("_ClearcoatRoughnessTex", texture);
            UnityMaterial.SetVector("_ClearcoatRoughnessTex_ST", textureSt);
            UnityMaterial.SetFloat("_ClearcoatRoughnessTex_R", textureRotation);
        }

        public void SetClearcoatTexture(Texture texture, Vector4 textureSt, float textureRotation)
        {
            UnityMaterial.SetTexture("_ClearcoatTex", texture);
            UnityMaterial.SetVector("_ClearcoatTex_ST", textureSt);
            UnityMaterial.SetFloat("_ClearcoatTex_R", textureRotation);
        }

        public void SetMaskTexture(Texture texture)
        {
            UnityMaterial.SetTexture("_MaskTex", texture);
        }

        public void SetMaskTextureCoord(float textureCoord)
        {
            UnityMaterial.SetFloat("_MaskTexCoord", textureCoord);
        }

        public void SetNormalScale(float scale)
        {
            UnityMaterial.SetFloat("_NormalScale", scale);
        }

        public void SetNormalTexture(Texture texture, Vector4 textureSt, float textureRotation)
        {
            UnityMaterial.SetTexture("_NormalTex", texture);
            UnityMaterial.SetVector("_NormalTex_ST", textureSt);
            UnityMaterial.SetFloat("_NormalTex_R", textureRotation);
        }

        public void SetMetallicFactor(float factor)
        {
            UnityMaterial.SetFloat("_MetallicFactor", factor);
        }

        public void SetRoughnessFactor(float factor)
        {
            UnityMaterial.SetFloat("_RoughnessFactor", factor);
        }

        public void SetMetallicRoughnessTexture(Texture texture, Vector4 textureSt, float textureRotation)
        {
            UnityMaterial.SetTexture("_MetallicRoughnessTex", texture);
            UnityMaterial.SetVector("_MetallicRoughnessTex_ST", textureSt);
            UnityMaterial.SetFloat("_MetallicRoughnessTex_R", textureRotation);
        }

        public void SetEmissiveFactor(Color factor)
        {
            UnityMaterial.SetColor("_EmissiveFactor", factor);
        }

        public void SetEmissiveTexture(Texture texture, Vector4 textureSt, float textureRotation)
        {
            UnityMaterial.SetTexture("_EmissiveTex", texture);
            UnityMaterial.SetVector("_EmissiveTex_ST", textureSt);
            UnityMaterial.SetFloat("_EmissiveTex_R", textureRotation);
        }

        public void SetOcclusionStrength(float strength)
        {
            UnityMaterial.SetFloat("_OcclusionStrength", strength);
        }

        public void SetOcclusionTexture(Texture texture, Vector4 textureSt, float textureRotation)
        {
            UnityMaterial.SetTexture("_OcclusionTex", texture);
            UnityMaterial.SetVector("_OcclusionTex_ST", textureSt);
            UnityMaterial.SetFloat("_OcclusionTex_R", textureRotation);
        }

        
    }
}
