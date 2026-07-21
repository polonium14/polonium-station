using Content.Client.Resources;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets.Hud;

[CommonSheetlet]
public sealed class TargetingSheetlet : Sheetlet<PalettedStylesheet>
{
    private static readonly (string StyleClass, string TextureName)[] Parts =
    {
        ("TargetDollButtonHead", "head"),
        ("TargetDollButtonChest", "torso"),
        ("TargetDollButtonLeftArm", "leftarm"),
        ("TargetDollButtonRightArm", "rightarm"),
        ("TargetDollButtonLeftLeg", "leftleg"),
        ("TargetDollButtonRightLeg", "rightleg"),
    };

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var rules = new List<StyleRule>();

        foreach (var (styleClass, textureName) in Parts)
        {
            var texture = ResCache.GetTexture($"/Textures/_Shitmed/Interface/Targeting/Doll/{textureName}_hover.png");

            rules.Add(
                E<TextureButton>()
                    .Class(styleClass)
                    .PseudoHovered()
                    .Prop(TextureButton.StylePropertyTexture, texture)
                    .Prop(Control.StylePropertyModulateSelf, StyleNano.NanoGold));
        }

        return rules.ToArray();
    }
}
