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

namespace SPR2Quake3Shader
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
            if (args == null || args.Length == 0)
            {
                Console.Error.WriteLine("SPR file required");
                return 2;
            }

            bool recursive = false;
            foreach (string arg in args)
            {
                if (arg.Equals("--recursive", StringComparison.InvariantCultureIgnoreCase))
                {
                    recursive = true;
                }
            }

            Dictionary<string, Vector4> radIntensities = SharedStuff.getRadIntensities("../");

            if (args[0] != "*")
            {
                ConvertSPR(args[0], ".", radIntensities);
            }
            else if (recursive)
            {
                string[] possibleWads = SharedStuff.crawlDirectory(".");
                foreach (string possibleWad in possibleWads)
                {
                    if (Path.GetExtension(possibleWad).Equals(".spr", StringComparison.InvariantCultureIgnoreCase))
                    {
                        ConvertSPR(possibleWad, ".", radIntensities);
                    }
                }
            }
            else
            {
                string[] wads = Directory.GetFiles(".", "*.spr");
                foreach (string wad in wads)
                {
                    ConvertSPR(wad, ".", radIntensities);
                }
            }

            return 0;
        }

        static void ConvertSPR(string sprPath, string startPath, Dictionary<string, Vector4> radIntensities)
        {

            string relativePath = Path.GetRelativePath(startPath, sprPath);
            string relativePathWithoutExtension = Path.Combine(Path.GetDirectoryName(relativePath),Path.GetFileNameWithoutExtension(sprPath)).Replace('\\','/');
            if (relativePathWithoutExtension.StartsWith("./"))
            {
                relativePathWithoutExtension = relativePathWithoutExtension.Substring(2);
            }

            byte[] byteData = File.ReadAllBytes(sprPath);

            using (MemoryStream ms = new MemoryStream(byteData))
            using (BinaryReader br = new BinaryReader(ms,Encoding.Latin1))
            {
                string magic = new string(br.ReadChars(4));
                if(magic != "IDSP")
                {
                    Console.WriteLine($"{sprPath} is not a supported sprite file (magic isn't IDSP).");
                    return;
                }
                int version = br.ReadInt32();
                SpriteType spriteType = (SpriteType)br.ReadInt32();
                SpriteTexFormat textureFormat = (SpriteTexFormat)br.ReadInt32();
                float boundingRadius = br.ReadSingle();
                int maxWidth = br.ReadInt32();
                int maxHeight = br.ReadInt32();
                int numFrames = br.ReadInt32();
                float breamLength = br.ReadSingle();
                int syncType = br.ReadInt32();

                UInt16 paletteLength = br.ReadUInt16();
                byte[] palette = br.ReadBytes(paletteLength * 3);

                List<ByteImage> frameImages = new List<ByteImage>();





                string originalFilename = Path.GetFileName(sprPath);
                string originalShaderName = Path.GetFileNameWithoutExtension(sprPath);
                string shaderName = SharedStuff.fixUpShaderName(originalShaderName);
                StringBuilder animMapString = new StringBuilder();
                animMapString.Append("animMap 10");

                (TextureType type, int specialMatchCount) = SharedStuff.TextureTypeFromTextureName(originalShaderName);

                bool isIndexAlpha = false;
                bool mustBeHigherThanWide = false;

                type |= SharedStuff.TextureTypeFromSpriteProperties(textureFormat, spriteType);
                if((type & TextureType.AutoSprite2) > 0)
                {
                    mustBeHigherThanWide = true;
                }
                if(textureFormat == SpriteTexFormat.IndexAlpha)
                {
                    isIndexAlpha = true;
                }
                

                Vector4? thisShaderLightIntensity = null;

                if (radIntensities.ContainsKey(originalShaderName))
                {
                    type |= TextureType.LightEmitting;
                    thisShaderLightIntensity = radIntensities[originalShaderName];
                } else if (radIntensities.ContainsKey(originalFilename))
                {
                    type |= TextureType.LightEmitting;
                    thisShaderLightIntensity = radIntensities[originalFilename];
                }


                for (int i = 0; i < numFrames; i++)
                {
                    _ = br.ReadInt32();
                    int offsetX = br.ReadInt32();
                    int offsetY = br.ReadInt32();
                    int width = br.ReadInt32();
                    int height = br.ReadInt32();

                    int oWidth = width;
                    int oHeight = height;

                    int halfWidth = width / 2;
                    int halfHeight = height / 2;
                    while((halfWidth*2) < width)
                    {
                        halfWidth++;
                    }
                    while((halfHeight * 2) < height)
                    {
                        halfHeight++;
                    }

                    int offsetLeft = offsetX;
                    int offsetTop = offsetY;
                    int offsetRight = width+offsetX;
                    int offsetBottom = offsetY-height;

                    offsetTop = -offsetTop;
                    offsetBottom = -offsetBottom;

                    offsetTop += halfHeight;
                    offsetBottom -= halfHeight;
                    offsetLeft += halfWidth;
                    offsetRight -= halfWidth;

                    int highestXOffset = Math.Max(Math.Abs(offsetLeft), Math.Abs(offsetRight));
                    int highestYOffset = Math.Max(Math.Abs(offsetBottom), Math.Abs(offsetTop));

                    halfHeight += highestYOffset;
                    halfWidth += highestXOffset;

                    width = halfWidth * 2;
                    height = halfHeight * 2;

                    offsetX = highestXOffset + offsetLeft;
                    offsetY = highestYOffset + offsetTop;

                    byte[] imgData = br.ReadBytes(width*height);

                    // Small image containing only this frame
                    ByteImage img = SharedStuff.GoldSrcImgToByteImage(oWidth, oHeight, imgData, palette, textureFormat == SpriteTexFormat.AlphaTest, false, isIndexAlpha);

                    // (Potentially) bigger image that has the image in the proper position while maintaining center alignment.
                    Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppRgb);
                    ByteImage fullImg = Helpers.BitmapToByteArray(bmp);
                    bmp.Dispose();
                    for(int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int fullX = x + offsetX;
                            int fullY = y + offsetY;
                            fullImg.imageData[fullY * fullImg.stride + fullX * 4] = img[y * img.stride + x * 4];
                            fullImg.imageData[fullY * fullImg.stride + fullX * 4 + 1] = img[y * img.stride + x * 4 + 1];
                            fullImg.imageData[fullY * fullImg.stride + fullX * 4 + 2] = img[y * img.stride + x * 4 + 2];
                            fullImg.imageData[fullY * fullImg.stride + fullX * 4 + 3] = img[y * img.stride + x * 4 + 3];
                        }
                    }

                    if(offsetX != 0 || offsetY != 0)
                    {
                        Console.WriteLine("highestXOffset != 0 || highestYOffset != 0");
                    }

                    img = fullImg;

                    frameImages.Add(img);

                    if(width > maxWidth)
                    {
                        maxWidth = width;
                    }
                    if(height > maxHeight)
                    {
                        maxHeight = height;
                    }
                }

                int index = 0;
                bool needResize = false;
                foreach(ByteImage image in frameImages)
                {
                    ByteImage img = image;


                    if (mustBeHigherThanWide && maxWidth >= maxHeight)
                    {
                        int newHeight = maxHeight;
                        while (newHeight <= maxWidth)
                        {
                            newHeight *= 2;
                        }
                        maxHeight = newHeight;
                    }

                    if (img.width != maxWidth || img.height != maxHeight)
                    {
                        // center it in the full size image.
                        int offsetX = (maxWidth - img.width) / 2;
                        int offsetY = (maxHeight - img.height) / 2;

                        
                        Bitmap bmp = new Bitmap(maxWidth, maxHeight, PixelFormat.Format32bppRgb);
                        ByteImage fullImg = Helpers.BitmapToByteArray(bmp);
                        bmp.Dispose();
                        for (int y = 0; y < img.height; y++)
                        {
                            for (int x = 0; x < img.width; x++)
                            {
                                int fullX = x + offsetX;
                                int fullY = y + offsetY;
                                fullImg.imageData[fullY * fullImg.stride + fullX * 4] = img.imageData[y * img.stride + x * 4];
                                fullImg.imageData[fullY * fullImg.stride + fullX * 4 + 1] = img.imageData[y * img.stride + x * 4 + 1];
                                fullImg.imageData[fullY * fullImg.stride + fullX * 4 + 2] = img.imageData[y * img.stride + x * 4 + 2];
                                fullImg.imageData[fullY * fullImg.stride + fullX * 4 + 3] = img.imageData[y * img.stride + x * 4 + 3];
                            }
                        }
                        img = fullImg;
                    }


                    int potWidth = 1;
                    int potHeight = 1;
                    while (potWidth < maxWidth)
                    {
                        potWidth *= 2;
                    }
                    while (potHeight < maxHeight)
                    {
                        potHeight *= 2;
                    }

                    bool mustResize = potWidth != maxWidth || potHeight != maxHeight;

                    ByteImage resizedImage = null;
                    bool resizedIsMain = false;
                    if (mustResize)
                    {
                        Bitmap imageBmp2 = new Bitmap(potWidth, potHeight, PixelFormat.Format32bppArgb);
                        resizedImage = Helpers.BitmapToByteArray(imageBmp2);
                        imageBmp2.Dispose();
                        SharedStuff.ResizeImage(img, resizedImage);
                        resizedIsMain = true;
                        needResize = true;
                    }


                    //string textureName = Path.GetFileNameWithoutExtension(sprPath) + $"_{index}";
                    string textureName = relativePathWithoutExtension + (frameImages.Count > 1 ?  $"_{index}" : "");
                    textureName = SharedStuff.fixUpShaderName(textureName);

                    string texturePath = $"textures/sprConvert/{textureName}";
                    Bitmap imageBmp = Helpers.ByteArrayToBitmap(img);
                    //imageBmp.Save($"{textureName}.tga");

                    animMapString.Append($" {texturePath}");

                    TGA myTGA = new TGA(imageBmp);
                    Directory.CreateDirectory("textures/sprConvert");
                    Directory.CreateDirectory(Path.GetDirectoryName(texturePath));
                    string suffix = resizedIsMain ? "_npot" : "";
                    myTGA.Save($"{texturePath}{suffix}.tga");
                    imageBmp.Dispose();

                    if (resizedImage != null)
                    {
                        imageBmp = Helpers.ByteArrayToBitmap(resizedImage);
                        //imageBmp.Save($"{textureName}.tga");

                        suffix = resizedIsMain ? "" : "_pot";

                        myTGA = new TGA(imageBmp);

                        myTGA.Save($"{texturePath}{suffix}.tga");
                        imageBmp.Dispose();
                    }

                    Console.WriteLine("test");

                    index++;
                }

                StringBuilder shaderString = new StringBuilder();
                StringBuilder shaderStringPOT = new StringBuilder();

                string shaderComment = $"//sprite:type:{(int)spriteType}:texFormat:{(int)textureFormat}:width:{maxWidth}:height:{maxHeight}";

                (bool onlyPOT, string shaderText)  = SharedStuff.MakeShader(type,$"textures/sprConvert/{SharedStuff.fixUpShaderName(relativePathWithoutExtension)}", frameImages.Count > 1 ? animMapString.ToString() :$"map textures/sprConvert/{SharedStuff.fixUpShaderName(relativePathWithoutExtension)}", needResize, thisShaderLightIntensity,null,null,true, shaderComment);
                
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

                Directory.CreateDirectory("shaders");
                File.AppendAllText("shaders/sprConvertShaders.shader", shaderString.ToString());
                File.AppendAllText("shaders/sprConvertShadersQ3MAP.shader", shaderStringPOT.ToString());

                return;
            }










            /*
            StringBuilder logString = new StringBuilder();

            Wad wad = WadParser.Parse(sprPath);



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
                            textureName = textureName.Substring(0, textureName.IndexOf('\0'));
                            if (!lump.Name.Equals(textureName, StringComparison.InvariantCultureIgnoreCase))
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
                        while (potWidth < width)
                        {
                            potWidth *= 2;
                        }
                        while (potHeight < height)
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

                        ByteImage image = SharedStuff.GoldSrcImgToByteImage(width, height, mip0PaletteOffsets, palette, (type & TextureType.Transparent) > 0 || lumpType == LumpType.Default1, lump.LumpType == 0x46);


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
                                has255 = has255 || (palette[i * 3] == 255);
                                has0 = has0 || (palette[i * 3] == 0);
                                lastColor2 = lastColor = palette[i * 3];

                            }
                            if (maybeDecal)
                            {
                                if ((!has255 && !has0) )
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


                        if ((type & TextureType.Toggling) == 0 && (type & TextureType.RandomTiling) == 0) // Those 2 types are handled elsewhere
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

                        if (resizedImage != null)
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
                        }
                        else
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

                (bool onlyPOT, string shaderText) = SharedStuff.MakeShader(groupedTexturesTypes[kvp.Key], baseTexturePath, mapInstruction.ToString(), togglingTexturesNPOT[kvp.Key], radIntensities.ContainsKey(baseName) ? radIntensities[baseName] : null);

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

                for (int x = 0; x < matrix.GetLength(0); x++)
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
                                image.imageData[image.stride * targetY + targetX * 4] = srcImage.imageData[yImg * srcImage.stride + xImg * 4];
                                image.imageData[image.stride * targetY + targetX * 4 + 1] = srcImage.imageData[yImg * srcImage.stride + xImg * 4 + 1];
                                image.imageData[image.stride * targetY + targetX * 4 + 2] = srcImage.imageData[yImg * srcImage.stride + xImg * 4 + 2];
                                image.imageData[image.stride * targetY + targetX * 4 + 3] = srcImage.imageData[yImg * srcImage.stride + xImg * 4 + 3];


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
            File.AppendAllText("shaders/wadConvertShadersQ3MAP.shader", shaderStringPOT.ToString());*/
        }






    }
}
