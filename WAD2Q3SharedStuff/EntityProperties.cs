using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WAD2Q3SharedStuff
{
    public class EntityProperties : Dictionary<string, string>, INotifyPropertyChanged
    {
        public EntityProperties() : base(StringComparer.InvariantCultureIgnoreCase)
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var kvp in this)
            {
                sb.Append($"\"{(kvp.Key + '\"').PadRight(10)} \"{kvp.Value}\"\n");
            }
            return sb.ToString();
        }

        static Regex singleEntityParseRegex = new Regex(@"(\s*""([^""]+)""[ \t]+""([^""]+)"")+\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static EntityProperties FromString(string propertiesString)
        {
            MatchCollection matches = singleEntityParseRegex.Matches(propertiesString);
            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 4)
                {
                    EntityProperties props = new EntityProperties();

                    int lineCount = match.Groups[2].Captures.Count;
                    for (int c = 0; c < lineCount; c++)
                    {
                        //Trace.WriteLine($"{match.Groups[2].Captures[c].Value}:{match.Groups[3].Captures[c].Value}");
                        props[match.Groups[2].Captures[c].Value] = match.Groups[3].Captures[c].Value;
                    }
                    return props;
                }
            }
            return new EntityProperties();
        }

        public string String => this.ToString();


        public override bool Equals(object obj)
        {
            EntityProperties other = obj as EntityProperties;
            if (!(other is null))
            {
                if (this.Count == other.Count)
                {
                    foreach (var kvp in this)
                    {
                        if (!other.ContainsKey(kvp.Key) || other[kvp.Key] != kvp.Value)
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash = 0;
            bool firstDone = false;

            string[] keys = this.Keys.ToArray();
            Array.Sort(keys, StringComparer.InvariantCultureIgnoreCase);

            foreach (var key in keys)
            {

                int hereHash = HashCode.Combine(key.GetHashCode(StringComparison.InvariantCultureIgnoreCase), this[key].GetHashCode(StringComparison.InvariantCultureIgnoreCase));
                if (!firstDone)
                {
                    hash = hereHash;
                }
                else
                {
                    hash = HashCode.Combine(hereHash, hash);
                    firstDone = true;
                }
            }
            return hash;
        }
    }
}
