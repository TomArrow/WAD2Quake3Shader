using PCRE;
using System;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using WAD2Q3SharedStuff;
using System.Collections.Generic;

namespace FilterMapShaderNames
{

    class SpriteInfo {
        public SpriteTexFormat texFormat = SpriteTexFormat.Normal;
        public SpriteType type = SpriteType.Oriented;
        public int width = 0;
        public int height = 0;
        public SpriteInfo(Dictionary<string,string> intoToParse)
        {
            //string shaderComment = $"//sprite:type:{(int)spriteType}:texFormat:{(int)textureFormat}:width:{maxWidth}:height:{maxHeight}";
            if (intoToParse.ContainsKey("width"))
            {
                if(!int.TryParse(intoToParse["width"],out this.width))
                {
                    this.width = 0;
                }
            }
            if (intoToParse.ContainsKey("height"))
            {
                if(!int.TryParse(intoToParse["height"],out this.height))
                {
                    this.height = 0;
                }
            }
            if (intoToParse.ContainsKey("texFormat"))
            {
                int tmp = 0;
                if(int.TryParse(intoToParse["texFormat"],out tmp))
                {
                    this.texFormat = (SpriteTexFormat)tmp;
                }
            }
            if (intoToParse.ContainsKey("type"))
            {
                int tmp = 0;
                if(int.TryParse(intoToParse["type"],out tmp))
                {
                    this.type = (SpriteType)tmp;
                }
            }
        }
    }


