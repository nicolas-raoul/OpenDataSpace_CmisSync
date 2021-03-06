//-----------------------------------------------------------------------
// <copyright file="EditWizardController.cs" company="GRAU DATA AG">
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

namespace CmisSync {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Drawing;
    using MonoMac.Foundation;
    using MonoMac.AppKit;

    using CmisSync.Lib.Cmis;
    using CmisSync.Lib.Config;
    using CmisSync.CmisTree;
    using System.Threading.Tasks;
    using CmisSync.Lib.Cmis.UiUtils;

    public partial class EditWizardController : MonoMac.AppKit.NSWindowController {
        #region Constructors

        // Called when created from unmanaged code
        public EditWizardController(IntPtr handle) : base(handle) {
            Initialize ();
        }

        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public EditWizardController(NSCoder coder) : base(coder) {
            Initialize();
        }

        // Call to load from the XIB/NIB file
        public EditWizardController(
            EditType type,
            CmisRepoCredentials credentials,
            string name,
            string remotePath,
            List<string> ignores,
            string localPath) : base("EditWizard")
        {
            FolderName = name;
            this.Credentials = credentials;
            this.remotePath = remotePath;
            this.Ignores = new List<string>(ignores);
            this.localPath = localPath;
            this.type = type;

            Initialize();

            Controller.OpenWindowEvent += () => {
                InvokeOnMainThread(() => {
                    this.Window.OrderFrontRegardless();
                });
            };
        }

        // Shared initialization code
        void Initialize() {
        }

        #endregion

        public enum EditType {
            EditFolder,
            EditCredentials
        };

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
        }

        public EditController Controller = new EditController();

        public string FolderName;
        public List<string> Ignores;
        public CmisRepoCredentials Credentials;

        public long DownloadLimit { get; set; }

        public long UploadLimit { get; set;
/*            get {
                if (this.UploadLimitCheckBox.State == NSCellStateValue.Off) {
                    return 0;
                } else {
                    long result;
                    if (long.TryParse(this.UploadLimitTextField.StringValue, out result)) {
                        return result;
                    } else {
                        return 0;
                    }
                }
            }

            set {
                value = value / 1024;
                this.UploadLimitCheckBox.State = value > 0 ? NSCellStateValue.On : NSCellStateValue.Off;
                this.UploadLimitTextField.Enabled = value > 0;
                this.UploadLimitTextField.StringValue = value.ToString();
            }*/
            /*            get {
            if (this.DownloadLimitCheckBox.State == NSCellStateValue.Off) {
                return 0;
            } else {
                long result;
                if (long.TryParse(this.DownloadLimitTextField.StringValue, out result)) {
                    return result;
                } else {
                    return 0;
                }
            }
        }

        set {
            value = value / 1024;
            this.DownloadLimitCheckBox.State = value > 0 ? NSCellStateValue.On : NSCellStateValue.Off;
            this.DownloadLimitTextField.Enabled = value > 0;
            this.DownloadLimitTextField.StringValue = value.ToString();
        }*/

        }

        private string remotePath;
        private string localPath;
        private EditType type;

        RootFolder Repo;
        private CmisTreeDataSource DataSource;
        private OutlineViewDelegate DataDelegate;
        private AsyncNodeLoader Loader;
        private Object loginLock = new Object();
        private bool isClosed;

