using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Crews.Utility.TgaSharp;
using Litdex.Random.PRNG;
using nz.doom.WadParser;
using WAD2Q3SharedStuff;

namespace WAD2Quake3Shader
{


    public enum LumpType
    {
        Gray = 0x40,
        Default1 = 0x42,
        Default2 = 0x43,
        Font = 0x46,
    }

    // TODO Take texture light emission from .rad files
    // TODO Allow combining texturetypes


    class Program
    {




        static int Main(string[] args)
        {
            if (args == null || args.Length != 1)
            {
                Console.Error.WriteLine("WAD file required");
                return 2;
            }

            Dictionary<string, Vector4> radIntensities = SharedStuff.getRadIntensities(".");

            if (args[0] != "*")
            {
                ConvertWad(args[0], radIntensities);
            }
            else
            {
                string[] wads = Directory.GetFiles(".", "*.wad");
                foreach(string wad in wads)
                {
                    ConvertWad(wad, radIntensities);
                }
            }

            return 0;
        }

        static void ConvertWad(string wadPath, Dictionary<string, Vector4> radIntensities)
        {

            StringBuilder logString = new StringBuilder();

            Wad wad = WadParser.Parse(wadPath);

            StringBuilder shaderString = new StringBuilder();
            StringBuilder shaderStringPOT = new StringBuilder();

            Dictionary<string, SortedSet<string>> togglingTextures = new Dictionary<string, SortedSet<string>>(StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string, TextureType> groupedTexturesTypes = new Dictionary<string, TextureType>(StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string, bool> togglingTexturesNPOT = new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string, SortedSet<string>> randomTilingTextures = new Dictionary<string, SortedSet<string>>(StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string, ByteImage> randomTilingPicsData = new Dictionary<string, ByteImage>(StringComparer.InvariantCultureIgnoreCase);

            foreach (Lump lump in wad.Lumps)
            {
                if (lump.LumpType != 0x40 && lump.LumpType != 0x42 && lump.LumpType != 0x43 && lump.LumpType != 0x46)
                {
                    Console.WriteLine($"Lump '{lump.Name}' is not of supported type, but type {lump.LumpType}");
                    continue;
                }

                LumpType lumpType = (LumpType)lump.LumpType;

                using (MemoryStream ms = new MemoryStream(lump.Bytes))
                {
                    using (BinaryReader br = new BinaryReader(ms))
                    {
                        string textureName = lump.Name;
                        if (lump.LumpType == 0x40 || lump.LumpType == 0x43)
                        {
                            textureName = Encoding.Latin1.GetString(br.ReadBytes(16));
                            textureName = textureName.Substring(0,textureName.IndexOf('\0'));
                            if (!lump.Name.Equals(textureName,StringComparison.InvariantCultureIgnoreCase))
                            {
                                Console.WriteLine($"Lump/texture name mismatch: Lump '{lump.Name}', texture name '{textureName}'");
                                textureName = lump.Name; // Weird shit.
                            }
                        }
                        int width = 128;
                        int height = 128;
                        if (lump.LumpType != 0x46)
                        {
                            width = br.ReadInt32();
                            height = br.ReadInt32();
                        }
                        if (lump.LumpType == 0x46)
                        {
                            int fontRowCount = br.ReadInt32();
                            int fontRowHeight = br.ReadInt32();
                            byte[] fontOffsetsWidths = br.ReadBytes(1024);
                        }

                        if (lump.LumpType == 0x40 || lump.LumpType == 0x43)
                        {
                            int mipOffset0 = br.ReadInt32();
                            int mipOffset1 = br.ReadInt32();
                            int mipOffset2 = br.ReadInt32();
                            int mipOffset3 = br.ReadInt32();
                        }
                        byte[] mip0PaletteOffsets = br.ReadBytes(width * height);

                        if (lump.LumpType == 0x40 || lump.LumpType == 0x43)
                        {
                            byte[] mip1PaletteOffsets = br.ReadBytes(width * height / 4);
                            byte[] mip2PaletteOffsets = br.ReadBytes(width * height / 16);
                            byte[] mip3PaletteOffsets = br.ReadBytes(width * height / 64);
                        }

                        byte[] randomShit = br.ReadBytes(2);

                        byte[] palette = null;

                        if (lump.LumpType == 0x42 || lump.LumpType == 0x43)
                        {
                            palette = br.ReadBytes(768);
                        }

                        (TextureType type, int specialMatchCount) = SharedStuff.TextureTypeFromTextureName(textureName);

                        

                        Vector4? thisShaderLightIntensity = null;

                        if (radIntensities.ContainsKey(textureName))
                        {
                            type |= TextureType.LightEmitting;
                            thisShaderLightIntensity = radIntensities[textureName];
                        }


                        int potWidth = 1;
                        int potHeight = 1;
                        while(potWidth < width)
                        {
                            potWidth *= 2;
                        }
                        while(potHeight < height)
                        {
                            potHeight *= 2;
                        }

                        bool mustResize = potWidth != width || potHeight != height;

                        ByteImage resizedImage = null;
                        if (mustResize)
                        {
                            Bitmap imageBmp2 = new Bitmap(potWidth, potHeight, PixelFormat.Format32bppArgb);
                            resizedImage = Helpers.BitmapToByteArray(imageBmp2);
                            imageBmp2.Dispose();
                        }


                        if ((type & TextureType.Toggling) > 0)
                        {
                            string key = textureName.Substring(2);
                            if (!togglingTextures.ContainsKey(key))
                            {
                                togglingTextures[key] = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
                            }
                            togglingTexturesNPOT[key] = resizedImage != null;
                            togglingTextures[key].Add(textureName);
                            groupedTexturesTypes[key] = type;
                        }
                        else if ((type & TextureType.RandomTiling) > 0)
                        {
                            string key = textureName.Substring(2);
                            if (!randomTilingTextures.ContainsKey(key))
                            {
                                randomTilingTextures[key] = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
                            }
                            randomTilingTextures[key].Add(textureName);
                            groupedTexturesTypes[key] = type;
                        }

                        ByteImage image = SharedStuff.GoldSrcImgToByteImage(width,height, mip0PaletteOffsets,palette, (type & TextureType.Transparent) > 0 || lumpType == LumpType.Default1, lump.LumpType == 0x46);


                        int maxPaletteOffset = 0;
                        for (int i = 0; i < mip0PaletteOffsets.Length; i++)
                        {
                            if (mip0PaletteOffsets[i] > maxPaletteOffset)
                            {
                                maxPaletteOffset = mip0PaletteOffsets[i];
                            }
                        }


                        if (resizedImage != null)
                        {
                            SharedStuff.ResizeImage(image, resizedImage);
                        }

                        randomTilingPicsData[textureName] = image;

                        /*if (!transparentPixelsFound && type == TextureType.Transparent) // Probably a decal.
                        {
                            type = TextureType.DecalDarken; 
                        }*/

                        // Try to detect if we are dealing with a decal
                        if ((type & TextureType.Transparent) > 0 && palette != null)
                        {
                            // Detect darkening decal
                            bool maybeDecal = true;
                            //bool maybeDarkenDecal = true;
                            //bool maybeBrightenDecal = true;
                            bool has255 = false;
                            bool has0 = false;

                            int lastColor = -1;
                            int lastColor2 = 256;
                            for (int i = 0; i <= maxPaletteOffset; i++)
                            {
                                if (palette[i * 3] != palette[i * 3 + 1] || palette[i * 3 + 2] != palette[i * 3])
                                {
                                    maybeDecal = false;
                                    break;
                                }
                                /*if (palette[i * 3] <= lastColor) // Not reliable
                                {
                                    maybeBrightenDecal = false;
                                }
                                if (palette[i * 3] >= lastColor2)
                                {
                                    maybeDarkenDecal = false;
                                }*/
                                has255 = has255 || (palette[i * 3] == 255);
                                has0 = has0 || (palette[i * 3] == 0);
                                lastColor2 = lastColor = palette[i * 3];

                            }
                            if (maybeDecal)
                            {
                                if ((!has255 && !has0) /*|| (!maybeBrightenDecal && !maybeDarkenDecal)*/)
                                {
                                    maybeDecal = false;
                                }
                                else
                                {
                                    if (has0 && !has255)
                                    {
                                        type = TextureType.DecalBrighten;
                                    }
                                    else if (has255 && !has0)
                                    {
                                        type = TextureType.DecalDarken;
                                    }
                                    else
                                    {
                                        // Look at edge pixels to tell them apart.
                                        int averageTotal = 0;
                                        int averageDivider = 0;
                                        for (int x = 0; x < width; x++)
                                        {
                                            int paletteOffset1 = mip0PaletteOffsets[x];
                                            int paletteOffset2 = mip0PaletteOffsets[(height - 1) * width + x];
                                            averageTotal += palette[paletteOffset1 * 3] + palette[paletteOffset2 * 3];
                                            averageDivider += 2;
                                        }
                                        for (int y = 1; y < height - 1; y++)
                                        {
                                            int paletteOffset1 = mip0PaletteOffsets[y * width];
                                            int paletteOffset2 = mip0PaletteOffsets[y * width + (width - 1)];
                                            averageTotal += palette[paletteOffset1 * 3] + palette[paletteOffset2 * 3];
                                            averageDivider += 2;
                                        }
                                        float average = (float)averageTotal / (float)averageDivider;
                                        if (average > 127.0f)
                                        {

                                            type = TextureType.DecalDarken;
                                        }
                                        else
                                        {

                                            type = TextureType.DecalBrighten;
                                        }
                                    }
                                }
                            }
                        }

                        string texturePath = SharedStuff.fixUpShaderName($"textures/wadConvert/{textureName}");


                        if((type & TextureType.Toggling) == 0 && (type & TextureType.RandomTiling) == 0) // Those 2 types are handled elsewhere
                        {
                            (bool onlyPOT, string shaderText) = SharedStuff.MakeShader(type, texturePath, $"map {texturePath}", resizedImage != null, thisShaderLightIntensity);

                            if (shaderText != null)
                            {
                                if (!onlyPOT)
                                {
                                    shaderString.Append(shaderText);
                                }
                                else
                                {
                                    shaderStringPOT.Append(shaderText);
                                }
                            }
                        }

                        bool resizedIsMain = false;

                        if(resizedImage != null)
                        {
                            resizedIsMain = true;
                        }



                        Bitmap imageBmp = Helpers.ByteArrayToBitmap(image);
                        //imageBmp.Save($"{textureName}.tga");

                        TGA myTGA = new TGA(imageBmp);
                        Directory.CreateDirectory("textures/wadConvert");
                        string suffix = resizedIsMain ? "_npot" : "";
                        if (type == TextureType.RandomTiling && textureName.Length > 1 && textureName[1] == '0')
                        {
                            myTGA.Save($"{texturePath}_original{suffix}.tga"); // 0 one is replaced with a tiled version.
                        } else
                        {
                            myTGA.Save($"{texturePath}{suffix}.tga");
                        }
                        imageBmp.Dispose();

                        if (resizedImage != null)
                        {
                            imageBmp = Helpers.ByteArrayToBitmap(resizedImage);
                            //imageBmp.Save($"{textureName}.tga");

                            suffix = resizedIsMain ? "" : "_pot";

                            myTGA = new TGA(imageBmp);
                            if (type == TextureType.RandomTiling && textureName.Length > 1 && textureName[1] == '0')
                            {
                                myTGA.Save($"{texturePath}_original{suffix}.tga"); // 0 one is replaced with a tiled version.
                            }
                            else
                            {
                                myTGA.Save($"{texturePath}{suffix}.tga");
                            }
                            imageBmp.Dispose();
                        }

                        logString.Append($"special{specialMatchCount} : {lumpType} : {type} : {lump.Name}\n");
                        Console.WriteLine(lump.Name);
                        Console.WriteLine(type);
                        Console.WriteLine(lumpType);
                        //Console.WriteLine(maxPaletteOffset);
                    }
                }

            }

            foreach (var kvp in togglingTextures)
            {
                string baseName = $"+0{kvp.Key}";

                string baseTexturePath = SharedStuff.fixUpShaderName($"textures/wadConvert/{baseName}");

                StringBuilder mapInstruction = new StringBuilder();
                int index = 0;
                foreach (string frame in kvp.Value)
                {
                    string texturePath = SharedStuff.fixUpShaderName($"textures/wadConvert/{frame}");
                    if (index++ == 0)
                    {
                        mapInstruction.Append($"animMap 10 {texturePath}");
                    }
                    else
                    {
                        mapInstruction.Append($" {texturePath}");
                    }
                }

                (bool onlyPOT, string shaderText) = SharedStuff.MakeShader(groupedTexturesTypes[kvp.Key], baseTexturePath, mapInstruction.ToString(), togglingTexturesNPOT[kvp.Key],radIntensities.ContainsKey(baseName) ? radIntensities[baseName] : null);

                if (shaderText != null)
                {
                    if (!onlyPOT)
                    {
                        shaderString.Append(shaderText);
                    }
                    else
                    {
                        shaderStringPOT.Append(shaderText);
                    }
                }

            }
            
            foreach (var kvp in randomTilingTextures)
            {
                string baseName = $"-0{kvp.Key}";

                List<string> srcImages = new List<string>();
                foreach (string frame in kvp.Value)
                {
                    srcImages.Add(frame);
                }
                int variations = srcImages.Count;

                int[,] matrix = SharedStuff.generateUniqueMatrix(variations); // Not true to original or anything but should do the trick

                int width = randomTilingPicsData[srcImages[0]].width;
                int height = randomTilingPicsData[srcImages[0]].height;

                int tiledWidth = width * matrix.GetLength(0);
                int tiledHeight = height * matrix.GetLength(1);

                Bitmap imageBmp = new Bitmap(tiledWidth, tiledHeight, PixelFormat.Format32bppArgb);
                ByteImage image = Helpers.BitmapToByteArray(imageBmp);
                imageBmp.Dispose();

                for (int x= 0; x < matrix.GetLength(0);x++)
                {
                    for (int y = 0; y < matrix.GetLength(1); y++)
                    {
                        int startX = x * width;
                        int startY = y * height;
                        for (int yImg = 0; yImg < height; yImg++)
                        {
                            for (int xImg = 0; xImg < width; xImg++)
                            {
                                int targetX = startX + xImg;
                                int targetY = startY + yImg;
                                ByteImage srcImage = randomTilingPicsData[srcImages[matrix[x, y]]];
                                image.imageData[image.stride * targetY + targetX * 4] = srcImage.imageData[yImg* srcImage.stride + xImg*4];
                                image.imageData[image.stride * targetY + targetX * 4+1] = srcImage.imageData[yImg* srcImage.stride + xImg*4+1];
                                image.imageData[image.stride * targetY + targetX * 4+2] = srcImage.imageData[yImg* srcImage.stride + xImg*4+2];
                                image.imageData[image.stride * targetY + targetX * 4+3] = srcImage.imageData[yImg* srcImage.stride + xImg*4+3];


                            }
                        }
                    }
                }




                // Resize to power of 2 if needed.
                int potWidth = 1;
                int potHeight = 1;
                while (potWidth < tiledWidth)
                {
                    potWidth *= 2;
                }
                while (potHeight < tiledHeight)
                {
                    potHeight *= 2;
                }
                bool mustResize = potWidth != tiledWidth || potHeight != tiledHeight;



                imageBmp = Helpers.ByteArrayToBitmap(image);
                //imageBmp.Save($"{textureName}.tga");

                TGA myTGA = new TGA(imageBmp);
                Directory.CreateDirectory("textures/wadConvert"); 
                string baseTexturePath = SharedStuff.fixUpShaderName($"textures/wadConvert/{baseName}");
                bool resizedIsMain = mustResize;
                string suffix = resizedIsMain ? "_npot" : "";
                myTGA.Save($"{baseTexturePath}{suffix}.tga");
                imageBmp.Dispose();


                (bool onlyPOT, string shaderText) = SharedStuff.MakeShader(groupedTexturesTypes[kvp.Key], baseTexturePath, $"\n\t\tmap {baseTexturePath}", mustResize, radIntensities.ContainsKey(baseName) ? radIntensities[baseName] : null);

                if (shaderText != null)
                {
                    if (!onlyPOT)
                    {
                        shaderString.Append(shaderText);
                    }
                    else
                    {
                        shaderStringPOT.Append(shaderText);
                    }
                }

                if (mustResize)
                {
                    ByteImage resizedImage = null;
                    Bitmap imageBmp2 = new Bitmap(potWidth, potHeight, PixelFormat.Format32bppArgb);
                    resizedImage = Helpers.BitmapToByteArray(imageBmp2);
                    imageBmp2.Dispose();

                    SharedStuff.ResizeImage(image, resizedImage);

                    imageBmp = Helpers.ByteArrayToBitmap(resizedImage);

                    myTGA = new TGA(imageBmp);
                    myTGA.Save($"{baseTexturePath}.tga");
                    imageBmp.Dispose();
                }


            }

            File.AppendAllText("wadConvert.log", logString.ToString());
            Directory.CreateDirectory("shaders");
            File.AppendAllText("shaders/wadConvertShaders.shader", shaderString.ToString());
            File.AppendAllText("shaders/wadConvertShadersQ3MAP.shader", shaderStringPOT.ToString());
        }

        

        

        
    }
}
