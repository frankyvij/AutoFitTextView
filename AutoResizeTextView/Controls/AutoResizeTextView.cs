using Android.Content;
using Android.Runtime;
using Android.Support.V7.Widget;
using Android.Graphics;
using Android.Text;
using Android.Util;
using Java.Lang;
using Android.Text.Method;
using System;
using System.Linq;
using Android.Widget;
using static Android.Text.Layout;
using System.Collections.Generic;
using Android.Content.Res;
using System.Collections.Concurrent;

namespace AutoResizeTextView.Controls
{
    public interface ISizeTester
    {
        /// <summary>
        /// Test the suggested size to see if it fits the boundaries
        /// </summary>
        /// <param name="suggestedSize"></param>
        /// <param name="availableSpace"></param>
        /// <returns></returns>
        int OnTestSize(int suggestedSize, RectF availableSpace);
    }

    public enum FieldValueType
    {
        NotSet = 0,
        GeneralText = 1
    }

    public class AutoResizeHelper : ISizeTester
    {
        // Target Textview Control
        private TextView _targetControl;
        public TextView targetControl
        {
            get { return _targetControl; }
            set
            {
                _targetControl = value;
                InitHelper();
            }
        }

        // Make calculation of text size dynamic
        public bool IsDynamic { get; set; }

        // Calculated size of the textview
        public int? calculatedSize = null;

        // If max-lines == 0, set the below line limit
        public static int NO_LINE_LIMIT = -1;

        // Available space to fit the text
        private RectF availableSpaceRect = new RectF();

        // Size tester
        private ISizeTester sizeTester;

        // Variables
        public float maxTextSize, spacingMult = 1.0f, spacingAdd = 0.0f, minTextSize;
        public int widthLimit, maxLines;
        private TextPaint paint;

        // Flag to manage initialization
        private bool initialized = false;

        #region Variables for efficiency

        // Manage enums
        public FieldValueType fieldValueType = FieldValueType.NotSet;

        // Store best size information
        public static ConcurrentDictionary<AutoResizeHelper, int> resizedControls = new ConcurrentDictionary<AutoResizeHelper, int>();

        #endregion

        // Initialize helper after the target control is set/updated
        private void InitHelper()
        {
            // Set the default Typeface: NotoSans
            targetControl.Typeface = Typeface.Create("NotoSans", TypefaceStyle.Normal);

            // Using the minimal recommended font size
            minTextSize = TypedValue.ApplyDimension(ComplexUnitType.Sp, 7, targetControl.Resources.DisplayMetrics);
            maxTextSize = TypedValue.ApplyDimension(ComplexUnitType.Sp, 99, targetControl.Resources.DisplayMetrics);

            paint = new TextPaint(targetControl.Paint);
            sizeTester = this;

            if (maxLines == 0)
                // No value was assigned during construction
                maxLines = NO_LINE_LIMIT;

            initialized = true;
        }

        // Check if it is a valid word wrap
        private bool IsValidWordWrap(char before, char after)
        {
            var numberOfBytes = System.Text.Encoding.UTF8.GetBytes(new char[] { before });
            return before == ' ' || before == '-' || before == '\n' || numberOfBytes.Length > 2;
        }

        // Adjust text size if required
        // i.e. if control is set to be dynamic, calculate the size again
        // otherwise return the old size
        public void AdjustTextSizeIfRequired()
        {
            if (targetControl != null && !string.IsNullOrEmpty(targetControl.Text))
            {
                // Clear the array of resized controls if the control is being disposed
                if (targetControl.Context.GetType() == typeof(Android.App.Activity) && ((Android.App.Activity)targetControl.Context).IsFinishing)
                {
                    resizedControls.Clear();
                }

                // Check if we have already resized the control of same value type
                if (fieldValueType != FieldValueType.NotSet && resizedControls.Where(p => p.Key == this).Count() == 0
                    && resizedControls.Where(p => p.Key.fieldValueType == fieldValueType).Count() > 0)
                {
                    calculatedSize = resizedControls.FirstOrDefault(p => p.Key.fieldValueType == fieldValueType).Value;
                    targetControl.SetTextSize(ComplexUnitType.Px, calculatedSize.Value);
                    return;
                }

                // If the size has not been calculated, calculate the best size
                if (!calculatedSize.HasValue || IsDynamic)
                {
                    AdjustTextSize();
                }
                // If the size has been already calculated, use the same size
                else
                {
                    targetControl.SetTextSize(ComplexUnitType.Px, calculatedSize.Value);
                }
            }
        }

