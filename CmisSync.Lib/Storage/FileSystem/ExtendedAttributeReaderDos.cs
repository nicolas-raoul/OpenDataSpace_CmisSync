//-----------------------------------------------------------------------
// <copyright file="ExtendedAttributeReaderDos.cs" company="GRAU DATA AG">
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

namespace CmisSync.Lib.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;

    using Microsoft.Win32.SafeHandles;

    public class ExtendedAttributeReaderDos : IExtendedAttributeReader
    {
#if ! __MonoCS__
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(
            string name,
            FILE_ACCESS_RIGHTS access,
            FileShare share,
            IntPtr security,
            FileMode mode,
            FILE_FLAGS flags,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeleteFile(string fileName);

        private enum FILE_ACCESS_RIGHTS : uint
        {
            GENERIC_READ = 0x80000000,
            GENERIC_WRITE = 0x40000000
        }

        private enum FILE_FLAGS : uint
        {
            WriteThrough = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x8000000,
            DeleteOnClose = 0x4000000,
            BackupSemantics = 0x2000000,
            PosixSemantics = 0x1000000,
            OpenReparsePoint = 0x200000,
            OpenNoRecall = 0x100000
        }
#endif

        private static FileStream CreateFileStream(string path, FileAccess access, FileMode mode, FileShare share)
        {
#if ! __MonoCS__
            SafeFileHandle handle = CreateFile(path, access, share, IntPtr.Zero, mode, 0, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                throw new IOException("Could not open file stream.");
            }
            return new FileStream(handle, access);
#else
            throw new WrongPlatformException();
#endif
        }

        public string GetExtendedAttribute(string path, string key)
        {
#if ! __MonoCS__
            if(string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Empty or null key is not allowed");
            }
            FileStream stream = CreateFileStream(string.Format("{0}:{1}", path, key), FILE_ACCESS_RIGHTS.GENERIC_READ, FileShare.Read, FileMode.Open);
            TextReader reader = new StreamReader(stream);

            string result = reader.ReadToEnd();
            reader.Close();
            CloseHandle(fileHandle);

            // int error = Marshal.GetLastWin32Error();
            return result;
#else
            throw new WrongPlatformException();
#endif
        }

        public void SetExtendedAttribute(string path, string key, string value)
        {
#if ! __MonoCS__
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Empty or null key is not allowed");
            }
            FileStream stream = CreateFileStream(string.Format("{0}:{1}", path, key), FILE_ACCESS_RIGHTS.GENERIC_WRITE, FileShare.Write, FileMode.Create);
            TextWriter writer = new StreamWriter(stream);
            writer.Write(value);
            writer.Close();
            CloseHandle(fileHandle);
#else
            throw new WrongPlatformException();
#endif
        }

        public void RemoveExtendedAttribute(string path, string key)
        {
#if ! __MonoCS__
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Empty or null key is not allowed");
            }

            DeleteFile(string.Format("{0}:{1}", path, key));
#else
            throw new WrongPlatformException();
#endif
        }

        public List<string> ListAttributeKeys(string path)
        {
#if ! __MonoCS__
            List<StreamInfo> streams = new List<StreamInfo>(FileStreamSearcher.GetStreams(new FileInfo(path)));
            foreach (StreamInfo stream in streams)
            {
                if (stream.Type == StreamType.AlternateData ||
                        stream.Type == StreamType.Data)
                {
                    yield return stream.Name;
                }
            }
#else
            throw new WrongPlatformException();
#endif
        }

        public bool IsFeatureAvailable(string path)
        {
#if ! __MonoCS__
            string fullPath = new FileInfo(path).FullName;
            DriveInfo info = new DriveInfo(Path.GetPathRoot(fullPath));
            return info.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
#else
            throw new WrongPlatformException();
#endif
        }
    }

#if ! __MonoCS__
    [StructLayout(LayoutKind.Sequential, Pack=1)]
    public struct Win32StreamID
    {
        public StreamType dwStreamId;
        public int dwStreamAttributes;
        public long Size;
        public int dwStreamNameSize;
    }

    public enum StreamType
    {
        Data = 1,
        ExternalData = 2,
        SecurityData = 3,
        AlternateData = 4,
        Link = 5,
        PropertyData = 6,
        ObjectID = 7,
        ReparseData = 8,
        SparseDock = 9
    }

    public struct StreamInfo
    {
        public StreamInfo(string name, StreamType type, long size)
        {
            Name = name;
            Type = type;
            Size = size;
        }
        public readonly string Name;
        public readonly StreamType Type;
        public readonly long Size;
    }

    public class FileStreamSearcher
    {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BackupRead(SafeFileHandle hFile, IntPtr lpBuffer,
            uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead,
            [MarshalAs(UnmanagedType.Bool)] bool bAbort,
            [MarshalAs(UnmanagedType.Bool)] bool bProcessSecurity,
            ref IntPtr lpContext);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BackupSeek(SafeFileHandle hFile,
            uint dwLowBytesToSeek, uint dwHighBytesToSeek,
            out uint lpdwLowByteSeeked, out uint lpdwHighByteSeeked,
            ref IntPtr lpContext);

        public static IEnumerable<StreamInfo> GetStreams(FileInfo file)
        {
            const int bufferSize = 4096;
            using (FileStream fs = file.OpenRead())
            {
                IntPtr context = IntPtr.Zero;
                IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
                try
                {
                    while (true)
                    {
                        uint numRead;
                        if (!BackupRead(fs.SafeFileHandle, buffer, (uint)Marshal.SizeOf(typeof(Win32StreamID)),
                            out numRead, false, true, ref context)) throw new Win32Exception();
                        if (numRead > 0)
                        {
                            Win32StreamID streamID = (Win32StreamID)Marshal.PtrToStructure(buffer, typeof(Win32StreamID));
                            string name = null;
                            if (streamID.dwStreamNameSize > 0)
                            {
                                if (!BackupRead(fs.SafeFileHandle, buffer, (uint)Math.Min(bufferSize, streamID.dwStreamNameSize),
                                    out numRead, false, true, ref context)) throw new Win32Exception();
                                name = Marshal.PtrToStringUni(buffer, (int)numRead / 2);
                            }

                            yield return new StreamInfo(name, streamID.dwStreamId, streamID.Size);

                            if (streamID.Size > 0)
                            {
                                uint lo, hi;
                                BackupSeek(fs.SafeFileHandle, uint.MaxValue, int.MaxValue, out lo, out hi, ref context);
                            }
                        }
                        else break;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                    uint numRead;
                    if (!BackupRead(fs.SafeFileHandle, IntPtr.Zero, 0, out numRead, true, false, ref context)) throw new Win32Exception();
                }
            }
        }
    }
#endif

}
