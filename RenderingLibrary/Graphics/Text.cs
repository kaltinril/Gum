﻿using System;
using System.Collections.Generic;
using RenderingLibrary.Content;
using RenderingLibrary.Math.Geometry;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.ObjectModel;
using BlendState = Gum.BlendState;
using MathHelper = ToolsUtilitiesStandard.Helpers.MathHelper;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Color = System.Drawing.Color;
using Matrix = System.Numerics.Matrix4x4;
using System.Linq;
using ToolsUtilitiesStandard.Helpers;
using System.Drawing;

namespace RenderingLibrary.Graphics
{
    #region TextRenderingMode Enum

    public enum TextRenderingMode
    {
        RenderTarget,
        CharacterByCharacter
    }

    #endregion

    #region TextRenderingPositionMode

    public enum TextRenderingPositionMode
    {
        SnapToPixel,
        FreeFloating
    }

    #endregion

    #region InlineVariable

    public class InlineVariable
    {
        public string VariableName;
        public int StartIndex;
        public int CharacterCount;
        public object Value;
    }

    #endregion

    public class Text : IRenderableIpso, IVisible, IText
    {
        #region Fields

        static SpriteFont mDefaultSpriteFont;
        static BitmapFont mDefaultBitmapFont;

        public static SpriteFont DefaultFont
        {
            get { return mDefaultSpriteFont; }
            set { mDefaultSpriteFont = value; }
        }

        public static BitmapFont DefaultBitmapFont
        {
            get { return mDefaultBitmapFont; }
            set {  mDefaultBitmapFont = value; }
        }

        /// <summary>
        /// Stores the width of the text object's texture before it has had a chance to render, not including
        /// the FontScale.
        /// </summary>
        /// <remarks>
        /// A text object may need to be positioned according to its dimensions. Normally this would
        /// use a text's render target texture. In some situations (before the render pass has occurred,
        /// or when using character-by-character rendering), the text may not have a render target texture.
        /// Therefore, the pre-rendered values provide size information.
        /// </remarks>
        int? mPreRenderWidth;
        /// <summary>
        /// Stores the height of the text object's texture before it has had a chance to render, not including
        /// the FontScale.
        /// </summary>
        /// <remarks>
        /// See mPreRenderWidth for more information about this member.
        /// </remarks>
        int? mPreRenderHeight;

        public Vector2 Position;

        public Color Color
        {
            get
            {
                return Color.FromArgb(mAlpha, mRed, mGreen, mBlue);
            }
            set
            {
                mRed = value.R;
                mGreen = value.G;
                mBlue = value.B;
                mAlpha = value.A;
            }
        }

        string mRawText;
        List<string> mWrappedText = new List<string>();
        float mWidth = 200;
        float mHeight = 200;
        LinePrimitive mBounds;

        public List<InlineVariable> InlineVariables { get; private set; } = new List<InlineVariable>();

        BitmapFont mBitmapFont;
        Texture2D mTextureToRender;

        IRenderableIpso mParent;

        ObservableCollection<IRenderableIpso> mChildren;

        int mAlpha = 255;
        int mRed = 255;
        int mGreen = 255;
        int mBlue = 255;

        float mFontScale = 1;

        public bool mIsTextureCreationSuppressed;

        SystemManagers mManagers;

        bool mNeedsBitmapFontRefresh = true;

        // For now this is going to be app-wide, but...maybe we want to make this instance-based?  I'm not sure, but
        // I don't want to inflate each text object to support something that may not be used, so we'll start with a static.
        // It'll break code but it won't be hard to respond to.
        // As of 0.8.7, CharacterByCharacter is the standard in Gum tool
        public static TextRenderingMode TextRenderingMode = TextRenderingMode.CharacterByCharacter;

        public static TextRenderingPositionMode TextRenderingPositionMode = TextRenderingPositionMode.SnapToPixel;

        public TextRenderingPositionMode? OverrideTextRenderingPositionMode = null;

        #endregion

        #region Properties

        ColorOperation IRenderableIpso.ColorOperation => ColorOperation.Modulate;

        /// <summary>
        /// The width needed to display the wrapped text. 
        /// </summary>
        public float WrappedTextWidth
        {
            get
            {
                if (mPreRenderWidth != null)
                {
                    return mPreRenderWidth.Value * mFontScale;
                }
                else if (mTextureToRender?.Width > 0)
                {
                    return mTextureToRender.Width * mFontScale;
                }
                else
                {
                    return 0;
                }
            }
        }

        public float WrappedTextHeight
        {
            get
            {
                if (mPreRenderHeight != null)
                {
                    return mPreRenderHeight.Value * mFontScale;
                }
                else if (mTextureToRender?.Height > 0)
                {
                    return mTextureToRender.Height * mFontScale;
                }
                else
                {
                    return 0;
                }
            }
        }

