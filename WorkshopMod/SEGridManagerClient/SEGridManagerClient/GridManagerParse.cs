using System;
using System.Collections.Generic;
using System.Text;

namespace SEGridManagerClient
{
    /// <summary>Parses TorchPlugin JSON replies (get-grids, get-blocks, block-delete) without external JSON libs.</summary>
    internal static class GridManagerParse
    {
        public struct GridRow
        {
            public string Name;
            public long EntityId;
        }

        public static bool TryParseGetGrids(string json, List<GridRow> outList)
        {
            outList.Clear();
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            int i = json.IndexOf("\"grids\"", StringComparison.OrdinalIgnoreCase);
            if (i < 0)
            {
                return false;
            }

            i = json.IndexOf('[', i);
            if (i < 0)
            {
                return false;
            }

            i++;
            while (i < json.Length)
            {
                SkipWs(json, ref i);
                if (i >= json.Length)
                {
                    break;
                }

                if (json[i] == ']')
                {
                    break;
                }

                if (json[i] == ',')
                {
                    i++;
                    continue;
                }

                if (json[i] != '{')
                {
                    i++;
                    continue;
                }

                i++;
                long entityId = 0;
                string name = string.Empty;
                while (i < json.Length && json[i] != '}')
                {
                    SkipWs(json, ref i);
                    if (i >= json.Length || json[i] != '"')
                    {
                        break;
                    }

                    var key = ReadJsonString(json, ref i);
                    SkipWs(json, ref i);
                    if (i >= json.Length || json[i] != ':')
                    {
                        break;
                    }

                    i++;
                    SkipWs(json, ref i);
                    if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i < json.Length && json[i] == '"')
                        {
                            name = ReadJsonString(json, ref i);
                        }
                    }
                    else if (string.Equals(key, "entity_id", StringComparison.OrdinalIgnoreCase))
                    {
                        entityId = ReadLong(json, ref i);
                    }
                    else
                    {
                        SkipValue(json, ref i);
                    }

                    SkipWs(json, ref i);
                    if (i < json.Length && json[i] == ',')
                    {
                        i++;
                    }
                }

                if (i < json.Length && json[i] == '}')
                {
                    i++;
                }

                outList.Add(new GridRow { Name = name ?? string.Empty, EntityId = entityId });
            }

            return true;
        }

        public static bool TryParseGetBlocks(string json, out string mainOwnerName, out Dictionary<string, int> blocks)
        {
            mainOwnerName = string.Empty;
            blocks = new Dictionary<string, int>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            int i = json.IndexOf("\"main_owner_name\"", StringComparison.OrdinalIgnoreCase);
            if (i >= 0)
            {
                i = json.IndexOf(':', i);
                if (i > 0)
                {
                    i++;
                    SkipWs(json, ref i);
                    if (i < json.Length)
                    {
                        if (json[i] == '"')
                        {
                            mainOwnerName = ReadJsonString(json, ref i);
                        }
                        else if (StartsWithLiteral(json, i, "null"))
                        {
                            i += 4;
                            mainOwnerName = string.Empty;
                        }
                    }
                }
            }

            i = json.IndexOf("\"blocks\"", StringComparison.OrdinalIgnoreCase);
            if (i < 0)
            {
                return false;
            }

            i = json.IndexOf('{', i);
            if (i < 0)
            {
                return false;
            }

            i++;
            while (i < json.Length)
            {
                SkipWs(json, ref i);
                if (i >= json.Length)
                {
                    break;
                }

                if (json[i] == '}')
                {
                    break;
                }

                if (json[i] != '"')
                {
                    i++;
                    continue;
                }

                var key = ReadJsonString(json, ref i);
                SkipWs(json, ref i);
                if (i >= json.Length || json[i] != ':')
                {
                    break;
                }

                i++;
                SkipWs(json, ref i);
                int count = ReadInt(json, ref i);
                if (!string.IsNullOrEmpty(key))
                {
                    blocks[key] = count;
                }

                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',')
                {
                    i++;
                }
            }

            return true;
        }

        public static bool TryParseBlockDeleteOk(string json, out bool success)
        {
            success = false;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            int i = json.IndexOf("\"success\"", StringComparison.OrdinalIgnoreCase);
            if (i < 0)
            {
                return false;
            }

            i = json.IndexOf(':', i);
            if (i < 0)
            {
                return false;
            }

            i++;
            SkipWs(json, ref i);
            if (i < json.Length && json[i] == 't')
            {
                success = StartsWithLiteral(json, i, "true");
                return true;
            }

            if (i < json.Length && json[i] == 'f')
            {
                success = false;
                return true;
            }

            return false;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }
        }

        private static string ReadJsonString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"')
            {
                return string.Empty;
            }

            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                var c = s[i];
                if (c == '"')
                {
                    i++;
                    return sb.ToString();
                }

                if (c == '\\' && i + 1 < s.Length)
                {
                    i++;
                    var n = s[i];
                    if (n == 'n')
                    {
                        sb.Append('\n');
                    }
                    else if (n == 'r')
                    {
                        sb.Append('\r');
                    }
                    else if (n == 't')
                    {
                        sb.Append('\t');
                    }
                    else if (n == '"')
                    {
                        sb.Append('"');
                    }
                    else if (n == '\\')
                    {
                        sb.Append('\\');
                    }
                    else
                    {
                        sb.Append(n);
                    }

                    i++;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            return sb.ToString();
        }

        private static long ReadLong(string s, ref int i)
        {
            int sign = 1;
            if (i < s.Length && s[i] == '-')
            {
                sign = -1;
                i++;
            }

            long v = 0;
            while (i < s.Length && char.IsDigit(s[i]))
            {
                v = v * 10 + (s[i] - '0');
                i++;
            }

            return v * sign;
        }

        private static int ReadInt(string s, ref int i)
        {
            return (int)ReadLong(s, ref i);
        }

        private static void SkipValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length)
            {
                return;
            }

            if (s[i] == '"')
            {
                ReadJsonString(s, ref i);
                return;
            }

            if (s[i] == '{' || s[i] == '[')
            {
                var open = s[i];
                var close = open == '{' ? '}' : ']';
                int depth = 0;
                while (i < s.Length)
                {
                    if (s[i] == open)
                    {
                        depth++;
                    }
                    else if (s[i] == close)
                    {
                        depth--;
                        i++;
                        if (depth == 0)
                        {
                            return;
                        }

                        continue;
                    }

                    i++;
                }

                return;
            }

            while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']')
            {
                i++;
            }
        }

        private static bool StartsWithLiteral(string s, int i, string lit)
        {
            if (i + lit.Length > s.Length)
            {
                return false;
            }

            return string.Compare(s, i, lit, 0, lit.Length, StringComparison.Ordinal) == 0;
        }
    }
}
