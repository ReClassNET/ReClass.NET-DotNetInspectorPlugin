using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace BrightIdeasSoftware
{
    /// <summary>
    /// The common interface supported by all style objects
    /// </summary>
    public interface IItemStyle
    {
        /// <summary>
        /// Gets or set the font that will be used by this style
        /// </summary>
        Font Font { get; set; }

        /// <summary>
        /// Gets or set the font style
        /// </summary>
        FontStyle FontStyle { get; set; }

        /// <summary>
        /// Gets or sets the ForeColor
        /// </summary>
        Color ForeColor { get; set; }

        /// <summary>
        /// Gets or sets the BackColor
        /// </summary>
        Color BackColor { get; set; }
    }

    /// <summary>
    /// Basic implementation of IItemStyle
    /// </summary>
    public class SimpleItemStyle : System.ComponentModel.Component, IItemStyle
    {
        /// <summary>
        /// Gets or sets the font that will be applied by this style
        /// </summary>
        [DefaultValue(null)]
        public Font Font
        {
            get { return font; }
            set { font = value; }
        }

        private Font font;

        /// <summary>
        /// Gets or sets the style of font that will be applied by this style
        /// </summary>
        [DefaultValue(FontStyle.Regular)]
        public FontStyle FontStyle
        {
            get { return fontStyle; }
            set { fontStyle = value; }
        }

        private FontStyle fontStyle;

        /// <summary>
        /// Gets or sets the color of the text that will be applied by this style
        /// </summary>
        [DefaultValue(typeof (Color), "")]
        public Color ForeColor
        {
            get { return foreColor; }
            set { foreColor = value; }
        }

        private Color foreColor;

        /// <summary>
        /// Gets or sets the background color that will be applied by this style
        /// </summary>
        [DefaultValue(typeof (Color), "")]
        public Color BackColor
        {
            get { return backColor; }
            set { backColor = value; }
        }

        private Color backColor;
    }

    /// <summary>
    /// Instances of this class control one the styling of one particular state
    /// (normal, hot, pressed) of a header control
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class HeaderStateStyle
    {
        /// <summary>
        /// Gets or sets the font that will be applied by this style
        /// </summary>
        [DefaultValue(null)]
        public Font Font {
            get { return font; }
            set { font = value; }
        }
        private Font font;

        /// <summary>
        /// Gets or sets the color of the text that will be applied by this style
        /// </summary>
        [DefaultValue(typeof(Color), "")]
        public Color ForeColor {
            get { return foreColor; }
            set { foreColor = value; }
        }
        private Color foreColor;

        /// <summary>
        /// Gets or sets the background color that will be applied by this style
        /// </summary>
        [DefaultValue(typeof(Color), "")]
        public Color BackColor {
            get { return backColor; }
            set { backColor = value; }
        }
        private Color backColor;

        /// <summary>
        /// Gets or sets the color in which a frame will be drawn around the header for this column
        /// </summary>
        [DefaultValue(typeof(Color), "")]
        public Color FrameColor {
            get { return frameColor; }
            set { frameColor = value; }
        }
        private Color frameColor;

        /// <summary>
        /// Gets or sets the width of the frame that will be drawn around the header for this column
        /// </summary>
        [DefaultValue(0.0f)]
        public float FrameWidth {
            get { return frameWidth; }
            set { frameWidth = value; }
        }
        private float frameWidth;
    }

    /// <summary>
    /// This class defines how a header should be formatted in its various states.
    /// </summary>
    public class HeaderFormatStyle : System.ComponentModel.Component
    {
        /// <summary>
        /// Create a new HeaderFormatStyle
        /// </summary>
        public HeaderFormatStyle() {
			Hot = new HeaderStateStyle();
			Normal = new HeaderStateStyle();
			Pressed = new HeaderStateStyle();
        }

        /// <summary>
        /// What sort of formatting should be applied to a column header when the mouse is over it?
        /// </summary>
        [Category("Appearance"),
         Description("How should the header be drawn when the mouse is over it?")]
        public HeaderStateStyle Hot {
            get { return hotStyle; }
            set { hotStyle = value; }
        }
        private HeaderStateStyle hotStyle;

        /// <summary>
        /// What sort of formatting should be applied to a column header in its normal state?
        /// </summary>
        [Category("Appearance"),
         Description("How should a column header normally be drawn")]
        public HeaderStateStyle Normal {
            get { return normalStyle; }
            set { normalStyle = value; }
        }
        private HeaderStateStyle normalStyle;

        /// <summary>
        /// What sort of formatting should be applied to a column header when pressed?
        /// </summary>
        [Category("Appearance"),
         Description("How should a column header be drawn when it is pressed")]
        public HeaderStateStyle Pressed {
            get { return pressedStyle; }
            set { pressedStyle = value; }
        }
        private HeaderStateStyle pressedStyle;

        /// <summary>
        /// Set the font for all three states
        /// </summary>
        /// <param name="font"></param>
        public void SetFont(Font font) {
			Normal.Font = font;
			Hot.Font = font;
			Pressed.Font = font;
        }

        /// <summary>
        /// Set the fore color for all three states
        /// </summary>
        /// <param name="color"></param>
        public void SetForeColor(Color color) {
			Normal.ForeColor = color;
			Hot.ForeColor = color;
			Pressed.ForeColor = color;
        }
    }
}