        public static bool RenderBoundaryDefault
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        int? maxLettersToShow;
        /// <summary>
        /// The maximum letters to display. This can be used to 
        /// create an effect where the text prints out letter-by-letter.
        /// </summary>
        public int? MaxLettersToShow
        {
            get => maxLettersToShow;
            set
            {
                if (maxLettersToShow != value)
                {
                    maxLettersToShow = value;

                    mNeedsBitmapFontRefresh = true;
                }
            }
        }

        int? maxNumberOfLines;
        public int? MaxNumberOfLines
        {
            get => maxNumberOfLines;
            set
            {
                if (maxNumberOfLines != value)
                {
                    maxNumberOfLines = value;
                    UpdateWrappedText();

                    UpdatePreRenderDimensions();
                }
            }
        }

        public bool IsTruncatingWithEllipsisOnLastLine { get; set; }
            // temp:
            = true;

        public string RawText
        {
            get
            {
                return mRawText;
            }
            set
            {
                if (mRawText != value)
                {
                    mRawText = value;
                    UpdateWrappedText();

                    UpdatePreRenderDimensions();
                }
            }
        }

        public List<string> WrappedText
        {
            get
            {
                return mWrappedText;
            }
        }

        public float X
        {
            get
            {
                return Position.X;
            }
            set
            {
                Position.X = value;
            }
        }

        public float Y
        {
            get
            {
                return Position.Y;
            }
            set
            {
                Position.Y = value;
            }
        }

        public bool FlipHorizontal { get; set; }

        public float Rotation { get; set; }

        public float Width
        {
            get
            {
                return mWidth;
            }
            set
            {
                if (mWidth != value)
                {
                    mWidth = value;
                    UpdateWrappedText();
                    UpdateLinePrimitive();
                    UpdatePreRenderDimensions();
                }

            }
        }

        public float Height
        {
            get
            {
                return mHeight;
            }
            set
            {
                if (mHeight != value)
                {
                    mHeight = value;

                    if (TextOverflowVerticalMode != TextOverflowVerticalMode.SpillOver)
                    {
                        UpdateWrappedText();
                    }

                    UpdateLinePrimitive();

                    if (TextOverflowVerticalMode != TextOverflowVerticalMode.SpillOver)
                    {
                        UpdatePreRenderDimensions();
                    }

                }
            }
        }

        public float EffectiveWidth
        {
            get
            {
                // I think we want to treat these individually so a 
                // width could be set but height could be default
                if (Width != 0)
                {
                    return Width;
                }
                // If there is a prerendered width/height, then that means that
                // the width/height has updated but it hasn't yet made its way to the
                // texture. This could happen when the text already has a texture, so give
                // priority to the prerendered values as they may be more up-to-date.
                else if (mPreRenderWidth.HasValue)
                {
                    return mPreRenderWidth.Value * mFontScale;
                }
                else if (mTextureToRender != null)
                {
                    if (mTextureToRender.Width == 0)
                    {
                        return 10;
                    }
                    else
                    {
                        return mTextureToRender.Width * mFontScale;
                    }
                }
                else
                {
                    // This causes problems when the text object has no text:
                    //return 32;
                    return 0;
                }
            }
        }

        public float EffectiveHeight
        {
            get
            {
                // See comment in Width
                if (Height != 0)
                {
                    return Height;
                }
                // See EffectiveWidth for an explanation of why the prerendered values need to come first
                else if (mPreRenderHeight.HasValue)
                {
                    return mPreRenderHeight.Value * mFontScale;
                }
                else if (mTextureToRender != null)
                {
                    if (mTextureToRender.Height == 0)
                    {
                        return 10;
                    }
                    else
                    {
                        return mTextureToRender.Height * mFontScale;
                    }
                }
                else
                {
                    return 32;
                }
            }
        }


        bool IRenderableIpso.ClipsChildren
        {
            get
            {
                return false;
            }
        }

        public HorizontalAlignment HorizontalAlignment
        {
            get;
            set;
        }

        public VerticalAlignment VerticalAlignment
        {
            get;
            set;
        }

        public IRenderableIpso Parent
        {
            get { return mParent; }
            set
            {
                if (mParent != value)
                {
                    if (mParent != null)
                    {
                        mParent.Children.Remove(this);
                    }
                    mParent = value;
                    if (mParent != null)
                    {
                        mParent.Children.Add(this);
                    }
                }
            }
        }

        TextOverflowVerticalMode textOverflowVerticalMode;
        public TextOverflowVerticalMode TextOverflowVerticalMode
        {
            get => textOverflowVerticalMode;
            set
            {
                if (textOverflowVerticalMode != value)
                {
                    textOverflowVerticalMode = value;
                    UpdateWrappedText();
                    UpdatePreRenderDimensions();
                }
            }
        }

