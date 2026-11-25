using UnityEditor;
using UnityEngine;

namespace TaxiSim.EditorTools
{
    /// <summary>
    /// Automatic import settings for downloaded textures in Assets/_Project/Resources/TaxiSimTextures:
    ///  * names containing normal / nor_gl / nrm  -> Normal map
    ///  * roughness / ao / height / displacement  -> linear (non-colour) data
    ///  * anything in the Sky sub-folder           -> HDRI cubemap for the skybox
    /// </summary>
    public sealed class TaxiSimTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Resources/TaxiSimTextures/")) return;
            var importer = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            if (path.Contains("/TaxiSimTextures/Sky/"))
            {
                importer.textureShape = TextureImporterShape.TextureCube;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }

            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Repeat;

            if (file.Contains("normal") || file.Contains("nor_gl") || file.Contains("_nor") || file.Contains("nrm"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (file.Contains("rough") || file.Contains("_ao") || file.Contains("ambientocclusion") ||
                     file.Contains("height") || file.Contains("disp") || file.Contains("metal"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
            }
        }
    }
}
