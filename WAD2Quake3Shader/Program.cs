using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Crews.Utility.TgaSharp;
using Litdex.Random.PRNG;
using nz.doom.WadParser;

namespace WAD2Quake3Shader
{
    enum TextureType { 
        Normal,
        Transparent,
        WaterFluid,
        Toggling,
        RandomTiling,
        LightEmitting,
        DecalDarken,
        DecalBrighten,
    }

    enum LumpType { 
        Gray = 0x40,
        Default1 = 0x42,
        Default2 = 0x43,
        Font = 0x46,
    }


    class Program
    {
        static int Main(string[] args)
        {
            if (args == null || args.Length != 1)
            {
                Console.Error.WriteLine("WAD file required");
                return 2;
            }

            if(args[0] != "*")
            {
                ConvertWad(args[0]);
            }
            else
            {
                string[] wads = Directory.GetFiles(".", "*.wad");
                foreach(string wad in wads)
                {
                    ConvertWad(wad);
                }
            }

            return 0;
        }

        static void ConvertWad(string wadPath)
        {

            StringBuilder logString = new StringBuilder();

            Wad wad = WadParser.Parse(wadPath);

            StringBuilder shaderString = new StringBuilder();

            Dictionary<string, SortedSet<string>> togglingTextures = new Dictionary<string, SortedSet<string>>(StringComparer.InvariantCultureIgnoreCase);
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
                            textureName = Encoding.Latin1.GetString(br.ReadBytes(16)).TrimEnd('\0');
                            if (lump.Name != textureName)
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

                        TextureType type = TextureType.Normal;
                        if (textureName.Length > 0)
                        {
                            switch (textureName[0])
                            {
                                case '{':
                                    type = TextureType.Transparent;
                                    break;
                                case '!':
                                    type = TextureType.WaterFluid;
                                    break;
                                case '+':
                                    type = TextureType.Toggling;
                                    break;
                                case '-':
                                    type = TextureType.RandomTiling;
                                    break;
                                case '~':
                                    type = TextureType.LightEmitting;
                                    break;
                            }
                        }

                        if (type == TextureType.Toggling)
                        {
                            string key = textureName.Substring(2);
                            if (!togglingTextures.ContainsKey(key))
                            {
                                togglingTextures[key] = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
                            }
                            togglingTextures[key].Add(textureName);
                        }
                        else if (type == TextureType.RandomTiling)
                        {
                            string key = textureName.Substring(2);
                            if (!randomTilingTextures.ContainsKey(key))
                            {
                                randomTilingTextures[key] = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);
                            }
                            randomTilingTextures[key].Add(textureName);
                        }


                        Bitmap imageBmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                        ByteImage image = Helpers.BitmapToByteArray(imageBmp);
                        imageBmp.Dispose();

                        int maxPaletteOffset = 0;
                        for (int i = 0; i < mip0PaletteOffsets.Length; i++)
                        {
                            if (mip0PaletteOffsets[i] > maxPaletteOffset)
                            {
                                maxPaletteOffset = mip0PaletteOffsets[i];
                            }
                        }