        public float Z
        {
            get;
            set;
        }

        public BitmapFont BitmapFont
        {
            get
            {
                return mBitmapFont;
            }
            set
            {
                mBitmapFont = value;

                UpdateWrappedText();
                UpdatePreRenderDimensions();

                mNeedsBitmapFontRefresh = true;
                //UpdateTextureToRender();
            }
        }

        public ObservableCollection<IRenderableIpso> Children
        {
            get { return mChildren; }
        }

        public int Alpha
        {
            get { return mAlpha; }
            set { mAlpha = value; }
        }

        public int Red
        {
            get { return mRed; }
            set { mRed = value; }
        }

        public int Green
        {
            get { return mGreen; }
            set { mGreen = value; }
        }

        public int Blue
        {
            get { return mBlue; }
            set { mBlue = value; }
        }

        public float FontScale
        {
            get { return mFontScale; }
            set
            {
                var newValue = System.Math.Max(0, value);

                if (newValue != mFontScale)
                {
                    mFontScale = newValue;
                    UpdateWrappedText();
                    mNeedsBitmapFontRefresh = true;
                    UpdatePreRenderDimensions();
                }
            }
        }

        public object Tag { get; set; }

        public BlendState BlendState { get; set; }

        Renderer Renderer
        {
            get
            {
                if (mManagers == null)
                {
                    return Renderer.Self;
                }
                else
                {
                    return mManagers.Renderer;
                }
            }
        }

        public bool RenderBoundary
        {
            get;
            set;
        }

        public bool Wrap
        {
            get { return false; }
        }

        float IPositionedSizedObject.Width
        {
            get
            {
                return EffectiveWidth;
            }
            set
            {
                Width = value;
            }
        }

        float IPositionedSizedObject.Height
        {
            get
            {
                return EffectiveHeight;
            }
            set
            {
                Height = value;
            }
        }

        public float DescenderHeight => BitmapFont?.DescenderHeight ?? 0;

        public float LineHeightMultiplier { get; set; } = 1;


        #endregion

        #region Methods

        static Text()
        {
            RenderBoundaryDefault = true;
        }

        public Text(SystemManagers managers, string text = "Hello")
        {
            Visible = true;
            RenderBoundary = RenderBoundaryDefault;

            mManagers = managers;
            mChildren = new ObservableCollection<IRenderableIpso>();

            mRawText = text;
            mNeedsBitmapFontRefresh = true;
            mBounds = new LinePrimitive(this.Renderer.SinglePixelTexture);
            mBounds.Color = Color.LightGreen;

            mBounds.Add(0, 0);
            mBounds.Add(0, 0);
            mBounds.Add(0, 0);
            mBounds.Add(0, 0);
            mBounds.Add(0, 0);
            HorizontalAlignment = Graphics.HorizontalAlignment.Left;
            VerticalAlignment = Graphics.VerticalAlignment.Top;

#if !TEST
            if (DefaultBitmapFont != null)
            {
                this.BitmapFont = DefaultBitmapFont;
            }
#endif
            UpdateLinePrimitive();
        }

