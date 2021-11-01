﻿using CodectoryCore.UI.Wpf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows;
using AutoHDR.UWP;
using AutoHDR.ProjectResources;

namespace AutoHDR.Profiles.Actions
{
    public class ProfileActionAdder : DialogViewModelBase
    {
        private bool _canCreate = false;


        private Type _actionType = null;

        public Type ActionType

        {
            get { return _actionType; }
            set { _actionType = value; OnPropertyChanged(); }
        }

        private IProfileAction _profileAction = null;

        public IProfileAction ProfileAction { get => _profileAction; private set { _profileAction = value; OnPropertyChanged(); } }


        public RelayCommand<object> OKClickCommand { get; private set; }

        public event EventHandler OKClicked;

        public ProfileActionAdder()
        {
            Title = Locale_Texts.AddProfileAction;
            CreateRelayCommands();
        }

        private void CreateRelayCommands()
        {
            OKClickCommand = new RelayCommand<object>(CreateBaseProfileAction);
        }



        private void UpdateCanCreate()
        {
            CanCreate = ActionType != null;
        }

        public bool CanCreate { get => _canCreate; set { _canCreate = value; OnPropertyChanged(); } }


        public void CreateBaseProfileAction(object parameter)
        {
            OKClicked?.Invoke(this, EventArgs.Empty);
            CloseDialog(parameter as Window);
        }
     

    }
}