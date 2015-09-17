//-----------------------------------------------------------------------
// <copyright file="LongFileAndPathNameSupportTest.cs" company="GRAU DATA AG">
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

namespace TestLibrary.StorageTests.FileSystemTests {
    using System;
    using System.IO;
#if !__MonoCS__
    using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
    using FileSystemInfo = Alphaleonis.Win32.Filesystem.FileSystemInfo;
    using DirectoryInfo = Alphaleonis.Win32.Filesystem.DirectoryInfo;
    using Directory = Alphaleonis.Win32.Filesystem.Directory;
    using Path = Alphaleonis.Win32.Filesystem.Path;
#endif
    using CmisSync.Lib.Storage.FileSystem;

    using NUnit.Framework;

    using TestLibrary.IntegrationTests;

    [TestFixture, Category("Medium")]
    public class LongFileAndPathNameSupportTest {
        private static readonly IFileSystemInfoFactory Factory = new FileSystemInfoFactory();

        // ext2, ext3, ext4, btrfs, zfs and ntfs maximum file name length is 255. reiserfs limits are dynamic and higher.
        private static readonly int MaxFileNameLength = 255;
        private string longName;
        private string longPath;
        private string testFolder;

        [SetUp]
        public void SetUp() {
            var config = ITUtils.GetConfig();
            string localPath = config[1].ToString();
            this.testFolder = Path.Combine(localPath, Guid.NewGuid().ToString());
            string shortName = "1234567890";
            this.longPath = shortName;
            for (int i = 0; i < 30; i++) {
                this.longPath = Path.Combine(this.longPath, shortName);
            }

            var dir = new DirectoryInfo(this.testFolder);
            dir.Create();
            this.longPath = dir.CreateSubdirectory(this.longPath).FullName;
            Console.WriteLine(this.longPath);
            
            this.longName = string.Empty;
            for (int i = 0; i < MaxFileNameLength; i++) {
                this.longName += "a";
            }
        }

        [TearDown]
        public void CleanUpTestFolder() {
            if (Directory.Exists(this.testFolder)) {
                Directory.Delete(this.testFolder, true);
            }
        }

        [Test]
        public void OpenFile() {
            var file = Factory.CreateFileInfo(Path.Combine(this.longPath, this.longName));
            using (file.Open(FileMode.CreateNew));

            using (file.Open(FileMode.Open));

            using (file.Open(FileMode.Truncate));
        }

        [Test]
        public void FileExists() {
            var file = Factory.CreateFileInfo(Path.Combine(this.longPath, this.longName));
            Assert.That(file.Exists, Is.False);
            using (file.Open(FileMode.CreateNew)) {
            }

            file.Refresh();
            Assert.That(file.Exists, Is.True);
        }

        [Test]
        public void MoveFile() {
            var file = Factory.CreateFileInfo(Path.Combine(this.longPath, "oldName"));
            using (file.Open(FileMode.CreateNew)) {
            }

            file.MoveTo(Path.Combine(this.testFolder, this.longName));
        }

        [Test]
        public void ReplaceFile() {
            var sourceFile = Factory.CreateFileInfo(Path.Combine(this.longPath, "source"));
            var destFile = Factory.CreateFileInfo(Path.Combine(this.longPath, this.longName));
            var backupFile = Factory.CreateFileInfo(Path.Combine(this.longPath, this.longName.Substring(1)));
            using (sourceFile.Open(FileMode.CreateNew)) {
            }

            using (destFile.Open(FileMode.CreateNew)) {
            }

            sourceFile.Replace(destFile, backupFile, true);
        }

        [Test]
        public void DeleteFile() {
            var file = Factory.CreateFileInfo(Path.Combine(this.longPath, this.longName));
            using (file.Open(FileMode.CreateNew)) {
            }

            file.Delete();

            Assert.That(file.Exists, Is.False);
        }

        [Test]
        public void CreateDirectory() {
            var dir = Factory.CreateDirectoryInfo(Path.Combine(this.longPath, this.longName));
            dir.Create();
        }

        [Test]
        public void DirectoryExists() {
            var dir = Factory.CreateDirectoryInfo(Path.Combine(this.longPath, this.longName));
            Assert.That(dir.Exists, Is.False);
            dir.Create();
            dir.Refresh();
            Assert.That(dir.Exists, Is.True);
        }

        [Test]
        public void DeleteDirectory() {
            var dir = Factory.CreateDirectoryInfo(Path.Combine(this.longPath, this.longName));
            dir.Create();
            dir.Delete(false);
        }

        [Test]
        public void GetUuid() {
            this.EnsureExtendedAttributesAreAvailable();
            var dir = Factory.CreateDirectoryInfo(Path.Combine(this.longPath, this.longName));
            dir.Create();

            Assert.That(dir.Uuid, Is.Null);
        }

        [Test]
        public void SetUuid() {
            this.EnsureExtendedAttributesAreAvailable();
            var dir = Factory.CreateDirectoryInfo(Path.Combine(this.longPath, this.longName));
            dir.Create();
            Guid uuid = Guid.NewGuid();
            dir.Uuid = uuid;

            Assert.That(dir.Uuid, Is.EqualTo(uuid));
        }

        [Test]
        public void GetFileUuid() {
            this.EnsureExtendedAttributesAreAvailable();
            var file = Factory.CreateFileInfo(Path.Combine(this.longPath, this.longName));
            using(file.Open(FileMode.CreateNew));

            Assert.That(file.Uuid, Is.Null);
        }

        [Test]
        public void TruncateModeToInt() {
            Assert.That((int)FileMode.Truncate, Is.EqualTo(5));
        }

        [Test]
        public void SetFileUuid() {
            this.EnsureExtendedAttributesAreAvailable();
            var file = Factory.CreateFileInfo(Path.Combine(this.longPath, this.longName));
            using(file.Open(FileMode.CreateNew));
            Guid uuid = Guid.NewGuid();
            file.Uuid = uuid;

            Assert.That(file.Uuid, Is.EqualTo(uuid));
        }

        [Test]
        public void GetLastWriteTimeUtc() {
            var file = Factory.CreateFileInfo(Path.Combine(this.longPath, this.longName));
            using (file.Open(FileMode.CreateNew)) {
            }

            Assert.That(file.LastWriteTimeUtc, Is.Not.EqualTo(DateTime.MinValue));
        }

        [Test]
        public void SetLastWriteTimeUtc() {
            var file = Factory.CreateFileInfo(Path.Combine(this.longPath, this.longName));
            using (file.Open(FileMode.CreateNew)) {
            }

            file.LastWriteTimeUtc = DateTime.UtcNow;
        }

        [Test]
        public void GetCreationTimeUtc() {
            var file = Factory.CreateFileInfo(Path.Combine(this.longPath, this.longName));
            using (file.Open(FileMode.CreateNew)) {
            }

            Assert.That(file.CreationTimeUtc, Is.Not.EqualTo(DateTime.MinValue));
        }

        private void EnsureExtendedAttributesAreAvailable() {
            var dir = new DirectoryInfoWrapper(new DirectoryInfo(this.testFolder));
            if (!dir.IsExtendedAttributeAvailable()) {
                Assert.Ignore("Extended Attribute not available on this machine");
            }
        }
    }
}