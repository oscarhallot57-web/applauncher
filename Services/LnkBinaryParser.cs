using System.IO;
using System.Text;

namespace AppLauncher.Services
{
    internal static class LnkBinaryParser
    {
        private const uint HasLinkTargetIDList = 0x1;
        private const uint HasLinkInfo = 0x2;
        private const uint HasName = 0x4;
        private const uint HasRelativePath = 0x8;
        private const uint HasWorkingDir = 0x10;
        private const uint HasArguments = 0x20;
        private const uint IsUnicode = 0x80;

        public static bool TryParse(string lnkPath, out string targetPath, out string arguments, out string workingDirectory)
        {
            targetPath = string.Empty;
            arguments = string.Empty;
            workingDirectory = string.Empty;

            try
            {
                byte[] data = File.ReadAllBytes(lnkPath);
                if (data.Length < 76) return false;

                using var stream = new MemoryStream(data);
                using var reader = new BinaryReader(stream);

                uint headerSize = reader.ReadUInt32();
                if (headerSize != 0x4C) return false;

                stream.Seek(16, SeekOrigin.Current);
                uint linkFlags = reader.ReadUInt32();
                stream.Seek(76 - 24, SeekOrigin.Current);

                if ((linkFlags & HasLinkTargetIDList) != 0)
                {
                    ushort idListSize = reader.ReadUInt16();
                    stream.Seek(idListSize, SeekOrigin.Current);
                }

                if ((linkFlags & HasLinkInfo) != 0)
                {
                    long linkInfoStart = stream.Position;
                    uint linkInfoSize = reader.ReadUInt32();
                    uint linkInfoHeaderSize = reader.ReadUInt32();
                    uint linkInfoFlags = reader.ReadUInt32();
                    reader.ReadUInt32();
                    uint localBasePathOffset = reader.ReadUInt32();
                    reader.ReadUInt32();
                    reader.ReadUInt32();

                    uint localBasePathOffsetUnicode = 0;
                    if (linkInfoHeaderSize >= 0x24)
                    {
                        localBasePathOffsetUnicode = reader.ReadUInt32();
                        reader.ReadUInt32();
                    }

                    bool hasLocalBasePath = (linkInfoFlags & 0x1) != 0;
                    if (hasLocalBasePath)
                    {
                        if (localBasePathOffsetUnicode != 0)
                            targetPath = ReadNullTerminatedUnicode(data, (int)(linkInfoStart + localBasePathOffsetUnicode));
                        else if (localBasePathOffset != 0)
                            targetPath = ReadNullTerminatedAnsi(data, (int)(linkInfoStart + localBasePathOffset));
                    }

                    stream.Position = linkInfoStart + linkInfoSize;
                }

                bool unicode = (linkFlags & IsUnicode) != 0;

                if ((linkFlags & HasName) != 0) SkipStringData(reader, unicode);
                if ((linkFlags & HasRelativePath) != 0) SkipStringData(reader, unicode);
                if ((linkFlags & HasWorkingDir) != 0) workingDirectory = ReadStringData(reader, unicode);
                if ((linkFlags & HasArguments) != 0) arguments = ReadStringData(reader, unicode);

                return !string.IsNullOrWhiteSpace(targetPath);
            }
            catch
            {
                return false;
            }
        }

        private static string ReadStringData(BinaryReader reader, bool unicode)
        {
            ushort count = reader.ReadUInt16();
            if (count == 0) return string.Empty;

            return unicode
                ? Encoding.Unicode.GetString(reader.ReadBytes(count * 2))
                : Encoding.Default.GetString(reader.ReadBytes(count));
        }

        private static void SkipStringData(BinaryReader reader, bool unicode)
        {
            ushort count = reader.ReadUInt16();
            reader.BaseStream.Seek(unicode ? count * 2 : count, SeekOrigin.Current);
        }

        private static string ReadNullTerminatedAnsi(byte[] data, int offset)
        {
            int end = offset;
            while (end < data.Length && data[end] != 0) end++;
            return Encoding.Default.GetString(data, offset, end - offset);
        }

        private static string ReadNullTerminatedUnicode(byte[] data, int offset)
        {
            int end = offset;
            while (end + 1 < data.Length && !(data[end] == 0 && data[end + 1] == 0)) end += 2;
            return Encoding.Unicode.GetString(data, offset, end - offset);
        }
    }
}