        char[] whatToSplitOn = new char[] { ' ' };
        private void UpdateWrappedText()
        {
            ///////////EARLY OUT/////////////
            if (this.BitmapFont == null)
            {
                return;
            }

            mWrappedText.Clear();

            var effectiveMaxNumberOfLines = MaxNumberOfLines;

            if (TextOverflowVerticalMode == TextOverflowVerticalMode.TruncateLine)
            {

                var maxLinesFromHeight = (int)(Height / BitmapFont.LineHeightInPixels);
                if (maxLinesFromHeight < effectiveMaxNumberOfLines || effectiveMaxNumberOfLines == null)
                {
                    effectiveMaxNumberOfLines = maxLinesFromHeight;
                }
            }

            if (effectiveMaxNumberOfLines == 0)
            {
                return;
            }
            /////////END EARLY OUT///////////

            bool didTruncate = false;

            float wrappingWidth = mWidth / mFontScale;
            if (mWidth == 0)
            {
                wrappingWidth = float.PositiveInfinity;
            }

            // This allocates like crazy but we're
            // on the PC and prob won't be calling this
            // very frequently so let's 
            String line = String.Empty;
            String returnString = String.Empty;

            // The user may have entered "\n" in the string, which would 
            // be written as "\\n".  Let's replace that, shall we?
            string stringToUse = null;
            List<string> wordArray = new List<string>();

            if (!string.IsNullOrEmpty(mRawText))
            {
                // multiline text editing in Gum can add \r's, so get rid of those:
                stringToUse = mRawText.Replace("\r\n", "\n");
                wordArray.AddRange(stringToUse.Split(whatToSplitOn));
            }

            float ellipsisWidth = 0;
            const string ellipsis = "...";
            if (effectiveMaxNumberOfLines > 0 && IsTruncatingWithEllipsisOnLastLine)
            {
                ellipsisWidth = MeasureString(ellipsis);
            }

            bool isLastLine = false;
            while (wordArray.Count != 0)
            {
                isLastLine = effectiveMaxNumberOfLines != null && mWrappedText.Count == effectiveMaxNumberOfLines - 1;

                string word = wordArray[0];
                var wordBeforeNewlineRemoval = word;
                var isLastWord = wordArray.Count == 1;

                bool containsNewline = false;

                if (ToolsUtilities.StringFunctions.ContainsNoAlloc(word, '\n'))
                {
                    word = word.Substring(0, word.IndexOf('\n'));
                    containsNewline = true;
                }

                // If it's not the last word, we show ellipsis, and the last word plus ellipsis won't fit, then we need
                // to include part of the word:

                float linePlusWordWidth = MeasureString(line + word);

                var shouldAddEllipsis =
                    IsTruncatingWithEllipsisOnLastLine &&
                    isLastLine &&
                    // If it's the last word, then we don't care if the ellipsis fit, we only want to see if the last word fits...
                    ((isLastWord && linePlusWordWidth > wrappingWidth) ||
                     // it's not the last word so we need to see if ellipsis fit
                     (!isLastWord && linePlusWordWidth + ellipsisWidth >= wrappingWidth));
                if (shouldAddEllipsis)
                {
                    var addedEllipsis = false;
                    for (int i = 1; i < word.Length; i++)
                    {
                        var substringEnd = word.SubstringEnd(i);

                        float linePlusWordSub = MeasureString(line + substringEnd);

                        if (linePlusWordSub + ellipsisWidth <= wrappingWidth)
                        {
                            mWrappedText.Add(line + substringEnd + ellipsis);
                            addedEllipsis = true;
                            break;
                        }
                    }

                    if (!addedEllipsis && line.EndsWith(" "))
                    {
                        mWrappedText.Add(line.SubstringEnd(1) + ellipsis);

                    }
                    break;
                }

                if (linePlusWordWidth > wrappingWidth)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        mWrappedText.Add(line);
                        if (mWrappedText.Count == effectiveMaxNumberOfLines)
                        {
                            didTruncate = true;
                            break;
                        }
                    }

                    //returnString = returnString + line + '\n';
                    line = String.Empty;
                }

                // If it's the first word and it's empty, don't add anything
                // update - but this prevents the word from sarting 
                //if ((!string.IsNullOrEmpty(word) || !string.IsNullOrEmpty(line)))
                {
                    if (wordArray.Count > 1 || word == "")
                    {
                        line = line + word + ' ';
                    }
                    else
                    {
                        line = line + word;
                    }
                }

                wordArray.RemoveAt(0);

                if (containsNewline)
                {
                    mWrappedText.Add(line);
                    if (mWrappedText.Count == effectiveMaxNumberOfLines)
                    {
                        didTruncate = true;

                        break;
                    }
                    line = string.Empty;
                    int indexOfNewline = wordBeforeNewlineRemoval.IndexOf('\n');
                    wordArray.Insert(0, wordBeforeNewlineRemoval.Substring(indexOfNewline + 1, wordBeforeNewlineRemoval.Length - (indexOfNewline + 1)));
                }
            }

            if (effectiveMaxNumberOfLines == null || mWrappedText.Count < effectiveMaxNumberOfLines)
            {
                mWrappedText.Add(line);
            }

            //if(didTruncate && AddEllipsisOnLastLine && mWrappedText.Count > 0)
            //{
            //    var lastLine = mWrappedText[mWrappedText.Count-1];

            //    int numberOfCharactersToRemove = 1;

            //    while(numberOfCharactersToRemove < lastLine.Length)
            //    {
            //        var endSubstring = lastLine.Substring(lastLine.Length - numberOfCharactersToRemove);

            //        var isLongEnough = MeasureString(endSubstring) > ellipsisWidth;

            //        if(isLongEnough)
            //        {
            //            break;
            //        }
            //        else
            //        {
            //            numberOfCharactersToRemove++;
            //        }
            //    }

            //    mWrappedText[mWrappedText.Count-1] = lastLine.Substring(0, lastLine.Length - numberOfCharactersToRemove) + "...";

