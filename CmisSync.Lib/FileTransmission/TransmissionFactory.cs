//-----------------------------------------------------------------------
// <copyright file="TransmissionFactory.cs" company="GRAU DATA AG">
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
﻿
namespace CmisSync.Lib.FileTransmission {
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;

    using CmisSync.Lib.Cmis;

    public class TransmissionFactory : ITransmissionFactory{
        private AbstractNotifyingRepository repository;
        private ITransmissionAggregator aggregator;
        private object collectionLock = new object();
        private HashSet<Transmission> transmissions = new HashSet<Transmission>();

        public TransmissionFactory(AbstractNotifyingRepository repo, ITransmissionAggregator aggregator) {
            if (repo == null) {
                throw new ArgumentNullException("repo");
            }

            if (aggregator == null) {
                throw new ArgumentNullException("aggregator");
            }

            this.repository = repo;
            this.aggregator = aggregator;
            this.repository.PropertyChanged += (sender, e) => {
                if (e.PropertyName == Utils.NameOf(() => this.repository.DownloadLimit)) {
                    lock (this.collectionLock) {
                        foreach (var transmission in this.transmissions) {
                            if (transmission.Type == TransmissionType.DOWNLOAD_NEW_FILE || transmission.Type == TransmissionType.DOWNLOAD_MODIFIED_FILE) {
                                transmission.MaxBandwidth = this.repository.DownloadLimit;
                            }
                        }
                    }
                } else if (e.PropertyName == Utils.NameOf(() => this.repository.UploadLimit)) {
                    lock (this.collectionLock) {
                        foreach (var transmission in this.transmissions) {
                            if (transmission.Type == TransmissionType.UPLOAD_NEW_FILE || transmission.Type == TransmissionType.UPLOAD_MODIFIED_FILE) {
                                transmission.MaxBandwidth = this.repository.UploadLimit;
                            }
                        }
                    }
                }
            };
        }

        public Transmission CreateTransmission(TransmissionType type, string path, string cachePath = null) {
            var transmission = new Transmission(type, path, cachePath) {
                Repository = this.repository.Name
            };
            switch (type) {
            case TransmissionType.DOWNLOAD_NEW_FILE:
                goto case TransmissionType.DOWNLOAD_MODIFIED_FILE;
            case TransmissionType.DOWNLOAD_MODIFIED_FILE:
                transmission.MaxBandwidth = this.repository.DownloadLimit;
                break;
            case TransmissionType.UPLOAD_NEW_FILE:
                goto case TransmissionType.UPLOAD_MODIFIED_FILE;
            case TransmissionType.UPLOAD_MODIFIED_FILE:
                transmission.MaxBandwidth = this.repository.UploadLimit;
                break;
            }

            lock (this.collectionLock) {
                this.transmissions.Add(transmission);
            }

            transmission.PropertyChanged += this.TransmissionFinished;
            this.aggregator.Add(transmission);
            return transmission;
        }

        private void TransmissionFinished(object sender, PropertyChangedEventArgs e) {
            var t = sender as Transmission;
            if (e.PropertyName == Utils.NameOf(() => t.Status)) {
                if (t.Status == TransmissionStatus.ABORTED || t.Status == TransmissionStatus.FINISHED) {
                    lock (this.collectionLock) {
                        this.transmissions.Remove(t);
                        t.PropertyChanged -= this.TransmissionFinished;
                    }
                }
            }
        }
    }
}