        // Adjust the text size
        private void AdjustTextSize()
        {
            // If the control is not yet initialized, return
            if (!initialized)
                return;

            int startSize = (int)minTextSize;
            int heightLimit = targetControl.MeasuredHeight - targetControl.CompoundPaddingBottom - targetControl.CompoundPaddingTop;
            widthLimit = targetControl.MeasuredWidth - targetControl.CompoundPaddingLeft - targetControl.CompoundPaddingRight;
            if (widthLimit <= 0)
                return;
            paint = new TextPaint(targetControl.Paint);
            availableSpaceRect.Right = widthLimit;
            availableSpaceRect.Bottom = heightLimit;
            SetTextSize(startSize);
        }

        // Set the text size
        private void SetTextSize(int startSize)
        {
            int textSize = binarySearch(startSize, (int)maxTextSize, sizeTester, availableSpaceRect);
            targetControl.SetTextSize(ComplexUnitType.Px, textSize);
        }

        // Search for the best size
        private int binarySearch(int start, int end, ISizeTester sizeTester, RectF availableSpace)
        {
            int lastBest = start, lo = start, hi = end - 1, mid;
            while (lo <= hi)
            {
                mid = lo + hi >> 1;
                int midValCmp = sizeTester.OnTestSize(mid, availableSpace);
                if (midValCmp < 0)
                {
                    lastBest = lo;
                    lo = mid + 1;
                }
                else if (midValCmp > 0)
                {
                    hi = mid - 1;
                    lastBest = hi;
                }
                else
                {
                    calculatedSize = mid;
                    return mid;
                }
            }

            // make sure to return last best
            // this is what should always be returned
            calculatedSize = lastBest;

            // update the list of resized controls
            if (fieldValueType != FieldValueType.NotSet)
            {
                if (resizedControls.Where(p => p.Key.fieldValueType == fieldValueType).Count() == 0)
                {
                    resizedControls.TryAdd(this, lastBest);
                }
                else if (resizedControls.Where(p => p.Key == this).Count() == 1)
                {
                    resizedControls[this] = lastBest;
                }
            }

            return lastBest;
        }

        // Test if the suggested size fits the control boundaries
        public int OnTestSize(int suggestedSize, RectF availableSpace)
        {
            RectF textRect = new RectF();

            paint.TextSize = suggestedSize;
            ITransformationMethod transformationMethod = targetControl.TransformationMethod;
            string text = null;
            if (transformationMethod != null)
            {
                text = transformationMethod.GetTransformation(targetControl.Text, targetControl);
            }

            // If text is null, use the value from Text object
            if (text == null)
            {
                text = targetControl.Text;
            }

            bool singleLine = targetControl.MaxLines == 1;
            if (singleLine)
            {
                textRect.Bottom = paint.FontSpacing;
                textRect.Right = paint.MeasureText(text);
            }
            else
            {
                StaticLayout layout = new StaticLayout(text, paint, widthLimit, Alignment.AlignNormal, spacingMult, spacingAdd, true);

                if (targetControl.MaxLines != NO_LINE_LIMIT && layout.LineCount > targetControl.MaxLines)
                    return 1;

                textRect.Bottom = layout.Height;
                int maxWidth = -1;
                int lineCount = layout.LineCount;
                for (int i = 0; i < lineCount; i++)
                {
                    int end = layout.GetLineEnd(i);
                    if (i < lineCount - 1 && end > 0 && !IsValidWordWrap(text[end - 1], text[end]))
                        return 1;
                    if (maxWidth < layout.GetLineRight(i) - layout.GetLineLeft(i))
                        maxWidth = (int)layout.GetLineRight(i) - (int)layout.GetLineLeft(i);
                }
                textRect.Right = maxWidth;
            }
            textRect.OffsetTo(0, 0);

            if (availableSpace.Contains(textRect))
                return -1;

            return 1;
        }
    }

    /// <summary>
    /// Auto Resize TextView
    /// </summary>
    public class AutoFitTextView : AppCompatTextView
    {
        public AutoResizeHelper TextSizeHelper { get; set; } = new AutoResizeHelper();

        public AutoFitTextView(Context context) : this(context, null, Android.Resource.Attribute.TextViewStyle)
        {
        }

        public AutoFitTextView(Context context, IAttributeSet attrs) : this(context, attrs, Android.Resource.Attribute.TextViewStyle)
        {
            // Check if the field value type was defined in XML
            TypedArray ta = context.ObtainStyledAttributes(attrs, Resource.Styleable.autoResizeElement, 0, 0);

            var fieldValType = ta.GetInt(Resource.Styleable.autoResizeElement_FieldValueType, 0);

            if (fieldValType != 0)
            {
                TextSizeHelper.fieldValueType = (FieldValueType)fieldValType;
                TextSizeHelper.maxTextSize = TypedValue.ApplyDimension(ComplexUnitType.Sp, 13, Resources.DisplayMetrics);
            }

            // Set include padding to off
            SetIncludeFontPadding(false);
        }

        public AutoFitTextView(IntPtr javaReference, JniHandleOwnership ownership) : base(javaReference, ownership)
        {
        }

