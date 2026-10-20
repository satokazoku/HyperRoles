using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HyperRoles.Modules
{
    public static class Translator
    {
        public static Dictionary<string, Dictionary<int, string>> translateMaps;

        public static void LoadLangs()
        {
            // 先に空の辞書を作っておく。CSVの読み込みに失敗しても、null のままにしない
            translateMaps = new Dictionary<string, Dictionary<int, string>>();

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("HyperRoles.Resources.string.csv");
            if (stream == null)
            {
                Main.Logger.LogError("翻訳用CSVが見つかりません: HyperRoles.Resources.string.csv(ビルドアクションを「埋め込みリソース」にしてください)");
                return;
            }

            // 外部ライブラリ(Csv)に頼らず、自前のパーサーで読む
            var rows = SimpleCsv.Parse(stream);
            if (rows.Count == 0) return;

            var headers = rows[0]; // 1行目は見出し(0列目は無視、1列目以降は言語ID)
            for (int r = 1; r < rows.Count; r++)
            {
                var values = rows[r];
                try
                {
                    // 空行や、コメント行(#始まり)は飛ばす
                    if (values.Length == 0 || values[0].Length == 0 || values[0][0] == '#') continue;

                    Dictionary<int, string> dic = new();
                    for (int i = 1; i < values.Length && i < headers.Length; i++)
                    {
                        int id = int.Parse(headers[i].Trim());
                        dic[id] = values[i].Replace("\\n", "\n").Replace("\\r", "\r");
                    }
                    translateMaps.TryAdd(values[0], dic);
                }
                catch (Exception ex)
                {
                    Main.Logger.LogInfo($"翻訳用CSVの登録に失敗: {ex.Message}");
                }
            }
        }

        public static string GetString(string str, SupportedLangs langId)
        {
            // 読み込み前に呼ばれても落ちないようにする(Load() で呼び忘れた場合の保険)
            if (translateMaps == null) LoadLangs();

            var res = $"{str}";
            if (str == null) return res;

            if (translateMaps.TryGetValue(str, out var dic))
            {
                if (dic.TryGetValue((int)langId, out var value) && !string.IsNullOrEmpty(value))
                {
                    res = value;
                }
                else
                {
                    // 該当の言語が無い/空のときは、English(0)を * 付きで出す
                    res = dic.TryGetValue(0, out var en) ? $"*{en}" : $"*{str}";
                }
            }
            return res;
        }

        public static string GetString(string s, Dictionary<string, string> replacementDic = null)
        {
            var language = TranslationController.InstanceExists ? TranslationController.Instance.currentLanguage : null;
            var langId = language != null ? language.languageID : SupportedLangs.English;

            string str = GetString(s, langId);
            if (replacementDic != null)
                foreach (var rd in replacementDic)
                {
                    str = str.Replace(rd.Key, rd.Value);
                }
            return str;
        }
    }

    /// <summary>
    /// 最小限のCSVパーサー。カンマ区切り・ダブルクォートで囲んだ値("" は " の意味)・改行(CRLF/LF)に対応。
    /// 外部のDLLを使わないので、BepInEx/plugins に HyperRoles.dll だけ置けば動く。
    /// </summary>
    static class SimpleCsv
    {
        public static List<string[]> Parse(Stream stream)
        {
            using var reader = new StreamReader(stream, new UTF8Encoding(false), true); // BOMがあれば取り除く
            var text = reader.ReadToEnd();

            var rows = new List<string[]>();
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                    continue;
                }

                if (c == '"') inQuotes = true;
                else if (c == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;

                    fields.Add(sb.ToString());
                    sb.Clear();
                    // 空行は飛ばす
                    if (!(fields.Count == 1 && fields[0].Length == 0)) rows.Add(fields.ToArray());
                    fields.Clear();
                }
                else sb.Append(c);
            }

            // 最後の行に改行が無い場合
            if (sb.Length > 0 || fields.Count > 0)
            {
                fields.Add(sb.ToString());
                rows.Add(fields.ToArray());
            }
            return rows;
        }
    }
}