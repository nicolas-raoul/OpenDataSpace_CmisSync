//-----------------------------------------------------------------------
// <copyright file="IFileConvenienceExtenders.cs" company="GRAU DATA AG">
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

namespace CmisSync.Lib.Storage.Database.Entities
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;

    using CmisSync.Lib.Storage.FileSystem;

    /// <summary>
    /// IFileInfo convenience extenders to check them against IMappedObjects
    /// </summary>
    public static class IFileConvenienceExtenders
    {
        /// <summary>
        /// Determines if file content is changed to the specified obj.
        /// </summary>
        /// <returns><c>true</c> if is the file content is different to the specified obj otherwise, <c>false</c>.</returns>
        /// <param name="file">File instance.</param>
        /// <param name="obj">Object to check the file content against.</param>
        /// <param name="scanOnlyIfModificationDateDiffers">If set to <c>true</c> content scan runs only if the modification date differs to given one.</param>
        public static bool IsContentChangedTo(this IFileInfo file, IMappedObject obj, bool scanOnlyIfModificationDateDiffers = false) {
            if (obj == null) {
                throw new ArgumentNullException("Given obj is null");
            }

            if (obj.LastContentSize < 0) {
                throw new ArgumentOutOfRangeException(string.Format("Given LastContentSize {0} is invalid for files", obj.LastContentSize.ToString()));
            }

            if (!file.Exists) {
                throw new FileNotFoundException(string.Format("File {0} does not exists", file.FullName));
            }

            if (obj.LastChecksum == null) {
                return true;
            }

            if (file.Length == obj.LastContentSize) {

                if (scanOnlyIfModificationDateDiffers && obj.LastLocalWriteTimeUtc == file.LastWriteTimeUtc) {
                    return false;
                } else {
                    using (var f = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                        byte[] fileHash = SHA1Managed.Create().ComputeHash(f);
                        return !fileHash.SequenceEqual(obj.LastChecksum);
                    }
                }
            } else {
                return true;
            }
        }
    }
}