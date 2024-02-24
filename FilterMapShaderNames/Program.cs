using PCRE;
using System;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using WAD2Q3SharedStuff;

namespace FilterMapShaderNames
{
    class Program
    {
        static Regex findShader = new Regex(@"(?<vecs>(?:\((?:\s*[-\d\.]+){3}\s*\)\s*){3}\s*)(?<shaderName>.*?)\s*\[", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        //static Regex entitiesParseRegex = new Regex(@"\{(\s*""([^""]+)""[ \t]+""([^""]+)"")+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        static Regex faceEndNumbersParse = new Regex(@"(?<start>\[\s*([-\d\.\+E]+)\s+([-\d\.\+E]+)\s+([-\d\.\+E]+)\s+([-\d\.\+E]+)\s*\]\s*(?:[-\d\.\+E]+\s*){3})(?<extraValues>(?:[-\d\.\+E]+\s*){3})?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static string propsBrushMatcher = @"(?<props>\{(\s*""([^""]+)""[ \t]+""([^""]+)"")+)(?<brushes>(?:\s*\{(?:[^\{\}]++|(?:(?<!\s)[\{\}])++|(?R))*+(?<=\s)\})*\s+\})";

        const int detailFlag = 0x8000000;

        static void Main(string[] args)
        {
            if(args.Length == 0 || !File.Exists(args[0]))
            {
                return;
            }
            string srcText = File.ReadAllText(args[0]);

            string destText = srcText;


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

            foreach (var entity in entities)
            {
                EntityProperties props = EntityProperties.FromString(entity.Value);
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
                    if(_light != null)
                    {
                        double[] _lightValues = SharedStuff.parseDoubleArray(_light);
                        if(_lightValues != null)
                        {
                            if(_lightValues.Length > 0)
                            {
                                lightR.addSample(_lightValues[0]);
                            }
                            if(_lightValues.Length > 1)
                            {
                                lightG.addSample(_lightValues[1]);
                            }
                            if(_lightValues.Length > 2)
                            {
                                lightB.addSample(_lightValues[2]);
                            }
                            if(_lightValues.Length > 3)
                            {
                                lightIntensity.addSample(_lightValues[3]);
                            }
                        }
                    }
                    if(angles != null)
                    {
                        double[] angleValues = SharedStuff.parseDoubleArray(angles);
                        if(angleValues != null)
                        {
                            if(angleValues.Length > 0)
                            {
                                lightPitch.addSample(angleValues[0]);
                            }
                            if(angleValues.Length > 1)
                            {
                                lightYaw.addSample(angleValues[1]);
                            }
                            if(angleValues.Length > 2)
                            {
                                lightRoll.addSample(angleValues[2]);
                            }
                        }
                    }
                    if(pitch != null)
                    {
                        double parsedValue = 0;
                        if (double.TryParse(pitch, out parsedValue))
                        {
                            lightPitch.addSample(parsedValue);
                        }
                    }
                    if(yaw != null)
                    {
                        double parsedValue = 0;
                        if (double.TryParse(yaw, out parsedValue))
                        {
                            lightYaw.addSample(parsedValue);
                        }
                    }
                }
            }

            double lightRVal = lightR.getValueOrDefault(255)/255.0;
            double lightGVal = lightG.getValueOrDefault(255) / 255.0;
            double lightBVal = lightB.getValueOrDefault(255) / 255.0;
            double lightIntensityVal = lightIntensity.getValueOrDefault(50)*2.0;
            double lightPitchVal = lightPitch.getValueOrDefault();
            double lightYawVal = lightYaw.getValueOrDefault();

            StringBuilder skyShader = new StringBuilder();

            string simplifiedMapname = SharedStuff.fixUpShaderName(Path.GetFileNameWithoutExtension(args[0]));
            string skyShaderName = SharedStuff.fixUpShaderName(simplifiedMapname + "_" + skyname);

            string skyShaderPath = $"GoldSrcSkies/{skyShaderName}";

            skyShader.Append($"\ntextures/{skyShaderPath}");
            skyShader.Append("\n{");
            skyShader.Append("\n\tqer_editorimage textures/skies/sky.tga");
            skyShader.Append("\n\tsurfaceparm sky");
            skyShader.Append("\n\tsurfaceparm noimpact");
            skyShader.Append("\n\tsurfaceparm nomarks");
            skyShader.Append("\n\tq3map_sunext "); 
            skyShader.Append(lightRVal.ToString("0.###")); 
            skyShader.Append(" "); 
            skyShader.Append(lightGVal.ToString("0.###")); 
            skyShader.Append(" "); 
            skyShader.Append(lightBVal.ToString("0.###")); 
            skyShader.Append(" "); 
            skyShader.Append(lightIntensityVal.ToString("0.###")); 
            skyShader.Append(" "); 
            skyShader.Append(lightYawVal.ToString("0.###")); 
            skyShader.Append(" "); 
            skyShader.Append((-lightPitchVal).ToString("0.###")); 
            skyShader.Append(" 5 32"); 
            skyShader.Append("\n\tq3map_lightRGB 1 1 1");
            skyShader.Append("\n\tq3map_surfacelight "+(lightIntensityVal).ToString("0.###"));
            skyShader.Append("\n\tq3map_lightsubdivide 512");
            skyShader.Append("\n\tnotc");
            skyShader.Append("\n\tq3map_nolightmap");
            skyShader.Append($"\n\tskyParms gfx/env/{skyname} 512 -");
            skyShader.Append("\n}\n");

            Directory.CreateDirectory("shaders");
            File.WriteAllText($"shaders/{simplifiedMapname}.shader", skyShader.ToString());


            destText = findShader.Replace(destText, (match) => {
                string shaderName = match.Groups["shaderName"].Value.Trim();
                if (shaderName.StartsWith("sky",StringComparison.InvariantCultureIgnoreCase))
                {
                    return match.Groups["vecs"] + " " + skyShaderPath + " [";
                }
                return match.Groups["vecs"] + " wadConvert/" + SharedStuff.fixUpShaderName(shaderName) + " [";
            });

            //destText = entitiesParseRegex.Replace(destText,(Match entity)=> {
            destText = PcreRegex.Replace(destText, propsBrushMatcher,(entity)=> {

                bool resave = false;
                string propsString = entity.Groups["props"].Value;
                string brushesString = entity.Groups["brushes"].Value;
                EntityProperties props = EntityProperties.FromString(propsString);
                if (props["classname"].Equals("env_sprite", StringComparison.InvariantCultureIgnoreCase) && props.ContainsKey("model"))
                {
                    props["classname"] = "misc_model";
                    props["model"] = "models/mdlConvert/"+Path.GetFileName(Path.ChangeExtension(props["model"],".obj"));
                    resave = true;
                }
                if (props["classname"].Equals("func_wall", StringComparison.InvariantCultureIgnoreCase))
                {
                    props["classname"] = "func_group";
                    brushesString = MakeFacesDetail(brushesString);
                    resave = true;
                }
                if (props["classname"].Equals("func_illusionary", StringComparison.InvariantCultureIgnoreCase))
                {
                    brushesString = MakeFacesDetail(brushesString);
                    resave = true;
                }
                
                if (props["classname"].Equals("ambient_generic", StringComparison.InvariantCultureIgnoreCase) && props.ContainsKey("message"))
                {
                    props["classname"] = "target_speaker";
                    props["noise"] = "sound/"+props["message"];
                    int originalSpawnFlags = 0;
                    if(props.ContainsKey("message") && int.TryParse(props["spawnflags"],out originalSpawnFlags))
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

            File.WriteAllText($"{args[0]}.filtered.map", destText);
        }

        static string MakeFacesDetail(string brushesText)
        {
            return faceEndNumbersParse.Replace(brushesText,(Match faceMatch)=> {
                if (faceMatch.Groups["extraValues"].Success)
                {
                    // We need to parse and add the detail flag
                    Int64[] extraValuesArray = SharedStuff.parseIntArray(faceMatch.Groups["extraValues"].Value);
                    extraValuesArray[0] |= detailFlag;

                    return faceMatch.Groups["start"].Value.Trim(new char[] { '\r', '\n' }) + $" {extraValuesArray[0]} {extraValuesArray[1]} {extraValuesArray[2]}\n";
                } else
                {
                    // We just append.
                    return faceMatch.Value.Trim(new char[] { '\r','\n'}) + $" {detailFlag} 0 0\n";
                }
            });
        }
    }
}