        public override void AwakeFromNib() {
            base.AwakeFromNib();

            this.SideSplashView.Image = new NSImage(UIHelpers.GetImagePathname("side-splash")) {
                Size = new SizeF(150, 482)
            };

            this.Header.StringValue = Properties_Resources.EditTitle;

            Repo = new RootFolder() {
                Name = FolderName,
                Id = Credentials.RepoId,
                Address = Credentials.Address.ToString()
            };
            Repo.Selected = true;
            IgnoredFolderLoader.AddIgnoredFolderToRootNode(Repo, Ignores);
            LocalFolderLoader.AddLocalFolderToRootNode(Repo, localPath);
            List<RootFolder> repos = new List<RootFolder>();
            repos.Add(Repo);

            Loader = new AsyncNodeLoader(
                Repo,
                Credentials,
                PredefinedNodeLoader.LoadSubFolderDelegate,
                PredefinedNodeLoader.CheckSubFolderDelegate);

            CancelButton.Title = Properties_Resources.DiscardChanges;
            FinishButton.Title = Properties_Resources.SaveChanges;

            DataDelegate = new OutlineViewDelegate();
            DataSource = new CmisTree.CmisTreeDataSource(repos);
            Outline.DataSource = DataSource;
            Outline.Delegate = DataDelegate;

            this.AddressLabel.StringValue = Properties_Resources.CmisWebAddress;
            this.UserLabel.StringValue = Properties_Resources.User;
            this.PasswordLabel.StringValue = Properties_Resources.Password;

            this.AddressText.StringValue = Credentials.Address.ToString();
            this.UserText.StringValue = Credentials.UserName;
            this.PasswordText.StringValue = Credentials.Password.ToString();
            this.AddressText.Enabled = false;
            this.UserText.Enabled = false;
            this.LoginStatusProgress.IsDisplayedWhenStopped = false;
            this.LoginStatusLabel.Hidden = true;
            this.FolderTab.Label = Properties_Resources.AddingFolder;
            this.CredentialsTab.Label = Properties_Resources.Credentials;
            this.DownloadLimitCheckBox.State = this.DownloadLimit > 0 ? NSCellStateValue.On : NSCellStateValue.Off;
            this.DownloadLimitTextField.Enabled = this.DownloadLimit > 0;
            this.DownloadLimitTextField.StringValue = this.DownloadLimit > 0 ? (this.DownloadLimit / 1024).ToString() : "100";
            this.UploadLimitCheckBox.State = this.UploadLimit > 0 ? NSCellStateValue.On : NSCellStateValue.Off;
            this.UploadLimitTextField.Enabled = this.UploadLimit > 0;
            this.UploadLimitTextField.StringValue = this.UploadLimit > 0 ? (this.UploadLimit / 1024).ToString() : "100";
            this.BandwidthTabView.Label = Properties_Resources.Bandwidth;
            this.UploadLimitBox.Title = Properties_Resources.UploadLimit;
            this.DownloadLimitBox.Title = Properties_Resources.DownloadLimit;

            switch (this.type) {
            case EditType.EditFolder:
                TabView.SelectAt(0);
                break;
            case EditType.EditCredentials:
                TabView.SelectAt(1);
                break;
            default:
                TabView.SelectAt(0);
                break;
            }

            //  GUI workaround to remove ignore folder {{
            this.TabView.Remove(this.FolderTab);
            //  GUI workaround to remove ignore folder }}

            Controller.CloseWindowEvent += delegate {
                Loader.Cancel();
                this.Window.PerformClose(this);
                this.Dispose();
            };

            InsertEvent();

            //  must be called after InsertEvent()
            Loader.Load(Repo);
            lock (loginLock) {
                isClosed = false;
            }

            OnPasswordChanged(this);
        }

        void InsertEvent() {
            DataSource.SelectedEvent += OutlineSelected;
            DataDelegate.ItemExpanded += OutlineItemExpanded;
            Loader.UpdateNodeEvent += AsyncNodeUpdate;
        }

        void RemoveEvent() {
            DataSource.SelectedEvent -= OutlineSelected;
            DataDelegate.ItemExpanded -= OutlineItemExpanded;
            Loader.UpdateNodeEvent -= AsyncNodeUpdate;
        }

        void AsyncNodeUpdate() {
            InvokeOnMainThread(delegate {
                DataSource.UpdateCmisTree(Repo);
                for (int i = 0; i < Outline.RowCount; ++i) {
                    Outline.ReloadItem(Outline.ItemAtRow(i));
                }
            });
        }

        void OutlineItemExpanded(NSNotification notification) {
            InvokeOnMainThread(delegate {
                NSCmisTree cmis = notification.UserInfo["NSObject"] as NSCmisTree;
                if (cmis == null) {
                    Console.WriteLine("ItemExpanded Error");
                    return;
                }

                NSCmisTree cmisRoot = cmis;
                while (cmisRoot.Parent != null) {
                    cmisRoot = cmisRoot.Parent;
                }

                if (Repo.Name != cmisRoot.Name) {
                    Console.WriteLine("ItemExpanded find root Error");
                    return;
                }

                Node node = cmis.GetNode(Repo);
                if (node == null) {
                    Console.WriteLine("ItemExpanded find node Error");
                    return;
                }

                Loader.Load(node);
            });
        }

