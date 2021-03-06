//-----------------------------------------------------------------------
// <copyright file="MockOfIFileTransmissionStorageUtil.cs" company="GRAU DATA AG">
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

namespace TestLibrary.TestUtils {
    using System;
    using System.Collections.Generic;

    using CmisSync.Lib.Storage.Database;
    using CmisSync.Lib.Storage.Database.Entities;
    using CmisSync.Lib.Storage.FileSystem;

    using Moq;

    public static class MockOfIFileTransmissionStorageUtil {
        public static void AddTransmission(this Mock<IFileTransmissionStorage> storage, IFileTransmissionObject obj) {
            var allTransmissions = new List<IFileTransmissionObject>(storage.Object.GetObjectList() ?? new List<IFileTransmissionObject>());
            allTransmissions.Add(obj);
            storage.Setup(s => s.GetObjectByLocalPath(It.Is<string>(path => path == obj.LocalPath))).Returns(obj);
            storage.Setup(s => s.GetObjectByRemoteObjectId(It.Is<string>(id => id == obj.RemoteObjectId))).Returns(obj);
            storage.Setup(s => s.GetObjectList()).Returns(allTransmissions);
        }

        public static void SetUpClearList(this Mock<IFileTransmissionStorage> storage) {
            storage.Setup(s => s.ClearObjectList()).Callback(() => storage.Setup(st => st.GetObjectList()).Returns(new List<IFileTransmissionObject>()));
            storage.Setup(s => s.GetObjectByLocalPath(It.IsAny<string>())).Returns((IFileTransmissionObject)null);
            storage.Setup(s => s.GetObjectByRemoteObjectId(It.IsAny<string>())).Returns((IFileTransmissionObject)null);
            storage.Setup(s => s.GetObjectList()).Returns(new List<IFileTransmissionObject>());
        }

        public static void SetUpChunkSize(this Mock<IFileTransmissionStorage> storage, long chunkSize) {
            storage.Setup(s => s.ChunkSize).Returns(chunkSize);
        }

        public static void VerifyThatNoObjectIsAddedChangedOrDeleted(this Mock<IFileTransmissionStorage> storage) {
            storage.Verify(s => s.ClearObjectList(), Times.Never());
            storage.Verify(s => s.SaveObject(It.IsAny<IFileTransmissionObject>()), Times.Never());
            storage.Verify(s => s.RemoveObjectByRemoteObjectId(It.IsAny<string>()), Times.Never());
        }
    }
}