    class Program
    {
        static Regex findShader = new Regex(@"(?<vecs>(?:\((?:\s*[-\d\.]+){3}\s*\)\s*){3}\s*)(?<shaderName>.*?)\s*\[", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        //static Regex entitiesParseRegex = new Regex(@"\{(\s*""([^""]+)""[ \t]+""([^""]+)"")+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        static Regex faceEndNumbersParse = new Regex(@"(?<start>\[\s*([-\d\.\+E]+)\s+([-\d\.\+E]+)\s+([-\d\.\+E]+)\s+([-\d\.\+E]+)\s*\]\s*(?:[-\d\.\+E]+\s*){3})(?<extraValues>(?:[-\d\.\+E]+\s*){3})?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static string propsBrushMatcher = @"(?<props>\{(\s*""([^""]+)""[ \t]+""([^""]+)"")+)(?<brushes>(?:\s*\{(?:[^\{\}]++|(?:[\{\}](?!\s))++|(?R))*+(?<=\s)\})*\s+\})";
        static string brushesMatcher = @"\s*(?<brush>(?:^|[\n\r])++\s*+(?<=(?:\s))\{(?:[^\{\}]++|(?:[\{\}](?!\s))++|(?R))*+(?<=\s)\})"; // Always prepend search subject with empty space or first brush may not be found

        static Regex facePointsParseRegex = new Regex(@"(?:^|\n)\s*(?<coordinates>(?<coordvec>\((?<vectorPart>\s*[-\d\.]+){3}\s*\)\s*){3})", RegexOptions.IgnoreCase | RegexOptions.Compiled);


        const int detailFlag = 0x8000000;

        static Regex shaderImageRegex = new Regex(@"\n[^\n]*?editorimage[ \t]+(?<image>[^$][^\s\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static void Main(string[] args)
        {

            // Todo show shader dupes
            int argIndex = 0;
            int folderIndex = 0;
            List<string> mapFiles = new List<string>();
            List<string> shaderDirectories = new List<string>();
            //string shaderDirectory = null;
            bool ignoreShaderList = false;
            while (argIndex < args.Length)
            {
                string argument = args[argIndex++];
                if (argument.Equals("-ignoreShaderList", StringComparison.InvariantCultureIgnoreCase))
                {
                    ignoreShaderList = true;
                }
                else if (argument.EndsWith(".map", StringComparison.InvariantCultureIgnoreCase))
                {
                    mapFiles.Add(argument);
                }
                else
                {
                    int folderType = folderIndex;
                    if (argument.StartsWith("shad:", StringComparison.InvariantCultureIgnoreCase))
                    {
                        argument = argument.Substring("shad:".Length);
                        folderType = 0;
                    }
                    else
                    {
                        folderIndex++;
                    }
                    switch (folderType)
                    {
                        case 0:
                            shaderDirectories.Add(argument);
                            //shaderDirectory = argument;
                            break;
                    }
                }
            }


            Dictionary<string, ShaderDupe> shaderDuplicates = new Dictionary<string, ShaderDupe>(StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string, string> parsedShaders = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string, string> parsedExcludeShaders = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (string shaderDirectory in shaderDirectories)
            {
                List<string> shaderListWhitelist = new List<string>();
                List<string> shadFiles = new List<string>();
                string[] shadFilesHere = SharedStuff.crawlDirectory(shaderDirectory);
                shadFiles.AddRange(shadFilesHere);

                // find shaderlist
                if (!ignoreShaderList)
                {
                    foreach (string file in shadFilesHere)
                    {
                        string basename = Path.GetFileNameWithoutExtension(file);
                        string extension = Path.GetExtension(file).ToLowerInvariant();
                        if (basename.ToLowerInvariant().Trim() == "shaderlist" && extension == ".txt")
                        {
                            string[] allowedShaderFiles = File.ReadAllLines(file);
                            foreach (string allowedShaderFile in allowedShaderFiles)
                            {
                                shaderListWhitelist.Add(allowedShaderFile.Trim());
                            }

                        }
                    }
                }



                shadFiles.Sort(); // Sort shaders alphabetically

                //Dictionary<string, string> shaderFiles = new Dictionary<string, string>();
                if (shaderListWhitelist.Count > 0 && !ignoreShaderList)
                {
                    // We want stuff to be read in the same order as shaderlist
                    // First shader found = kept.
                    foreach (string whitelistedShader in shaderListWhitelist)
                    {

                        foreach (string file in shadFiles)
                        {
                            string basename = Path.GetFileNameWithoutExtension(file);
                            string extension = Path.GetExtension(file).ToLowerInvariant();
                            if (extension == ".shader" && basename.Equals(whitelistedShader, StringComparison.InvariantCultureIgnoreCase))
                            {
                                SharedStuff.ParseShader(file, ref parsedShaders, shaderDuplicates);
                            }
                        }
                    }
                }

                foreach (string file in shadFiles)
                {
                    string basename = Path.GetFileNameWithoutExtension(file);
                    string extension = Path.GetExtension(file).ToLowerInvariant();
                    if (extension == ".shader")
                    {
                        if ((ignoreShaderList || shaderListWhitelist.Count == 0))
                        {
                            SharedStuff.ParseShader(file, ref parsedShaders, shaderDuplicates);
                        }
                        else if (shaderListWhitelist.Contains(basename))
                        {
                            // nuthin, already done above
                        }
                        else
                        {
                            Console.WriteLine($"Skipping {file}, not in shaderlist.txt");
                        }
                        //shaderFiles[basename] = file;
                    }
                }

            }

            

            Dictionary<string,bool> shaderNeedsResizing = new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string,SpriteInfo> shaderSpriteInfo = new Dictionary<string, SpriteInfo>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var shader in parsedShaders)
            {
                string shaderName = shader.Key;
                if (shaderName.EndsWith(":q3map"))
                {
                    shaderName = shaderName.Substring(0, shaderName.Length -":q3map".Length);
                }
                if ((shader.Key.StartsWith("textures/wadconvert/", StringComparison.InvariantCultureIgnoreCase) || shader.Key.StartsWith("textures/sprConvert/", StringComparison.InvariantCultureIgnoreCase)) && !shaderNeedsResizing.ContainsKey(shaderName))
                {
                    // Check the editorimage.
                    Match editorImageMatch = shaderImageRegex.Match(shader.Value);
                    if (editorImageMatch.Success && editorImageMatch.Groups["image"].Value.EndsWith("_npot"))
                    {
                        shaderNeedsResizing[shaderName] = true;
                    } else
                    {
                        shaderNeedsResizing[shaderName] = false;
                    }
                }
                if (shader.Key.StartsWith("textures/sprConvert/", StringComparison.InvariantCultureIgnoreCase) && !shaderSpriteInfo.ContainsKey(shaderName))
                {
                    var spriteInfoStrings = SharedStuff.GetSpriteInfo(shader.Value);
                    if(spriteInfoStrings != null)
                    {
                        SpriteInfo sprInfo = new SpriteInfo(spriteInfoStrings);
                        shaderSpriteInfo[shaderName] = sprInfo;
                    }
                    else
                    {
                        Console.WriteLine($"Shader '{shader.Key}' is missing sprite info, weird.");
                    }
                }
            }

            foreach (string mapFile in mapFiles)
            {
                ProcessMap(mapFile, parsedShaders, shaderNeedsResizing, shaderSpriteInfo);
            }

        }

        static void ProcessMap(string mapFile, Dictionary<string, string> parsedShaders, Dictionary<string, bool> shaderNeedsResizing, Dictionary<string, SpriteInfo> shaderSpriteInfo) { 

            if (!File.Exists(mapFile))
            {
                return;
            }
            string srcText = File.ReadAllText(mapFile);

            string destText = srcText;

            StringBuilder shaderFile = new StringBuilder();




            //MatchCollection entities = entitiesParseRegex.Matches(destText);
            var entities = PcreRegex.Matches(destText, propsBrushMatcher);

            AverageHelperDouble lightR = new AverageHelperDouble();
            AverageHelperDouble lightG = new AverageHelperDouble();
            AverageHelperDouble lightB = new AverageHelperDouble();
            AverageHelperDouble lightIntensity = new AverageHelperDouble();
            AverageHelperDouble lightPitch = new AverageHelperDouble();
            AverageHelperDouble lightYaw = new AverageHelperDouble();
            AverageHelperDouble lightRoll = new AverageHelperDouble();

            string skyname = "desert";

            Dictionary<string, string> specializedShaders = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            destText = PcreRegex.Replace(destText, propsBrushMatcher, (entity) => { 
                // It's a replace because we may wanna create variations of a shader based on brush render properties.
                // But otherwise we really just wanna read the skybox data n shit

                bool resave = false;
                string propsString = entity.Groups["props"].Value;
                string brushesString = entity.Groups["brushes"];
                EntityProperties props = EntityProperties.FromString(propsString);

                RenderProperties renderProperties = SharedStuff.ParseRenderProperties(props);

                if (renderProperties != null)
                {
                    string renderPropsHash = renderProperties.GetHashString().Substring(0, 6);

                    // Do: If func_water add waterFluid property, respect wave height maybe?
                    brushesString = findShader.Replace(brushesString,(Match match) => {
                        string shaderName = match.Groups["shaderName"].Value.Trim();
                        string normalFinalShaderName = SharedStuff.fixUpShaderName($"textures/wadConvert/{shaderName}");
                        bool neededResize = false;

                        string replacementShaderName = $"{shaderName}_r{renderPropsHash}";
                        string replacementShaderFinalName = SharedStuff.fixUpShaderName($"textures/wadConvert/{replacementShaderName}");
                        if (shaderNeedsResizing.ContainsKey(normalFinalShaderName))
                        {
                            if (shaderNeedsResizing[normalFinalShaderName])
                            {
                                neededResize = true;
                            }
                        }
                        (TextureType type, int stuff) = SharedStuff.TextureTypeFromTextureName(shaderName);
                        (bool onlyPot, string shaderString) = SharedStuff.MakeShader(type, normalFinalShaderName, $"map {normalFinalShaderName}", neededResize, null, renderProperties, replacementShaderFinalName);
                        //shaderFile.Append(shaderString);

                        specializedShaders[replacementShaderFinalName] = shaderString;

                        resave = true;

                        return match.Groups["vecs"] + " " + replacementShaderName + " [";
                    });
                }

                if (props["classname"].Equals("worldspawn", StringComparison.InvariantCultureIgnoreCase))
                {
                    skyname = props.ContainsKey("skyname") ? props["skyname"] : skyname;
                    skyname = props.ContainsKey("cl_skyname") ? props["cl_skyname"] : skyname;
                }
                else if (props["classname"].Equals("light_environment", StringComparison.InvariantCultureIgnoreCase))
                {
                    string _light = props.ContainsKey("_light") ? props["_light"] : null;
                    string angles = props.ContainsKey("angles") ? props["angles"] : null;
                    string pitch = props.ContainsKey("pitch") ? props["pitch"] : null;
                    string yaw = props.ContainsKey("angle") ? props["angle"] : null;

                    double thisLightPitch = 0, thisLightYaw = 0, thisLightRoll = 0;
                    bool thisLightPitchFound = false, thisLightYawFound = false, thisLightRollFound = false;

                    if (_light != null)
                    {
                        double[] _lightValues = SharedStuff.parseDoubleArray(_light);
                        if (_lightValues != null)
                        {
                            if (_lightValues.Length > 0)
                            {
                                lightR.addSample(_lightValues[0]);
                            }
                            if (_lightValues.Length > 1)
                            {
                                lightG.addSample(_lightValues[1]);
                            }
                            if (_lightValues.Length > 2)
                            {
                                lightB.addSample(_lightValues[2]);
                            }
                            if (_lightValues.Length > 3)
                            {
                                lightIntensity.addSample(_lightValues[3]);
                            }
                        }
                    }
                    if (angles != null)
                    {
                        double[] angleValues = SharedStuff.parseDoubleArray(angles);
                        if (angleValues != null)
                        {
                            if(angleValues.Length == 1)
                            {
                                if(angleValues[0] == -1)
                                {
                                    thisLightPitch = 90; // Should be -90 but i flip it around later anyway?
                                    thisLightPitchFound = true;
                                } else if(angleValues[0] == -1)
                                {
                                    thisLightPitch = -90; // Should be 90 but i flip it around later anyway?
                                    thisLightPitchFound = true;
                                }
                            } else { 
                                if (angleValues.Length > 0)
                                {
                                    thisLightPitch = angleValues[0];
                                    thisLightPitchFound = true;
                                    //lightPitch.addSample(angleValues[0]);
                                }
                                if (angleValues.Length > 1)
                                {
                                    thisLightYaw = angleValues[1];
                                    thisLightYawFound = true;
                                    //lightYaw.addSample(angleValues[1]);
                                }
                                if (angleValues.Length > 2)
                                {
                                    thisLightRoll = angleValues[2];
                                    thisLightRollFound = true;
                                    //lightRoll.addSample(angleValues[2]);
                                }
                            }
                        }
                    }
                    if (pitch != null)
                    {
                        double parsedValue = 0;
                        if (double.TryParse(pitch, out parsedValue))
                        {
                            thisLightPitch = parsedValue;
                            thisLightPitchFound = true;
                            //lightPitch.addSample(parsedValue);
                        }
                    }
                    if (yaw != null)
                    {
                        double parsedValue = 0;
                        if (double.TryParse(yaw, out parsedValue))
                        {
                            thisLightYaw = parsedValue;
                            thisLightYawFound = true;
                            //lightYaw.addSample(parsedValue);
                        }
                    }

                    if (thisLightPitchFound)
                    {
                        lightPitch.addSample(thisLightPitch);
                    }
                    if (thisLightYawFound)
                    {
                        lightYaw.addSample(thisLightYaw);
                    }
                    if (thisLightRollFound)
                    {
                        lightRoll.addSample(thisLightRoll);
                    }
                }

                if (resave)
                {
                    return $"{{\n{props.ToString()}\n{brushesString}";
                }
                else
                {
                    return entity.Value;
                }
            });



            double lightRVal = lightR.getValueOrDefault(255)/255.0;
            double lightGVal = lightG.getValueOrDefault(255) / 255.0;
            double lightBVal = lightB.getValueOrDefault(255) / 255.0;
            double lightIntensityVal = lightIntensity.getValueOrDefault(50); // had it *2 but it was a bit too bright, maybe leave. but that was jka compile? hm
            double lightPitchVal = lightPitch.getValueOrDefault(90);
            double lightYawVal = lightYaw.getValueOrDefault(0);


            string simplifiedMapname = SharedStuff.fixUpShaderName(Path.GetFileNameWithoutExtension(mapFile));
            string skyShaderName = SharedStuff.fixUpShaderName(simplifiedMapname + "_" + skyname);

            string skyShaderPath = $"GoldSrcSkies/{skyShaderName}";

            shaderFile.Append($"\ntextures/{skyShaderPath}");
            shaderFile.Append("\n{");
            shaderFile.Append("\n\tqer_editorimage textures/skies/sky.tga");
            shaderFile.Append("\n\tsurfaceparm sky");
            shaderFile.Append("\n\tsurfaceparm noimpact");
            shaderFile.Append("\n\tsurfaceparm nomarks");
            shaderFile.Append("\n\tq3map_sunext "); 
            shaderFile.Append(lightRVal.ToString("0.###")); 
            shaderFile.Append(" "); 
            shaderFile.Append(lightGVal.ToString("0.###")); 
            shaderFile.Append(" "); 
            shaderFile.Append(lightBVal.ToString("0.###")); 
            shaderFile.Append(" "); 
            shaderFile.Append(lightIntensityVal.ToString("0.###")); 
            shaderFile.Append(" ");
            double theAngle = lightYawVal - 180; // sunlight in q3 is defined as the direction ur looking to see the sun, not the direction the sun is pointing?
            while (theAngle < -180) 
            {
                theAngle += 360;
            }
            shaderFile.Append((theAngle).ToString("0.###")); 
            shaderFile.Append(" "); 
            shaderFile.Append((-lightPitchVal).ToString("0.###")); 
            shaderFile.Append(" 5 32"); 
            shaderFile.Append("\n\tq3map_lightRGB 1 1 1");
            shaderFile.Append("\n\tq3map_surfacelight "+(lightIntensityVal).ToString("0.###"));
            shaderFile.Append("\n\tq3map_lightsubdivide 512");
            shaderFile.Append("\n\tnotc");
            shaderFile.Append("\n\tq3map_nolightmap");
            shaderFile.Append($"\n\tskyParms gfx/env/{skyname} 512 -");
            shaderFile.Append("\n}\n");


            foreach(var kvp in specializedShaders)
            {
                shaderFile.Append(kvp.Value);
            }

            Directory.CreateDirectory("shaders");
            File.WriteAllText($"shaders/{simplifiedMapname}.shader", shaderFile.ToString());


            destText = findShader.Replace(destText, (match) => {
                string shaderName = match.Groups["shaderName"].Value.Trim();
                if (shaderName.StartsWith("sky",StringComparison.InvariantCultureIgnoreCase))
                {
                    return match.Groups["vecs"] + " " + skyShaderPath + " [";
                }
                return match.Groups["vecs"] + " wadConvert/" + SharedStuff.fixUpShaderName(shaderName) + " [";
            });

            StringBuilder newEntities = new StringBuilder();
            StringBuilder spritePatches = new StringBuilder();

            int ladderIndex = 0;

            //destText = entitiesParseRegex.Replace(destText,(Match entity)=> {
            destText = PcreRegex.Replace(destText, propsBrushMatcher,(entity)=> {

                bool resave = false;
                string propsString = entity.Groups["props"].Value;
                string brushesString = entity.Groups["brushes"].Value;
                EntityProperties props = EntityProperties.FromString(propsString);

                double explicitPitch = 0;
                double explicitYaw = 0;
                double explicitRoll = 0;
                bool anglesFound = false;
                if (props.ContainsKey("pitch"))
                {

                    string angleTmp = props["pitch"];
                    double pitchVal = 0;
                    if (double.TryParse(angleTmp, out pitchVal))
                    {
                        explicitPitch = -pitchVal;
                        angleTmp = (-pitchVal).ToString("#.000");
                    }
                    props.Remove("pitch");
                    props["pitch"] = angleTmp;
                    anglesFound = true;
                    resave = true;
                }
                if (props.ContainsKey("yaw")) // Make lowercase
                {
                    string angleTmp = props["yaw"];
                    if (!double.TryParse(angleTmp, out explicitYaw))
                    {
                        explicitYaw = 0;
                    }
                    props.Remove("yaw");
                    props["yaw"] = angleTmp;
                    anglesFound = true;
                    resave = true;
                }
                if (props.ContainsKey("roll")) // Make lowercase
                {
                    string angleTmp = props["roll"];
                    if (!double.TryParse(angleTmp, out explicitRoll))
                    {
                        explicitRoll = 0;
                    }
                    props.Remove("roll");
                    props["roll"] = angleTmp;
                    anglesFound = true;
                    resave = true;
                }
                if (props.ContainsKey("Angles") || anglesFound)
                {
                    // Make it lowercase cuz radiant is a bit of a dummy sometimes :)
                    // Also, seems like we have to invert pitch possibly.
                    string anglesTmp = props.ContainsKey("Angles") ? props["Angles"] : "0 0 0";
                    double[] anglesParsed = SharedStuff.parseDoubleArray(anglesTmp);
                    if(anglesParsed.Length == 3) // Q3 doesn't have extra keys for this stuff.
                    {
                        anglesParsed[0] = -anglesParsed[0];
                        if (explicitYaw != 0)
                        {
                            anglesParsed[1] = explicitYaw;
                        }
                        if (explicitPitch != 0)
                        {
                            anglesParsed[0] = explicitPitch;
                        }
                        if (explicitRoll != 0)
                        {
                            anglesParsed[2] = explicitRoll;
                        }
                        anglesTmp = (anglesParsed[0]).ToString("#.000") + " " + anglesParsed[1].ToString("#.000") + " " + anglesParsed[2].ToString("#.000");
                    } else if (anglesParsed.Length == 1)
                    {
                        double parsedNumber = anglesParsed[0];
                        anglesParsed = new double[3];
                        if (parsedNumber == -1)
                        {
                            anglesParsed[0] = -90;
                            anglesParsed[1] = 0;
                            anglesParsed[2] = 0;
                        } else if (parsedNumber == -2)
                        {
                            anglesParsed[0] = 90;
                            anglesParsed[1] = 0;
                            anglesParsed[2] = 0;
                        }
                        if (explicitYaw != 0)
                        {
                            anglesParsed[1] = explicitYaw;
                        }
                        if (explicitPitch != 0)
                        {
                            anglesParsed[0] = explicitPitch;
                        }
                        if (explicitRoll != 0)
                        {
                            anglesParsed[2] = explicitRoll;
                        }
                        anglesTmp = (anglesParsed[0]).ToString("#.000") + " " + anglesParsed[1].ToString("#.000") + " " + anglesParsed[2].ToString("#.000");
                    }
                    props.Remove("Angles");
                    props["angles"] = anglesTmp;
                    resave = true;
                }
                if (props.ContainsKey("model") && props["model"].EndsWith(".mdl", StringComparison.InvariantCultureIgnoreCase))
                {
                    //props["classname"] = "misc_model";
                    //props["model"] = "models/mdlConvert/" + Path.GetFileName(Path.ChangeExtension(props["model"], ".obj"));
                    //resave = true;
                    props["classname"] = "misc_model";
                    props["model"] = "models/mdlConvert/" + Path.GetFileName(Path.ChangeExtension(props["model"], ".obj"));
                    int originalSpawnFlags = 0;
                    if (props.ContainsKey("spawnflags") && int.TryParse(props["spawnflags"], out originalSpawnFlags))
                    {
                        props["spawnflags_original"] = props["spawnflags"];
                    }
                    // props["spawnflags"] = "2"; // turns into a solid since cycler_sprite is solid :)
                    props["spawnflags"] = "0"; // actually dont, it seems to make shit solid that shouldnt be (trees with big transparent boxes for the branches texture and shit)
                    resave = true;
                } else if (props.ContainsKey("model") && props["model"].EndsWith(".spr", StringComparison.InvariantCultureIgnoreCase))
                {
                    string relativePath = props["model"];
                    if (!relativePath.StartsWith("sprites"))
                    {
                        Console.WriteLine($"{relativePath} doesn't start with sprites/ wtf");
                    } else
                    {
                        relativePath = relativePath.Substring("sprites/".Length);
                    }
                    string relativePathWithoutExtension = Path.Combine(Path.GetDirectoryName(relativePath), Path.GetFileNameWithoutExtension(relativePath)).Replace('\\', '/');
                    string textureName = relativePathWithoutExtension;
                    textureName = SharedStuff.fixUpShaderName(textureName);
                    string texturePath = $"textures/sprConvert/{textureName}";

                    if (shaderSpriteInfo.ContainsKey(texturePath))
                    {
                        SpriteInfo spriteInfo = shaderSpriteInfo[texturePath];
                        Vector3 origin = new Vector3();
                        Vector3 angles = new Vector3();

                        Vector3 forward = new Vector3() { Y = 1 };
                        Vector3 right = new Vector3() { X = 1 };
                        Vector3 up = new Vector3() { Z = 1 };
                        float scale = 1.0f;
                        if (props.ContainsKey("scale"))
                        {
                            if(!float.TryParse(props["scale"].Trim(),out scale))
                            {
                                scale = 1.0f;
                                Console.WriteLine($"Sprite {relativePath} has weird scale (unparsable): " + props["scale"]);
                            }
                        }

                        if (props.ContainsKey("origin"))
                        {
                            float[] parsedOrigin = SharedStuff.parseFloatArray( props["origin"]);
                            if(parsedOrigin.Length != 3)
                            {
                                Console.WriteLine($"Sprite {relativePath} has weird origin (not 3 numbers): "+ props["origin"]);
                            } else
                            {
                                origin.X = parsedOrigin[0];
                                origin.Y = parsedOrigin[1];
                                origin.Z = parsedOrigin[2];
                            }
                        } else
                        {
                            Console.WriteLine($"Sprite {relativePath} without origin...");
                        }

                        if (props.ContainsKey("angles"))
                        {
                            float[] parsedAngles = SharedStuff.parseFloatArray( props["angles"]);
                            if(parsedAngles.Length != 3)
                            {
                                Console.WriteLine($"Sprite {relativePath} has weird angles (not 3 numbers): " + props["angles"]);
                            } else
                            {
                                angles.X = parsedAngles[0];
                                angles.Y = parsedAngles[1];
                                angles.Z = parsedAngles[2];

                                SharedStuff.AngleVectors(angles, out forward, out right, out up);
                            }
                        } else
                        {
                            Console.WriteLine($"Sprite {relativePath} without angles...");
                        }


                        Vector3 topleft = origin - right * spriteInfo.width * scale * 0.5f + up * spriteInfo.height * scale * 0.5f;
                        Vector3 topright = origin + right * spriteInfo.width * scale * 0.5f + up * spriteInfo.height * scale * 0.5f;
                        Vector3 bottomleft = origin - right * spriteInfo.width * scale * 0.5f - up * spriteInfo.height * scale * 0.5f;
                        Vector3 bottomright = origin + right * spriteInfo.width * scale * 0.5f - up * spriteInfo.height * scale * 0.5f;

                        spritePatches.Append("\n{");
                        spritePatches.Append("\n\t\"classname\" \"func_group\"");
                        spritePatches.Append("\n\t{");
                        spritePatches.Append("\n\tpatchDef2");
                        spritePatches.Append("\n\t{");
                        spritePatches.Append($"\n\t{texturePath}");
                        spritePatches.Append("\n\t( 3 3 0 0 0 )");
                        spritePatches.Append("\n\t(");


                        // Top Row
                        spritePatches.Append("\n\t( ");

                        spritePatches.Append("( ");
                        spritePatches.Append(topleft.X.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append(topleft.Y.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append(topleft.Z.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append("0 0");
                        spritePatches.Append(" ) ");

                        spritePatches.Append("( ");
                        spritePatches.Append((0.5f*(topleft+topright)).X.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append((0.5f * (topleft + topright)).Y.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append((0.5f * (topleft + topright)).Z.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append("0.5 0");
                        spritePatches.Append(" ) ");

                        spritePatches.Append("( ");
                        spritePatches.Append(topright.X.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append(topright.Y.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append(topright.Z.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append("1 0");
                        spritePatches.Append(" ) ");

                        spritePatches.Append(")");


                        // Middle Row
                        spritePatches.Append("\n\t( ");

                        spritePatches.Append("( ");
                        spritePatches.Append((0.5f * (topleft + bottomleft)).X.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append((0.5f * (topleft + bottomleft)).Y.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append((0.5f * (topleft + bottomleft)).Z.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append("0 0.5");
                        spritePatches.Append(" ) ");

                        spritePatches.Append("( ");
                        spritePatches.Append((0.25f*(topleft + bottomleft+ bottomright + topright)).X.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append((0.25f * (topleft + bottomleft + bottomright + topright)).Y.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append((0.25f * (topleft + bottomleft + bottomright + topright)).Z.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append("0.5 0.5");
                        spritePatches.Append(" ) ");

                        spritePatches.Append("( ");
                        spritePatches.Append((0.5f * (bottomright + topright)).X.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append((0.5f * (bottomright + topright)).Y.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append((0.5f * (bottomright + topright)).Z.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append("1 0.5");
                        spritePatches.Append(" ) ");

                        spritePatches.Append(")");


                        // Bottom Row
                        spritePatches.Append("\n\t( ");

                        spritePatches.Append("( ");
                        spritePatches.Append(bottomleft.X.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append(bottomleft.Y.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append(bottomleft.Z.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append("0 1");
                        spritePatches.Append(" ) ");

                        spritePatches.Append("( ");
                        spritePatches.Append((0.5f * (bottomleft + bottomright)).X.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append((0.5f * (bottomleft + bottomright)).Y.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append((0.5f * (bottomleft + bottomright)).Z.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append("0.5 1");
                        spritePatches.Append(" ) ");

                        spritePatches.Append("( ");
                        spritePatches.Append(bottomright.X.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append(bottomright.Y.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append(bottomright.Z.ToString("#.000"));
                        spritePatches.Append(" ");
                        spritePatches.Append("1 1");
                        spritePatches.Append(" ) ");

                        spritePatches.Append(")");



                        spritePatches.Append("\n\t)");
                        spritePatches.Append("\n\t}");
                        spritePatches.Append("\n\t}");
                        spritePatches.Append("\n}");

                        Console.WriteLine("test");

                    } else
                    {
                        Console.WriteLine($"Sprite info for {relativePath} not found");
                    }

                    

                    //props["classname"] = "misc_model";
                    //props["model"] = "models/mdlConvert/" + Path.GetFileName(Path.ChangeExtension(props["model"], ".obj"));
                    //resave = true;
                }
                /*if (props["classname"].Equals("env_sprite", StringComparison.InvariantCultureIgnoreCase) && props.ContainsKey("model"))
                {
                    props["classname"] = "misc_model";
                    props["model"] = "models/mdlConvert/"+Path.GetFileName(Path.ChangeExtension(props["model"],".obj"));
                    resave = true;
                }
                else if (props["classname"].Equals("cycler_sprite", StringComparison.InvariantCultureIgnoreCase) && props.ContainsKey("model"))
                {
                    props["classname"] = "misc_model";
                    props["model"] = "models/mdlConvert/"+Path.GetFileName(Path.ChangeExtension(props["model"],".obj"));
                    int originalSpawnFlags = 0;
                    if (props.ContainsKey("spawnflags") && int.TryParse(props["spawnflags"], out originalSpawnFlags))
                    {
                        props["spawnflags_original"] = props["spawnflags"];
                    }
                   // props["spawnflags"] = "2"; // turns into a solid since cycler_sprite is solid :)
                    props["spawnflags"] = "0"; // actually dont, it seems to make shit solid that shouldnt be (trees with big transparent boxes for the branches texture and shit)
                    resave = true;
                }
                else if (props.ContainsKey("model") && props["model"].EndsWith(".mdl",StringComparison.InvariantCultureIgnoreCase))
                {
                    props["classname"] = "misc_model";
                    props["model"] = "models/mdlConvert/"+Path.GetFileName(Path.ChangeExtension(props["model"],".obj"));
                    resave = true;
                }*/
                if (props["classname"].Equals("func_wall", StringComparison.InvariantCultureIgnoreCase))
                {
                    props["classname"] = "func_group";
                    brushesString = MakeFacesDetail(brushesString);
                    resave = true;
                }
                else if (props["classname"].Equals("func_water", StringComparison.InvariantCultureIgnoreCase))
                {
                    props["classname"] = "func_group";
                    brushesString = MakeFacesDetail(brushesString);
                    resave = true;
                }
                else if (props["classname"].Equals("func_ladder", StringComparison.InvariantCultureIgnoreCase))
                {
                    ladderIndex++;
                    List<Side> completedSides = new List<Side>();
                    props["classname"] = "trigger_push";
                    var brushMatches = PcreRegex.Matches($" {brushesString}",brushesMatcher);
                    foreach (var brushMatch in brushMatches)
                    {
                        List<Side> sides = new List<Side>();
                        MatchCollection faceMatches = facePointsParseRegex.Matches(brushMatch.Groups["brush"].Value);
                        foreach (Match faceMatch in faceMatches)
                        {
                            string coordinates = faceMatch.Groups["coordinates"].Value;
                            Side side = new Side();
                            side.points = SharedStuff.parseVector3Array(coordinates);
                            sides.Add(side);
                        }

                        Solid brush = new Solid();
                        brush.sides = sides.ToArray();

                        foreach (Side side in sides)
                        {
                            completedSides.Add(Side.completeSide(side, brush));
                        }
                    }
                    Vector3 centerPoint = new Vector3();
                    float highestPoint = float.NegativeInfinity;
                    int faceCount = 0;
                    foreach (Side side in completedSides)
                    {
                        foreach (Vector3 point in side.points)
                        {
                            if(point.Z > highestPoint)
                            {
                                highestPoint = point.Z;
                            }
                            centerPoint += point;
                            faceCount++;
                        }
                    }
                    if(faceCount > 0)
                    {
                        centerPoint /= (float)faceCount;
                        Vector3 destination = centerPoint;
                        destination.Z = highestPoint + 64;
                        EntityProperties targetProps = new EntityProperties();
                        targetProps["classname"] = "target_position";
                        targetProps["targetname"] = $"ladder{ladderIndex}";
                        props["target"] = $"ladder{ladderIndex}";
                        targetProps["origin"] = destination.X.ToString("#.000") + " " + destination.Y.ToString("#.000") + " " + destination.Z.ToString("#.000");

                        newEntities.Append("\n{");
                        newEntities.Append(targetProps.ToString());
                        newEntities.Append("\n}");
                    }

                    brushesString = MakeFacesDetail(brushesString);
                    resave = true;
                }
                else if (props["classname"].Equals("func_illusionary", StringComparison.InvariantCultureIgnoreCase)) // We do make a nonsolid shader for this higher up
                {
                    props["classname"] = "func_group";
                    brushesString = MakeFacesDetail(brushesString);
                    resave = true;
                }
                
                if (props["classname"].Equals("ambient_generic", StringComparison.InvariantCultureIgnoreCase) && props.ContainsKey("message"))
                {
                    props["classname"] = "target_speaker";
                    props["noise"] = "sound/"+props["message"];
                    int originalSpawnFlags = 0;
                    if(props.ContainsKey("spawnflags") && int.TryParse(props["spawnflags"],out originalSpawnFlags))
                    {
                        props["spawnflags_original"] = props["spawnflags"];
                    }
                    if((originalSpawnFlags & 16)>0) // Start silent
                    {
                        props["spawnflags"] = "2"; // looped, start silent
                    } else
                    {
                        props["spawnflags"] = "1"; // looped
                    }
                    // Todo check spawnflag 32 "not toggled". however generally speaking we need to check if if we're dealing with a looping sound, which is based purely on the .wav file. ... sigh. fuck it for now.
                    resave = true;
                }

                if (resave)
                {
                    return $"{{\n{props.ToString()}\n{brushesString}";
                } else
                {
                    return entity.Value;
                }
            });

            destText += newEntities.ToString();
            destText += spritePatches.ToString();

            File.WriteAllText($"{mapFile}.filtered.map", destText);
        }

        static string MakeFacesDetail(string brushesText)
        {
            return faceEndNumbersParse.Replace(brushesText,(Match faceMatch)=> {
                if (faceMatch.Groups["extraValues"].Success)
                {
                    // We need to parse and add the detail flag
                    Int64[] extraValuesArray = SharedStuff.parseIntArray(faceMatch.Groups["extraValues"].Value);
                    extraValuesArray[0] |= detailFlag;

                    return faceMatch.Groups["start"].Value.Trim(new char[] { '\r', '\n', ' ' }) + $" {extraValuesArray[0]} {extraValuesArray[1]} {extraValuesArray[2]}\n";
                } else
                {
                    // We just append.
                    return faceMatch.Value.Trim(new char[] { '\r','\n', ' '}) + $" {detailFlag} 0 0\n";
                }
            });
        }
        

    }
}
