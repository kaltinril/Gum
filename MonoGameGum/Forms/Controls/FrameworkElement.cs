﻿using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;


using RenderingLibrary.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ToolsUtilities;
using System.Threading;


#if FRB
using FlatRedBall.Forms.GumExtensions;
using FlatRedBall.Forms.Input;
using FlatRedBall.Gui;
using FlatRedBall.Input;
using FlatRedBall.Instructions;
using InteractiveGue = global::Gum.Wireframe.GraphicalUiElement;
namespace FlatRedBall.Forms.Controls;
#else
using MonoGameGum.Input;
namespace MonoGameGum.Forms.Controls;
#endif

#region Enums

public enum TabDirection
{
    Up,
    Down
}

public enum TabbingFocusBehavior
{
    FocusableIfInputReceiver,
    SkipOnTab
}
#endregion

#if !FRB
public class KeyEventArgs : EventArgs
{
    public Microsoft.Xna.Framework.Input.Keys Key { get; set; }
}
#endif

public delegate void KeyEventHandler(object sender, KeyEventArgs e);

public class FrameworkElement
{
#if FRB
        public static Cursor MainCursor => GuiManager.Cursor;

        public static List<Xbox360GamePad> GamePadsForUiControl => GuiManager.GamePadsForUiControl;
#else
    public static ICursor MainCursor { get; set; }

    public static List<Input.GamePad> GamePadsForUiControl { get; private set; } = new List<Input.GamePad>();

#endif

#if !FRB

    /// <summary>
    /// Container used to hold popups such as the ListBox which appears when clicking on a combo box.
    /// </summary>
    public static InteractiveGue PopupRoot { get; set; }

    /// <summary>
    /// Container used to hold modal objects. If any object is added to this container, then all other
    /// UI does not receive events.
    /// </summary>
    public static InteractiveGue ModalRoot { get; set; }

#endif

    protected bool isFocused;
    protected double timeFocused;
    /// <summary>
    /// Whether this element has input focus. If true, the element shows its focused
    /// state and receives input from keyboards and gamepads.
    /// </summary>
    public virtual bool IsFocused
    {
        get { return isFocused; }
        set
        {
            if (value != isFocused)
            {
                isFocused = value && IsEnabled;

                if (isFocused && this is IInputReceiver inputReceiver)
                {
                    InteractiveGue.CurrentInputReceiver = inputReceiver;
                }

                UpdateState();

                PushValueToViewModel();

                if (isFocused)
                {
                    timeFocused = InteractiveGue.CurrentGameTime;
                    GotFocus?.Invoke(this, null);
                }
                else
                {
                    LostFocus?.Invoke(this, null);

                    if (this is IInputReceiver inputReceiver2 && InteractiveGue.CurrentInputReceiver == inputReceiver2)
                    {
                        InteractiveGue.CurrentInputReceiver = null;
                    }
                }
            }
            // this resolves possible stale states:
            else
            {
                if (isFocused && this is IInputReceiver inputReceiver)
                {
                    InteractiveGue.CurrentInputReceiver = inputReceiver;
                }
            }

        }
    }

    protected Dictionary<string, string> vmPropsToUiProps = new Dictionary<string, string>();

    public object BindingContext
    {
        get => Visual?.BindingContext;
        set
        {
            if (value != BindingContext && Visual != null)
            {
                Visual.BindingContext = value;
            }

        }
    }


    /// <summary>
    /// The height in pixels. This is a calculated value considering HeightUnits and Height.
    /// </summary>
    public float ActualHeight => Visual.GetAbsoluteHeight();
    /// <summary>
    /// The width in pixels. This is a calculated value considering WidthUnits and Width;
    /// </summary>
    public float ActualWidth => Visual.GetAbsoluteWidth();

    /// <summary>
    /// Returns the left of this element in absolute (screen) coordinates
    /// </summary>
    public float AbsoluteLeft => Visual.AbsoluteLeft;
    /// <summary>
    /// Returns the top of this element in absolute (screen) coordinates
    /// </summary>
    public float AbsoluteTop => Visual.AbsoluteTop;