            //}
            //if (mManagers == null || mManagers.IsCurrentThreadPrimary)
            //{
            //    UpdateTextureToRender();
            //}
            //else
            {
                mNeedsBitmapFontRefresh = true;
            }
        }

        private float MeasureString(string whatToMeasure)
        {
            if (this.BitmapFont != null)
            {
                return BitmapFont.MeasureString(whatToMeasure);
            }
            else if (DefaultBitmapFont != null)
            {
                return DefaultBitmapFont.MeasureString(whatToMeasure);
            }
            else
            {
#if TEST
                return 0;
#else
                float wordWidth = DefaultFont.MeasureString(whatToMeasure).X;
                return wordWidth;
#endif
            }
        }

        // made public so that objects that need to position based off of the texture can force call this
        public void TryUpdateTextureToRender()
        {
            if (mNeedsBitmapFontRefresh)
            {
                UpdateTextureToRender();
            }
        }

        void IRenderable.PreRender()
        {
            TryUpdateTextureToRender();
        }

        public void UpdateTextureToRender()
        {
            if (!mIsTextureCreationSuppressed && TextRenderingMode == TextRenderingMode.RenderTarget)
            {
                BitmapFont fontToUse = mBitmapFont;
                if (mBitmapFont == null)
                {
                    fontToUse = DefaultBitmapFont;
                }


                if (fontToUse != null)
                {
                    //if (mTextureToRender != null)
                    //{
                    //    mTextureToRender.Dispose();
                    //    mTextureToRender = null;
                    //}

                    var returnedRenderTarget = fontToUse.RenderToTexture2D(WrappedText, this.HorizontalAlignment,
                        mManagers, mTextureToRender, this, MaxLettersToShow);
                    bool isNewInstance = returnedRenderTarget != mTextureToRender;

                    if (isNewInstance && mTextureToRender != null)
                    {
                        mTextureToRender.Dispose();

                        if (mTextureToRender is RenderTarget2D)
                        {
                            (mTextureToRender as RenderTarget2D).ContentLost -= SetNeedsRefresh;
                        }
                        mTextureToRender = null;
                    }
                    mTextureToRender = returnedRenderTarget;

                    if (isNewInstance && mTextureToRender is RenderTarget2D)
                    {
                        (mTextureToRender as RenderTarget2D).ContentLost += SetNeedsRefresh;
                        mTextureToRender.Name = "Render Target for Text " + this.Name;

                    }
                }
                else if (mBitmapFont == null)
                {
                    if (mTextureToRender != null)
                    {
                        mTextureToRender.Dispose();
                        mTextureToRender = null;
                    }
                }

                mPreRenderWidth = null;
                mPreRenderHeight = null;

                mNeedsBitmapFontRefresh = false;
            }
        }

        void SetNeedsRefresh(object sender, EventArgs args)
        {
            mNeedsBitmapFontRefresh = true;
        }

        void UpdateLinePrimitive()
        {
            LineRectangle.UpdateLinePrimitive(mBounds, this);

        }


        public void Render(ISystemManagers managers)
        {
            if (AbsoluteVisible)
            {
                var systemManagers = (SystemManagers)managers;
                var spriteRenderer = systemManagers.Renderer.SpriteRenderer;
                // Moved this out of here - it's manually called by the TextManager
                // This is required because we can't update in the draw call now that
                // we're using RenderTargets
                //if (mNeedsBitmapFontRefresh)
                //{
                //    UpdateTextureToRender();
                //}
                if (RenderBoundary)
                {
                    LineRectangle.RenderLinePrimitive(mBounds, spriteRenderer, this, systemManagers, false);
                }

                if (TextRenderingMode == TextRenderingMode.CharacterByCharacter)
                {
                    RenderCharacterByCharacter(spriteRenderer);
                }
                else // RenderTarget
                {
                    if (mTextureToRender == null)
                    {
                        RenderUsingSpriteFont(spriteRenderer);
                    }
                    else
                    {
                        RenderUsingBitmapFont(spriteRenderer, systemManagers);
                    }
                }
            }
        }

        // todo: reduce allocs by using a static here (static is prob okay since it can't be multithreaded)
        static List<int> widths = new List<int>();
        private void RenderCharacterByCharacter(SpriteRenderer spriteRenderer)
        {
            BitmapFont fontToUse = mBitmapFont;
            if (mBitmapFont == null)
            {
                fontToUse = DefaultBitmapFont;
            }


            if (fontToUse != null)
            {
                widths.Clear();
                int requiredWidth;
                fontToUse.GetRequiredWidthAndHeight(WrappedText, out requiredWidth, out int _, widths);
                UpdateIpsoForRendering();


                if (InlineVariables.Count > 0)
                {
                    DrawWithInlineVariables(fontToUse, requiredWidth, spriteRenderer);
                }
                else
                {
                    var absoluteLeft = mTempForRendering.GetAbsoluteLeft();
                    var absoluteTop = mTempForRendering.GetAbsoluteTop();
                    fontToUse.DrawTextLines(WrappedText, HorizontalAlignment,
                        this,
                        requiredWidth, widths, spriteRenderer, Color,
                        absoluteLeft,
                        absoluteTop,
                        this.GetAbsoluteRotation(), mFontScale, mFontScale, maxLettersToShow, OverrideTextRenderingPositionMode, lineHeightMultiplier: LineHeightMultiplier);
                }

            }
        }

        List<string> lineByLineList = new List<string>() { "" };

        class StyledSubstring
        {
            public List<InlineVariable> Variables = new List<InlineVariable>();
            public string Substring;
            public int StartIndex;

            public override string ToString()
            {
                var toReturn = Substring ?? "<null>";

                foreach (var variable in Variables)
                {
                    toReturn += $" {variable.VariableName} = {variable.Value}";
                }
                return toReturn;
            }
        }

        private void DrawWithInlineVariables(BitmapFont fontToUse, int requiredWidth, SpriteRenderer spriteRenderer)
        {
            var absoluteTop = mTempForRendering.GetAbsoluteTop();

            int startOfLineIndex = 0;

            var rotation = this.GetAbsoluteRotation();
            float topOfLine = absoluteTop;
            for (int i = 0; i < WrappedText.Count; i++)
            {
                var absoluteLeft = mTempForRendering.GetAbsoluteLeft();
                var lineOfText = WrappedText[i];

                var color = Color;

                var substrings = GetStyledSubstrings(startOfLineIndex, lineOfText, color);

                if (substrings.Count == 0)
                {
                    lineByLineList[0] = lineOfText;
                    fontToUse.DrawTextLines(lineByLineList, HorizontalAlignment,
                        this,
                        requiredWidth, widths, spriteRenderer, color,
                        absoluteLeft,
                        topOfLine,
                        this.GetAbsoluteRotation(), mFontScale, mFontScale, maxLettersToShow, OverrideTextRenderingPositionMode, lineHeightMultiplier: LineHeightMultiplier);

                    topOfLine += fontToUse.EffectiveLineHeight(mFontScale, mFontScale);

                }
                else
                {

                    var lineHeight = fontToUse.EffectiveLineHeight(mFontScale, 1);
                    var defaultBaseline = fontToUse.BaselineY;

                    float currentFontScale = FontScale;
                    BitmapFont currentFont = fontToUse;
                    foreach (var substring in substrings)
                    {
                        for (int variableIndex = 0; variableIndex < substring.Variables.Count; variableIndex++)
                        {
                            var variable = substring.Variables[variableIndex];
                            if (variable.VariableName == nameof(FontScale))
                            {
                                currentFontScale = (float)variable.Value;
                                lineHeight = System.Math.Max(lineHeight, currentFont.EffectiveLineHeight(currentFontScale, 1));
                            }
                            else if (variable.VariableName == nameof(BitmapFont))
                            {
                                currentFont = (BitmapFont)variable.Value;
                                lineHeight = System.Math.Max(lineHeight, currentFont.EffectiveLineHeight(currentFontScale, 1));
                            }
                        }
                    }

                    foreach (var substring in substrings)
                    {
                        lineByLineList[0] = substring.Substring;
                        color = Color;
                        var fontScale = mFontScale;
                        var effectiveFont = fontToUse;
                        for (int variableIndex = 0; variableIndex < substring.Variables.Count; variableIndex++)
                        {
                            var variable = substring.Variables[variableIndex];
                            if (variable.VariableName == nameof(Color))
                            {
                                color = (System.Drawing.Color)variable.Value;
                            }
                            else if (variable.VariableName == nameof(FontScale))
                            {
                                fontScale = (float)variable.Value;
                            }
                            else if (variable.VariableName == nameof(BitmapFont))
                            {
                                effectiveFont = (BitmapFont)variable.Value;
                            }
                            else if (variable.VariableName == nameof(Red))
                            {
                                color = color.WithRed((byte)variable.Value);
                            }
                            else if (variable.VariableName == nameof(Green))
                            {
                                color = color.WithGreen((byte)variable.Value);
                            }
                            else if (variable.VariableName == nameof(Blue))
                            {
                                color = color.WithBlue((byte)variable.Value);
                            }

                        }

                        var effectiveTopOfLine = topOfLine;

                        if (fontToUse != effectiveFont)
                        {
                            var baselineDifference = fontToUse.BaselineY - effectiveFont.BaselineY;
                            effectiveTopOfLine += baselineDifference * fontScale;
                        }

                        var rect = effectiveFont.DrawTextLines(lineByLineList, HorizontalAlignment,
                            this,
                            requiredWidth, widths, spriteRenderer, color,
                            absoluteLeft,
                            effectiveTopOfLine,
                            rotation, fontScale, fontScale, maxLettersToShow, OverrideTextRenderingPositionMode, lineHeightMultiplier: LineHeightMultiplier);

                        absoluteLeft += rect.Width;

                    }

                    topOfLine += lineHeight;
                }
                startOfLineIndex += lineOfText.Length;
            }
        }

        private List<StyledSubstring> GetStyledSubstrings(int startOfLineIndex, string lineOfText, Color color)
        {
            List<StyledSubstring> substrings = new List<StyledSubstring>();
            int currentSubstringStart = 0;

            List<InlineVariable> currentlyActiveInlines = new List<InlineVariable>();
            List<InlineVariable> inlinesForThisCharacter = new List<InlineVariable>();

            int relativeLetterIndex = 0;
            for (; relativeLetterIndex < lineOfText.Length; relativeLetterIndex++)
            {
                inlinesForThisCharacter.Clear();
                var absoluteIndex = startOfLineIndex + relativeLetterIndex;

                var startNewRun = relativeLetterIndex == 0;
                var endLastRun = false;
                foreach (var variable in InlineVariables)
                {

                    if (absoluteIndex >= variable.StartIndex && absoluteIndex < variable.StartIndex + variable.CharacterCount)
                    {
                        if (currentlyActiveInlines.Contains(variable) == false)
                        {
                            startNewRun = true;
                            endLastRun = true;
                        }
                        inlinesForThisCharacter.Add(variable);
                    }
                }

                foreach (var variable in currentlyActiveInlines)
                {
                    if (absoluteIndex >= variable.StartIndex + variable.CharacterCount)
                    {
                        startNewRun = true;
                        endLastRun = true;
                    }
                }

                if(endLastRun && substrings.Count > 0)
                {
                    var lastSubstring = substrings.Last();
                    lastSubstring.Substring = lineOfText.Substring(currentSubstringStart, relativeLetterIndex - currentSubstringStart);
                }

                if (startNewRun)
                {
                    currentSubstringStart = relativeLetterIndex;

                    var styledSubstring = new StyledSubstring();
                    styledSubstring.Variables.AddRange(inlinesForThisCharacter);
                    styledSubstring.StartIndex = relativeLetterIndex;

                    if (relativeLetterIndex == lineOfText.Length - 1)
                    {
                        styledSubstring.Substring = lineOfText.Substring(currentSubstringStart);
                    }

                    substrings.Add(styledSubstring);

                    currentlyActiveInlines.Clear();
                    currentlyActiveInlines.AddRange(inlinesForThisCharacter);
                }
            }

            var endSubstring = substrings.LastOrDefault();
            if (endSubstring != null)
            {
                endSubstring.Substring = lineOfText.Substring(currentSubstringStart, relativeLetterIndex - currentSubstringStart);
            }

            //if (lastSubstring == null && substrings.Count == 0 )
            //{
            //    var styledSubstring = new StyledSubstring();
            //    // no styles
            //    styledSubstring.Substring = lineOfText.Substring(0, letter);
            //    styledSubstring.StartIndex = startOfLineIndex;
            //    substrings.Add(styledSubstring);
            //}

            return substrings;
        }

        private void RenderUsingBitmapFont(SpriteRenderer spriteRenderer, SystemManagers managers)
        {
            UpdateIpsoForRendering();

            if (mBitmapFont?.AtlasedTexture != null)
            {
                mBitmapFont.RenderAtlasedTextureToScreen(WrappedText, this.HorizontalAlignment, mTextureToRender.Height,
                    Color.FromArgb(mAlpha, mRed, mGreen, mBlue), Rotation, mFontScale, managers, spriteRenderer, this);
            }
            else
            {
                Sprite.Render(managers, spriteRenderer, mTempForRendering, mTextureToRender,
                    Color.FromArgb(mAlpha, mRed, mGreen, mBlue), null, false, Rotation,
                    treat0AsFullDimensions: false,
                    objectCausingRendering: this);

            }
        }

        private void UpdateIpsoForRendering()
        {
            if (mTempForRendering == null)
            {
                // Why do we need managers?
                //mTempForRendering = new LineRectangle(managers);
                // And why do we even need a line rectangle?
                mTempForRendering = new InvisibleRenderable();
            }

            mTempForRendering.X = this.X;
            mTempForRendering.Y = this.Y;

            if (mPreRenderWidth.HasValue)
            {
                mTempForRendering.Width = this.mPreRenderWidth.Value * mFontScale;
                mTempForRendering.Height = this.mPreRenderHeight.Value * mFontScale;
            }
            else
            {
                mTempForRendering.Width = this.mTextureToRender.Width * mFontScale;
                mTempForRendering.Height = this.mTextureToRender.Height * mFontScale;
            }
            //mTempForRendering.Parent = this.Parent;

            float widthDifference = this.EffectiveWidth - mTempForRendering.Width;

            Vector3 alignmentOffset = Vector3.Zero;

            if (this.HorizontalAlignment == Graphics.HorizontalAlignment.Center)
            {
                alignmentOffset.X = widthDifference / 2.0f;
            }
            else if (this.HorizontalAlignment == Graphics.HorizontalAlignment.Right)
            {
                alignmentOffset.X = widthDifference;
            }

            if (this.VerticalAlignment == Graphics.VerticalAlignment.Center)
            {
                alignmentOffset.Y = (this.EffectiveHeight - mTempForRendering.Height) / 2.0f;
            }
            else if (this.VerticalAlignment == Graphics.VerticalAlignment.Bottom)
            {
                alignmentOffset.Y = this.EffectiveHeight - mTempForRendering.Height;
            }

            var absoluteRotation = this.GetAbsoluteRotation();
            if (absoluteRotation != 0)
            {
                var matrix = Matrix.CreateRotationZ(-MathHelper.ToRadians(absoluteRotation));

                alignmentOffset = Vector3.Transform(alignmentOffset, matrix);
            }

            mTempForRendering.X += alignmentOffset.X;
            mTempForRendering.Y += alignmentOffset.Y;

            if (this.Parent != null)
            {
                mTempForRendering.X += Parent.GetAbsoluteX();
                mTempForRendering.Y += Parent.GetAbsoluteY();

            }
        }

        IRenderableIpso mTempForRendering;

        private void RenderUsingSpriteFont(SpriteRenderer spriteRenderer)
        {

            Vector2 offset = new Vector2(this.Renderer.Camera.RenderingXOffset, Renderer.Camera.RenderingYOffset);

            float leftSide = offset.X + this.GetAbsoluteX();
            float topSide = offset.Y + this.GetAbsoluteY();

            SpriteFont font = DefaultFont;
            // Maybe this hasn't been loaded yet?
            if (font != null)
            {
                var lineCount = mWrappedText.Count;
                switch (this.VerticalAlignment)
                {
                    case Graphics.VerticalAlignment.Top:
                        offset.Y = topSide;
                        break;
                    case Graphics.VerticalAlignment.Bottom:
                        {
                            float requiredHeight = (lineCount) * font.LineSpacing;

                            offset.Y = topSide + (this.Height - requiredHeight);

                            break;
                        }
                    case Graphics.VerticalAlignment.Center:
                        {
                            float requiredHeight = (lineCount) * font.LineSpacing;

                            offset.Y = topSide + (this.Height - requiredHeight) / 2.0f;
                            break;
                        }
                }



                float offsetY = offset.Y;

                for (int i = 0; i < lineCount; i++)
                {
                    offset.X = leftSide;
                    offset.Y = (int)offsetY;

                    string line = mWrappedText[i];

                    if (HorizontalAlignment == Graphics.HorizontalAlignment.Right)
                    {
                        offset.X = leftSide + (Width - font.MeasureString(line).X);
                    }
                    else if (HorizontalAlignment == Graphics.HorizontalAlignment.Center)
                    {
                        offset.X = leftSide + (Width - font.MeasureString(line).X) / 2.0f;
                    }

                    offset.X = (int)offset.X; // so we don't have half-pixels that render weird

                    spriteRenderer.DrawString(font, line, offset, Color, this);
                    offsetY += DefaultFont.LineSpacing;
                }
            }
        }

        public override string ToString()
        {
            return this.Name;
        }

        public void SuppressTextureCreation()
        {
            mIsTextureCreationSuppressed = true;
        }

        public void EnableTextureCreation()
        {
            mIsTextureCreationSuppressed = false;
            mNeedsBitmapFontRefresh = true;
            //UpdateTextureToRender();
        }

        public void SetNeedsRefreshToTrue()
        {
            mNeedsBitmapFontRefresh = true;
        }

        public void UpdatePreRenderDimensions()
        {

            if (this.mBitmapFont != null)
            {
                int requiredWidth = 0;
                int requiredHeight = 0;

                if (this.mRawText != null)
                {
                    mBitmapFont.GetRequiredWidthAndHeight(WrappedText, out requiredWidth, out requiredHeight);
                }

                mPreRenderWidth = (int)(requiredWidth + .5f);
                mPreRenderHeight = (int)(requiredHeight * LineHeightMultiplier + .5f);
            }
        }
        #endregion

        void IRenderableIpso.SetParentDirect(IRenderableIpso parent)
        {
            mParent = parent;
        }

        #region IVisible Implementation

        public bool Visible
        {
            get;
            set;
        }

        public bool AbsoluteVisible
        {
            get
            {
                if (((IVisible)this).Parent == null)
                {
                    return Visible;
                }
                else
                {
                    return Visible && ((IVisible)this).Parent.AbsoluteVisible;
                }
            }
        }

        IVisible IVisible.Parent
        {
            get
            {
                return ((IRenderableIpso)this).Parent as IVisible;
            }
        }

        #endregion
    }

    public static class StringExtensions
    {
        public static string SubstringEnd(this string value, int lettersToRemove)
        {
            if (value.Length <= lettersToRemove)
            {
                return string.Empty;
            }
            else
            {
                return value.Substring(0, value.Length - lettersToRemove);
            }
        }
    }

}
