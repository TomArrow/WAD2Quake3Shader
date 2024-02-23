using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Litdex.Random.PRNG;

namespace WAD2Q3SharedStuff
{
    [Flags]
    public enum TextureType
    {
        Normal = (1 << 0),
        Transparent = (1 << 1),
        WaterFluid = (1 << 2),
        Toggling = (1 << 3),
        RandomTiling = (1 << 4),
        LightEmitting = (1 << 5),
        DecalDarken = (1 << 6),
        DecalBrighten = (1 << 7),
        Scroll = (1 << 8),
        Lava = (1 << 9),
        Slime = (1 << 10),
        PseudoWater = (1 << 11),
        PseudoLava = (1 << 12),
        PseudoSlime = (1 << 13),
        Additive = (1 << 14),
        Fullbright = (1 << 15),
        Chrome = (1 << 16),
    }


    public static class SharedStuff
    {
        public static int[,] generateUniqueMatrix(int variations)
        {
            int sideMultiplier = 1;
            while (sideMultiplier < variations)
            {
                sideMultiplier *= 2;
            }
            int[,] matrix = new int[sideMultiplier, sideMultiplier];
            bool[,] filled = new bool[sideMultiplier, sideMultiplier];

            int[] usages = new int[variations];

            var rnd = new Shishua(new ulong[] { 1, 2, 3, 4 });

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
                                if (filled[x2, y2] && matrix[x2, y2] == i)
                                {
                                    int smolX = Math.Min(x, x2);
                                    int bigX = Math.Max(x, x2);
                                    int smolY = Math.Min(y, y2);
                                    int bigY = Math.Max(y, y2);
                                    float shortestXDist = Math.Min(bigX - smolX, sideMultiplier - bigX + smolX);
                                    float shortestYDist = Math.Min(bigY - smolY, sideMultiplier - bigY + smolY);
                                    float shortestDistance = (float)Math.Sqrt(shortestXDist * shortestXDist + shortestYDist * shortestYDist);
                                    if (shortestDistances[i] > shortestDistance)
                                    {
                                        shortestDistances[i] = shortestDistance;
                                    }
                                }
                            }
                        }

