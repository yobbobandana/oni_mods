using HarmonyLib; // Harmony
using KMod; // UserMod2
using System.Reflection; // BindingFlags
using UnityEngine; // Texture2D
using System.Runtime.InteropServices; // Marshal

namespace LinuxSunlightFix
{
    // this just does the default thing for now
    public class LinuxSunlightFix : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
        }
    }
    
    // -------------------------------
    // a patch to fix the sun lighting
    // -------------------------------
    
    [HarmonyPatch(typeof(PropertyTextures))]
    [HarmonyPatch("UpdateProperty")]
    public class LinuxExposedSunlight_Patch
    {
        // i have not found any way to use a private struct as a parameter type,
        // so i'll just duplicate it here.
        private struct TextureProperties
        {
            public string name;
            public PropertyTextures.Property simProperty;
            public TextureFormat textureFormat;
            public FilterMode filterMode;
            public bool updateEveryFrame;
            public bool updatedExternally;
            public bool blend;
            public float blendSpeed;
            public string texturePropertyName;
        }
        
        // divert only the case we want to handle
        private static bool Prefix(TextureProperties p, int x0, int y0, int x1, int y1)
        {
            // which is the ExposedToSunlight texture
            if (p.simProperty == PropertyTextures.Property.ExposedToSunlight)
            {
                // don't bother if called while loading
                if (Game.Instance.IsLoading())
                {
                    return false;
                }
                
                // divert the texure data.
                // i'm copying this because i don't want to mess with it.
                int W = Grid.WidthInCells;
                int H = Grid.HeightInCells;
                int dataLength = W * H;
                byte[] rawData = new byte[dataLength];
                Marshal.Copy(PropertyTextures.externalExposedToSunlight, rawData, 0, dataLength);
                
                // flip the copied data
                int halfH = H / 2;
                for (int x = 0; x < W; x++)
                {
                    for (int y = 0; y < halfH; y++)
                    {
                        // byte offsets of the pixels to swap.
                        // format is 1-byte A
                        int pos1 = (y * W) + x;
                        int pos2 = ((H - 1 - y) * W) + x;
                        // swap the bytes
                        byte cached = rawData[pos1];
                        rawData[pos1] = rawData[pos2];
                        rawData[pos2] = cached;
                    }
                }
                
                // sent the modified data directly,
                // in stead of the unmodified data which would normally be sent.
                int simProperty = (int)p.simProperty;
                var eutRefl = typeof(PropertyTextures).GetField("externallyUpdatedTextures", BindingFlags.Instance | BindingFlags.NonPublic);
                Texture2D[] externallyUpdatedTextures = (Texture2D[])eutRefl.GetValue(PropertyTextures.instance);
                externallyUpdatedTextures[simProperty].LoadRawTextureData(rawData);
                externallyUpdatedTextures[simProperty].Apply();
                
                // skip handling the base method as there's nothing else there
                return false;
            }
            // for anything else proceed as normal
            return true;
        }
    }
}
