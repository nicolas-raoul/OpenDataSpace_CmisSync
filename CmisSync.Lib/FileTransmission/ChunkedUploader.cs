﻿//-----------------------------------------------------------------------
// <copyright file="ChunkedUploader.cs" company="GRAU DATA AG">
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

namespace CmisSync.Lib.FileTransmission {
    using System;
    using System.IO;
    using System.Security.Cryptography;

    using CmisSync.Lib.Events;
    using CmisSync.Lib.HashAlgorithm;

    using DataSpace.Common.Streams;
    using DataSpace.Common.Transmissions;

    using DotCMIS.Client;
    using DotCMIS.Data.Impl;

    /// <summary>
    /// Chunked uploader takes a file and splits the upload into chunks.
    /// Resuming a failed upload is possible.
    /// </summary>
    public class ChunkedUploader : SimpleFileUploader {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChunkedUploader"/> class.
        /// </summary>
        /// <param name='chunkSize'>
        /// Chunk size.
        /// </param>
        public ChunkedUploader(long chunkSize = 1024 * 1024) {
            if (chunkSize <= 0) {
                throw new ArgumentException("The chunk size must be a positive number and cannot be zero or less");
            }

            this.ChunkSize = chunkSize;
        }

        /// <summary>
        /// Gets the size of a chunk.
        /// </summary>
        /// <value>The size of the chunk.</value>
        public long ChunkSize { get; private set; }

        /// <summary>
        ///  Uploads the file.
        ///  Resumes an upload if the given localFileStream.Position is larger than zero.
        /// </summary>
        /// <returns>The new CMIS document.</returns>
        /// <param name='remoteDocument'>Remote document where the local content should be uploaded to.</param>
        /// <param name='localFileStream'>Local file stream.</param>
        /// <param name='transmission'>Transmission status where the uploader should report its uploading status.</param>
        /// <param name='hashAlg'>Hash alg which should be used to calculate a checksum over the uploaded content.</param>
        /// <param name='overwrite'>If true, the local content will overwrite the existing content.</param>
        /// <param name="update">Is called on every new chunk, if not <c>null</c>.</param>
        /// <exception cref="UploadFailedException">
        /// Contains the last successful remote document state. This is needed for continue a failed upload.
        /// </exception>
        public override IDocument UploadFile(
            IDocument remoteDocument,
            Stream localFileStream,
            Transmission transmission,
            HashAlgorithm hashAlg,
            bool overwrite = true,
            Action<byte[], long> update = null)
        {
            if (transmission == null) {
                throw new ArgumentNullException("transmission");
            }

            if (localFileStream == null) {
                throw new ArgumentNullException("localFileStream");
            }

            if (remoteDocument == null) {
                throw new ArgumentNullException("remoteDocument");
            }

            var result = remoteDocument;
            for (long offset = localFileStream.Position; offset < localFileStream.Length; offset += this.ChunkSize) {
                bool isFirstChunk = offset == 0;
                bool isLastChunk = (offset + this.ChunkSize) >= localFileStream.Length;
                using (var hashstream = new NonClosingHashStream(localFileStream, hashAlg, CryptoStreamMode.Read))
                using (var chunkstream = new ChunkedStream(hashstream, this.ChunkSize))
                using (var offsetstream = new OffsetStream(chunkstream, offset))
                using (var transmissionStream = transmission.CreateStream(offsetstream)) {
                    transmission.Length = localFileStream.Length;
                    transmission.Position = offset;
                    chunkstream.ChunkPosition = offset;

                    var contentStream = new ContentStream();
                    contentStream.FileName = remoteDocument.Name;
                    contentStream.MimeType = Cmis.MimeType.GetMIMEType(remoteDocument.Name);
                    if (isLastChunk) {
                        contentStream.Length = localFileStream.Length - offset;
                    } else {
                        contentStream.Length = this.ChunkSize;
                    }

                    contentStream.Stream = transmissionStream;
                    try {
                        if (isFirstChunk && result.ContentStreamId != null && overwrite) {
                            result.DeleteContentStream(true);
                        }

                        result.AppendContentStream(contentStream, isLastChunk, true);
                        var reuse = hashAlg as IReusableHashAlgorithm;
                        if (reuse != null && update != null) {
                            using (HashAlgorithm hash = (HashAlgorithm)reuse.Clone()) {
                                hash.TransformFinalBlock(new byte[0], 0, 0);
                                update(hash.Hash, result.ContentStreamLength.GetValueOrDefault());
                            }
                        }
                    } catch (Exception e) {
                        if (e is AbortedException) {
                            throw;
                        }

                        if (e.InnerException is AbortedException) {
                            throw e.InnerException;
                        }

                        throw new UploadFailedException(e, result);
                    }
                }
            }

            hashAlg.TransformFinalBlock(new byte[0], 0, 0);
            return result;
        }
    }
}