    public float Height
    {
        get { return Visual.Height; }
        set
        {
#if DEBUG
            if (float.IsNaN(value))
            {
                throw new Exception("NaN value not supported for FrameworkElement Height");
            }
            if (float.IsPositiveInfinity(value) || float.IsNegativeInfinity(value))
            {
                throw new Exception();
            }
#endif
            Visual.Height = value;
        }
    }
    public float Width
    {
        get { return Visual.Width; }
        set
        {
#if DEBUG
            if (float.IsNaN(value))
            {
                throw new Exception("NaN value not supported for FrameworkElement Width");
            }
            if (float.IsPositiveInfinity(value) || float.IsNegativeInfinity(value))
            {
                throw new Exception();
            }
#endif
            Visual.Width = value;
        }
    }


#if FRB
    /// <summary>
    /// The X position of the left side of the element in pixels.
    /// </summary>
    [Obsolete("Use AbsoluteLeft")]
    public float ActualX => Visual.GetLeft();

    /// <summary>
    /// The Y position of the top of the element in pixels (positive Y is down).
    /// </summary>
    [Obsolete("Use AbsoluteTop")]
    public float ActualY => Visual.GetTop();

#endif

    public float X
    {
        get { return Visual.X; }
        set { Visual.X = value; }
    }
    public float Y
    {
        get { return Visual.Y; }
        set { Visual.Y = value; }
    }

