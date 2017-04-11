using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using AutoResizeTextView.Controls;
using Android.Graphics;

namespace AutoResizeTextView
{
    [Activity(Label = "AutoResizeTextView", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);
            
            // Get controls
            EditText editTextView = FindViewById<EditText>(Resource.Id.editTextView);
            AutoFitTextView autoFitTextView = FindViewById<AutoFitTextView>(Resource.Id.autoFitTextView);
            TextView normalTextView = FindViewById<TextView>(Resource.Id.textView);

            // Set properties
            autoFitTextView.LayoutParameters.Height = 200;

            normalTextView.Hint = "Normal Text View";
            normalTextView.LayoutParameters.Height = 200;

            // Make auto fit text view to calculate size dynamically
            // Do not use this feature if you do not want to resize the text view over and over again
            autoFitTextView.TextSizeHelper.IsDynamic = true;

            // Following functions helps you set other properties
            //************//
            //autoFitTextView.SetMaxTextSize(80);
            //autoFitTextView.SetMinTextSize(12);

            // Following enum has been created to group certain text views that should have the same text size
            // For example, a view that should currency rates may have different lengths
            // To achieve a UI that allows all these textview to have same text size
            // Set the fieldValueType to a same enum value
            //************//
            //autoFitTextView.TextSizeHelper.fieldValueType = FieldValueType.GeneralText;

            // Use the following piece of code to clear the resized controls cache
            // In above example, we discussed how a view with currencies would like same text size
            // Once you move between different content views, you may want to retain or recalculate the FieldValueType's text size
            // If you wish to re-calculate the text size, clear the resized controls list
            // If you wish to retain the text size - do not do anything with the resized controls list
            //************//
            //AutoResizeHelper.resizedControls.Clear();

            // You may not need to do so, but if you want to be able to manually trigger the text size calculation
            // You can do that using the following command
            //************//
            //autoFitTextView.TextSizeHelper.AdjustTextSizeIfRequired();

            // PLAY WITH THE FOLLOWING TO ACHIEVE THE DESIRED BEHAVIOR
            autoFitTextView.SetMaxLines(0);
            autoFitTextView.SetSingleLine(true);

            editTextView.TextChanged += (sender, args) =>
            {
                autoFitTextView.Text = editTextView.Text;
                normalTextView.Text = editTextView.Text;
            };
        }
    }
}