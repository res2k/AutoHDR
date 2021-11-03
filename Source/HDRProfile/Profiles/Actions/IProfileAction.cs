﻿using CodectoryCore.UI.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoHDR.Profiles.Actions
{
    public interface IProfileAction
    {
        string ActionName { get;}
        string ActionTypeName { get; }
        ActionEndResult RunAction(params object[] parameter);
    }
}
