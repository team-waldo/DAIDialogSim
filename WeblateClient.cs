using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using System.Text.RegularExpressions;

using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE1006 // 명명 스타일

namespace DAIDialogSim
{
    public class WeblateClient
    {
        private const string BASE_URL = "http://akintos.iptime.org";
        private const string PROJECT_NAME = "dai";

        private readonly HttpClient client;
        private string authToken;

        private WeblateData data;

        public WeblateClient()
        {
            client = new HttpClient();
        }

        public void SetData(WeblateData data)
        {
            this.data = data;
        }

        public void SetAuthToken(string key)
        {
            key = key.Trim();
            authToken = key;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/javascript"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", key);
        }

        public string TestAuth()
        {
            var result = client.GetAsync($"{BASE_URL}/api/projects/{PROJECT_NAME}/").Result;

            if (result.IsSuccessStatusCode)
            {
                return "인증 성공";
            }

            if (string.IsNullOrWhiteSpace(authToken))
            {
                return "API키가 없음";
            }

            if (!Regex.IsMatch(authToken, "^[a-zA-Z0-9]{40}$"))
            {
                return "API키가 올바르지 않음";
            }
            
            return "인증 실패";
        }

        public T Request<T>(string path)
        {
            var response = client.GetAsync($"{BASE_URL}/api/{path}").Result;
            if (response.IsSuccessStatusCode)
            {
                string content = response.Content.ReadAsStringAsync().Result;
                return JsonSerializer.Deserialize<T>(content);
            }
            return default;
        }

        public bool TryGetTranslation(uint key, out TranslationUnit unit)
        {
            unit = null;
            if (data.TryGetValue(key.ToString(), out var value))
            {
                unit = Request<TranslationUnit>($"units/{value.id}/");
                return unit != default;
            }
            return false;
        }

        public string GetDialogueLink(uint key)
        {
            int position = data[key.ToString()].pos;
            return $"{BASE_URL}/translate/{PROJECT_NAME}/dialogue/ko/?offset=" + position;
        }
    }

    public class TranslationUnit
    {
        public string translation { get; set; }
        public string source { get; set; }
        public string target { get; set; }
        public string location { get; set; }
        public string context { get; set; }
        public string comment { get; set; }
        public string flags { get; set; }
        public bool fuzzy { get; set; }
        public bool translated { get; set; }
        public bool approved { get; set; }
        public int position { get; set; }
        public int id { get; set; }
        public string web_url { get; set; }
    }

    public class WeblateData : Dictionary<string, DialogueUnit>
    {
        public static WeblateData LoadJson(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return JsonSerializer.DeserializeAsync<WeblateData>(stream).Result;
            }
        }

        public async static Task<WeblateData> LoadJsonAsync(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return await JsonSerializer.DeserializeAsync<WeblateData>(stream);
            }
        }

        public static WeblateData LoadCsv(string path)
        {
            var result = new WeblateData();
            string row;

            using (var reader = new StreamReader(path))
            {
                while (!string.IsNullOrWhiteSpace(row = reader.ReadLine()))
                {
                    var parts = row.Split(',');
                    if (parts.Length != 3)
                        throw new IOException("Failed to parse CSV file");
                    var key = parts[0];
                    int id = int.Parse(parts[1]);
                    int pos = int.Parse(parts[2]);

                    result.Add(key, new DialogueUnit() { id = id, pos = pos });
                }
            }

            return result;
        }
    }

    public class DialogueUnit
    {
        public int id { get; set; }

        public int pos { get; set; }
    }
}
