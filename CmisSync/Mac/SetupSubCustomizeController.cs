//-----------------------------------------------------------------------
// <copyright file="SetupSubCustomizeController.cs" company="GRAU DATA AG">
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General private License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General private License for more details.
//
//   You should have received a copy of the GNU General private License
//   along with this program. If not, see http://www.gnu.org/licenses/.
//
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using MonoMac.Foundation;
using MonoMac.AppKit;

namespace CmisSync
{
    public partial class SetupSubCustomizeController : MonoMac.AppKit.NSViewController
    {

        #region Constructors

        // Called when created from unmanaged code
        public SetupSubCustomizeController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }
        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public SetupSubCustomizeController (NSCoder coder) : base (coder)
        {
            Initialize ();
        }
        // Call to load from the XIB/NIB file
        public SetupSubCustomizeController (SetupController controller) : base ("SetupSubCustomize", NSBundle.MainBundle)
        {
            this.Controller = controller;
            Initialize ();
        }
        // Shared initialization code
        void Initialize ()
        {
        }

        #endregion

        protected override void Dispose (bool disposing)
        {
            base.Dispose (disposing);
            Console.WriteLine (this.GetType ().ToString () + " disposed " + disposing.ToString ());
        }

        public override void AwakeFromNib ()
        {
            base.AwakeFromNib ();

            this.RepoNameLabel.StringValue = Properties_Resources.ChangeRepoPath;
            this.LocalPathLabel.StringValue = Properties_Resources.EnterLocalFolderName;

            this.RepoNameDelegate = new TextFieldDelegate ();
            this.RepoNameText.Delegate = this.RepoNameDelegate;
            this.LocalPathDelegate = new TextFieldDelegate ();
            this.LocalPathText.Delegate = this.LocalPathDelegate;

            this.BackButton.Title = Properties_Resources.Back;
            this.AddButton.Title = Properties_Resources.Add;
            this.CancelButton.Title = Properties_Resources.Cancel;

            InsertEvent ();

            //  Must be called after InsertEvent()
            string name = Controller.saved_address.Host.ToString();
            foreach (var repository in Controller.repositories) {
                if (repository.Id == Controller.saved_repository) {
                    name += "/" + repository.Name;
                    break;
                }
            }
            this.RepoNameText.StringValue = name;
            this.LocalPathText.StringValue = Path.Combine (Controller.DefaultRepoPath, name);

            CheckLocalPathText ();
        }

        void InsertEvent()
        {
            this.RepoNameDelegate.StringValueChanged += CheckRepoNameText;
            this.LocalPathDelegate.StringValueChanged += CheckLocalPathText;
            Controller.UpdateAddProjectButtonEvent += SetAddButton;
            Controller.LocalPathExists += LocalPathExistsHandler;
        }

        void RemoveEvent()
        {
            this.RepoNameDelegate.StringValueChanged -= CheckRepoNameText;
            this.LocalPathDelegate.StringValueChanged -= CheckLocalPathText;
            Controller.UpdateAddProjectButtonEvent -= SetAddButton;
            Controller.LocalPathExists -= LocalPathExistsHandler;
        }

        bool LocalPathExistsHandler(string path) {
            NSAlert alert = NSAlert.WithMessage(
                String.Format( Properties_Resources.ConfirmExistingLocalFolderText, path),
                "No, I want to choose another target",
                "Yes, I understand the risk",
                null,
                "");
            alert.Icon = new NSImage (UIHelpers.GetImagePathname ("process-syncing-error", "icns"));
            int i = alert.RunModal();
            return (i == 0);
        }

        void SetAddButton(bool enabled)
        {
            InvokeOnMainThread (delegate
            {
                AddButton.Enabled = enabled;
//                AddButton.KeyEquivalent = "\r";
            });
        }

        void CheckRepoNameText()
        {
            InvokeOnMainThread (delegate
            {
                try {
                        LocalPathText.StringValue = Path.Combine (Controller.DefaultRepoPath, RepoNameText.StringValue);
                } catch (Exception) {
                }
                CheckCustomizeInput ();
            });
        }

        void CheckLocalPathText()
        {
            InvokeOnMainThread (delegate
            {
                CheckCustomizeInput ();
            });
        }

        private void CheckCustomizeInput()
        {
            string error = Controller.CheckRepoPathAndName (LocalPathText.StringValue, RepoNameText.StringValue);
            if (!String.IsNullOrEmpty (error)) {
                WarnText.StringValue = error;
                WarnText.TextColor = NSColor.Red;
            } else {
                try {
                    Controller.CheckRepoPathExists (LocalPathText.StringValue);
                    WarnText.StringValue = "";
                } catch (ArgumentException e) {
                    WarnText.TextColor = NSColor.Orange;
                    WarnText.StringValue = e.Message;
                }
            }
        }

        SetupController Controller;
        TextFieldDelegate RepoNameDelegate;
        TextFieldDelegate LocalPathDelegate;

        partial void OnAdd (MonoMac.Foundation.NSObject sender)
        {
            RemoveEvent();
            Controller.CustomizePageCompleted(RepoNameText.StringValue, LocalPathText.StringValue);
        }

        partial void OnBack (MonoMac.Foundation.NSObject sender)
        {
            RemoveEvent();
            Controller.BackToPage2();
        }

        partial void OnCancel (MonoMac.Foundation.NSObject sender)
        {
            RemoveEvent();
            Controller.PageCancelled();
        }

        partial void OnLocalPath (MonoMac.Foundation.NSObject sender)
        {
            NSOpenPanel OpenPanel = NSOpenPanel.OpenPanel;
            OpenPanel.AllowsMultipleSelection = false;
            OpenPanel.CanChooseFiles = false;
            OpenPanel.CanChooseDirectories = true;
            OpenPanel.CanCreateDirectories = true;
            OpenPanel.DirectoryUrl = new NSUrl("file://localhost" + Controller.DefaultRepoPath);
            if(OpenPanel.RunModal() == 1) {
                string path = OpenPanel.Urls[0].Path;
                try{
                    LocalPathText.StringValue = Path.Combine(path, RepoNameText.StringValue);
                } catch(Exception) {
                    LocalPathText.StringValue = path;
                }
                CheckCustomizeInput();
            }
        }

        //strongly typed view accessor
        public new SetupSubCustomize View {
            get {
                return (SetupSubCustomize)base.View;
            }
        }
    }
}

