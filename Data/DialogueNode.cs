using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DAIDialogSim.Data
{
    public class DialogueNode
    {
        [JsonPropertyName("guid")]
        public Guid GUID { get; set; }

        [JsonPropertyName("type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public NodeType Type { get; set; }

        [JsonPropertyName("child")]
        public Guid[] Child { get; set; }

        [JsonPropertyName("paraphrase")]
        public uint Paraphrase { get; set; }

        [JsonPropertyName("hovertext")]
        public uint Hovertext { get; set; }

        [JsonPropertyName("text")]
        public uint Text { get; set; }

        [JsonPropertyName("speaker")]
        public string Speaker { get; set; }

        [JsonPropertyName("linked_line")]
        public Guid LinkedLine { get; set; }

        [JsonPropertyName("parent")]
        public Guid Parent { get; set; }

        public string ParaphraseString { get => DialogueSimulator.GetText(Paraphrase); }
        public string HovertextString { get => DialogueSimulator.GetText(Hovertext); }
        public string TextString { get => DialogueSimulator.GetText(Text); }

        public DialogueNode GetLinkedNode()
        {
            return DialogueSimulator.GetNode(LinkedLine);
        }

        public DialogueNode GetParent()
        {
            if (Parent == Guid.Empty)
                return null;
            return DialogueSimulator.GetNode(Parent);
        }

        public bool HasAnyChild()
        {
            if (this.Type == NodeType.Conversation || this.Type == NodeType.ConversationLine)
            {
                return Child.Length > 0;
            }
            return this.LinkedLine != Guid.Empty;
        }

        public bool HasText()
        {
            return Paraphrase != default || Hovertext != default || Text != default;
        }

        public bool IsObsolete()
        {
            return !HasAnyChild() && !HasText();
        }

        public override int GetHashCode()
        {
            return GUID.GetHashCode();
        }

        public enum NodeType
        {
            Conversation,
            ConversationLine,
            ConversationLink,
        }
    }
}
