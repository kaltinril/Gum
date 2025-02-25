﻿using System;
using System.Linq;
using XnaAndWinforms;
using System.Windows.Forms;
using RenderingLibrary.Math.Geometry;
using RenderingLibrary.Graphics;
using RenderingLibrary;
using Gum.DataTypes;
using RenderingLibrary.Content;
using Gum.Managers;
using Gum.Plugins.BaseClasses;
using Gum.ToolCommands;
using System.ComponentModel.Composition;
using FlatRedBall.AnimationEditorForms.Controls;
using Microsoft.Xna.Framework.Graphics;

using WinCursor = System.Windows.Forms.Cursor;
using Gum.Plugins;

using Color = System.Drawing.Color;
using Matrix = System.Numerics.Matrix4x4;
using System.Security.Policy;
using Gum.Plugins.InternalPlugins.EditorTab.Services;

namespace Gum.Wireframe
{
    #region WireframeControlPlugin Class

    [Export(typeof(Gum.Plugins.BaseClasses.PluginBase))]
    public class WireframeControlPlugin : InternalPlugin
    {

        public override void StartUp()
        {
            this.ProjectLoad += new Action<GumProjectSave>(OnProjectLoad);
        }

        void OnProjectLoad(GumProjectSave obj)
        {
            GuiCommands_Old.Self.UpdateWireframeToProject();
        }
    }

    #endregion

    public class WireframeControl : GraphicsDeviceControl
    {
        #region Fields

        WireframeEditControl mWireframeEditControl;

        LineRectangle mCanvasBounds;

        public Color ScreenBoundsColor = Color.LightBlue;

        bool mHasInitialized = false;

        Ruler mTopRuler;
        Ruler mLeftRuler;

        public event Action CameraChanged;

        bool mouseHasEntered = false;


        public bool CanvasBoundsVisible
        {
            get => mCanvasBounds.Visible;
            set => mCanvasBounds.Visible = value;
        }

        public bool RulersVisible
        {
            get => mLeftRuler.Visible;
            set
            {
                mLeftRuler.Visible = value;
                mTopRuler.Visible = value;
            }
        }

        #endregion

        #region Properties

        public LineRectangle ScreenBounds
        {
            get { return mCanvasBounds; }
        }

        new InputLibrary.Cursor Cursor
        {
            get
            {
                return InputLibrary.Cursor.Self;
            }
        }

        Camera Camera
        {
            get { return Renderer.Self.Camera; }
        }

        #endregion

        public event EventHandler AfterXnaInitialize;

        #region Event Methods


        void OnKeyDown(object sender, KeyEventArgs e)
        {
            HotkeyManager.Self.HandleKeyDownWireframe(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            bool handled = HotkeyManager.Self.ProcessCmdKeyWireframe(ref msg, keyData);

            if (handled)
            {
                return true;
            }
            else
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }
        }


        #endregion

        #region Initialize Methods

        public void Initialize(WireframeEditControl wireframeEditControl, Panel wireframeParentPanel, 
            HotkeyManager hotkeyManager)
        {
            try
            {
                mWireframeEditControl = wireframeEditControl;


                mWireframeEditControl.ZoomChanged += HandleZoomChanged;

                SystemManagers.Default = new SystemManagers();
                SystemManagers.Default.Initialize(GraphicsDevice);

                ToolFontService.Self.Initialize();
                ToolLayerService.Self.Initialize();

                Renderer.TextureFilter = TextureFilter.Point;


                LoaderManager.Self.ContentLoader = new ContentLoader();

                LoaderManager.Self.Initialize(null, "content/TestFont.fnt", Services, null);
                CameraController.Self.Initialize(Camera, mWireframeEditControl, Width, Height, hotkeyManager);
                CameraController.Self.CameraChanged += () => CameraChanged?.Invoke();

                InputLibrary.Cursor.Self.Initialize(this);
                InputLibrary.Keyboard.Self.Initialize(this);

                mCanvasBounds = new LineRectangle();
                mCanvasBounds.IsDotted = true;
                mCanvasBounds.Name = "Gum Screen Bounds";
                mCanvasBounds.Width = 800;
                mCanvasBounds.Height = 600;
                mCanvasBounds.Color = ScreenBoundsColor;

                this.KeyDown += OnKeyDown;
                this.MouseDown += CameraController.Self.HandleMouseDown;
                this.MouseMove += CameraController.Self.HandleMouseMove;
                this.MouseWheel += CameraController.Self.HandleMouseWheel;

                this.MouseEnter += (not, used) =>
                {
                    mouseHasEntered = true;
                };
                this.MouseLeave += (not, used) =>
                {
                    mouseHasEntered = false;
                };

                if (AfterXnaInitialize != null)
                {
                    AfterXnaInitialize(this, null);
                }

                UpdateCanvasBoundsToProject();

                mHasInitialized = true;

            }
            catch(Exception exception)
            {
                MessageBox.Show("Error initializing the wireframe control\n\n" + exception);
            }
        }

