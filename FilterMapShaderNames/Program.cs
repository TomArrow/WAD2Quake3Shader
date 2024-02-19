using System;
using System.IO;
using System.Text.RegularExpressions;

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
                return match.Groups["vecs"] + " " + fixUpShaderName(match.Groups["shaderName"].Value) + " [";
            });

            File.WriteAllText($"{args[0]}.filtered.map", destText);
        }
        static Regex badShaderNameChar = new Regex(@"[^-_\w\d:\\\/]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static string fixUpShaderName(string shaderName)
        {
            return badShaderNameChar.Replace(shaderName, "_");
        }
    }
}