                        bool transparentPixelsFound = false;

                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int paletteOffset = mip0PaletteOffsets[y * width + x];
                                if (palette != null)
                                {
                                    image.imageData[image.stride * y + x * 4] = palette[paletteOffset * 3];
                                    image.imageData[image.stride * y + x * 4 + 1] = palette[paletteOffset * 3 + 1];
                                    image.imageData[image.stride * y + x * 4 + 2] = palette[paletteOffset * 3 + 2];
                                }
                                else
                                {
                                    image.imageData[image.stride * y + x * 4] = (byte)paletteOffset;
                                    image.imageData[image.stride * y + x * 4 + 1] = (byte)paletteOffset;
                                    image.imageData[image.stride * y + x * 4 + 2] = (byte)paletteOffset;
                                }
                                image.imageData[image.stride * y + x * 4 + 3] = ((type == TextureType.Transparent || lumpType == LumpType.Default1) && paletteOffset == 255) ? (byte)0 : (byte)255;
                                if (lump.LumpType == 0x46 && paletteOffset == 0)
                                {
                                    // Special font thing?
                                    image.imageData[image.stride * y + x * 4 + 3] = 0;
                                    transparentPixelsFound = true;
                                }
                                if (paletteOffset == 255)
                                {
                                    transparentPixelsFound = true;
                                }
                            }
                        }

                        randomTilingPicsData[textureName] = image;

                        /*if (!transparentPixelsFound && type == TextureType.Transparent) // Probably a decal.
                        {
                            type = TextureType.DecalDarken; 
                        }*/

                        if (type == TextureType.Transparent && palette != null)
                        {
                            // Try to detect if we are dealing with a decal
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

                        string texturePath = fixUpShaderName($"textures/wadConvert/{textureName}");

                        if (type == TextureType.WaterFluid || type == TextureType.Transparent || type == TextureType.DecalDarken || type == TextureType.DecalBrighten || type == TextureType.LightEmitting)
                        {
                            shaderString.Append($"\n{texturePath}\n{{");
                            shaderString.Append($"\n\tqer_editorimage {texturePath}");
                            switch (type)
                            {
                                case TextureType.WaterFluid:
                                    shaderString.Append($"\n\tsurfaceparm nonsolid");
                                    shaderString.Append($"\n\tsurfaceparm nonopaque");
                                    shaderString.Append($"\n\tsurfaceparm water");
                                    shaderString.Append($"\n\tsurfaceparm trans");
                                    shaderString.Append($"\n\tqer_trans 0.5");
                                    shaderString.Append($"\n\tq3map_material Water");
                                    shaderString.Append($"\n\ttessSize 100");
                                    shaderString.Append($"\n\tdeformvertexes wave 100 sin 0 2.5 0 0.5");

                                    shaderString.Append($"\n\t{{");
                                    shaderString.Append($"\n\t\tmap {texturePath}");
                                    shaderString.Append($"\n\t\tblendFunc GL_ONE GL_ONE_MINUS_SRC_ALPHA");
                                    shaderString.Append($"\n\t\talphaGen const 0.8");
                                    shaderString.Append($"\n\t\ttcMod turb 0 0.05 0 0.2");
                                    shaderString.Append($"\n\t}}");

                                    shaderString.Append($"\n\t{{");
                                    shaderString.Append($"\n\t\tmap $lightmap");
                                    shaderString.Append($"\n\t\tblendFunc GL_DST_COLOR GL_ZERO");
                                    shaderString.Append($"\n\t}}");

                                    shaderString.Append($"\n\t//{{");
                                    shaderString.Append($"\n\t//\tmap textures/random_environment_maybe");
                                    shaderString.Append($"\n\t//\tblendFunc GL_ONE GL_ONE");
                                    shaderString.Append($"\n\t//\ttcGen environment");
                                    shaderString.Append($"\n\t//\ttcMod turb 0 0.05 0 0.2");
                                    shaderString.Append($"\n\t//}}");
                                    break;
                                case TextureType.Transparent:
                                    shaderString.Append($"\n\tsurfaceparm alphashadow");
                                    shaderString.Append($"\n\tsurfaceparm nonopaque");
                                    shaderString.Append($"\n\tsurfaceparm trans");
                                    shaderString.Append($"\n\tcull none");
                                    shaderString.Append($"\n\tqer_trans 0.5");

                                    shaderString.Append($"\n\t{{");
                                    shaderString.Append($"\n\t\tmap {texturePath}");
                                    shaderString.Append($"\n\t\tblendFunc GL_SRC_ALPHA GL_ONE_MINUS_SRC_ALPHA");
                                    shaderString.Append($"\n\t\tdepthWrite");
                                    shaderString.Append($"\n\t\talphaFunc GE128");
                                    shaderString.Append($"\n\t}}");

                                    shaderString.Append($"\n\t{{");
                                    shaderString.Append($"\n\t\tmap $lightmap");
                                    shaderString.Append($"\n\t\tblendFunc filter");
                                    shaderString.Append($"\n\t\tdepthFunc equal");
                                    shaderString.Append($"\n\t}}");
                                    break;
                                case TextureType.DecalDarken: // Simple multiply
                                    shaderString.Append($"\n\tpolygonOffset");
                                    shaderString.Append($"\n\tq3map_nolightmap");
                                    shaderString.Append($"\n\tqer_trans 0.5");

                                    shaderString.Append($"\n\t{{");
                                    shaderString.Append($"\n\t\tmap {texturePath}");
                                    shaderString.Append($"\n\t\tblendFunc filter");
                                    shaderString.Append($"\n\t}}");
                                    break;
                                case TextureType.DecalBrighten: // Simple multiply
                                    shaderString.Append($"\n\tpolygonOffset");
                                    shaderString.Append($"\n\tq3map_nolightmap");
                                    shaderString.Append($"\n\tqer_trans 0.5");

                                    shaderString.Append($"\n\t{{");
                                    shaderString.Append($"\n\t\tmap {texturePath}");
                                    shaderString.Append($"\n\t\tblendFunc GL_ONE GL_ONE");
                                    shaderString.Append($"\n\t}}");
                                    break;
                                case TextureType.LightEmitting:
                                    shaderString.Append($"\n\tq3map_surfacelight 1500");
                                    shaderString.Append($"\n\tq3map_lightsubdivide 64");
                                    shaderString.Append($"\n\tq3map_nolightmap");

                                    shaderString.Append($"\n\t{{");
                                    shaderString.Append($"\n\t\tmap {texturePath}");
                                    shaderString.Append($"\n\t\trgbGen const ( 0.9 0.9 0.9 )");
                                    shaderString.Append($"\n\t}}");

                                    shaderString.Append($"\n\t{{");
                                    shaderString.Append($"\n\t\tmap {texturePath}");
                                    shaderString.Append($"\n\t\tblendFunc GL_ONE GL_ONE");
                                    shaderString.Append($"\n\t\trgbGen const ( 0.1 0.1 0.1 )");
                                    shaderString.Append($"\n\t\tglow");
                                    shaderString.Append($"\n\t}}");
                                    break;
                            }




                            shaderString.Append($"\n}}\n");
                        }


                        imageBmp = Helpers.ByteArrayToBitmap(image);
                        //imageBmp.Save($"{textureName}.tga");

                        TGA myTGA = new TGA(imageBmp);
                        Directory.CreateDirectory("textures/wadConvert");
                        if(type == TextureType.RandomTiling && textureName.Length > 1 && textureName[1] == '0')
                        {
                            myTGA.Save($"{texturePath}_original.tga"); // 0 one is replaced with a tiled version.
                        } else
                        {
                            myTGA.Save($"{texturePath}.tga");
                        }
                        imageBmp.Dispose();

                        logString.Append($"{lumpType} : {type} : {lump.Name}\n");
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

                string baseTexturePath = fixUpShaderName($"textures/wadConvert/{baseName}");

                shaderString.Append($"\n{baseTexturePath}\n{{");
                shaderString.Append($"\n\tqer_editorimage {baseTexturePath}");
                shaderString.Append($"\n\tcull disable");

                shaderString.Append($"\n\t{{\n");
                int index = 0;
                foreach (string frame in kvp.Value)
                {
                    string texturePath = fixUpShaderName($"textures/wadConvert/{frame}");
                    if (index++ == 0)
                    {
                        shaderString.Append($"\t\tanimMap 0.1 {texturePath}");
                    }
                    else
                    {
                        shaderString.Append($" {texturePath}");
                    }
                }
                shaderString.Append($"\n\t}}");

                shaderString.Append($"\n\t{{");
                shaderString.Append($"\n\t\tmap $lightmap");
                shaderString.Append($"\n\t\tblendFunc GL_DST_COLOR GL_ZERO");
                shaderString.Append($"\n\t}}");


                shaderString.Append($"\n}}\n");
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

                int[,] matrix = generateUniqueMatrix(variations); // Not true to original or anything but should do the trick

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

                imageBmp = Helpers.ByteArrayToBitmap(image);
                //imageBmp.Save($"{textureName}.tga");

                TGA myTGA = new TGA(imageBmp);
                Directory.CreateDirectory("textures/wadConvert"); 
                string baseTexturePath = fixUpShaderName($"textures/wadConvert/{baseName}");
                myTGA.Save($"{baseTexturePath}.tga");
                imageBmp.Dispose();

            }

            File.AppendAllText("wadConvert.log", logString.ToString());
            Directory.CreateDirectory("shaders");
            File.AppendAllText("shaders/wadConvertShaders.shader", shaderString.ToString());
        }

        static int[,] generateUniqueMatrix(int variations)
        {
            int sideMultiplier = 1;
            while (sideMultiplier < variations)
            {
                sideMultiplier *= 2;
            }
            int[,] matrix = new int[sideMultiplier, sideMultiplier];
            bool[,] filled = new bool[sideMultiplier, sideMultiplier];

            int[] usages = new int[variations];

            var rnd = new Shishua(new ulong[] { 1,2,3,4 });

            float[] shortestDistances = new float[variations];

            for (int y = 0; y < sideMultiplier; y++)
            {
                for (int x = 0; x < sideMultiplier; x++)
                {
                    float biggestShortestDistance = 0;

                    List<int> biggestShortestDistanceCandidates = new List<int>();

                    for (int i = 0; i < variations; i++)
                    {
                        shortestDistances[i] = float.PositiveInfinity;
                        // Rate each variation for this place.
                        // Rating is distance to the same variation in the existing matrix.
                        for (int y2 = 0; y2 < sideMultiplier; y2++)
                        {
                            for (int x2 = 0; x2 < sideMultiplier; x2++)
                            {
                                if (filled[x2,y2] && matrix[x2,y2] == i)
                                {
                                    int smolX = Math.Min(x, x2);
                                    int bigX = Math.Max(x, x2);
                                    int smolY = Math.Min(y, y2);
                                    int bigY = Math.Max(y, y2);
                                    float shortestXDist = Math.Min(bigX-smolX, sideMultiplier-bigX + smolX);
                                    float shortestYDist = Math.Min(bigY-smolY, sideMultiplier-bigY + smolY);
                                    float shortestDistance = (float)Math.Sqrt(shortestXDist* shortestXDist+ shortestYDist* shortestYDist);
                                    if(shortestDistances[i] > shortestDistance)
                                    {
                                        shortestDistances[i] = shortestDistance;
                                    }
                                }
                            }
                        }

                        if(biggestShortestDistance < shortestDistances[i])
                        {
                            biggestShortestDistance = shortestDistances[i];
                            biggestShortestDistanceCandidates.Clear();
                            biggestShortestDistanceCandidates.Add(i);
                        } else if (biggestShortestDistance == shortestDistances[i])
                        {
                            biggestShortestDistanceCandidates.Add(i);
                        }

                    }

                    if(biggestShortestDistanceCandidates.Count == 1)
                    {
                        filled[x, y] = true;
                        matrix[x, y] = biggestShortestDistanceCandidates[0];
                        usages[biggestShortestDistanceCandidates[0]]++;
                    } else
                    {
                        List<int> finalCandidates = new List<int>();
                        int lowestUsageCount = int.MaxValue;
                        for(int i = 0; i < biggestShortestDistanceCandidates.Count; i++)
                        {
                            if (usages[biggestShortestDistanceCandidates[i]] < lowestUsageCount)
                            {
                                finalCandidates.Clear();
                                finalCandidates.Add(biggestShortestDistanceCandidates[i]);
                            } else if (usages[biggestShortestDistanceCandidates[i]] == lowestUsageCount)
                            {
                                finalCandidates.Add(biggestShortestDistanceCandidates[i]);
                            }
                        }

                        if (finalCandidates.Count == 1)
                        {
                            filled[x, y] = true;
                            matrix[x, y] = finalCandidates[0];
                            usages[finalCandidates[0]]++;
                        }
                        else
                        {
                            int pickedOne = rnd.NextInt(0,finalCandidates.Count);
                            filled[x, y] = true;
                            matrix[x, y] = finalCandidates[pickedOne];
                            usages[finalCandidates[pickedOne]]++;
                        }

                    }

                }
            }



            return matrix;
        }


        static Regex badShaderNameChar = new Regex(@"[^-_\w\d:\\\/]",RegexOptions.Compiled|RegexOptions.IgnoreCase);
        static string fixUpShaderName(string shaderName)
        {
            return badShaderNameChar.Replace(shaderName, "_").ToLower();
        }

    }
}