                        if (biggestShortestDistance < shortestDistances[i])
                        {
                            biggestShortestDistance = shortestDistances[i];
                            biggestShortestDistanceCandidates.Clear();
                            biggestShortestDistanceCandidates.Add(i);
                        }
                        else if (biggestShortestDistance == shortestDistances[i])
                        {
                            biggestShortestDistanceCandidates.Add(i);
                        }

                    }

                    if (biggestShortestDistanceCandidates.Count == 1)
                    {
                        filled[x, y] = true;
                        matrix[x, y] = biggestShortestDistanceCandidates[0];
                        usages[biggestShortestDistanceCandidates[0]]++;
                    }
                    else
                    {
                        List<int> finalCandidates = new List<int>();
                        int lowestUsageCount = int.MaxValue;
                        for (int i = 0; i < biggestShortestDistanceCandidates.Count; i++)
                        {
                            if (usages[biggestShortestDistanceCandidates[i]] < lowestUsageCount)
                            {
                                finalCandidates.Clear();
                                finalCandidates.Add(biggestShortestDistanceCandidates[i]);
                            }
                            else if (usages[biggestShortestDistanceCandidates[i]] == lowestUsageCount)
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
                            int pickedOne = rnd.NextInt(0, finalCandidates.Count);
                            filled[x, y] = true;
                            matrix[x, y] = finalCandidates[pickedOne];
                            usages[finalCandidates[pickedOne]]++;
                        }

                    }

                }
            }



            return matrix;
        }

        public static void ResizeImage(ByteImage original, ByteImage result) // Really ugly bilinear implementation, probably unperformant af. But works. Shrug.
        {
            bool scaleY = original.height != result.height;
            bool scaleX = original.width != result.width;

            double smolRatioX = (double)(original.width - 1) / (double)(result.width - 1);
            double smolRatioY = (double)(original.height - 1) / (double)(result.height - 1);

            for (int x = 0; x < result.width; x++)
            {
                for (int y = 0; y < result.height; y++)
                {
                    double sourceX = (double)x * smolRatioX;
                    double sourceY = (double)y * smolRatioY;

                    int sourceXLow = scaleX ? (int)(Math.Floor(sourceX) + 0.5) : x;
                    int sourceXHigh = scaleX ? (int)(Math.Ceiling(sourceX) + 0.5) : x;
                    int sourceYLow = scaleY ? (int)(Math.Floor(sourceY) + 0.5) : y;
                    int sourceYHigh = scaleY ? (int)(Math.Ceiling(sourceY) + 0.5) : y;

                    double sourceXLowWeight = 1.0 - sourceX + (double)sourceXLow;
                    double sourceXHighWeight = 1.0 - (double)sourceXHigh + sourceX;
                    double sourceYLowWeight = 1.0 - sourceY + (double)sourceYLow;
                    double sourceYHighWeight = 1.0 - (double)sourceYHigh + sourceY;

                    Vector4 topLeft = new Vector4()
                    {
                        X = original.imageData[sourceYLow * original.stride + sourceXLow * 4],
                        Y = original.imageData[sourceYLow * original.stride + sourceXLow * 4 + 1],
                        Z = original.imageData[sourceYLow * original.stride + sourceXLow * 4 + 2],
                        W = original.imageData[sourceYLow * original.stride + sourceXLow * 4 + 3],
                    };
                    Vector4 topRight = scaleX ? new Vector4()
                    {
                        X = original.imageData[sourceYLow * original.stride + sourceXHigh * 4],
                        Y = original.imageData[sourceYLow * original.stride + sourceXHigh * 4 + 1],
                        Z = original.imageData[sourceYLow * original.stride + sourceXHigh * 4 + 2],
                        W = original.imageData[sourceYLow * original.stride + sourceXHigh * 4 + 3],
                    } : new Vector4();
                    Vector4 bottomLeft = scaleY ? new Vector4()
                    {
                        X = original.imageData[sourceYHigh * original.stride + sourceXLow * 4],
                        Y = original.imageData[sourceYHigh * original.stride + sourceXLow * 4 + 1],
                        Z = original.imageData[sourceYHigh * original.stride + sourceXLow * 4 + 2],
                        W = original.imageData[sourceYHigh * original.stride + sourceXLow * 4 + 3],
                    } : new Vector4();
                    Vector4 bottomRight = (scaleX && scaleY) ? new Vector4()
                    {
                        X = original.imageData[sourceYHigh * original.stride + sourceXHigh * 4],
                        Y = original.imageData[sourceYHigh * original.stride + sourceXHigh * 4 + 1],
                        Z = original.imageData[sourceYHigh * original.stride + sourceXHigh * 4 + 2],
                        W = original.imageData[sourceYHigh * original.stride + sourceXHigh * 4 + 3],
                    } : new Vector4();

                    Vector4 finalValue = new Vector4();
                    if (scaleY && scaleX)
                    {
                        Vector4 top = (topLeft * (float)sourceXLowWeight + topRight * (float)sourceXHighWeight) / (float)(sourceXLowWeight + sourceXHighWeight);
                        Vector4 bottom = (bottomLeft * (float)sourceXLowWeight + bottomRight * (float)sourceXHighWeight) / (float)(sourceXLowWeight + sourceXHighWeight);
                        finalValue = (top * (float)sourceYLowWeight + bottom * (float)sourceYHighWeight) / (float)(sourceYLowWeight + sourceYHighWeight);
                    }
                    else if (scaleY)
                    {
                        finalValue = (topLeft * (float)sourceYLowWeight + bottomLeft * (float)sourceYHighWeight) / (float)(sourceYLowWeight + sourceYHighWeight);
                    }
                    else if (scaleX)
                    {
                        finalValue = (topLeft * (float)sourceXLowWeight + topRight * (float)sourceXHighWeight) / (float)(sourceXLowWeight + sourceXHighWeight);
                    }


                    result.imageData[y * result.stride + x * 4] = (byte)Math.Clamp((int)(Math.Round(finalValue.X) + 0.5), 0, 255);
                    result.imageData[y * result.stride + x * 4 + 1] = (byte)Math.Clamp((int)(Math.Round(finalValue.Y) + 0.5), 0, 255);
                    result.imageData[y * result.stride + x * 4 + 2] = (byte)Math.Clamp((int)(Math.Round(finalValue.Z) + 0.5), 0, 255);
                    result.imageData[y * result.stride + x * 4 + 3] = (byte)Math.Clamp((int)(Math.Round(finalValue.W) + 0.5), 0, 255);
                }
            }
        }

        class StageProperties
        {
            public string blendFunc = "";
            public string rgbGen = "";
            public string tcMod = "";
        }

        // TODO For any shader that gets deformvertexes, make a version without it. It's not always desirable (3d blocks of water streaming down for example).
        public static (bool, string) MakeShader(TextureType type, string shaderName, string mapString, bool resized, Vector4? radIntensity)
        {
            StringBuilder shaderString = new StringBuilder();
            bool shaderWritten = false;
            if ((type & TextureType.WaterFluid) > 0 || (type & TextureType.Transparent) > 0 || (type & TextureType.DecalDarken) > 0 || (type & TextureType.DecalBrighten) > 0 
                || (type & TextureType.LightEmitting) > 0 || (type & TextureType.Scroll) > 0 || (type & TextureType.Toggling) > 0 || (type & TextureType.Lava) > 0 || (type & TextureType.Slime) > 0
                || (type & TextureType.PseudoWater) > 0   || (type & TextureType.PseudoLava) > 0   || (type & TextureType.PseudoSlime) > 0   || (type & TextureType.Additive) > 0   || (type & TextureType.Fullbright) > 0  || (type & TextureType.Chrome) > 0)
            {
                shaderWritten = true;

                StringBuilder lightmapStage = new StringBuilder();
                StringBuilder mainMapStage = new StringBuilder();


                lightmapStage.Append($"\n\t{{");
                mainMapStage.Append($"\n\t{{");

                lightmapStage.Append($"\n\t\tmap $lightmap");
                mainMapStage.Append($"\n\t\t{mapString}");

                int surfaceLightIntensityOverride = 0;

                bool hasLightMapStage = true;
                bool lightMapStageComesFirst = true;

                StageProperties mainStageProps = new StageProperties();
                StageProperties lightStageProps = new StageProperties();

                mainStageProps.rgbGen = $"\n\t\trgbGen identity";
                lightStageProps.rgbGen = $"\n\t\trgbGen identity";

                shaderString.Append($"\n{shaderName}\n{{");
                if (resized)
                {
                    shaderString.Append($"\n\tqer_editorimage {shaderName}_npot");
                }
                else
                {
                    shaderString.Append($"\n\tqer_editorimage {shaderName}");
                }

                if ((type & TextureType.Additive) > 0 ||(type & TextureType.DecalBrighten) > 0 || (type & TextureType.DecalDarken) > 0 || (type & TextureType.LightEmitting) > 0 || (type & TextureType.Fullbright) > 0 || (type & TextureType.Lava) > 0)
                {
                    hasLightMapStage = false;
                }
                if ((type & TextureType.Transparent) > 0)
                {
                    lightMapStageComesFirst = false;
                }

                if (hasLightMapStage)
                {
                    if (lightMapStageComesFirst)
                    {
                        mainStageProps.blendFunc = $"\n\t\tblendFunc filter";
                    }
                    else
                    {
                        lightStageProps.blendFunc = $"\n\t\tblendFunc filter";
                    }
                }

                if ((type & TextureType.WaterFluid) > 0)
                {
                    if ((type & TextureType.PseudoWater) == 0)
                    {
                        shaderString.Append($"\n\tsurfaceparm nonsolid");
                        shaderString.Append($"\n\tsurfaceparm nonopaque");
                        shaderString.Append($"\n\tsurfaceparm water");
                        shaderString.Append($"\n\tsurfaceparm trans");
                        shaderString.Append($"\n\tsort seeThrough");
                        shaderString.Append($"\n\tq3map_material Water");
                        shaderString.Append($"\n\tqer_trans 0.5");
                        mainStageProps.blendFunc = $"\n\t\tblendFunc GL_SRC_ALPHA GL_ONE_MINUS_SRC_ALPHA";
                        mainMapStage.Append($"\n\t\talphaGen const 0.5");
                        lightStageProps.blendFunc = $"\n\t\tblendFunc GL_DST_COLOR GL_ZERO";
                    }
                    else
                    {
                        mainStageProps.blendFunc = $"\n\t\tblendFunc filter";
                        lightStageProps.blendFunc = $"\n\t\tblendFunc GL_ONE GL_ZERO";
                    }

                    shaderString.Append($"\n\ttessSize 100");
                    shaderString.Append($"\n\tdeformvertexes wave 100 sin 0 2.5 0 0.5");

                    mainStageProps.tcMod += $"\n\t\ttcMod turb 0 0.05 0 0.2";
                    //mainMapStage.Append($"\n\t\ttcMod turb 0 0.05 0 0.2");
                    //mainStageProps.rgbGen = $"\n\t\trgbGen identity";

                    //lightStageProps.rgbGen = $"\n\t\trgbGen identity";
                }
                if ((type & TextureType.Slime) > 0)
                {
                    if ((type & TextureType.PseudoSlime) == 0)
                    {
                        shaderString.Append($"\n\tsurfaceparm nonsolid");
                        shaderString.Append($"\n\tsurfaceparm nonopaque");
                        shaderString.Append($"\n\tsurfaceparm water");
                        shaderString.Append($"\n\tsurfaceparm slime");
                        shaderString.Append($"\n\tsurfaceparm trans");
                        shaderString.Append($"\n\tsort seeThrough");
                        shaderString.Append($"\n\tqer_trans 0.5");
                        mainStageProps.blendFunc = $"\n\t\tblendFunc GL_SRC_ALPHA GL_ONE_MINUS_SRC_ALPHA";
                        mainMapStage.Append($"\n\t\talphaGen const 0.8");
                        lightStageProps.blendFunc = $"\n\t\tblendFunc GL_DST_COLOR GL_ZERO";
                    }
                    else
                    {
                        mainStageProps.blendFunc = $"\n\t\tblendFunc filter";
                        lightStageProps.blendFunc = $"\n\t\tblendFunc GL_ONE GL_ZERO";
                    }

                    shaderString.Append($"\n\ttessSize 100");
                    shaderString.Append($"\n\tdeformvertexes wave 100 sin 0 2.5 0 0.25");

                    mainStageProps.tcMod += $"\n\t\ttcMod turb 0 0.25 0 0.1";
                    //mainMapStage.Append($"\n\t\ttcMod turb 0 0.05 0 0.2");
                    //mainStageProps.rgbGen = $"\n\t\trgbGen identity";

                    //lightStageProps.rgbGen = $"\n\t\trgbGen identity";
                }
                if ((type & TextureType.Lava) > 0)
                {
                    type |= TextureType.LightEmitting; // Should already be the case but whatever, let's be safe.
                    if ((type & TextureType.PseudoLava) == 0)
                    {
                        // Pseudo lava doesn't have lava properties but looks like lava
                        shaderString.Append($"\n\tsurfaceparm lava");
                        shaderString.Append($"\n\tsurfaceparm noimpact");
                        shaderString.Append($"\n\tsurfaceparm nonsolid");
                    }
                    shaderString.Append($"\n\tsurfaceparm nomarks");
                    shaderString.Append($"\n\ttessSize 100");
                    shaderString.Append($"\n\tdeformvertexes wave 100 sin 0 2.5 0 0.25");

                    surfaceLightIntensityOverride = 3000;

                    mainStageProps.blendFunc = $"\n\t\tblendFunc GL_ONE GL_ZERO";
                    mainStageProps.tcMod += $"\n\t\ttcMod turb 0 0.25 0 0.1";
                }
                if ((type & TextureType.Toggling) > 0)
                {

                    //mainStageProps.rgbGen = $"\n\t\trgbGen identity";

                    //lightStageProps.rgbGen = $"\n\t\trgbGen identity";
                    //lightStageProps.blendFunc = $"\n\t\tblendFunc filter";

                }
                if ((type & TextureType.Transparent) > 0)
                {
                    shaderString.Append($"\n\tsurfaceparm alphashadow");
                    shaderString.Append($"\n\tsurfaceparm nonopaque");
                    shaderString.Append($"\n\tsurfaceparm trans");
                    shaderString.Append($"\n\tcull none");
                    shaderString.Append($"\n\tqer_trans 0.5");


                    mainStageProps.blendFunc = $"\n\t\tblendFunc GL_SRC_ALPHA GL_ONE_MINUS_SRC_ALPHA";
                    mainMapStage.Append($"\n\t\tdepthWrite");
                    mainMapStage.Append($"\n\t\talphaFunc GE128");
                    //mainStageProps.rgbGen = $"\n\t\trgbGen identity";

                    //lightStageProps.blendFunc = $"\n\t\tblendFunc filter";
                    lightmapStage.Append($"\n\t\tdepthFunc equal");
                    //lightStageProps.rgbGen = $"\n\t\trgbGen identity";
                }
                if ((type & TextureType.DecalDarken) > 0 || (type & TextureType.DecalBrighten) > 0 || (type & TextureType.Additive) > 0)
                {
                    shaderString.Append($"\n\tpolygonOffset");
                    shaderString.Append($"\n\tq3map_nolightmap");
                    shaderString.Append($"\n\tqer_trans 0.5");
                }
                if ((type & TextureType.DecalDarken) > 0)
                {
                    //mainStageProps.rgbGen = $"\n\t\trgbGen identity";
                    mainStageProps.blendFunc = $"\n\t\tblendFunc filter";
                }
                if ((type & TextureType.DecalBrighten) > 0 || (type & TextureType.Additive) > 0)
                {
                    mainStageProps.blendFunc = $"\n\t\tblendFunc GL_ONE GL_ONE";
                    // mainStageProps.rgbGen = $"\n\t\trgbGen identity";
                }
                if ((type & TextureType.LightEmitting) > 0)
                {
                    if (radIntensity != null)
                    {
                        shaderString.Append($"\n\tq3map_lightRGB ");
                        shaderString.Append(radIntensity.Value.X.ToString("0.###"));
                        shaderString.Append(" ");
                        shaderString.Append(radIntensity.Value.Y.ToString("0.###"));
                        shaderString.Append(" ");
                        shaderString.Append(radIntensity.Value.Z.ToString("0.###"));
                        shaderString.Append($"\n\tq3map_surfacelight ");
                        shaderString.Append(radIntensity.Value.W.ToString("0.###"));
                    }
                    else if (surfaceLightIntensityOverride != 0)
                    {

                        shaderString.Append($"\n\tq3map_surfacelight {surfaceLightIntensityOverride}");
                    }
                    else
                    {
                        shaderString.Append($"\n\tq3map_surfacelight 500");
                    }
                    shaderString.Append($"\n\tq3map_lightsubdivide 64");
                    shaderString.Append($"\n\tq3map_nolightmap");

                    mainStageProps.rgbGen = $"\n\t\trgbGen const ( 0.7 0.7 0.7 )";
                }
                if ((type & TextureType.Fullbright) > 0 && (type & TextureType.LightEmitting) == 0)
                {
                    
                    shaderString.Append($"\n\tq3map_nolightmap");

                }

                if ((type & TextureType.Scroll) > 0)
                {
                    mainStageProps.tcMod += $"\n\t\ttcMod scroll -0.5 0";
                }

                mainMapStage.Append(mainStageProps.blendFunc);
                mainMapStage.Append(mainStageProps.rgbGen);
                mainMapStage.Append(mainStageProps.tcMod);

                lightmapStage.Append(lightStageProps.blendFunc);
                lightmapStage.Append(lightStageProps.rgbGen);
                lightmapStage.Append(lightStageProps.tcMod);


                lightmapStage.Append($"\n\t}}");
                mainMapStage.Append($"\n\t}}");


                if (hasLightMapStage && lightMapStageComesFirst)
                {
                    shaderString.Append(lightmapStage.ToString());
                }

                shaderString.Append(mainMapStage.ToString());

                if (hasLightMapStage && !lightMapStageComesFirst)
                {
                    shaderString.Append(lightmapStage.ToString());
                }



                if ((type & TextureType.LightEmitting) > 0)
                {

                    shaderString.Append($"\n\t{{");
                    //shaderString.Append($"\n\t\tmap {texturePath}");
                    shaderString.Append($"\n\t\t{mapString}");
                    shaderString.Append($"\n\t\tblendFunc GL_ONE GL_ONE");
                    shaderString.Append($"\n\t\trgbGen const ( 0.3 0.3 0.3 )");
                    shaderString.Append(mainStageProps.tcMod);
                    shaderString.Append($"\n\t\tglow");
                    shaderString.Append($"\n\t}}");
                }

                if ((type & TextureType.WaterFluid) > 0)
                {
                    shaderString.Append($"\n\t// Uncomment for some reflection?");
                    shaderString.Append($"\n\t//{{");
                    shaderString.Append($"\n\t//\tmap textures/random_environment_maybe");
                    shaderString.Append($"\n\t//\tblendFunc GL_ONE GL_ONE");
                    shaderString.Append($"\n\t//\ttcGen environment");
                    shaderString.Append($"\n\t//\ttcMod turb 0 0.05 0 0.2");
                    shaderString.Append($"\n\t//}}");
                }
                if ((type & TextureType.Chrome) > 0)
                {
                    shaderString.Append($"\n\t// Uncomment for some reflection?");
                    shaderString.Append($"\n\t//{{");
                    shaderString.Append($"\n\t//\tmap textures/random_environment_maybe");
                    shaderString.Append($"\n\t//\tblendFunc GL_ONE GL_ONE");
                    shaderString.Append($"\n\t//\ttcGen environment");
                    shaderString.Append($"\n\t//}}");
                }


                shaderString.Append($"\n}}\n");
            }

            //bool resizedIsMain = false;

            bool onlyPOT = false;

            if (!shaderWritten && resized /*&& type != TextureType.Toggling && type != TextureType.RandomTiling*/) // Toggling/RandomTiling are handled elsewhere
            {
                shaderString.Append($"\n{shaderName}:q3map\n{{");
                shaderString.Append($"\n\tqer_editorimage {shaderName}_npot");
                shaderString.Append($"\n\t{{");
                shaderString.Append($"\n\t\tmap $lightmap");
                shaderString.Append($"\n\t\trgbGen identity");
                shaderString.Append($"\n\t}}");

                shaderString.Append($"\n\t{{");
                //shaderString.Append($"\n\t\tmap {shaderName}");
                shaderString.Append($"\n\t\t{mapString}");
                shaderString.Append($"\n\t\trgbGen identity");
                shaderString.Append($"\n\t\tblendFunc filter");
                shaderString.Append($"\n\t}}");
                shaderString.Append($"\n}}\n");
                onlyPOT = true;
            }

            return (onlyPOT, shaderString.Length > 0 ? shaderString.ToString() : null);
        }



        public static ByteImage GoldSrcImgToByteImage(int width, int height, byte[] mip0PaletteOffsets, byte[] palette, bool transparency, bool font)
        {
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
                        image.imageData[image.stride * y + x * 4 + 2] = palette[paletteOffset * 3];
                        image.imageData[image.stride * y + x * 4 + 1] = palette[paletteOffset * 3 + 1];
                        image.imageData[image.stride * y + x * 4] = palette[paletteOffset * 3 + 2];
                    }
                    else
                    {
                        image.imageData[image.stride * y + x * 4] = (byte)paletteOffset;
                        image.imageData[image.stride * y + x * 4 + 1] = (byte)paletteOffset;
                        image.imageData[image.stride * y + x * 4 + 2] = (byte)paletteOffset;
                    }
                    image.imageData[image.stride * y + x * 4 + 3] = (transparency && paletteOffset == 255) ? (byte)0 : (byte)255;
                    if (font && paletteOffset == 0)
                    {
                        // Special font thing? Is this correct? idk
                        image.imageData[image.stride * y + x * 4 + 3] = 0;
                        transparentPixelsFound = true;
                    }
                    if (paletteOffset == 255)
                    {
                        transparentPixelsFound = true;
                    }
                }
            }

            return image;
        }

        static Regex pseudoLavaRegex = new Regex(@"^l+a+v+a+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex pseudoSlimeRegex = new Regex(@"^s+l+i+m+e+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex pseudoWaterRegex = new Regex(@"^w+a+t+e+r+", RegexOptions.IgnoreCase | RegexOptions.Compiled);


        public static (TextureType,int) TextureTypeFromTextureName(string textureName)
        {
            TextureType type = 0;
            int nameStartIndex = 0;
            bool specialMatchFoudn = true;
            int specialMatchCount = 0;
            bool onlyNameSearch = false;
            while (specialMatchFoudn && (textureName.Length - nameStartIndex) > 0)
            {
                if (textureName[nameStartIndex] == '_')
                {
                    nameStartIndex++;
                    onlyNameSearch = true;
                }
                specialMatchFoudn = true;
                if (!onlyNameSearch)
                {
                    switch (textureName[nameStartIndex])
                    {
                        case '{':
                            type |= TextureType.Transparent;
                            break;
                        case '!':
                            type |= TextureType.WaterFluid;
                            break;
                        case '+':
                            type |= TextureType.Toggling;
                            nameStartIndex++;
                            break;
                        case '-':
                            type |= TextureType.RandomTiling;
                            nameStartIndex++;
                            break;
                        case '~':
                            type |= TextureType.LightEmitting;
                            break;
                        default:
                            specialMatchFoudn = false;
                            break;
                    }
                }
                else
                {
                    specialMatchFoudn = false;
                }

                if (!specialMatchFoudn)
                {
                    Match match = null;
                    string textureHere = textureName.Substring(nameStartIndex);
                    if (textureHere.StartsWith("water", StringComparison.InvariantCultureIgnoreCase))
                    {
                        type |= TextureType.WaterFluid;
                        nameStartIndex += "water".Length - 1;
                        specialMatchFoudn = true;
                    }
                    else if (textureHere.StartsWith("scroll", StringComparison.InvariantCultureIgnoreCase))
                    {
                        type |= TextureType.Scroll;
                        nameStartIndex += "scroll".Length - 1;
                        specialMatchFoudn = true;
                    }
                    else if (textureHere.StartsWith("lava", StringComparison.InvariantCultureIgnoreCase))
                    {
                        type |= TextureType.Lava | TextureType.LightEmitting;
                        nameStartIndex += "lava".Length - 1;
                        specialMatchFoudn = true;
                    }
                    else if (textureHere.StartsWith("slime", StringComparison.InvariantCultureIgnoreCase))
                    {
                        type |= TextureType.Slime;
                        nameStartIndex += "slime".Length - 1;
                        specialMatchFoudn = true;
                    }
                    else if ((match = pseudoWaterRegex.Match(textureHere)).Success)
                    {
                        type |= TextureType.WaterFluid | TextureType.PseudoWater;
                        nameStartIndex += match.Value.Length - 1;
                        specialMatchFoudn = true;
                    }
                    else if ((match = pseudoLavaRegex.Match(textureHere)).Success)
                    {
                        type |= TextureType.Lava | TextureType.PseudoLava | TextureType.LightEmitting;
                        nameStartIndex += match.Value.Length - 1;
                        specialMatchFoudn = true;
                    }
                    else if ((match = pseudoSlimeRegex.Match(textureHere)).Success)
                    {
                        type |= TextureType.Slime | TextureType.PseudoSlime;
                        nameStartIndex += match.Value.Length - 1;
                        specialMatchFoudn = true;
                    }
                }
                if (specialMatchFoudn)
                {
                    nameStartIndex++;
                    specialMatchCount++;
                }
            }
            return (type,specialMatchCount);
        }

        static Regex badShaderNameChar = new Regex(@"[^-_\w\d:\\\/]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static string fixUpShaderName(string shaderName)
        {
            return badShaderNameChar.Replace(shaderName, "_").ToLower();
        }


        static Regex radRegex = new Regex(@"(?:[\r\n]|^)\s*(?<texName>[^\s]+)\s*(?<r>[E\d\.\-\+]+)\s+(?<g>[E\d\.\-\+]+)\s+(?<b>[E\d\.\-\+]+)\s+(?<intensity>[E\d\.\-\+]+)", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        public static Dictionary<string, Vector4> getRadIntensities(string folder)
        {
            Dictionary<string, Vector4> radIntensities = new Dictionary<string, Vector4>(StringComparer.InvariantCultureIgnoreCase);

            string[] rads = Directory.GetFiles(folder, "*.rad");
            if (rads != null && rads.Length > 0)
            {
                foreach (string rad in rads)
                {
                    string radContent = File.ReadAllText(rad);
                    MatchCollection radMatches = radRegex.Matches(radContent);
                    foreach (Match radMatch in radMatches)
                    {
                        string texName = radMatch.Groups["texName"].Value;
                        Vector4 info = new Vector4()
                        {
                            X = float.Parse(radMatch.Groups["r"].Value) / 255.0f,
                            Y = float.Parse(radMatch.Groups["g"].Value) / 255.0f,
                            Z = float.Parse(radMatch.Groups["b"].Value) / 255.0f,
                            W = float.Parse(radMatch.Groups["intensity"].Value) / 5.0f, // Dumb guess :)
                        };
                        radIntensities[texName] = info;
                    }
                }
            }
            return radIntensities;
        }

    }
}