        void OutlineSelected(NSCmisTree cmis, int selected) {
            InvokeOnMainThread(delegate {
                Node node = cmis.GetNode(Repo);
                if (node == null) {
                    Console.WriteLine("SelectedEvent find node Error");
                }

                node.Selected = (selected != 0);
                DataSource.UpdateCmisTree(Repo);

                for (int i = 0; i < Outline.RowCount; ++i) {
                    try {
                        Outline.ReloadItem(Outline.ItemAtRow(i));
                    } catch (Exception e) {
                        Console.WriteLine (e);
                    }
                }
            });
        }

        partial void OnCancel(MonoMac.Foundation.NSObject sender) {
            lock (loginLock) {
                isClosed = true;
            }

            Loader.Cancel();
            RemoveEvent();
            Controller.CloseWindow();
        }

        partial void OnPasswordChanged(NSObject sender) {
            this.LoginStatusLabel.StringValue = "logging in...";
            this.LoginStatusLabel.Hidden = false;
            //  monomac bug: animation GUI effect will cause GUI to hang, when backend thread is busy
//            this.LoginStatusProgress.StartAnimation(this);
            ServerCredentials cred = new ServerCredentials() {
                Address = Credentials.Address,
                Binding = Credentials.Binding,
                UserName = Credentials.UserName,
                Password = PasswordText.StringValue
            };
            PasswordText.Enabled = false;
            new TaskFactory().StartNew(() => {
                try {
                    cred.GetRepositories();
                    this.InvokeOnMainThread(()=> {
                        lock (loginLock) {
                            if (!isClosed) {
                                this.LoginStatusLabel.StringValue = "login successful";
                            }
                        }
                    });
                } catch(Exception e) {
                    this.InvokeOnMainThread(() => {
                        lock (loginLock) {
                            if (!isClosed) {
                                this.LoginStatusLabel.StringValue = "login failed: " + e.Message;
                            }
                        }
                    });
                }

                this.InvokeOnMainThread(() => {
                    lock (loginLock) {
                        PasswordText.Enabled = true;
                        if (!isClosed) {
                            this.LoginStatusProgress.StopAnimation(this);
                        }
                    }
                });
            });
        }

        partial void OnFinish(MonoMac.Foundation.NSObject sender) {
            lock (loginLock) {
                isClosed = true;
            }

            Loader.Cancel();
            RemoveEvent();
            Ignores = NodeModelUtils.GetIgnoredFolder(Repo);
            Credentials.Password = PasswordText.StringValue;
            if (this.UploadLimitCheckBox.State == NSCellStateValue.Off) {
                this.UploadLimit = 0;
            } else {
                long limit;
                if (long.TryParse(this.UploadLimitTextField.StringValue, out limit)) {
                    this.UploadLimit = limit * 1024;
                } else {
                    this.UploadLimit = 0;
                }
            }

            if (this.DownloadLimitCheckBox.State == NSCellStateValue.Off) {
                this.DownloadLimit = 0;
            } else {
                long limit;
                if (long.TryParse(this.DownloadLimitTextField.StringValue, out limit)) {
                    this.DownloadLimit = limit * 1024;
                } else {
                    this.DownloadLimit = 0;
                }
            }

            Controller.SaveFolder();
            Controller.CloseWindow();
        }

        partial void DownloadLimitClicked(NSObject sender) {
            this.DownloadLimitTextField.Enabled = this.DownloadLimitCheckBox.State == NSCellStateValue.On;
        }

        partial void UploadLimitClicked(NSObject sender) {
            this.UploadLimitTextField.Enabled = this.UploadLimitCheckBox.State == NSCellStateValue.On;
        }

        //strongly typed window accessor
        public new EditWizard Window {
            get {
                return (EditWizard)base.Window;
            }
        }
    }
}