using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Forms;
using GitUIPluginInterfaces;
using GitUIPluginInterfaces.BuildServerIntegration;
using ResourceManager;
using TeamCityIntegration.Settings;

namespace GitExtensions.GitLabPipelinesPlugin.Settings
{
    [Export(typeof(IBuildServerSettingsUserControl))]
    [BuildServerSettingsUserControlMetadata(GitLabPipelinesAdapter.PluginName)]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public partial class SettingsUserControl : GitExtensionsControl, IBuildServerSettingsUserControl
    {
        private string _defaultProjectName;
        private readonly TranslationString _failToLoadProjectMessage = new TranslationString("Failed to load the projects and build list." + Environment.NewLine + "Please verify the server url.");
        private readonly TranslationString _failToLoadProjectCaption = new TranslationString("Error when loading the projects and build list");

        public SettingsUserControl()
        {
            InitializeComponent();
            InitializeComplete();

            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        }

        public void Initialize(string defaultProjectName, IEnumerable<string> remotes)
        {
            _defaultProjectName = defaultProjectName;
            SetChooseBuildButtonState();
        }

        public void LoadSettings(ISettingsSource buildServerConfig)
        {
            if (buildServerConfig != null)
            {
                gitLabServerUrl.Text = buildServerConfig.GetString("GitLabBuildServerUrl", string.Empty);
                projectName.Text = buildServerConfig.GetString("GitLabProjectName", _defaultProjectName);
                userToken.Text = buildServerConfig.GetString("GitLabToken", string.Empty);
            }
        }

        public void SaveSettings(ISettingsSource buildServerConfig)
        {
            buildServerConfig.SetString("GitLabBuildServerUrl", gitLabServerUrl.Text);
            buildServerConfig.SetString("GitLabProjectName", projectName.Text);
            buildServerConfig.SetString("GitLabToken", userToken.Text);
        }

        private void buttonProjectChooser_Click(object sender, EventArgs e)
        {
            try
            {
                var buildChooser = new BuildChooser(gitLabServerUrl.Text, userToken.Text, "", "");
                var result = buildChooser.ShowDialog(this);

                if (result == DialogResult.OK)
                {
                    projectName.Text = buildChooser.ProjectName;
                }
            }
            catch
            {
                MessageBox.Show(this, _failToLoadProjectMessage.Text, _failToLoadProjectCaption.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TeamCityServerUrl_TextChanged(object sender, EventArgs e)
        {
            SetChooseBuildButtonState();
        }

        private void SetChooseBuildButtonState()
        {
            buttonProjectChooser.Enabled = !string.IsNullOrWhiteSpace(gitLabServerUrl.Text);
        }
    }
}
