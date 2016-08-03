﻿//-----------------------------------------------------------------------
// <copyright file="NtfsACLsTest.cs" company="GRAU DATA AG">
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
#if !__MonoCS__
    using System;
    using System.IO;
    using System.Security.AccessControl;
    using System.Security.Principal;

    using NUnit.Framework;
    using CmisSync.Lib.Storage.FileSystem;

    [TestFixture]
    public class NtfsACLsTest {
        private SecurityIdentifier actualUser;
        private DirectoryInfo testFolder;

        [TestFixtureSetUp]
        public void GrabActualUser() {
            actualUser = WindowsIdentity.GetCurrent().User;
        }

        [SetUp]
        public void CreateTestFolder() {
            string tempPath = Path.GetTempPath();
            var tempFolder = new DirectoryInfo(tempPath);
            Assert.That(tempFolder.Exists, Is.True);
            testFolder = tempFolder.CreateSubdirectory(Guid.NewGuid().ToString()).CreateSubdirectory(Guid.NewGuid().ToString());
        }

        [TearDown]
        public void RemoveTestFolder() {
            if (testFolder.Exists)
            {
                var dir = new DirectoryInfoWrapper(this.testFolder);
                dir.CanMoveOrRenameOrDelete = true;
                dir.ReadOnly = false;
                testFolder.Delete(true);
            }

            if (testFolder.Parent.Exists)
            {
                testFolder.Parent.Delete(true);
            }
        }

        [Test]
        public void RenameOrMoveOrRemoveFolderIsForbidden() {
            new DirectoryInfoWrapper(testFolder).CanMoveOrRenameOrDelete = false;
            var fullName = testFolder.FullName;

            Assert.Throws<IOException>(() => testFolder.Delete());
            Assert.That(testFolder.FullName, Is.EqualTo(fullName));
            Assert.That(testFolder.Exists, Is.True);
            Assert.Throws<IOException>(() => testFolder.MoveTo(Path.Combine(testFolder.Parent.FullName, "anotherName" + Guid.NewGuid().ToString())));
            Assert.That(testFolder.FullName, Is.EqualTo(fullName));
            Assert.Throws<IOException>(() => testFolder.MoveTo(Path.Combine(testFolder.Parent.Parent.FullName, testFolder.Name)));
            Assert.That(testFolder.FullName, Is.EqualTo(fullName));
        }
    }
#endif
}