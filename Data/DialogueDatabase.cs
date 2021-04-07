using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DAIDialogSim.Data
{
    public class DialogueDatabase
    {
        [JsonPropertyName("conversations")]
        public List<DialogueNode> Conversations { get; set; }

        [JsonPropertyName("stringtable")]
        public Dictionary<string, string> StringTable { get; set; }

        public static async Task<DialogueDatabase> Deserialize(string path)
        {
            using (var reader = File.OpenRead(path))
            {
                return await JsonSerializer.DeserializeAsync<DialogueDatabase>(reader);
            }
        }
    }
}
