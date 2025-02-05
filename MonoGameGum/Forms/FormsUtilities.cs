﻿using Gum.Managers;
using Gum.Wireframe;
using GumRuntime;
using Microsoft.Xna.Framework;
using MonoGameGum.Forms.Controls;
using MonoGameGum.Forms.DefaultFromFileVisuals;
using MonoGameGum.Forms.DefaultVisuals;
using MonoGameGum.GueDeriving;
using MonoGameGum.Input;
using RenderingLibrary;
using RenderingLibrary.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoGameGum.Forms
{
    public class FormsUtilities
    {
        static Cursor cursor;

        public static Cursor Cursor => cursor;

        static MonoGameGum.Input.Keyboard keyboard;

        public static Keyboard Keyboard => keyboard;

        public static GamePad[] Gamepads { get; private set; } = new GamePad[4];

        /// <summary>
        /// Initializes defaults to enable FlatRedBall Forms. This method should be called before using Forms.
        /// </summary>
        /// <remarks>
        /// Projects can make further customization to Forms such as by modifying the FrameworkElement.Root or the DefaultFormsComponents.
        /// </remarks>
        public static void InitializeDefaults()
        {
            FrameworkElement.DefaultFormsComponents[typeof(Button)] = typeof(DefaultButtonRuntime);
            FrameworkElement.DefaultFormsComponents[typeof(CheckBox)] = typeof(DefaultCheckboxRuntime);
            FrameworkElement.DefaultFormsComponents[typeof(ComboBox)] = typeof(DefaultComboBoxRuntime);
            FrameworkElement.DefaultFormsComponents[typeof(Label)] = typeof(DefaultLabelRuntime);
            FrameworkElement.DefaultFormsComponents[typeof(ListBox)] = typeof(DefaultListBoxRuntime);
            FrameworkElement.DefaultFormsComponents[typeof(ListBoxItem)] = typeof(DefaultListBoxItemRuntime);
            FrameworkElement.DefaultFormsComponents[typeof(Menu)] = typeof(DefaultMenuRuntime);
            FrameworkElement.DefaultFormsComponents[typeof(MenuItem)] = typeof(DefaultMenuItemRuntime);
            FrameworkElement.DefaultFormsComponents[typeof(ScrollBar)] = typeof(DefaultScrollBarRuntime);
            FrameworkElement.DefaultFormsComponents[typeof(ScrollViewer)] = typeof(DefaultScrollViewerRuntime);
            FrameworkElement.DefaultFormsComponents[typeof(TextBox)] = typeof(DefaultTextBoxRuntime);
            FrameworkElement.DefaultFormsComponents[typeof(PasswordBox)] = typeof(DefaultTextBoxRuntime);
            FrameworkElement.DefaultFormsComponents[typeof(Slider)] = typeof(DefaultSliderRuntime);

            cursor = new Cursor();

            keyboard = new MonoGameGum.Input.Keyboard();

            for (int i = 0; i < Gamepads.Length; i++)
            {
                Gamepads[i] = new GamePad();
            }

            // Do an initial update to update connectivity
            UpdateGamepads(0);

            FrameworkElement.MainCursor = cursor;

            if (SystemManagers.Default == null)
            {
                throw new InvalidOperationException("You must call this method after initializing SystemManagers.Default");
            }

            FrameworkElement.PopupRoot = CreateFullscreenContainer(nameof(FrameworkElement.PopupRoot));
            FrameworkElement.ModalRoot = CreateFullscreenContainer(nameof(FrameworkElement.ModalRoot));
        }

        static ContainerRuntime CreateFullscreenContainer(string name)
        {
            var container = new ContainerRuntime();

            container.WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute;
            container.HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute;
            container.Width = GraphicalUiElement.CanvasWidth;
            container.Height = GraphicalUiElement.CanvasHeight;

            container.AddToManagers();

            return container;
        }

        static List<GraphicalUiElement> innerList = new List<GraphicalUiElement>();

        [Obsolete("Use the overload which takes a Game as the first argument, and pass the game instance.")]
        public static void Update(GameTime gameTime, GraphicalUiElement rootGue)
        {
            Update(null, gameTime, rootGue);
        }

        static List<GraphicalUiElement> innerRootList = new List<GraphicalUiElement>();
        public static void Update(Game game, GameTime gameTime, GraphicalUiElement rootGue)
        {
            innerRootList.Clear();
            if(rootGue != null)
            {
                innerRootList.Add(rootGue);
            }
            Update(game, gameTime, innerRootList);
        }

        public static void Update(Game game, GameTime gameTime, IEnumerable<GraphicalUiElement> roots)
        { 
            // tolerate null games for now...
            var shouldProcess = game == null || game.IsActive;

            if(!shouldProcess)
            {
                return;
            }

            cursor.Activity(gameTime.TotalGameTime.TotalSeconds);
            keyboard.Activity(gameTime.TotalGameTime.TotalSeconds);
            UpdateGamepads(gameTime.TotalGameTime.TotalSeconds);
            innerList.Clear();

            if (FrameworkElement.ModalRoot.Children.Count > 0)
            {
#if DEBUG
                if(FrameworkElement.ModalRoot.Managers == null)
                {
                    throw new InvalidOperationException("The ModalRoot has a Managers property of null. Did you accidentally call RemoveFromManagers?");
                }
#endif
                SetDimensionsToCanvas(FrameworkElement.ModalRoot);

                // make sure this is the last:
                foreach (var layer in SystemManagers.Default.Renderer.Layers)
                {
                    if (layer.Renderables.Contains(FrameworkElement.ModalRoot.RenderableComponent) && layer.Renderables.Last() != FrameworkElement.ModalRoot.RenderableComponent)
                    {
                        layer.Remove(FrameworkElement.ModalRoot.RenderableComponent as IRenderableIpso);
                        layer.Add(FrameworkElement.ModalRoot.RenderableComponent as IRenderableIpso);
                    }
                }

                for(int i = FrameworkElement.ModalRoot.Children.Count - 1; i > -1; i--)
                {
                    var item = FrameworkElement.ModalRoot.Children[i];
                    if (item is GraphicalUiElement itemAsGue)
                    {
                        innerList.Add(itemAsGue);
                        // only the top-most element receives input
                        break;
                    }
                }
            }
            else
            {

                if(roots != null)
                {
                    innerList.AddRange(roots);
                }

                var isRootInRoots = roots?.Contains(FrameworkElement.PopupRoot) == true;

                if (!isRootInRoots && FrameworkElement.PopupRoot.Children.Count > 0)
                {
#if DEBUG
                    if (FrameworkElement.PopupRoot.Managers == null)
                    {
                        throw new InvalidOperationException("The PopupRoot has a Managers property of null. Did you accidentally call RemoveFromManagers?");
                    }
#endif

                    SetDimensionsToCanvas(FrameworkElement.PopupRoot);
                    // make sure this is the last:
                    foreach (var layer in SystemManagers.Default.Renderer.Layers)
                    {
                        if (layer.Renderables.Contains(FrameworkElement.PopupRoot.RenderableComponent) && layer.Renderables.Last() != FrameworkElement.PopupRoot.RenderableComponent)
                        {
                            layer.Remove(FrameworkElement.PopupRoot.RenderableComponent as IRenderableIpso);
                            layer.Add(FrameworkElement.PopupRoot.RenderableComponent as IRenderableIpso);
                        }
                    }

                    foreach (var item in FrameworkElement.PopupRoot.Children)
                    {
                        if (item is GraphicalUiElement itemAsGue)
                        {
                            innerList.Add(itemAsGue);
                        }
                    }
                }
            }


            //FrameworkElement.Root.DoUiActivityRecursively(cursor, keyboard, gameTime.TotalGameTime.TotalSeconds);
              GueInteractiveExtensionMethods.DoUiActivityRecursively(innerList, cursor, keyboard, gameTime.TotalGameTime.TotalSeconds);
        }

        private static void UpdateGamepads(double time)
        {
            for (int i = 0; i < Gamepads.Length; i++)
            {
                var gamepadState = Microsoft.Xna.Framework.Input.GamePad.GetState((int)i);
                Gamepads[i].Activity(gamepadState, time);
            }
        }

        static void SetDimensionsToCanvas(InteractiveGue container)
        {
            // Just to be safe, we'll set X and Y:
            container.X = 0;
            container.Y = 0;
            container.WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute;
            container.HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute;
            container.Width = GraphicalUiElement.CanvasWidth;
            container.Height = GraphicalUiElement.CanvasHeight;
        }

        public static void RegisterFromFileFormRuntimeDefaults()
        {
#if DEBUG
            if(ObjectFinder.Self.GumProjectSave == null)
            {
                throw new InvalidOperationException("A Gum project (gumx) must be loaded and assigned to" +
                    "ObjectFinder.Self.GumProjectSave before making this call");
            }
#endif
            // Some thoughts about this method:
            // 1. We can probably be more efficient 
            //    here by doing a single loop for categories
            //    and behaviors rather than calling Any multiple
            //    times.
            // 2. I believe Gum Forms was written before behaviors
            //    were used. Therefore a lot here use categories instead
            //    of behaviors. New items (like Menu) are using behaviors
            //    and old controls should proably be migrated over if we have
            //    any conflicts.
            foreach (var component in ObjectFinder.Self.GumProjectSave.Components)
            {
                if (component.Categories.Any(item => item.Name == "ButtonCategory"))
                {
                    ElementSaveExtensions.RegisterGueInstantiationType(
                        component.Name,
                        typeof(DefaultFromFileButtonRuntime));
                }
                else if (component.Categories.Any(item => item.Name == "CheckBoxCategory"))
                {
                    ElementSaveExtensions.RegisterGueInstantiationType(
                        component.Name,
                        typeof(DefaultFromFileCheckBoxRuntime));
                }
                else if (component.Categories.Any(item => item.Name == "ComboBoxCategory"))
                {
                    ElementSaveExtensions.RegisterGueInstantiationType(
                        component.Name,
                        typeof(DefaultFromFileComboBoxRuntime));
                }
                else if (component.Categories.Any(item => item.Name == "ListBoxCategory"))
                {
                    ElementSaveExtensions.RegisterGueInstantiationType(
                        component.Name,
                        typeof(DefaultFromFileListBoxRuntime));
                }
                else if(component.Behaviors.Any(item => item.BehaviorName == "MenuBehavior"))
                {
                    ElementSaveExtensions.RegisterGueInstantiationType(
                        component.Name,
                        typeof(DefaultFromFileMenuRuntime));
                }
                else if (component.Behaviors.Any(item => item.BehaviorName == "MenuItemBehavior"))
                {
                    ElementSaveExtensions.RegisterGueInstantiationType(
                        component.Name,
                        typeof(DefaultFromFileMenuItemRuntime));

                    // cannot do this until we can instantiate from-file elements without accessing the element
                    //FrameworkElement.DefaultFormsComponents[typeof(MenuItem)] = typeof(DefaultFromFileMenuItemRuntime);

                }
                else if (component.Categories.Any(item => item.Name == "PasswordBoxCategory"))
                {
                    ElementSaveExtensions.RegisterGueInstantiationType(
                        component.Name,
                        typeof(DefaultFromFilePasswordBoxRuntime));
                }
                else if (component.Categories.Any(item => item.Name == "RadioButtonCategory"))
                {
                    ElementSaveExtensions.RegisterGueInstantiationType(
                        component.Name,
                        typeof(DefaultFromFileRadioButtonRuntime));
                }
                else if (component.Categories.Any(item => item.Name == "ScrollBarCategory"))
                {
                    ElementSaveExtensions.RegisterGueInstantiationType(
                        component.Name,
                        typeof(DefaultFromFileScrollBarRuntime));
                }
                else if (component.Categories.Any(item => item.Name == "SliderCategory"))
                {
                    ElementSaveExtensions.RegisterGueInstantiationType(
                        component.Name,
                        typeof(DefaultFromFileSliderRuntime));
                }
                else if (component.Categories.Any(item => item.Name == "TextBoxCategory"))
                {
                    ElementSaveExtensions.RegisterGueInstantiationType(
                        component.Name,
                        typeof(DefaultFromFileTextBoxRuntime));
                }
            }
        }
    }
}
