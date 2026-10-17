using Content.Client._Polonium.Tutorial.Lobby;
using Content.Client.Changelog;
using Content.Client.UserInterface.Systems.EscapeMenu;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Lobby;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;

namespace Content.Client.Info
{
    public sealed class LinkBanner : BoxContainer
    {
        [Access(typeof(IClientsideNavTutorialStep), typeof(SharedTutorialLobbyManager))]
        public Button? TutorialButton { get; }

        private readonly IConfigurationManager _cfg;
        private readonly TutorialManager _tutorial;

        private ValueList<(CVarDef<string> cVar, Button button)> _infoLinks;

        public LinkBanner()
        {
            var buttons = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal
            };
            AddChild(buttons);

            var uriOpener = IoCManager.Resolve<IUriOpener>();
            _cfg = IoCManager.Resolve<IConfigurationManager>();
            _tutorial = IoCManager.Resolve<TutorialManager>();

            var rulesButton = new Button() {Text = Loc.GetString("server-info-rules-button")};
            rulesButton.OnPressed += args => new RulesAndInfoWindow().Open();
            buttons.AddChild(rulesButton);

            AddInfoButton("server-info-discord-button", CCVars.InfoLinksDiscord);
            AddInfoButton("server-info-website-button", CCVars.InfoLinksWebsite);
            AddInfoButton("server-info-wiki-button", CCVars.InfoLinksWiki);
            AddInfoButton("server-info-forum-button", CCVars.InfoLinksForum);
            AddInfoButton("server-info-telegram-button", CCVars.InfoLinksTelegram);

            var guidebookController = UserInterfaceManager.GetUIController<GuidebookUIController>();
            var guidebookButton = new Button() { Text = Loc.GetString("server-info-guidebook-button") };
            guidebookButton.OnPressed += _ =>
            {
                guidebookController.ToggleGuidebook();
            };
            buttons.AddChild(guidebookButton);

            var changelogButton = new ChangelogButton();
            changelogButton.OnPressed += args => UserInterfaceManager.GetUIController<ChangelogUIController>().ToggleWindow();
            buttons.AddChild(changelogButton);

            TutorialButton = new Button()
            {
                Text = Loc.GetString("server-info-introduction-button"),
                Visible = false,
            };
            TutorialButton.OnPressed += _ =>
            {
                _tutorial.OpenTrainingHopWindow();
            };
            buttons.AddChild(TutorialButton);

            void AddInfoButton(string loc, CVarDef<string> cVar)
            {
                var button = new Button { Text = Loc.GetString(loc) };
                button.OnPressed += _ => uriOpener.OpenUri(_cfg.GetCVar(cVar));
                buttons.AddChild(button);
                _infoLinks.Add((cVar, button));
            }
        }

        protected override void EnteredTree()
        {
            // LinkBanner is constructed before the client even connects to the server due to UI refactor stuff.
            // We need to update these buttons when the UI is shown.

            base.EnteredTree();

            foreach (var (cVar, link) in _infoLinks)
            {
                link.Visible = _cfg.GetCVar(cVar) != "";
            }

            _cfg.OnValueChanged(CCVars.TutorialMode, OnTutorialModeChanged);
            _cfg.OnValueChanged(CCVars.TutorialSolitaryServerConnectionString, OnTutorialHopChanged);
            UpdateTutorialButton();
        }

        protected override void ExitedTree()
        {
            _cfg.UnsubValueChanged(CCVars.TutorialMode, OnTutorialModeChanged);
            _cfg.UnsubValueChanged(CCVars.TutorialSolitaryServerConnectionString, OnTutorialHopChanged);
            base.ExitedTree();
        }

        private void OnTutorialModeChanged(string _) => UpdateTutorialButton();

        private void OnTutorialHopChanged(string _) => UpdateTutorialButton();

        private void UpdateTutorialButton()
        {
            if (TutorialButton == null)
                return;

            // hop leftover from main would otherwise keep this on the training box
            TutorialButton.Visible =
                _tutorial.GetIntroMode() == SharedTutorialSystem.IntroMain
                && !string.IsNullOrEmpty(_cfg.GetCVar(CCVars.TutorialSolitaryServerConnectionString));
        }
    }
}
