//-----------------------------------------------------------------------
// <copyright file="CrudIT.cs" company="GRAU DATA AG">
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

namespace TestLibrary.IntegrationTests.SelectiveIgnoreTests {
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using CmisSync.Lib;
    using CmisSync.Lib.Cmis;
    using CmisSync.Lib.Cmis.ConvenienceExtenders;
    using CmisSync.Lib.Config;
    using CmisSync.Lib.Events;
    using CmisSync.Lib.SelectiveIgnore;

    using DotCMIS.Client;

    using Moq;

    using NUnit.Framework;

    using TestLibrary.TestUtils;

    // Important!
    // These tests tend to be Erratic Tests, they can not be used for QA
    // This more of a "rapid szenario creation" class
    // Please do write predictable unit tests for all fixes (IT here is not enough)

    [TestFixture, TestName("SelectiveIgnore"), Category("SelectiveIgnore"), Category("Slow"), Timeout(180000)]
    public class CrudIT : BaseFullRepoTest {
        [Test]
        public void SelectiveIgnoreSupportTest() {
            this.session.SupportsSelectiveIgnore();
        }

        [Test]
        public void IgnoreRemoteFolder() {
            this.session.EnsureSelectiveIgnoreSupportIsAvailable();
            var folder = this.remoteRootDir.CreateFolder("ignored");

            folder.IgnoreAllChildren();

            var context = OperationContextFactory.CreateContext(this.session, false, true);
            var underTest = this.session.GetObject(folder.Id, context) as IFolder;

            Assert.That(folder.AreAllChildrenIgnored(), Is.True);
            Assert.That(underTest.AreAllChildrenIgnored(), Is.True);
        }

        [Test]
        public void RemoteIgnoredFolderIsSynced() {
            this.session.EnsureSelectiveIgnoreSupportIsAvailable();
            var folderName = "ignored";
            var ignoredFolder = this.remoteRootDir.CreateFolder(folderName);
            ignoredFolder.IgnoreAllChildren();
            ignoredFolder.CreateFolder("sub");

            this.InitializeAndRunRepo();

            var folder = this.session.GetObject(ignoredFolder.Id) as IFolder;
            Assert.That(folder.AreAllChildrenIgnored(), Is.True);
            Assert.That(this.localRootDir.GetDirectories()[0].Name, Is.EqualTo(folderName));
            Assert.That(this.localRootDir.GetDirectories()[0].GetDirectories(), Is.Empty);
        }

        [Test]
        public void RemoteFolderIsSyncedAndChangedToIgnored([Values(true, false)]bool contentChanges) {
            this.ContentChangesActive = contentChanges;
            this.session.EnsureSelectiveIgnoreSupportIsAvailable();
            var ignoredFolder = this.remoteRootDir.CreateFolder("ignored");
            var subFolder = ignoredFolder.CreateFolder("sub");

            string remoteTree = @"
ignored
└── sub";
            string localTree = remoteTree;
            Assert.That(new FolderTree(remoteTree), Is.EqualTo(new FolderTree(ignoredFolder)));
            this.InitializeAndRunRepo();
            Assert.That(new FolderTree(localTree), Is.EqualTo(new FolderTree(this.localRootDir.GetDirectories()[0])));
            this.WaitForRemoteChanges();
            this.AddStartNextSyncEvent();
            this.repo.Run();

            ignoredFolder.Refresh();
            ignoredFolder.IgnoreAllChildren();
            subFolder.Refresh();
            subFolder.CreateFolder("bla");
            remoteTree = @"
ignored
└── sub
    └── bla";
            Assert.That(new FolderTree(remoteTree), Is.EqualTo(new FolderTree(ignoredFolder)));
            Assert.That(new FolderTree(localTree), Is.EqualTo(new FolderTree(this.localRootDir.GetDirectories()[0])));
            this.AddStartNextSyncEvent(true);
            this.repo.Run();

            Assert.That(new FolderTree(remoteTree), Is.EqualTo(new FolderTree(ignoredFolder)));
            Assert.That(new FolderTree(localTree), Is.EqualTo(new FolderTree(this.localRootDir.GetDirectories()[0])));
        }

