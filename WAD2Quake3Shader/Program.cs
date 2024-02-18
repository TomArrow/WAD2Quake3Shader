using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using Crews.Utility.TgaSharp;
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
        Decal,
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

            StringBuilder logString = new StringBuilder(); 

            Wad wad = WadParser.Parse(args[0]);

            foreach (Lump lump in wad.Lumps)
            {
                if(lump.LumpType != 0x40 && lump.LumpType != 0x42 && lump.LumpType != 0x43 && lump.LumpType != 0x46)
                {
                    Console.WriteLine($"Lump '{lump.Name}' is not of supported type, but type {lump.LumpType}");
                    continue;
                }

                LumpType lumpType = (LumpType)lump.LumpType;

                using (MemoryStream ms = new MemoryStream(lump.Bytes))
                {
                    using(BinaryReader br = new BinaryReader(ms))
                    {
                        string textureName = lump.Name;
                        if (lump.LumpType == 0x40 || lump.LumpType == 0x43) {
                            textureName = Encoding.Latin1.GetString(br.ReadBytes(16)).TrimEnd('\0');
                            if(lump.Name != textureName)
                            {
                                Console.WriteLine($"Lump/texture name mismatch: Lump '{lump.Name}', texture name '{textureName}'");
                                textureName = lump.Name; // Weird shit.
                            }
                        }
                        int width = 128;
                        int height = 128;
                        if(lump.LumpType != 0x46)
                        {
                            width = br.ReadInt32();
                            height = br.ReadInt32();
                        }
                        if(lump.LumpType == 0x46)
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
                        byte[] mip0PaletteOffsets = br.ReadBytes(width*height);

                        if (lump.LumpType == 0x40 || lump.LumpType == 0x43)
                        {
                            byte[] mip1PaletteOffsets = br.ReadBytes(width * height /4);
                            byte[] mip2PaletteOffsets = br.ReadBytes(width * height /16);
                            byte[] mip3PaletteOffsets = br.ReadBytes(width * height /64);
                        }

                        byte[] randomShit = br.ReadBytes(2);

                        byte[] palette = null;

                        if (lump.LumpType == 0x42 || lump.LumpType == 0x43)
                        {
                            palette = br.ReadBytes(768);
                        }

                        TextureType type = TextureType.Normal;
                        if(textureName.Length > 0)
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


                        Bitmap imageBmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                        ByteImage image = Helpers.BitmapToByteArray(imageBmp);
                        imageBmp.Dispose();

                        /*int maxPaletteOffset = 0;
                        for (int i=0;i< mip0PaletteOffsets.Length; i++)
                        {
                            if (mip0PaletteOffsets[i] > maxPaletteOffset)
                            {
                                maxPaletteOffset = mip0PaletteOffsets[i];
                            }
                        }*/

                        bool transparentPixelsFound = false;

                        for(int y= 0; y < height; y++)
                        {
                            for(int x = 0; x < width; x++)
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

                        if (!transparentPixelsFound && type == TextureType.Transparent) // Probably a decal.
                        {
                            type = TextureType.Decal; 
                        }


                        imageBmp = Helpers.ByteArrayToBitmap(image);
                        //imageBmp.Save($"{textureName}.tga");

                        TGA myTGA = new TGA(imageBmp);
                        Directory.CreateDirectory("textures");
                        myTGA.Save($"textures/{textureName}.tga");
                        imageBmp.Dispose();

                        logString.Append($"{lumpType} : {type} : {lump.Name}\n");
                        Console.WriteLine(lump.Name);
                        Console.WriteLine(type);
                        Console.WriteLine(lumpType);
                        //Console.WriteLine(maxPaletteOffset);
                    }
                }

            }

            File.AppendAllText("wadConvert.log", logString.ToString());

            return 0;
        }
    }
}
