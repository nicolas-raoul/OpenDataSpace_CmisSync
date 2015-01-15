﻿//-----------------------------------------------------------------------
// <copyright file="FileTransmissionStorageTest.cs" company="GRAU DATA AG">
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

namespace TestLibrary.StorageTests.DataBaseTests
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.IO;

    using CmisSync.Lib.Storage.FileSystem;
    using CmisSync.Lib.Storage.Database;
    using CmisSync.Lib.Storage.Database.Entities;

    using DBreeze;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    public class FileTransmissionStorageTest
    {
        private DBreezeEngine Engine;
        private string DBreezePath = null;
        private Mock<IFileInfo> LocalFile;
        private Mock<DotCMIS.Client.IDocument> RemoteFile;

        [SetUp]
        public void SetUp()
        {
            Engine = new DBreezeEngine(new DBreezeConfiguration { Storage = DBreezeConfiguration.eStorage.MEMORY });

            DBreezePath = Path.Combine(Path.GetTempPath(), "FileTransmissionStorageTest-DBreeze");

            LocalFile = new Mock<IFileInfo>();
            LocalFile.SetupAllProperties();
            LocalFile.Setup(f => f.Length).Returns(1024);
            LocalFile.Setup(f => f.Name).Returns("FileTransmissionStorageTest.file");
            LocalFile.Setup(f => f.FullName).Returns(Path.Combine(Path.GetTempPath(), LocalFile.Object.Name));
            LocalFile.Setup(f => f.Exists).Returns(true);
            LocalFile.Object.LastWriteTimeUtc = DateTime.UtcNow;

            RemoteFile = new Mock<DotCMIS.Client.IDocument>();
            RemoteFile.Setup(m => m.LastModificationDate).Returns(LocalFile.Object.LastWriteTimeUtc);
            RemoteFile.Setup(m => m.Paths).Returns(new List<string>() { "/RemoteFile" });
            RemoteFile.Setup(m => m.ChangeToken).Returns("ChangeToken");
        }

        [TearDown]
        public void TearDown()
        {
            this.Engine.Dispose();
            if (Directory.Exists(DBreezePath))
            {
                Directory.Delete(DBreezePath, true);
            }
        }

        [Test, Category("Fast"), Category("FileTransmissionStorage")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorThrowsExceptionIfEngineIsNull()
        {
            new FileTransmissionStorage(null);
        }

        [Test, Category("Fast"), Category("FileTransmissionStorage")]
        public void ConstructorTakesData()
        {
            new FileTransmissionStorage(Engine);
        }

        [Test, Category("Fast"), Category("FileTransmissionStorage")]
        public void GetObjectListReturnsZeroSizeListFromEmptyStorage()
        {
            var storage = new FileTransmissionStorage(Engine);
            Assert.That(storage.GetObjectList().Count, Is.EqualTo(0));
        }

        [Test, Category("Fast"), Category("FileTransmissionStorage")]
        public void SaveObjectThrowsExceptionIfObjectIsInvalid()
        {
            var storage = new FileTransmissionStorage(Engine);
            var obj = new Mock<IFileTransmissionObject>();

            //  argument cannot be null
            Assert.Throws<ArgumentNullException>(() => storage.SaveObject(null));
            Assert.That(storage.GetObjectList().Count, Is.EqualTo(0));

            //  IFileTransmissionObject.LocalPath cannot be null
            Assert.Throws<ArgumentNullException>(() => storage.SaveObject(obj.Object));
            Assert.That(storage.GetObjectList().Count, Is.EqualTo(0));

            //  IFileTransmissionObject.LocalPath cannot be empty string
            obj.Setup(m => m.LocalPath).Returns(string.Empty);
            Assert.Throws<ArgumentException>(() => storage.SaveObject(obj.Object));
            Assert.That(storage.GetObjectList().Count, Is.EqualTo(0));

            //  IFileTransmissionObject.RemoteObjectId cannot be null
            obj.Setup(m => m.LocalPath).Returns("/LocalPath");
            Assert.Throws<ArgumentNullException>(() => storage.SaveObject(obj.Object));
            Assert.That(storage.GetObjectList().Count, Is.EqualTo(0));

            //  IFileTransmissionObject.RemoteObjectId cannot be empty string
            obj.Setup(m => m.RemoteObjectId).Returns(string.Empty);
            Assert.Throws<ArgumentException>(() => storage.SaveObject(obj.Object));
            Assert.That(storage.GetObjectList().Count, Is.EqualTo(0));

            //  argument should be FileTransmissionObject 
            obj.Setup(m => m.RemoteObjectId).Returns("RemoteObjectId");
            Assert.Throws<ArgumentException>(() => storage.SaveObject(obj.Object));
            Assert.That(storage.GetObjectList().Count, Is.EqualTo(0));
        }

        [Test, Category("Fast"), Category("FileTransmissionStorage")]
        public void SaveObjectOnMultipleTimes()
        {
            var storage = new FileTransmissionStorage(Engine);

            for (int i = 1; i <= 10; ++i)
            {
                RemoteFile.Setup(m => m.Id).Returns("RemoteObjectId" + i.ToString());
                var obj = new FileTransmissionObject(CmisSync.Lib.Events.FileTransmissionType.UPLOAD_NEW_FILE, LocalFile.Object, RemoteFile.Object);
                Assert.DoesNotThrow(() => storage.SaveObject(obj));
                Assert.That(storage.GetObjectList().Count, Is.EqualTo(i));
                Assert.That(storage.GetObjectList().First(foo => foo.LocalPath == LocalFile.Object.FullName && foo.RemoteObjectId == "RemoteObjectId" + i.ToString()), Is.Not.Null);
                Assert.That(storage.GetObjectByRemoteObjectId("RemoteObjectId" + i.ToString()), Is.Not.Null);
            }

            for (int i = 1; i <= 10; ++i)
            {
                Assert.That(storage.GetObjectList().First(foo => foo.LocalPath == LocalFile.Object.FullName && foo.RemoteObjectId == "RemoteObjectId" + i.ToString()), Is.Not.Null);
                Assert.That(storage.GetObjectByRemoteObjectId("RemoteObjectId" + i.ToString()), Is.Not.Null);
            }
        }

        [Test, Category("Fast"), Category("FileTransmissionStorage")]
        public void SaveObjectBehaviorOverride()
        {
            var storage = new FileTransmissionStorage(Engine);
            RemoteFile.Setup(m => m.Id).Returns("RemoteObjectId");

            for (int i = 1; i <= 10; ++i)
            {
                var obj = new FileTransmissionObject(CmisSync.Lib.Events.FileTransmissionType.UPLOAD_NEW_FILE, LocalFile.Object, RemoteFile.Object);
                Assert.DoesNotThrow(() => storage.SaveObject(obj));
                Assert.That(storage.GetObjectList().Count, Is.EqualTo(1));
                Assert.That(storage.GetObjectList().First(foo => foo.LocalPath == LocalFile.Object.FullName && foo.RemoteObjectId == "RemoteObjectId"), Is.Not.Null);
                Assert.That(storage.GetObjectByRemoteObjectId("RemoteObjectId"), Is.Not.Null);
            }
        }

        [Test, Category("Fast"), Category("FileTransmissionStorage")]
        public void RemoveObjectThrowsExceptionIfRemoteObjectIdIsInvalid()
        {
            var storage = new FileTransmissionStorage(Engine);

            Assert.Throws<ArgumentNullException>(() => storage.RemoveObjectByRemoteObjectId(null));
            Assert.Throws<ArgumentException>(() => storage.RemoveObjectByRemoteObjectId(string.Empty));
            Assert.DoesNotThrow(() => storage.RemoveObjectByRemoteObjectId("RemoteObjectId"));
        }

        [Test, Category("Fast"), Category("FileTransmissionStorage")]
        public void RemoveObjectOnMultipleTimes()
        {
            var storage = new FileTransmissionStorage(Engine);

            for (int i = 1; i <= 10; ++i)
            {
                RemoteFile.Setup(m => m.Id).Returns("RemoteObjectId" + i.ToString());
                var obj = new FileTransmissionObject(CmisSync.Lib.Events.FileTransmissionType.UPLOAD_NEW_FILE, LocalFile.Object, RemoteFile.Object);
                Assert.DoesNotThrow(() => storage.SaveObject(obj));
                Assert.That(storage.GetObjectList().Count, Is.EqualTo(i));
                Assert.That(storage.GetObjectList().First(foo => foo.LocalPath == LocalFile.Object.FullName && foo.RemoteObjectId == "RemoteObjectId" + i.ToString()), Is.Not.Null);
            }

            for (int i = 1; i <= 10; ++i)
            {
                Assert.DoesNotThrow(() => storage.RemoveObjectByRemoteObjectId("RemoteObjectId" + i.ToString()));
                Assert.That(storage.GetObjectList().Count, Is.EqualTo(10 - i));
                Assert.Throws<InvalidOperationException>(() => storage.GetObjectList().First(foo => foo.LocalPath == LocalFile.Object.FullName && foo.RemoteObjectId == "RemoteObjectId" + i.ToString()));
            }
        }

        [Test, Category("Fast"), Category("FileTransmissionStorage")]
        public void RemoveObjectOnMultipleTimesForSameRemoteObjectId()
        {
            var storage = new FileTransmissionStorage(Engine);

            RemoteFile.Setup(m => m.Id).Returns("RemoteObjectId");
            var obj = new FileTransmissionObject(CmisSync.Lib.Events.FileTransmissionType.UPLOAD_NEW_FILE, LocalFile.Object, RemoteFile.Object);
            Assert.DoesNotThrow(() => storage.SaveObject(obj));
            Assert.That(storage.GetObjectList().Count, Is.EqualTo(1));

            for (int i = 1; i <= 10; ++i)
            {
                Assert.DoesNotThrow(() => storage.RemoveObjectByRemoteObjectId("RemoteObjectId"));
                Assert.That(storage.GetObjectList().Count, Is.EqualTo(0));
            }
        }

        [Test, Category("Fast"), Category("FileTransmissionStorage")]
        public void ClearObjectList()
        {
            var storage = new FileTransmissionStorage(Engine);

            for (int i = 1; i <= 10; ++i)
            {
                RemoteFile.Setup(m => m.Id).Returns("RemoteObjectId" + i.ToString());
                var obj = new FileTransmissionObject(CmisSync.Lib.Events.FileTransmissionType.UPLOAD_NEW_FILE, LocalFile.Object, RemoteFile.Object);
                Assert.DoesNotThrow(() => storage.SaveObject(obj));
                Assert.That(storage.GetObjectList().Count, Is.EqualTo(i));
                Assert.That(storage.GetObjectList().First(foo => foo.LocalPath == LocalFile.Object.FullName && foo.RemoteObjectId == "RemoteObjectId" + i.ToString()), Is.Not.Null);
            }

            storage.ClearObjectList();

            Assert.That(storage.GetObjectList().Count, Is.EqualTo(0));
        }


        [Test, Category("Fast"), Category("FileTransmissionStorage")]
        public void GetObjectListOnPersistedStorage()
        {
            var conf = new DBreezeConfiguration
            {
                DBreezeDataFolderName = DBreezePath,
                Storage = DBreezeConfiguration.eStorage.DISK
            };

            using (var engine = new DBreezeEngine(conf))
            {
                var storage = new FileTransmissionStorage(engine);
                for (int i = 1; i <= 10; ++i)
                {
                    RemoteFile.Setup(m => m.Id).Returns("RemoteObjectId" + i.ToString());
                    var obj = new FileTransmissionObject(CmisSync.Lib.Events.FileTransmissionType.UPLOAD_NEW_FILE, LocalFile.Object, RemoteFile.Object);
                    Assert.DoesNotThrow(() => storage.SaveObject(obj));
                    Assert.That(storage.GetObjectList().Count, Is.EqualTo(i));
                    Assert.That(storage.GetObjectList().First(foo => foo.LocalPath == LocalFile.Object.FullName && foo.RemoteObjectId == "RemoteObjectId" + i.ToString()), Is.Not.Null);
                }
            }

            using (var engine = new DBreezeEngine(conf))
            {
                var storage = new FileTransmissionStorage(engine);
                for (int i = 1; i <= 10; ++i)
                {
                    Assert.That(storage.GetObjectList().First(foo => foo.LocalPath == LocalFile.Object.FullName && foo.RemoteObjectId == "RemoteObjectId" + i.ToString()), Is.Not.Null);
                }
                Assert.That(storage.GetObjectList().Count, Is.EqualTo(10));
            }
        }
    }
}