        [Test]
        public void DeleteLocalFolderAfterRemoteIgnore([Values(true, false)]bool contentChanges) {
            this.ContentChangesActive = contentChanges;
            this.session.EnsureSelectiveIgnoreSupportIsAvailable();
            string folderName = "ignored";
            var ignoredFolder = this.remoteRootDir.CreateFolder(folderName);
            ignoredFolder.IgnoreAllChildren();

            this.InitializeAndRunRepo(swallowExceptions: !contentChanges);

            Assert.That(this.localRootDir.GetDirectories()[0].Name, Is.EqualTo(folderName));

            var localFolder = this.localRootDir.GetDirectories()[0];

            this.AddStartNextSyncEvent();

            localFolder.Delete();

            this.WaitUntilQueueIsNotEmpty();
            this.AddStartNextSyncEvent();
            this.repo.Run();

            this.remoteRootDir.Refresh();
            Assert.That(this.remoteRootDir.GetChildren(), Is.Empty);
        }

        [Test]
        public void IgnoreMultipleDeviceIds() {
            this.session.EnsureSelectiveIgnoreSupportIsAvailable();
            var folder = this.remoteRootDir.CreateFolder("folder");
            var anotherUuid = Guid.NewGuid();
            folder.IgnoreAllChildren(anotherUuid.ToString());

            Assert.That(folder.AreAllChildrenIgnored(), Is.False);

            folder.IgnoreAllChildren();

            Assert.That(folder.AreAllChildrenIgnored(), Is.True);
        }

        [Test]
        public void StopIgnoringFolderForThisDeviceId() {
            this.session.EnsureSelectiveIgnoreSupportIsAvailable();
            var folder = this.remoteRootDir.CreateFolder("folder");
            var anotherId = Guid.NewGuid().ToString().ToLower();
            var ownId = ConfigManager.CurrentConfig.DeviceId.ToString().ToLower();
            folder.IgnoreAllChildren(anotherId);
            Assert.That(folder.AreAllChildrenIgnored(), Is.False);
            folder.IgnoreAllChildren(ownId);
            Assert.That(folder.AreAllChildrenIgnored(), Is.True);
            folder.RemoveSyncIgnore(ownId);
            Assert.That(folder.AreAllChildrenIgnored(), Is.False);
            Assert.That(folder.IgnoredDevices().Contains(anotherId));
            Assert.That(folder.IgnoredDevices().Count, Is.EqualTo(1));
            folder.RemoveAllSyncIgnores();
            Assert.That(folder.AreAllChildrenIgnored(), Is.False);
            Assert.That(folder.IgnoredDevices(), Is.Empty);
        }

        [Test]
        public void RemoveMultipleDeviceIds() {
            this.session.EnsureSelectiveIgnoreSupportIsAvailable();
            var folder = this.remoteRootDir.CreateFolder("folder");
            for (int i = 1; i <= 10; i++) {
                folder.IgnoreAllChildren(Guid.NewGuid().ToString().ToLower());
                Assert.That(folder.IgnoredDevices().Count, Is.EqualTo(i));
            }

            for (int i = 9; i >= 0; i--) {
                folder.RemoveSyncIgnore(folder.IgnoredDevices().First());
                Assert.That(folder.IgnoredDevices().Count, Is.EqualTo(i));
            }
        }

        [Test]
        public void SetIgnorePropertyChangesModificationDate() {
            this.session.EnsureSelectiveIgnoreSupportIsAvailable();
            this.session.EnsureServerCanUpdateModificationDate();

            var folder = this.remoteRootDir.CreateFolder("folder");
            var past = DateTime.UtcNow - TimeSpan.FromDays(7);
            folder.UpdateLastWriteTimeUtc(past);
            var anotherDeviceId = Guid.NewGuid();
            folder.IgnoreAllChildren(anotherDeviceId.ToString());

            this.InitializeAndRunRepo();
            this.localRootDir.GetDirectories()[0].CreateSubdirectory("NotIgnored");
            this.WaitUntilQueueIsNotEmpty();
            this.AddStartNextSyncEvent();
            this.repo.Run();

            folder.Refresh();
            Assert.That(folder.LastModificationDate, Is.EqualTo(DateTime.UtcNow).Within(1).Days);
            Assert.That(folder.GetChildren(), Is.Not.Empty);
        }
    }
}