using Content.Shared._Starlight.CCVar;

// ReSharper disable CheckNamespace
namespace Content.Client.Options.UI.Tabs;

public sealed partial class GraphicsTab
{
    private void AddLightingOptions()
    {
        Control.AddOptionCheckBox(StarlightCCVars.LightTonemap, LightTonemapCheckBox);
        var bloom = Control.AddOptionCheckBox(StarlightCCVars.Bloom, BloomCheckBox);

        Control.AddOptionPercentSlider(StarlightCCVars.BloomStrength, BloomStrengthSlider, 0f, 3f);
        Control.AddOptionPercentSlider(StarlightCCVars.BloomThreshold, BloomThresholdSlider, 0f, 0.6f);

        BloomRadiusSlider.Slider.Rounded = true;
        Control.AddOption(new OptionSliderFloatCVar(
            Control,
            _cfg,
            StarlightCCVars.BloomRadius,
            BloomRadiusSlider,
            2f,
            30f,
            1f,
            (_, value) => value.ToString("0")));

        bloom.ImmediateValueChanged += _ => UpdateLightingSettingsVisibility();
    }

    private void UpdateLightingSettingsVisibility()
    {
        BloomSettings.Visible = BloomCheckBox.Pressed;
    }
}
