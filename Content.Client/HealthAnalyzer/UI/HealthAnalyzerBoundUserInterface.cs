using Content.Shared._Shitmed.Medical.HealthAnalyzer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.HealthAnalyzer.UI
{
    [UsedImplicitly]
    public sealed class HealthAnalyzerBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private HealthAnalyzerWindow? _window;

        public HealthAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _window = this.CreateWindow<HealthAnalyzerWindow>();

            _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

            _window.OnModeChanged += (mode, owner) =>
                SendMessage(new HealthAnalyzerModeSelectedMessage(EntMan.GetNetEntity(owner), mode));
            _window.OnBodyPartSelected += (part, owner) =>
                SendMessage(new HealthAnalyzerPartSelectedMessage(EntMan.GetNetEntity(owner), part));
        }

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            if (_window == null)
                return;

            switch (message)
            {
                case HealthAnalyzerBodyMessage body:
                    _window.Populate(body);
                    break;
                case HealthAnalyzerOrgansMessage organs:
                    _window.Populate(organs);
                    break;
                case HealthAnalyzerChemicalsMessage chemicals:
                    _window.Populate(chemicals);
                    break;
            }
        }
    }
}
