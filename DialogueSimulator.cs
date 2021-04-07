using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using DAIDialogSim.Data;

namespace DAIDialogSim
{
    public class DialogueSimulator
    {
        private static readonly Brush ForegroundBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0xD6, 0xD6));

        #region Singleton

        private static DialogueSimulator instance;

        public static DialogueSimulator GetInstance()
        {
            if (instance == null)
                instance = new DialogueSimulator();
            return instance;
        }

        #endregion Singleton


        public SettingsManager Settings { get; set; }

        public WeblateClient Client { get; set; }

        public System.Windows.Controls.RichTextBox TextBox { get; set; }

        private readonly Dictionary<uint, string> StringTable;

        private readonly Dictionary<Guid, DialogueNode> DialogueDictionary;

        private readonly Dictionary<string, DialogueNode> ShortGuidDictionary;

        private readonly Dictionary<DialogueNode, List<DialogueNode>> ReverseNodeMap;

        private Paragraph lastResponseParagraph;

        private DialogueNode oldestNode;

        private DialogueSimulator()
        {
            StringTable = new Dictionary<uint, string>();
            DialogueDictionary = new Dictionary<Guid, DialogueNode>();
            ShortGuidDictionary = new Dictionary<string, DialogueNode>();
            ReverseNodeMap = new Dictionary<DialogueNode, List<DialogueNode>>();
        }

        public void AddNodeReverseReference(DialogueNode parent, DialogueNode child)
        {
            List<DialogueNode> lst;
            if (!ReverseNodeMap.TryGetValue(child, out lst))
            {
                ReverseNodeMap[child] = lst = new List<DialogueNode>();
            }
            lst.Add(parent);
        }

        public void SetDatabase(DialogueDatabase db)
        {
            StringTable.Clear();

            foreach (var item in db.StringTable)
            {
                StringTable.Add(uint.Parse(item.Key), item.Value);
            }

            DialogueDictionary.Clear();

            foreach (var node in db.Conversations)
            {
                DialogueDictionary.Add(node.GUID, node);
                ShortGuidDictionary.Add(node.GUID.ToString().Substring(0, 8), node);
            }

            ReverseNodeMap.Clear();

            foreach (var node in db.Conversations)
            {
                if (node.Type == DialogueNode.NodeType.ConversationLink && node.LinkedLine != Guid.Empty)
                {
                    AddNodeReverseReference(node, GetNode(node.LinkedLine));
                }
                else
                {
                    foreach (var child in node.Child)
                    {
                        AddNodeReverseReference(node, GetNode(child));
                    }
                }
            }
        }

        public void ClearAndStartNode(DialogueNode node)
        {
            ClearDocument();

            oldestNode = node;
            ShowMoreHistory();

            StartNode(node);
        }

        private void StartNode(DialogueNode node)
        {
            var currentNode = node;
            Paragraph firstParagrph = null;

            while (!node.IsObsolete())
            {
                var paragraph = GetDialogueParagraph(currentNode);
                if (paragraph != null)
                    AddParagraph(paragraph);
                if (firstParagrph == null)
                    firstParagrph = paragraph;

                var children = GetValidChildren(currentNode);

                if (children.Count == 0)
                {
                    AddParagraph(new Paragraph(new Run("[End of conversation]")));
                    break;
                }
                else if (children.Count == 1)
                {
                    currentNode = children[0];
                    continue;
                }
                else
                {
                    ShowChoices(children);
                    break;
                }
            }
        }

        private void ShowChoices(IList<DialogueNode> nodes)
        {
            var paragraph = new Paragraph();

            int i = 0;
            foreach (var node in nodes)
            {
                var speaker = node.Speaker;
                var stringId = node.Paraphrase != 0 ? node.Paraphrase : node.Text;

                string source = StringTable[stringId];

                string text = source;

                if (Settings.EnableTranslation && Client != null && Client.TryGetTranslation(stringId, out var tr))
                {
                    if (tr.fuzzy)
                        tr.target = "[수정 필요] " + tr.target;

                    if (!tr.translated && !tr.fuzzy)
                        text = source;
                    else
                        text = tr.target;
                }

                string line = $"[{++i}] {speaker} : {text}\n";

                var run = new Run(line);

                Hyperlink choiceLink = new Hyperlink(run)
                {
                    NavigateUri = new Uri($"dialog://{node.GUID}"),
                    TextDecorations = null,       // Remove underline
                    Foreground = ForegroundBrush    // Remove blue hyperlink color
                };
                choiceLink.RequestNavigate += HandleRequestNavigate;

                paragraph.Inlines.Add(choiceLink);
            }

            AddParagraph(paragraph);
            lastResponseParagraph = paragraph;
        }

        public void ShowMoreHistory()
        {
            if (oldestNode == null)
                return;

            var currentNode = oldestNode;
            int nodeCount = 0;

            while (currentNode.Parent != Guid.Empty)
            {
                currentNode = currentNode.GetParent();

                var paragraph = GetDialogueParagraph(currentNode);
                if (paragraph == null)
                    continue;
                InsertParagraphFront(paragraph);
                if (++nodeCount >= 10)
                    break;
            }

            oldestNode = currentNode;
        }

        private void AddParagraph(Paragraph block)
        {
            TextBox.Document.Blocks.Add(block);
            TextBox.ScrollToEnd();
        }

        private void InsertParagraphFront(Paragraph block)
        {
            if (TextBox.Document.Blocks.Count == 0)
            {
                AddParagraph(block);
            }
            else
            {
                TextBox.Document.Blocks.InsertBefore(TextBox.Document.Blocks.FirstBlock, block);
                TextBox.ScrollToHome();
            }
        }

        private void ClearDocument()
        {
            oldestNode = null;
            lastResponseParagraph = null;
            TextBox.Document.Blocks.Clear();
        }

        public IList<DialogueNode> GetValidChildren(DialogueNode node)
        {
            var result = new List<DialogueNode>();

            if (node.Type == DialogueNode.NodeType.ConversationLink)
            {
                var linkedNode = node.GetLinkedNode();
                if (linkedNode.IsObsolete())
                    return result;

                if (linkedNode.HasText())
                {
                    result.Add(linkedNode);
                }
                else
                {
                    result.AddRange(GetValidChildren(linkedNode));
                }
                return result;
            }

            foreach (var guid in node.Child)
            {
                var childNode = GetNode(guid);

                if (childNode.IsObsolete())
                    continue;

                if (childNode.HasText())
                {
                    result.Add(childNode);
                    continue;
                }
                else
                {
                    result.AddRange(GetValidChildren(childNode));
                }
            }

            return result;
        }

        public Paragraph GetDialogueParagraph(DialogueNode node)
        {
            if (!node.HasText() && node.Type != DialogueNode.NodeType.Conversation)
                return null;

            var paragraph = new Paragraph();

            if (node.Type == DialogueNode.NodeType.Conversation)
            {
                var startRun = new Run("[Beginning  of conversation]");
                paragraph.Inlines.Add(startRun);

                if (!node.HasText())
                    return paragraph;
                else
                    paragraph.Inlines.Add(new Run("\n\n"));
            }

            var speakerRun = new Run(node.Speaker);
            speakerRun.FontWeight = FontWeights.Bold;
            speakerRun.FontSize *= 1.5;
            paragraph.Inlines.Add(speakerRun);

            if (Settings.ShowDialogueId)
            {
                var shortGuid = node.GUID.ToString().Substring(0, 8);
                var nodeIdRun = new Run(" " + shortGuid);
                nodeIdRun.Foreground = Brushes.LightGray;
                nodeIdRun.FontSize *= 1;
                nodeIdRun.FontFamily = new FontFamily("Consolas, Courier New");

                Hyperlink nodeIdLink = new Hyperlink(nodeIdRun)
                {
                    NavigateUri = new Uri($"dialognew://{node.GUID}"),
                    TextDecorations = null,         // Remove underline
                    Foreground = ForegroundBrush    // Remove blue hyperlink color
                };
                nodeIdLink.RequestNavigate += HandleRequestNavigate;

                paragraph.Inlines.Add(nodeIdLink);
            }

            if (node.Paraphrase != default)
            {
                var link = MakeDialogueHyperlink("Paraphrase: ", node.Paraphrase);
                paragraph.Inlines.Add(link);
            }

            if (node.Hovertext != default)
            {
                var link = MakeDialogueHyperlink("Hovertext: ", node.Hovertext);
                paragraph.Inlines.Add(link);
            }

            if (node.Text != default)
            {
                var link = MakeDialogueHyperlink("", node.Text);
                paragraph.Inlines.Add(link);
            }

            return paragraph;
        }

        private Hyperlink MakeDialogueHyperlink(string prefix, uint stringId)
        {
            Hyperlink textLink = new Hyperlink()
            {
                NavigateUri = new Uri(Client.GetDialogueLink(stringId)),
                TextDecorations = null,       // Remove underline
                Foreground = ForegroundBrush    // Remove blue hyperlink color
            };

            TranslationUnit unit = null;
            bool success = Settings.EnableTranslation && Client.TryGetTranslation(stringId, out unit);
            bool hasTranslation = success && (unit.translated || unit.fuzzy);

            if (!Settings.EnableTranslation || !hasTranslation || Settings.ShowSource)
            {
                textLink.Inlines.Add(new Run("\n"));
                if (!string.IsNullOrEmpty(prefix))
                {
                    var sourcePrefixRun = new Run( prefix);
                    textLink.Inlines.Add(sourcePrefixRun);
                }
                var sourceTextRun = new Run(StringTable[stringId]);
                textLink.Inlines.Add(sourceTextRun);
            }

            if (Settings.EnableTranslation && hasTranslation)
            {
                textLink.Inlines.Add(new Run("\n"));
                if (!string.IsNullOrEmpty(prefix))
                {
                    var targetPrefixRun = new Run(prefix);
                    if (Settings.ShowSource)
                        targetPrefixRun.Foreground = Brushes.Transparent;

                    textLink.Inlines.Add(targetPrefixRun);
                }

                var targetTextRun = new Run(unit.target);
                if (unit.fuzzy)
                    targetTextRun.Foreground = Brushes.Red;
                if (unit.approved)
                    targetTextRun.Foreground = Brushes.LightGreen;

                textLink.Inlines.Add(targetTextRun);
            }

            textLink.RequestNavigate += HandleRequestNavigate;
            return textLink;
        }

        private void HandleRequestNavigate(object sender, RequestNavigateEventArgs args)
        {
            var uri = args.Uri;
            if (uri.Scheme == "http")
            {
                System.Diagnostics.Process.Start(uri.ToString());
                return;
            }

            var node = GetNode(new Guid(uri.Host));

            if (lastResponseParagraph != null && TextBox.Document.Blocks.Remove(lastResponseParagraph))
                lastResponseParagraph = null;

            if (uri.Scheme == "dialog")
                StartNode(node);
            else if (uri.Scheme == "dialognew")
                ClearAndStartNode(node);
            // throw new NotImplementedException(); // TODO: Implement hyperlink 
        }

        #region static methods

        public static string GetText(uint id)
        {
            return instance.StringTable[id];
        }

        public static DialogueNode GetNode(Guid guid)
        {
            return instance.DialogueDictionary[guid];
        }

        public static bool TryGetNode(string shortGuid, out DialogueNode node)
        {
            return instance.ShortGuidDictionary.TryGetValue(shortGuid, out node);
        }

        #endregion static methods
    }
}
