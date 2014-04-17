using System;
using System.IO;

using DotCMIS.Client;

using CmisSync.Lib.Events;
using CmisSync.Lib.Storage;
using CmisSync.Lib.Data;

namespace CmisSync.Lib.Sync.Solver
{
    public class RemoteObjectDeleted : ISolver
    {
        public virtual void Solve(ISession session, IMetaDataStorage storage, IFileSystemInfo localFileInfo, IObjectId remoteId){
            if(localFileInfo is IDirectoryInfo){
                var localFolder = localFileInfo as IDirectoryInfo;
                localFolder.Delete(true);
            }
            storage.RemoveObject(storage.GetObjectByLocalPath(localFileInfo));
        }
    }
}

