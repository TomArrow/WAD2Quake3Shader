﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Crews.Utility.TgaSharp;
using Litdex.Random.PRNG;
using Sledge.Formats;
using Sledge.Formats.Model.Goldsource;
//using nz.doom.WadParser;
using WAD2Q3SharedStuff;

namespace MDL2Quake3OBJ_NET
{
    class Program
    {
        static Regex mtlMaterialNamesRegex = new Regex(@"(?<start>(?:\n|^)\s*map_kd\s+)(?<texName>[^\s]+)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        static int Main(string[] args)
        {
            if (args == null || args.Length != 1)
            {
                Console.Error.WriteLine("MDL file required");
                return 2;
            }

            Dictionary<string, Vector4> radIntensities = SharedStuff.getRadIntensities("../");

            if (args[0] != "*")
            {
                ConvertMDL(args[0], radIntensities);
            }
            else
            {
                string[] wads = Directory.GetFiles(".", "*.mdl");
                foreach (string wad in wads)
                {
                    ConvertMDL(wad, radIntensities);
                }
            }

            return 0;
        }

        public static void ConvertMDL(string filename, Dictionary<string, Vector4> radIntensities)
        {


            string filePathFull = Path.GetFullPath(filename);
            MdlFile mdl = MdlFile.FromFile(filePathFull, true); // Need full path here because idk, Sledge is WEIRD and throwing random access exceptions for no fucking reason
            float smallestZ = 0;


            Directory.CreateDirectory("models/mdlConvert");

            string thisFilename = Path.GetFileName(filename);
            string thisFilename_NoExt = Path.GetFileNameWithoutExtension(filename);

            string filePathOutputRelative = Path.Combine("models/mdlConvert",Path.ChangeExtension(Path.GetFileName(filename),".obj"));
            string mtlPath = Path.Combine("models/mdlConvert",Path.ChangeExtension(thisFilename, ".mtl"));

            bool conversionSuccess = false;

            // Do assimp conversion of model itself
            try
            {
                Process assimpProc = new Process();
                assimpProc.StartInfo.RedirectStandardOutput = true;
                assimpProc.StartInfo.RedirectStandardError = true;
                assimpProc.StartInfo.FileName = "assimp";
                assimpProc.StartInfo.Arguments = $"export \"{filePathFull}\" \"{filePathOutputRelative}\"";
                assimpProc.Start();
                assimpProc.WaitForExit();

                Console.WriteLine(assimpProc.StandardOutput.ReadToEnd());
                Console.WriteLine(assimpProc.StandardError.ReadToEnd());
                conversionSuccess = true;
            } catch(Exception ex)
            {
                Console.WriteLine($"Couldn't convert {filename} with assimp. Assimp not found.");
            }


            Dictionary<string,string> textureNames = new Dictionary<string,string>(StringComparer.InvariantCultureIgnoreCase); 
            
            StringBuilder shaderString = new StringBuilder();
            StringBuilder shaderStringPOT = new StringBuilder();

            foreach (Texture tex in mdl.Textures)
            {

                string rawTextureName = tex.Name;
                string textureName = rawTextureName;

                ByteImage image = SharedStuff.GoldSrcImgToByteImage(tex.Width, tex.Height, tex.Data, tex.Palette, (tex.Flags & TextureFlags.Masked) > 0, false);
                (TextureType type, int specialMatchCount) = SharedStuff.TextureTypeFromTextureName(tex.Name);

                Vector4? thisShaderLightIntensity = null;

                if (radIntensities.ContainsKey(textureName))
                {
                    type |= TextureType.LightEmitting;
                    thisShaderLightIntensity = radIntensities[textureName];
                }

                int dotPlace = textureName.LastIndexOf(".");
                if (dotPlace != -1)
                {
                    textureName = textureName.Substring(0,dotPlace);
                }

                if (radIntensities.ContainsKey(textureName))
                {
                    type |= TextureType.LightEmitting;
                    thisShaderLightIntensity = radIntensities[textureName];
                }

                textureName = $"{thisFilename_NoExt}_{textureName}";

                textureName = SharedStuff.fixUpShaderName(textureName);

                textureNames[rawTextureName] = textureName;

                if ((tex.Flags & TextureFlags.Chrome) > 0)
                {
                    type |= TextureType.Chrome; // TODO?
                }
                if((tex.Flags & TextureFlags.Masked) > 0)
                {
                    type |= TextureType.Transparent;
                }
                if((tex.Flags & TextureFlags.Additive) > 0)
                {
                    type |= TextureType.Additive;
                }
                if((tex.Flags & TextureFlags.Fullbright) > 0)
                {
                    type |= TextureType.Fullbright;
                }

                int width = tex.Width;
                int height = tex.Height;

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
                bool resizedIsMain = false;
                if (mustResize)
                {
                    Bitmap imageBmp2 = new Bitmap(potWidth, potHeight, PixelFormat.Format32bppArgb);
                    resizedImage = Helpers.BitmapToByteArray(imageBmp2);
                    imageBmp2.Dispose();
                    SharedStuff.ResizeImage(image, resizedImage);
                    resizedIsMain = true;
                }

                Bitmap imageBmp = Helpers.ByteArrayToBitmap(image);
                //imageBmp.Save($"{textureName}.tga");

                string texturePath = SharedStuff.fixUpShaderName($"models/mdlConvert/{textureName}");


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



                TGA myTGA = new TGA(imageBmp);
                Directory.CreateDirectory("models/mdlConvert");
                string suffix = resizedIsMain ? "_npot" : "";
                //if (type == TextureType.RandomTiling && textureName.Length > 1 && textureName[1] == '0')
                //{
                //    myTGA.Save($"{texturePath}_original{suffix}.tga"); // 0 one is replaced with a tiled version.
                //}
                //else
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

            }


            string mtlContent = null;

            if (conversionSuccess)
            {
                if (File.Exists(mtlPath))
                {
                    mtlContent = File.ReadAllText(mtlPath);

                    bool anyReplacements = false;
                    mtlContent = mtlMaterialNamesRegex.Replace(mtlContent, (Match a) => {
                        string texName = a.Groups["texName"].Value;
                        if (!textureNames.ContainsKey(texName))
                        {
                            return a.Value;
                        }
                        anyReplacements = true;
                        return a.Groups["start"].Value + textureNames[texName];
                    });

                    if (anyReplacements)
                    {
                        File.WriteAllText(mtlPath, mtlContent);
                    }
                } else {
                    Console.WriteLine($"{mtlPath} not found. Oo");
                }

            }





            Directory.CreateDirectory("shaders");
            File.AppendAllText("shaders/mdlConvertShaders.shader", shaderString.ToString());
            File.AppendAllText("shaders/mdlConvertShadersQ3MAP.shader", shaderStringPOT.ToString());
        }


    }
}
