﻿using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfDataUi.DataTypes;

namespace WpfDataUi.Controls
{
    /// <summary>
    /// Interaction logic for ListBoxDisplay.xaml
    /// </summary>
    public partial class ListBoxDisplay : UserControl, IDataUi
    {
        InstanceMember mInstanceMember;
        public InstanceMember InstanceMember
        { 
            get => mInstanceMember; 
            set
            {
                bool instanceMemberChanged = mInstanceMember != value;
                if (mInstanceMember != null && instanceMemberChanged)
                {
                    mInstanceMember.PropertyChanged -= HandlePropertyChange;
                }
                mInstanceMember = value;
                if (mInstanceMember != null && instanceMemberChanged)
                {
                    mInstanceMember.PropertyChanged += HandlePropertyChange;
                }
                Refresh();

            }
        }

        public ListBoxDisplay()
        {
            InitializeComponent();
        }

        public bool SuppressSettingProperty { get; set; }

        static SolidColorBrush DefaultValueBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 255, 180)){Opacity = 0.5f};

        public void Refresh(bool forceRefreshEvenIfFocused = false)
        {
            bool canRefresh = true;

            if(canRefresh)
            {
                SuppressSettingProperty = true;

                //mTextBoxLogic.RefreshDisplay();

                this.Label.Text = InstanceMember.DisplayName;
                this.RefreshContextMenu(ListBox.ContextMenu);
                //this.RefreshContextMenu(StackPanel.ContextMenu);

                if (InstanceMember.IsDefault)
                {
                    this.ListBox.Background = DefaultValueBackground;
                }
                else
                {
                    ClearValue(BackgroundProperty);
                }

                //HintTextBlock.Visibility = !string.IsNullOrEmpty(InstanceMember?.DetailText) ? Visibility.Visible : Visibility.Collapsed;
                //HintTextBlock.Text = InstanceMember?.DetailText;
                TrySetValueOnUi(InstanceMember?.Value);
                //RefreshIsEnabled();

                SuppressSettingProperty = false;
            }
        }

        public ApplyValueResult TryGetValueOnUi(out object result)
        {
            // todo - need to make this more flexible, but for now let's just support strings:
            var propertyType = InstanceMember?.PropertyType;
            if(propertyType == typeof(List<string>))
            {
                var value = new List<string>();

                foreach(var item in ListBox.Items)
                {
                    value.Add(item?.ToString());
                }

                result = value;

                return ApplyValueResult.Success;

            }
            else if(propertyType == typeof(List<int>))
            {
                var value = new List<int>();

                foreach(var item in ListBox.Items)
                {
                    if(int.TryParse(item?.ToString(), out int intResult))
                    {
                        value.Add(intResult);
                    }
                }

                result = value;

                return ApplyValueResult.Success;
            }
            // do the same as above, but this time for List<float>
            else if(propertyType == typeof(List<float>))
            {
                var value = new List<float>();
                foreach(var item in ListBox.Items)
                {
                    if(float.TryParse(item?.ToString(), out float floatResult))
                    {
                        value.Add(floatResult);
                    }
                }
                result = value;
                return ApplyValueResult.Success;
            }
            else if (propertyType == typeof(List<Vector2>))
            {
                var value = new List<Vector2>();
                foreach (var item in ListBox.Items)
                {
                    if (TryParse(item?.ToString(), out Vector2? vectorResult))
                    {
                        value.Add(vectorResult.Value);
                    }
                }
                result = value;
                return ApplyValueResult.Success;
            }
            else
            {
                result = null;
                return ApplyValueResult.NotSupported;
            }
        }

        public ApplyValueResult TrySetValueOnUi(object value)
        {
            if(value is List<string> valueAsList)
            {
                var newList = new List<string>();
                newList.AddRange(valueAsList);
                ListBox.ItemsSource = newList;
            }
            else if(value is List<int> valueAsIntList)
            {
                var newList = new List<int>();
                newList.AddRange(valueAsIntList);
                ListBox.ItemsSource = newList;
            }
            else if(value is List<float> valueAsFloatList)
            {
                var newList = new List<float>();
                newList.AddRange(valueAsFloatList);
                ListBox.ItemsSource = newList;
            }
            else
            {
                // todo - we may want to clone the list here too to prevent unintentional editing of the underlying list
                ListBox.ItemsSource = value as IEnumerable;
            }
            return ApplyValueResult.Success;
        }

        private void AddButtonClicked(object sender, RoutedEventArgs e)
        {
            NewEntryGrid.Visibility = Visibility.Visible;
            NotEditingEntryStackPanel.Visibility = Visibility.Collapsed;
            NewTextBox.Focus();
        }

        private void ListBox_KeyDown(object sender, KeyEventArgs e)
        {
            var isCtrlDown =
                (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl));

            if (e.Key == Key.Delete)
            {
                var selectedItem = ListBox.SelectedIndex;

                if(selectedItem > -1)
                {
                    var listToRemoveFrom = ListBox.ItemsSource as IList;
                    
                    if(ListBox.SelectedIndex < listToRemoveFrom.Count)
                    {
                        // Don't let a user delete the last 3 points in a Polygon (4th point is to close the shape)
                        int minValue = ListIsPolygonPoints(listToRemoveFrom) ? 4 : 0;
                        if (listToRemoveFrom.Count > minValue)
                        {
                            listToRemoveFrom.RemoveAt(ListBox.SelectedIndex);
                        }
                    }
                }
                this.TrySetValueOnInstance();

                TryDoManualRefresh();
            }
            else if(e.Key == Key.C && isCtrlDown)
            {
                var selectedItem = ListBox.SelectedItem as string;

                if(!string.IsNullOrEmpty(selectedItem))
                {
                    Clipboard.SetText(selectedItem);
                }
            }
            else if(e.Key == Key.V && isCtrlDown)
            {
                var text = Clipboard.GetText();

                if(!string.IsNullOrEmpty(text))
                {
                    HandleAddTextItem(text);
                }
            }
        }

        // TODO: Find a better way to determine if the ListView is Polygon Points
        private bool ListIsPolygonPoints(IList testList)
        {
            return testList is List<System.Numerics.Vector2>;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            HandleAddTextItem(NewTextBox.Text);
        }

        private void HandleAddTextItem(string text)
        {
            var listToAddTo = ListBox.ItemsSource as IList;
            if (listToAddTo != null)
            {
                if(listToAddTo is List<string> stringList)
                {
                    stringList.Add(text);
                }
                else if (listToAddTo is List<int> intList)
                {
                    if (int.TryParse(text, out int intResult))
                    {
                        intList.Add(intResult);
                    }
                }
                else if (listToAddTo is List<float> floatList)
                {
                    if (float.TryParse(text, out float floatResult))
                    {
                        floatList.Add(floatResult);
                    }
                }
                else if(listToAddTo is List<System.Numerics.Vector2> vector2List)
                {
                    Vector2? toAdd = null;

                    if(TryParse(text, out toAdd))
                    {
                        vector2List.Add(toAdd.Value);
                    }
                    else
                    {
                        MessageBox.Show("Could not parse the values. Value must be two numbers separated by a comma, such as \"10,20\"");
                    }
                }
            }
            NewTextBox.Text = null;
            NewEntryGrid.Visibility = Visibility.Collapsed;
            NotEditingEntryStackPanel.Visibility = Visibility.Visible;
            this.TrySetValueOnInstance();

            TryDoManualRefresh();
        }

        private static bool TryParse(string text, out Vector2? parsedValue)
        {
            parsedValue = null;

            if(text?.StartsWith("<") == true)
            {
                text = text.Substring(1);
            }
            if(text?.EndsWith(">") == true)
            {
                text = text.Substring(0, text.Length - 1);
            }

            if (text?.Contains(",") == true)
            {
                var splitValues = text.Split(',');

                if (splitValues.Length == 2)
                {
                    if (float.TryParse(splitValues[0], out float firstValue) &&
                        float.TryParse(splitValues[1], out float secondValue))
                    {
                        parsedValue = new Vector2(firstValue, secondValue);
                    }
                }
            }
            return parsedValue != null;
        }

        private void TryDoManualRefresh()
        {
            var itemSourceList = ListBox.ItemsSource as IList;

            var needsManualRefresh = !(itemSourceList is INotifyCollectionChanged);
            if (needsManualRefresh)
            {
                ListBox.ItemsSource = null;
                TrySetValueOnUi(InstanceMember?.Value);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            HandleCancelItem();
        }

        private void HandleCancelItem()
        {
            NewTextBox.Text = null;
            NewEntryGrid.Visibility = Visibility.Collapsed;
            NotEditingEntryStackPanel.Visibility = Visibility.Visible;
        }

        private void NewTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                e.Handled = true;
                HandleAddTextItem(NewTextBox.Text);

            }
            else if(e.Key == Key.Escape)
            {
                e.Handled = true;
                HandleCancelItem();
            }
        }

        private void HandlePropertyChange(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InstanceMember.Value))
            {
                this.Refresh();

            }
        }

    }
}
