using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using DAIDialogSim.Properties;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

namespace DAIDialogSim
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow
    {
        private readonly DialogueSimulator sim;
        private readonly Settings settings;
        private readonly WeblateClient client;

        public MainWindow()
        {
            InitializeComponent();
            this.TitleCharacterCasing = CharacterCasing.Normal;

            this.sim = DialogueSimulator.GetInstance();
            sim.TextBox = RichTextBoxDialogue;

            this.settings = Settings.Default;
            sim.Settings = settings;

            InitSettingsCheckBox();

            this.client = new WeblateClient();
            client.SetAuthToken(settings.AuthToken);
            sim.Client = client;

            TestAuth();

            RichTextBoxDialogue.Document = new FlowDocument(new Paragraph(new Run("Enter node ID and press [Start] button.")));
        }

        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var controller = await this.ShowProgressAsync("Please wait...", "Loading database");

            controller.SetProgress(0.2);
            var db = await Data.DialogueDatabase.Deserialize("data/database.json");

            controller.SetProgress(0.4);
            controller.SetMessage("Processing database...");
            sim.SetDatabase(db);

            controller.SetProgress(0.6);
            controller.SetMessage("Loading weblate data...");
            var data = WeblateData.LoadCsv("data/weblate.csv");
            client.SetData(data);

            await controller.CloseAsync();
        }

        #region UI event handlers

        private void ButtonFindPrevious_Click(object sender, RoutedEventArgs e)
        {
            sim.ShowMoreHistory();
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            if (DialogueSimulator.TryGetNode(TextBoxNode.Text.ToLowerInvariant(), out var node))
            {
                sim.ClearAndStartNode(node);
            }
            else
            {
                this.ShowMessageAsync("Error", $"올바르지 않은 노드 ID: {TextBoxNode.Text}");
            }
        }

        private async void ButtonAuthSetting_Click(object sender, RoutedEventArgs e)
        {
            var result = await this.ShowInputAsync("weblate 연동", "weblate API 키를 입력하세요");
            if (!string.IsNullOrWhiteSpace(result))
            {
                settings.AuthToken = result;
                client.SetAuthToken(result);

                TestAuth();
            }
        }

        private void TestAuth()
        {
            var result = client.TestAuth();
            this.LabelAuth.Content = "상태: " + result;
        }


        private void HandleRequestNavigate(object sender, RequestNavigateEventArgs args)
        {
            var uri = args.Uri;
            System.Diagnostics.Process.Start(uri.ToString());
        }

        #endregion UI event handlers

        #region Settings CheckBox

        private void InitSettingsCheckBox()
        {
            CheckBoxShowSource.IsChecked = settings.ShowSource;
            CheckBoxShowNumber.IsChecked = settings.ShowDialogueNo;
            CheckBoxShowNode.IsChecked = settings.ShowDialogueId;
            CheckBoxShowCondition.IsChecked = settings.ShowCondition;
            CheckBoxEnableTranslation.IsChecked = settings.EnableTranslation;
        }

        private void OptionCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == CheckBoxShowNode)
                settings.ShowDialogueId = CheckBoxShowNode.IsChecked.Value;
            else if (sender == CheckBoxShowNumber)
                settings.ShowDialogueNo = CheckBoxShowNumber.IsChecked.Value;
            else if (sender == CheckBoxEnableTranslation)
                settings.EnableTranslation = CheckBoxEnableTranslation.IsChecked.Value;
            else if (sender == CheckBoxShowSource)
                settings.ShowSource = CheckBoxShowSource.IsChecked.Value;
            else if (sender == CheckBoxShowCondition)
                settings.ShowCondition = CheckBoxShowCondition.IsChecked.Value;
        }

        #endregion Settings CheckBox

    }
}