        public void ShareLayerReferences(LayerService layerService)
        {

            SelectionManager.Self.Initialize(layerService);

            ShapeManager.Self.Add(mCanvasBounds, layerService.OverlayLayer);


            this.mTopRuler = new Ruler(this, SystemManagers.Default,
                InputLibrary.Cursor.Self, 
                InputLibrary.Keyboard.Self,
                ToolFontService.Self, 
                ToolLayerService.Self,
                layerService);
            mLeftRuler = new Ruler(this, SystemManagers.Default, 
                InputLibrary.Cursor.Self, 
                InputLibrary.Keyboard.Self,
                ToolFontService.Self, 
                ToolLayerService.Self, 
                layerService);
            mLeftRuler.RulerSide = RulerSide.Left;

        }

        void HandleZoomChanged(object sender, EventArgs e)
        {
            this.mLeftRuler.ZoomValue = mWireframeEditControl.PercentageValue / 100.0f;
            this.mTopRuler.ZoomValue = mWireframeEditControl.PercentageValue / 100.0f;

            Invalidate();
        }

        #endregion



        bool isInActivity = false;

        void Activity()
        {
            if (!isInActivity)
            {
                isInActivity = true;
#if DEBUG
                try
#endif
                {
                    InputLibrary.Cursor.Self.StartCursorSettingFrameStart();
                    TimeManager.Self.Activity();

                    SpriteManager.Self.Activity(TimeManager.Self.CurrentTime);


                    InputLibrary.Cursor.Self.Activity(TimeManager.Self.CurrentTime);
                    InputLibrary.Keyboard.Self.Activity();

                    // This doesn't work, I think it might be because the Window isn't reading keys unless
                    // it is focused...
                    //if(InputLibrary.Keyboard.Self.KeyPushed(Microsoft.Xna.Framework.Input.Keys.Escape))
                    //{

                    //}

                    bool isOver = this.mTopRuler.HandleXnaUpdate(InputLibrary.Cursor.Self.IsInWindow) ||
                        mLeftRuler.HandleXnaUpdate(InputLibrary.Cursor.Self.IsInWindow);


                    // But we want the selection to update the handles to the selected object
                    // after editing is done.  SelectionManager.LateActivity lets us do that.  LateActivity must
                    // come after EditingManager.Activity.

                    // Update 1/15/2019
                    // When the user uses scroll bars we get selection to underlying objects.
                    // We don't want that, so we'll check if the mouse has entered the control.
                    // I may have to update this at some point to force deselection if the mouse
                    // has not entered so things don't stay highlighted when exiting the control
                    // Update 2 - yea, we def need to pass in mouseHasEntered == false to force no highlight
                    
                    if (mTopRuler.IsCursorOver == false && mLeftRuler.IsCursorOver == false)
                    {
                        var shouldForceNoHighlight = mouseHasEntered == false &&
                            // If the mouse is over the element tree view, we don't want to force unhlighlights since they can highlight when over the tree view items
                            ElementTreeViewManager.Self.HasMouseOver == false;

                        SelectionManager.Self.Activity(shouldForceNoHighlight);
                        // EditingManager activity must happen after SelectionManager activity
                        EditingManager.Self.Activity();

                        SelectionManager.Self.LateActivity();
                    }
                    DragDropManager.Self.Activity();

                    InputLibrary.Cursor.Self.EndCursorSettingFrameStart();
                }
#if DEBUG
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }
#endif
            }

            isInActivity = false;
        }

        /// <summary>
        /// Updates the wireframe to match the project settings - specifically the canvas width/height
        /// </summary>
        public void UpdateCanvasBoundsToProject()
        {
            var gumProject = ProjectManager.Self.GumProjectSave;
            if (mCanvasBounds != null && gumProject != null)
            {
                mCanvasBounds.Width = gumProject.DefaultCanvasWidth;
                mCanvasBounds.Height = gumProject.DefaultCanvasHeight;

                CanvasBoundsVisible = gumProject.ShowCanvasOutline;
                RulersVisible = gumProject.ShowRuler;
            }
        }

        protected override void PreDrawUpdate()
        {
            if (mHasInitialized)
            {
                Activity();
            }
        }

        protected override void Draw()
        {
            if (mHasInitialized)
            {
                var backgroundColor = new Microsoft.Xna.Framework.Color();
                if(ProjectManager.Self.GeneralSettingsFile != null)
                {
                    backgroundColor.R = ProjectManager.Self.GeneralSettingsFile.CheckerColor1R;
                    backgroundColor.G = ProjectManager.Self.GeneralSettingsFile.CheckerColor1G;
                    backgroundColor.B = ProjectManager.Self.GeneralSettingsFile.CheckerColor1B;
                }
                else
                {
                    backgroundColor.R = 150;
                    backgroundColor.G = 150;
                    backgroundColor.B = 150;
                }
                GraphicsDevice.Clear(backgroundColor);

                PluginManager.Self.BeforeRender();

                Renderer.Self.Draw((SystemManagers)null);

                PluginManager.Self.AfterRender();

            }
        }

        public void RefreshGuides()
        {
            // setting GuideValues forces a refresh
            mTopRuler.GuideValues = mTopRuler.GuideValues.ToArray();

            mLeftRuler.GuideValues = mLeftRuler.GuideValues.ToArray();
        }
    }
}
