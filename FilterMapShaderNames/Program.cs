using System;
using System.IO;
using System.Text.RegularExpressions;
using WAD2Q3SharedStuff;

namespace FilterMapShaderNames
{
    class Program
    {
        static Regex findShader = new Regex(@"(?<vecs>(?:\((?:\s*[-\d\.]+){3}\s*\)\s*){3}\s*)(?<shaderName>.*?)\s*\[", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        static void Main(string[] args)
        {
            if(args.Length == 0 || !File.Exists(args[0]))
            {
                return;
            }
            string srcText = File.ReadAllText(args[0]);

            string destText = findShader.Replace(srcText, (match) => {
                return match.Groups["vecs"] + " wadConvert/" + SharedStuff.fixUpShaderName(match.Groups["shaderName"].Value.Trim()) + " [";
            });

            File.WriteAllText($"{args[0]}.filtered.map", destText);
        }
    }
}
