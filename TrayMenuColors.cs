using System.Drawing;
using System.Windows.Forms;

namespace QuickPrompt;

internal sealed class TrayMenuColors : ProfessionalColorTable
{
    private static readonly Color Panel = Color.FromArgb(23, 25, 30);
    private static readonly Color Hover = Color.FromArgb(43, 52, 73);
    private static readonly Color Line = Color.FromArgb(50, 54, 63);
    public TrayMenuColors() => UseSystemColors = false;
    public override Color ToolStripDropDownBackground => Panel;
    public override Color MenuItemSelected => Hover;
    public override Color MenuItemBorder => Hover;
    public override Color MenuBorder => Line;
    public override Color ImageMarginGradientBegin => Panel;
    public override Color ImageMarginGradientMiddle => Panel;
    public override Color ImageMarginGradientEnd => Panel;
    public override Color SeparatorDark => Line;
    public override Color SeparatorLight => Panel;
}