        public AutoFitTextView(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
            TextSizeHelper.targetControl = this;
        }

        public override void SetAllCaps(bool allCaps)
        {
            base.SetAllCaps(allCaps);
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetTypeface(Typeface tf, [GeneratedEnum] TypefaceStyle style)
        {
            base.SetTypeface(tf, style);
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetTextSize([GeneratedEnum] ComplexUnitType unit, float size)
        {
            base.SetTextSize(unit, size);

            if (!TextSizeHelper.calculatedSize.HasValue)
                TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetMaxLines(int maxlines)
        {
            base.SetMaxLines(maxlines);
            TextSizeHelper.maxLines = maxlines;
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetSingleLine()
        {
            base.SetSingleLine();
            TextSizeHelper.maxLines = 1;
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetSingleLine(bool singleLine)
        {
            base.SetSingleLine(singleLine);
            if (singleLine)
                TextSizeHelper.maxLines = 1;
            else TextSizeHelper.maxLines = AutoResizeHelper.NO_LINE_LIMIT;
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetLines(int lines)
        {
            base.SetLines(lines);
            TextSizeHelper.maxLines = lines;
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetLineSpacing(float add, float mult)
        {
            base.SetLineSpacing(add, mult);
            TextSizeHelper.spacingMult = mult;
            TextSizeHelper.spacingAdd = add;
        }

        public void SetMaxTextSize(float maxTextSize)
        {
            TextSizeHelper.maxTextSize = maxTextSize;
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public void SetMinTextSize(float minTextSize)
        {
            TextSizeHelper.minTextSize = minTextSize;
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void RequestLayout()
        {
            TextSizeHelper.AdjustTextSizeIfRequired();
            base.RequestLayout();
        }


        protected override void OnTextChanged(ICharSequence text, int start, int lengthBefore, int lengthAfter)
        {
            base.OnTextChanged(text, start, lengthBefore, lengthAfter);
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);
            if (w != oldw || h != oldh)
                TextSizeHelper.AdjustTextSizeIfRequired();
        }
    }

    /// <summary>
    /// Auto Resize Button
    /// </summary>
    public class AutoFitButton : AppCompatButton
    {
        // Init helper
        public AutoResizeHelper TextSizeHelper = new AutoResizeHelper();

        public AutoFitButton(Context context) : this(context, null, Android.Resource.Attribute.TextViewStyle)
        {
        }

        public AutoFitButton(Context context, IAttributeSet attrs) : this(context, attrs, Android.Resource.Attribute.TextViewStyle)
        {
        }

        public AutoFitButton(IntPtr javaReference, JniHandleOwnership ownership) : base(javaReference, ownership)
        {
        }

        public AutoFitButton(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
            TextSizeHelper.targetControl = this;
        }

        public override void SetAllCaps(bool allCaps)
        {
            base.SetAllCaps(allCaps);

            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetTypeface(Typeface tf, [GeneratedEnum] TypefaceStyle style)
        {
            base.SetTypeface(tf, style);

            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetTextSize([GeneratedEnum] ComplexUnitType unit, float size)
        {
            base.SetTextSize(unit, size);

            if (!TextSizeHelper.calculatedSize.HasValue)
                TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetMaxLines(int maxlines)
        {
            base.SetMaxLines(maxlines);
            TextSizeHelper.maxLines = maxlines;
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetSingleLine()
        {
            base.SetSingleLine();
            TextSizeHelper.maxLines = 1;
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetSingleLine(bool singleLine)
        {
            base.SetSingleLine(singleLine);
            if (singleLine)
                TextSizeHelper.maxLines = 1;
            else TextSizeHelper.maxLines = AutoResizeHelper.NO_LINE_LIMIT;
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetLines(int lines)
        {
            base.SetLines(lines);
            TextSizeHelper.maxLines = lines;
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void SetLineSpacing(float add, float mult)
        {
            base.SetLineSpacing(add, mult);
            TextSizeHelper.spacingMult = mult;
            TextSizeHelper.spacingAdd = add;
        }

        public void SetMaxTextSize(float maxTextSize)
        {
            TextSizeHelper.maxTextSize = maxTextSize;
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public void SetMinTextSize(float minTextSize)
        {
            TextSizeHelper.minTextSize = minTextSize;
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        public override void RequestLayout()
        {
            TextSizeHelper.AdjustTextSizeIfRequired();
            base.RequestLayout();
        }

        protected override void OnTextChanged(ICharSequence text, int start, int lengthBefore, int lengthAfter)
        {
            base.OnTextChanged(text, start, lengthBefore, lengthAfter);
            TextSizeHelper.AdjustTextSizeIfRequired();
        }

        protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);
            if (w != oldw || h != oldh)
                TextSizeHelper.AdjustTextSizeIfRequired();
        }
    }
}