    bool isEnabled = true;
    public virtual bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (isEnabled != value)
            {
                if (value == false && IsFocused)
                {
                    // If we disabled this, then unfocus it, and select next tab
                    //this.HandleTab(TabDirection.Down, this);
                    // Update 10/2/2020
                    // Actually this is causing
                    // some annoying behavior. If
                    // a button is used to buy items
                    // which can be bought multiple times,
                    // the button may become disabled after
                    // the user has no more money, automatically
                    // focusing on the next button which may result
                    // in unintended actions.
                }
                isEnabled = value;
                Visual.IsEnabled = value;

            }
        }
    }

    public bool IsVisible
    {
        get { return Visual.Visible; }
        set { Visual.Visible = value; }
    }

    public string Name
    {
        get { return Visual.Name; }
        set { Visual.Name = value; }
    }

    public FrameworkElement ParentFrameworkElement
    {
        get
        {
            var parent = this.Visual.Parent;

            while (parent is InteractiveGue parentGue)
            {
                var parentForms = parentGue.FormsControlAsObject as FrameworkElement;

                if (parentForms != null)
                {
                    return parentForms;
                }
                else
                {
                    parent = parent.Parent;
                }
            }

            return null;
        }
    }

    InteractiveGue visual;
    public InteractiveGue Visual
    {
        get { return visual; }
        set
        {
#if DEBUG
            // allow the visual to be un-assigned if it was assigned before, like if a forms control is getting removed.
            if (value == null && visual == null)
            {
                throw new ArgumentNullException("Visual cannot be assigned to null");
            }
#endif
            if (visual != value)
            {
                if (visual != null)
                {
                    // unsubscribe:
                    visual.BindingContextChanged -= HandleVisualBindingContextChanged;
                    visual.EnabledChange -= HandleEnabledChanged;
                    ReactToVisualRemoved();
                }


                visual = value;
                if (visual != null)
                {
                    ReactToVisualChanged();
                    UpdateAllUiPropertiesToVm();

                    visual.BindingContextChanged += HandleVisualBindingContextChanged;
                    visual.EnabledChange += HandleEnabledChanged;
                }
            }

        }
    }

    /// <summary>
    /// Contains the default association between Forms Controls and Gum Runtime Types. 
    /// This dictionary enabled forms controls (like TextBox) to automatically create their own visuals.
    /// The key in the dictionary is the type of Forms control.
    /// </summary>
    /// <remarks>
    /// This dictionary simplifies working with FlatRedBall.Forms in code. It allows one piece of code 
    /// (which may be generated by Glue) to associate the Forms controls with a Gum runtime type. Once 
    /// this association is made, controls can be created without specifying a gum runtime. For example:
    /// var button = new Button();
    /// button.Visual.AddToManagers();
    /// button.Click += HandleButtonClick;
    /// 
    /// Note that this association is used when instantiating a new Forms type in code, but it is not used when instantiating
    /// a new Gum runtime type - the Gum runtime must instantiate and associate its Forms object in its own code.
    /// </remarks>
    /// <example>
    /// FrameworkElement.DefaultFormsComponents[typeof(FlatRedBall.Forms.Controls.Button)] = 
    ///     typeof(ProjectName.GumRuntimes.LargeMenuButtonRuntime);
    /// </example>
    public static Dictionary<Type, Type> DefaultFormsComponents { get; private set; } = new Dictionary<Type, Type>();

    protected static InteractiveGue GetGraphicalUiElementFor(FrameworkElement element)
    {
        var type = element.GetType();
        return GetGraphicalUiElementForFrameworkElement(type);
    }

    public static InteractiveGue GetGraphicalUiElementForFrameworkElement(Type type)
    {
        if (DefaultFormsComponents.ContainsKey(type))
        {
            var gumType = DefaultFormsComponents[type];

            // The bool/bool constructor is required to match the FlatRedBall.Forms functionality
            // of being able to be Gum-first or forms-first. The 2nd bool in particular tells the runtime
            // whether to create a forms object. Yes, this is less convenient for the user who is manually
            // creating runtimes, but it's worth it for the standard behavior of the user creating instances
            // of Gum objects, and to be able to create Forms objects in Gum tool
            var boolBoolConstructor = gumType.GetConstructor(new[] { typeof(bool), typeof(bool) });
            if(boolBoolConstructor != null)
            {
                return boolBoolConstructor.Invoke(new object[] { true, false }) as InteractiveGue;
            }
            else
            {
                throw new Exception($"The Runtime type {gumType} needs to have a two-argument constructor, both bools, where" +
                    " the second argument controls whether the object should internally create a Forms instance.");
            }


        }
        else
        {
            var baseType = type.BaseType;

            if (baseType == typeof(object) || baseType == typeof(FrameworkElement))
            {
                var message =
                    $"Could not find default Gum Component for {type}. You can solve this by adding a Gum type for {type} to " +
                    $"{nameof(FrameworkElement)}.{nameof(DefaultFormsComponents)}, or constructing the Gum object itself.";

                throw new Exception(message);
            }
            else
            {
                return GetGraphicalUiElementForFrameworkElement(baseType);
            }
        }
    }

    public TabbingFocusBehavior GamepadTabbingFocusBehavior { get; set; } = TabbingFocusBehavior.FocusableIfInputReceiver;


    #region Events

    public event EventHandler GotFocus;
    public event EventHandler LostFocus;
    public event EventHandler Loaded;
    public event KeyEventHandler KeyDown;

    #endregion


    public FrameworkElement()
    {
        Visual = GetGraphicalUiElementFor(this);
        Visual.FormsControlAsObject = this;
    }

    public FrameworkElement(InteractiveGue visual)
    {
        if (visual != null)
        {
            this.Visual = visual;
        }
    }

    public void AddChild(FrameworkElement child)
    {
        if (child.Visual == null)
        {
            throw new InvalidOperationException("The child must have a Visual before being added to the parent");
        }
        if (this.Visual == null)
        {
            throw new InvalidOperationException("This must have its Visual set before having children added");
        }

        child.Visual.Parent = this.Visual;
    }

    protected bool GetIfIsOnThisOrChildVisual(ICursor cursor)
    {
        var isOnThisOrChild =
            cursor.WindowOver == this.Visual ||
            (cursor.WindowOver != null && cursor.WindowOver.IsInParentChain(this.Visual));

        return isOnThisOrChild;
    }

    /// <summary>
    /// Calls the loaded event. This should not be called in custom code, but instead is called by Gum
    /// </summary>
    public virtual void CallLoaded() => Loaded?.Invoke(this, null);

    public void Close()
    {
#if FRB
        if (!FlatRedBallServices.IsThreadPrimary())
        {

            InstructionManager.AddSafe(CloseInternal);
        }
        else
#endif
        {
            CloseInternal();
        }
    }

    private void CloseInternal()
    {
        var inputReceiver = InteractiveGue.CurrentInputReceiver;
        if (inputReceiver != null)
        {
            if (inputReceiver is InteractiveGue gue)
            {
                if (gue.IsInParentChain(this.Visual))
                {
                    InteractiveGue.CurrentInputReceiver = null;
                }
            }
            else if (inputReceiver is FrameworkElement frameworkElement)
            {
                if (frameworkElement.Visual?.IsInParentChain(this.Visual) == true)
                {
                    InteractiveGue.CurrentInputReceiver = null;
                }
            }
        }
        Visual.RemoveFromManagers();
    }

    /// <summary>
    /// Displays this element visually and adds it to the underlying managers for Cursor interaction.
    /// </summary>
    /// <remarks>
    /// This is typically only called if the element is instantiated in code. Elements added
    /// to a Gum screen in Gum will automatically be displayed when the Screen is created, and calling
    /// this will result in the object being added twice.</remarks>
    /// <param name="layer">The layer to add this to, can be null to add it directly to managers</param>
    public void Show(Layer layer = null)
    {
#if DEBUG
        if (Visual == null)
        {
            throw new InvalidOperationException("Visual must be set before calling Show");
        }
#endif


#if FRB
        if (!FlatRedBallServices.IsThreadPrimary())
        {
            InstructionManager.AddSafe(() =>
            {
                Visual.AddToManagers(RenderingLibrary.SystemManagers.Default, gumLayer);
            });

        }
        else
#endif
        {
            Visual.AddToManagers(RenderingLibrary.SystemManagers.Default, layer);
        }
    }

    /// <summary>
    /// Displays this visual element (calls Show), and returns a task which completes once
    /// the dialog is removed.
    /// </summary>
    /// <param name="layer">The Layer to be used to display the element.</param>
    /// <returns>A task which will complete once this element is removed from managers.</returns>
    //        public async Task<bool?> ShowDialog(Layer layer = null)
    //        {
    //#if DEBUG
    //            if (Visual == null)
    //            {
    //                throw new InvalidOperationException("Visual must be set before calling Show");
    //            }
    //#endif
    //            var semaphoreSlim = new SemaphoreSlim(1);

    //            void HandleRemovedFromManagers(object sender, EventArgs args) => semaphoreSlim.Release();
    //            Visual.RemovedFromGuiManager += HandleRemovedFromManagers;

    //            semaphoreSlim.Wait();
    //            Show(layer);
    //            await semaphoreSlim.WaitAsync();

    //            Visual.RemovedFromGuiManager -= HandleRemovedFromManagers;
    //            // for now, return null, todo add dialog results

    //            semaphoreSlim.Dispose();

    //            return null;
    //        }

    /// <summary>
    /// Every-frame logic. This will automatically be called if this element is added to the FrameworkElementManager
    /// </summary>
    public virtual void Activity()
    {

    }

    public void RepositionToKeepInScreen()
    {
#if DEBUG
        if (Visual == null)
        {
            throw new InvalidOperationException("Visual hasn't yet been set");
        }
        if (Visual.Parent != null)
        {
            throw new InvalidOperationException("This cannot be moved to keep in screen because it depends on its parent's position");
        }
#endif
        //var cameraTop = 0;
        var cameraBottom = Renderer.Self.Camera.ClientHeight / Renderer.Self.Camera.Zoom;
        //var cameraLeft = 0;
        var cameraRight = Renderer.Self.Camera.ClientWidth / Renderer.Self.Camera.Zoom;

        var thisBottom = this.Visual.AbsoluteY + this.Visual.GetAbsoluteHeight();
        if (thisBottom > cameraBottom)
        {
            // assume absolute positioning (for now?)
            this.Y -= (thisBottom - cameraBottom);
        }
    }

    protected virtual void ReactToVisualChanged()
    {

    }

    protected virtual void ReactToVisualRemoved()
    {

    }

    private void HandleViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        var vmPropertyName = e.PropertyName;
        var updated = UpdateUiToVmProperty(vmPropertyName);
        // If we ever support skia do this:
        //if (updated)
        //{
        //    this.EffectiveManagers?.InvalidateSurface();
        //}
    }

    public void SetBinding(string uiProperty, string vmProperty)
    {
        if (vmPropsToUiProps.ContainsKey(vmProperty))
        {
            vmPropsToUiProps.Remove(vmProperty);
        }

        // This prevents single UI properties from being bound to multiple VM properties
        if (vmPropsToUiProps.Any(item => item.Value == uiProperty))
        {
            var toRemove = vmPropsToUiProps.Where(item => item.Value == uiProperty).ToArray();

            foreach (var kvp in toRemove)
            {
                vmPropsToUiProps.Remove(kvp.Key);
            }
        }



        vmPropsToUiProps.Add(vmProperty, uiProperty);

        if (BindingContext != null)
        {
            UpdateUiToVmProperty(vmProperty);
        }
    }

    protected virtual void HandleVisualBindingContextChanged(object sender, BindingContextChangedEventArgs args)
    {
        if (args.OldBindingContext is INotifyPropertyChanged oldAsPropertyChanged)
        {
            oldAsPropertyChanged.PropertyChanged -= HandleViewModelPropertyChanged;
        }
        if (BindingContext != null)
        {
            UpdateAllUiPropertiesToVm();
            if (BindingContext is INotifyPropertyChanged newAsPropertyChanged)
            {
                newAsPropertyChanged.PropertyChanged += HandleViewModelPropertyChanged;
            }

        }
    }



    void HandleEnabledChanged(object sender, EventArgs args)
    {
        if (Visual != null)
        {
            this.IsEnabled = Visual.IsEnabled;
        }
    }

    private void UpdateAllUiPropertiesToVm()
    {
        foreach (var vmProperty in vmPropsToUiProps.Keys)
        {
            UpdateUiToVmProperty(vmProperty);
        }
    }

    private bool UpdateUiToVmProperty(string vmPropertyName)
    {
        var updated = false;
        if (vmPropsToUiProps.ContainsKey(vmPropertyName))
        {
            var vmProperty = BindingContext.GetType().GetProperty(vmPropertyName);
            if (vmProperty == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Could not find property {vmPropertyName} in {BindingContext.GetType()}");
            }
            else
            {
                var vmValue = vmProperty.GetValue(BindingContext, null);

                var uiProperty = this.GetType().GetProperty(vmPropsToUiProps[vmPropertyName]);

                if (uiProperty == null)
                {
                    throw new Exception($"The {this.GetType()} with name {this.Name} is binding a missing UI property ({vmPropsToUiProps[vmPropertyName]}) " +
                        $"to a ViewModel Property ({vmPropertyName})");
                }

                if (uiProperty.PropertyType == typeof(string))
                {
                    var stringToSet = vmValue?.ToString();
                    uiProperty.SetValue(this, stringToSet, null);
                }
                else
                {
                    try
                    {
                        var convertedValue = BindableGue.ConvertValue(vmValue, uiProperty.PropertyType, null);

                        uiProperty.SetValue(this, vmValue, null);
                    }
                    catch (ArgumentException ae)
                    {
                        var message = $"Could not bind the UI property {this.GetType().Name}.{uiProperty.Name} to the view model property {vmProperty} " +
                            $"because the view model property is not of type {uiProperty.PropertyType}";
                        throw new InvalidOperationException(message);
                    }
                }
                updated = true;
            }
        }
        return updated;
    }


    protected void PushValueToViewModel([CallerMemberName] string uiPropertyName = null)
    {
        var kvp = vmPropsToUiProps.FirstOrDefault(item => item.Value == uiPropertyName);

        if (kvp.Value == uiPropertyName)
        {
            var vmPropName = kvp.Key;

            var vmProperty = BindingContext?.GetType().GetProperty(vmPropName);

            if (vmProperty?.CanWrite == true)
            {
                var uiProperty = this.GetType().GetProperty(uiPropertyName);
                if (uiProperty != null)
                {
                    var uiValue = uiProperty.GetValue(this, null);

                    try
                    {
                        var convertedValue = BindableGue.ConvertValue(uiValue, vmProperty.PropertyType, null);

                        vmProperty.SetValue(BindingContext, convertedValue, null);
                    }
                    catch (System.ArgumentException argumentException)
                    {
                        throw new Exception($"Could not convert UI value {GetType().Name}.{uiPropertyName} of type {uiProperty.PropertyType} " +
                            $"into ViewModel {BindingContext.GetType().Name}.{vmProperty.Name} of type {vmProperty.PropertyType}", argumentException);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Whether to use left and right directions as navigation. If false, left and right directions are ignored for navigation.
    /// </summary>
    public bool IsUsingLeftAndRightGamepadDirectionsForNavigation { get; set; } = true;

    protected void HandleGamepadNavigation(Input.GamePad gamepad)
    {
        if (gamepad.ButtonRepeatRate(Buttons.DPadDown) ||
            (IsUsingLeftAndRightGamepadDirectionsForNavigation && gamepad.ButtonRepeatRate(Buttons.DPadRight)) ||
            gamepad.LeftStick.AsDPadPushedRepeatRate(DPadDirection.Down) ||
            (IsUsingLeftAndRightGamepadDirectionsForNavigation && gamepad.LeftStick.AsDPadPushedRepeatRate(DPadDirection.Right)))
        {
            this.HandleTab(TabDirection.Down, this);
        }
        else if (gamepad.ButtonRepeatRate(Buttons.DPadUp) ||
            (IsUsingLeftAndRightGamepadDirectionsForNavigation && gamepad.ButtonRepeatRate(Buttons.DPadLeft)) ||
            gamepad.LeftStick.AsDPadPushedRepeatRate(DPadDirection.Up) ||
            (IsUsingLeftAndRightGamepadDirectionsForNavigation && gamepad.LeftStick.AsDPadPushedRepeatRate(DPadDirection.Left)))
        {
            this.HandleTab(TabDirection.Up, this);
        }
    }

#if FRB
    protected void HandleGamepadNavigation(Xbox360GamePad gamepad)
    {
        if (gamepad.ButtonRepeatRate(FlatRedBall.Input.Xbox360GamePad.Button.DPadDown) ||
            (IsUsingLeftAndRightGamepadDirectionsForNavigation && gamepad.ButtonRepeatRate(FlatRedBall.Input.Xbox360GamePad.Button.DPadRight)) ||
            gamepad.LeftStick.AsDPadPushedRepeatRate(FlatRedBall.Input.Xbox360GamePad.DPadDirection.Down) ||
            (IsUsingLeftAndRightGamepadDirectionsForNavigation && gamepad.LeftStick.AsDPadPushedRepeatRate(FlatRedBall.Input.Xbox360GamePad.DPadDirection.Right)))
        {
            this.HandleTab(TabDirection.Down, this);
        }
        else if (gamepad.ButtonRepeatRate(FlatRedBall.Input.Xbox360GamePad.Button.DPadUp) ||
            (IsUsingLeftAndRightGamepadDirectionsForNavigation && gamepad.ButtonRepeatRate(FlatRedBall.Input.Xbox360GamePad.Button.DPadLeft)) ||
            gamepad.LeftStick.AsDPadPushedRepeatRate(FlatRedBall.Input.Xbox360GamePad.DPadDirection.Up) ||
            (IsUsingLeftAndRightGamepadDirectionsForNavigation && gamepad.LeftStick.AsDPadPushedRepeatRate(FlatRedBall.Input.Xbox360GamePad.DPadDirection.Left)))
        {
            this.HandleTab(TabDirection.Up, this);
        }
    }
    protected void HandleGamepadNavigation(GenericGamePad gamepad)
    {
        AnalogStick leftStick = gamepad.AnalogSticks.Length > 0
            ? gamepad.AnalogSticks[0]
            : null;

        if (gamepad.DPadRepeatRate(FlatRedBall.Input.Xbox360GamePad.DPadDirection.Down) ||
            (IsUsingLeftAndRightGamepadDirectionsForNavigation && gamepad.DPadRepeatRate(FlatRedBall.Input.Xbox360GamePad.DPadDirection.Right)) ||
            leftStick?.AsDPadPushedRepeatRate(FlatRedBall.Input.Xbox360GamePad.DPadDirection.Down) == true ||
            (IsUsingLeftAndRightGamepadDirectionsForNavigation && leftStick?.AsDPadPushedRepeatRate(FlatRedBall.Input.Xbox360GamePad.DPadDirection.Right) == true))
        {
            this.HandleTab(TabDirection.Down, this);
        }
        else if (gamepad.DPadRepeatRate(FlatRedBall.Input.Xbox360GamePad.DPadDirection.Up) ||
            (IsUsingLeftAndRightGamepadDirectionsForNavigation && gamepad.DPadRepeatRate(FlatRedBall.Input.Xbox360GamePad.DPadDirection.Left)) ||
            leftStick?.AsDPadPushedRepeatRate(FlatRedBall.Input.Xbox360GamePad.DPadDirection.Up) == true ||
            (IsUsingLeftAndRightGamepadDirectionsForNavigation && leftStick?.AsDPadPushedRepeatRate(FlatRedBall.Input.Xbox360GamePad.DPadDirection.Left) == true))
        {
            this.HandleTab(TabDirection.Up, this);
        }
    }
#endif

    public void HandleTab(TabDirection tabDirection = TabDirection.Down, FrameworkElement requestingElement = null)
    {
        if (requestingElement == null)
        {
            requestingElement = this;
        }

        ////////////////////Early Out/////////////////
        if (((IVisible)requestingElement.Visual).AbsoluteVisible == false)
        {
            return;
        }
        /////////////////End Early Out/////////////////
        Collection<IRenderableIpso> children = Visual.Children;

        var parentGue = requestingElement.Visual.Parent as InteractiveGue;

        HandleTab(tabDirection, requestingElement.Visual, parentGue, shouldAskParent: true);
    }


    public static bool HandleTab(TabDirection tabDirection, InteractiveGue requestingVisual,
        InteractiveGue parentVisual, bool shouldAskParent)
    {
        void UnFocusRequestingVisual()
        {
            if (requestingVisual?.FormsControlAsObject is FrameworkElement requestingFrameworkElement)
            {
                requestingFrameworkElement.IsFocused = false;
            }
        }

        IList<GraphicalUiElement> children = parentVisual?.Children.Cast<GraphicalUiElement>().ToList();
        if (children == null && requestingVisual != null)
        {
            children = requestingVisual.ElementGueContainingThis?.ContainedElements.Where(item => item.Parent == null).ToList();
        }

        //// early out/////////////
        if (children == null)
        {
            return false;
        }

        int newIndex;

        if (requestingVisual == null)
        {
            newIndex = tabDirection == TabDirection.Down ? 0 : children.Count - 1;
        }
        else
        {
            int index = 0;

            bool forceSelect = false;

            if (tabDirection == TabDirection.Down)
            {
                index = 0;
            }
            else
            {
                index = children.Count - 1;
            }

            if (!forceSelect)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    var childElement = children[i];

                    if (childElement == requestingVisual)
                    {
                        index = i;
                        break;
                    }
                }
            }



            if (tabDirection == TabDirection.Down)
            {
                newIndex = index + 1;
            }
            else
            {
                newIndex = index - 1;
            }
        }

        var didChildHandle = false;
        var didReachEndOfChildren = false;
        while (true)
        {
            if ((newIndex >= children.Count && tabDirection == TabDirection.Down) ||
                (newIndex < 0 && tabDirection == TabDirection.Up))
            {
                didReachEndOfChildren = true;
                break;
            }
            else
            {
                var childAtI = children[newIndex] as InteractiveGue;
                var elementAtI = childAtI?.FormsControlAsObject as FrameworkElement;

                if (elementAtI is IInputReceiver && elementAtI.IsVisible == true &&
                    elementAtI.IsEnabled &&
                    elementAtI.Visual.HasEvents &&
                    elementAtI.GamepadTabbingFocusBehavior == TabbingFocusBehavior.FocusableIfInputReceiver)
                {
                    elementAtI.IsFocused = true;

                    UnFocusRequestingVisual();

                    didChildHandle = true;
                    break;
                }
                else
                {
                    if (childAtI?.Visible == true && childAtI.IsEnabled && (elementAtI == null || elementAtI.IsEnabled))

                    {

                        // let this try to handle it:
                        didChildHandle = HandleTab(tabDirection, null, childAtI, shouldAskParent: false);

                        if (didChildHandle)
                        {
                            UnFocusRequestingVisual();
                        }
                    }

                    if (!didChildHandle)
                    {
                        if (tabDirection == TabDirection.Down)
                        {
                            newIndex++;
                        }
                        else
                        {
                            newIndex--;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        if (didChildHandle == false)
        {
            if (didReachEndOfChildren)
            {
                bool didFocusNewItem = false;
                if (shouldAskParent)
                {
                    // if this is a dominant window, don't allow tabbing out
                    //var isDominant = GuiManager.DominantWindows.Contains(parentVisual);

                    //if (!isDominant)
                    {
                        if (parentVisual?.Parent != null)
                        {
                            didFocusNewItem = HandleTab(tabDirection, parentVisual, parentVisual.Parent as InteractiveGue, shouldAskParent: true);
                        }
                        else
                        {
                            didFocusNewItem = HandleTab(tabDirection, parentVisual, null, shouldAskParent: true);
                        }
                    }
                }
                if (didFocusNewItem)
                {
                    UnFocusRequestingVisual();
                }
                return didFocusNewItem;
            }
        }
        return didChildHandle;
    }

    /// <summary>
    /// Gets the state according to the element's current properties (such as whether it is enabled) and applies it
    /// to refresh the Visual's appearance.
    /// </summary>
    public virtual void UpdateState() { }

    protected void RaiseKeyDown(KeyEventArgs e)
    {
        KeyDown?.Invoke(this, e);
    }

    public const string DisabledState = "Disabled";
    public const string DisabledFocusedState = "DisabledFocused";
    public const string EnabledState = "Enabled";
    public const string FocusedState = "Focused";
    public const string HighlightedState = "Highlighted";
    public const string HighlightedFocusedState = "HighlightedFocused";
    public const string PushedState = "Pushed";


    protected string GetDesiredState()
    {
        var cursor = MainCursor;

#if DEBUG
        if(cursor == null)
        {
            throw new InvalidOperationException("MainCursor must be assigned before performing any UI logic");
        }
#endif

        var primaryDown = cursor.PrimaryDown;

        bool pushedByGamepads = false;
        for(int i = 0; i < GamePadsForUiControl.Count; i++)
        {
            pushedByGamepads = pushedByGamepads || (GamePadsForUiControl[i].ButtonDown(Buttons.A));
        }

        var isTouchScreen = false;


        if (IsEnabled == false)
        {
            if (isFocused)
            {
                return DisabledFocusedState;
            }
            else
            {
                return DisabledState;
            }
        }
        else if (IsFocused)
        {
            if (cursor.WindowPushed == visual && primaryDown)
            {
                return PushedState;
            }
            else if(pushedByGamepads)
            {
                return PushedState;
            }
            // Even if the cursor is reported as being over the button, if the
            // cursor got its input from a touch screen then the cursor really isn't
            // over anything. Therefore, we only show the highlighted state if the cursor
            // is a physical on-screen cursor
            else if (GetIfIsOnThisOrChildVisual(cursor) &&
                !isTouchScreen)
            {
                return HighlightedFocusedState;
            }
            else
            {
                return FocusedState;
            }
        }
        else if (GetIfIsOnThisOrChildVisual(cursor))
        {
            if (cursor.WindowPushed == visual && primaryDown)
            {
                return PushedState;
            }
            // Even if the cursor is reported as being over the button, if the
            // cursor got its input from a touch screen then the cursor really isn't
            // over anything. Therefore, we only show the highlighted state if the cursor
            // is a physical on-screen cursor
            else if (!isTouchScreen)
            {
                return HighlightedState;
            }
            else
            {
                return EnabledState;
            }
        }
        else
        {
            return EnabledState;
        }
    }

    protected string GetDesiredStateWithChecked(bool? isChecked)
    {
        var baseState = GetDesiredState();

        if (isChecked == true)
        {
            return baseState + "On";
        }
        else if (isChecked == false)
        {
            return baseState + "Off";
        }
        else
        {
            return baseState + "Indeterminate";
        }
    }


}
