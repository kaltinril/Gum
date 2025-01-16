﻿using Gum.Wireframe;
using MonoGameGum.Forms.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonoGameGum.Forms.DefaultFromFileVisuals;

internal class DefaultFromFileMenuItemRuntime : InteractiveGue
{
    public DefaultFromFileMenuItemRuntime(bool fullInstantiation = true, bool tryCreateFormsObject = true) :
    base()
    { }

    public override void AfterFullCreation()
    {
        base.AfterFullCreation();
        if (FormsControl == null)
        {
            FormsControlAsObject = new MenuItem(this);
        }
    }
    public MenuItem FormsControl => FormsControlAsObject as MenuItem;

}