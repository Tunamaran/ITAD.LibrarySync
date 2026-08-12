namespace ITAD.LibrarySync.Core.Launchers.Ubisoft;

internal static class UbisoftBinaryParser
{
    internal sealed record ConfigurationRecord(
        int InstallId,
        int LaunchId,
        int YamlOffset,
        int YamlSize);

    internal static int ConvertData(int data)
    {
        if (data > 256 * 256)
        {
            data -= (int)(128 * 256 * Math.Ceiling(data / (256.0 * 256)));
            data -= (int)(128 * Math.Ceiling(data / 256.0));
        }
        else if (data > 256)
        {
            data -= (int)(128 * Math.Ceiling(data / 256.0));
        }

        return data;
    }

    internal static IReadOnlyList<ConfigurationRecord> ParseConfigurationRecords(byte[] content)
    {
        var records = new Dictionary<int, ConfigurationRecord>();
        var globalOffset = 0;

        try
        {
            while (globalOffset < content.Length)
            {
                var data = content.AsSpan(globalOffset);
                var (objectSize, installId, launchId, headerSize) = ParseConfigurationHeader(data);

                launchId = launchId == 0 || launchId == installId ? installId : launchId;

                if (objectSize > 500)
                {
                    records[installId] = new ConfigurationRecord(
                        installId,
                        launchId,
                        globalOffset + headerSize,
                        objectSize);
                }

                var previousOffset = globalOffset;
                globalOffset += objectSize + headerSize;

                if (globalOffset < content.Length && content[globalOffset] != 0x0A)
                {
                    (objectSize, _, _, headerSize) = ParseConfigurationHeader(data, secondEight: true);
                    globalOffset = previousOffset + objectSize + headerSize;
                }
            }
        }
        catch
        {
            // Corrupted cache — return whatever was parsed.
        }

        return records.Values.ToList();
    }

    internal static HashSet<int> ParseOwnedIds(byte[] content)
    {
        var owned = new HashSet<int>();
        if (content.Length <= 0x108)
            return owned;

        var globalOffset = 0x108;

        try
        {
            while (globalOffset < content.Length)
            {
                var data = content.AsSpan(globalOffset);
                var (launchId, launchId2, recordSize) = ParseOwnershipHeader(data);
                if (launchId is null || recordSize is null)
                    break;

                owned.Add(launchId.Value);
                if (launchId2.HasValue && launchId2 != launchId)
                    owned.Add(launchId2.Value);

                globalOffset += recordSize.Value;
            }
        }
        catch
        {
            // Corrupted cache — return whatever was parsed.
        }

        return owned;
    }

    private static (int ObjectSize, int InstallId, int LaunchId, int HeaderSize) ParseConfigurationHeader(
        ReadOnlySpan<byte> header,
        bool secondEight = false)
    {
        try
        {
            if (header.Length < 4)
                return (0, 0, 0, 10);

            var offset = 1;
            var multiplier = 1;
            var recordSize = 0;
            var tmpSize = 0;

            if (secondEight)
            {
                while (offset < header.Length &&
                       (header[offset] != 0x08 || (offset + 1 < header.Length && header[offset + 1] == 0x08)))
                {
                    recordSize += header[offset] * multiplier;
                    multiplier *= 256;
                    offset++;
                    tmpSize++;
                }
            }
            else
            {
                while (offset < header.Length && (header[offset] != 0x08 || recordSize == 0))
                {
                    recordSize += header[offset] * multiplier;
                    multiplier *= 256;
                    offset++;
                    tmpSize++;
                }
            }

            recordSize = ConvertData(recordSize);
            offset++; // skip 0x08

            multiplier = 1;
            var launchId = 0;
            while (offset < header.Length &&
                   (header[offset] != 0x10 || (offset + 1 < header.Length && header[offset + 1] == 0x10)))
            {
                launchId += header[offset] * multiplier;
                multiplier *= 256;
                offset++;
            }

            launchId = ConvertData(launchId);
            offset++; // skip 0x10

            multiplier = 1;
            var launchId2 = 0;
            while (offset < header.Length &&
                   (header[offset] != 0x1A || (offset + 1 < header.Length && header[offset + 1] == 0x1A)))
            {
                launchId2 += header[offset] * multiplier;
                multiplier *= 256;
                offset++;
            }

            launchId2 = ConvertData(launchId2);

            if (recordSize - offset < 128 && recordSize >= 128)
            {
                tmpSize--;
                recordSize++;
            }

            return (recordSize - offset, launchId, launchId2, offset + tmpSize + 1);
        }
        catch
        {
            return (0, 0, 0, 10);
        }
    }

    private static (int? LaunchId, int? LaunchId2, int? RecordSize) ParseOwnershipHeader(ReadOnlySpan<byte> header)
    {
        try
        {
            if (header.Length < 2 || header[0] != 0x0A)
                return (null, null, null);

            var offset = 1;
            var multiplier = 1;
            var recordSize = 0;
            var tmpSize = 0;

            while (offset < header.Length && (header[offset] != 0x08 || recordSize == 0))
            {
                recordSize += header[offset] * multiplier;
                multiplier *= 256;
                offset++;
                tmpSize++;
            }

            recordSize = ConvertData(recordSize);
            offset++; // skip 0x08

            multiplier = 1;
            var launchId = 0;
            while (offset < header.Length &&
                   (header[offset] != 0x10 || (offset + 1 < header.Length && header[offset + 1] == 0x10)))
            {
                launchId += header[offset] * multiplier;
                multiplier *= 256;
                offset++;
            }

            launchId = ConvertData(launchId);
            offset++; // skip 0x10

            multiplier = 1;
            var launchId2 = 0;
            while (offset < header.Length && header[offset] != 0x22)
            {
                launchId2 += header[offset] * multiplier;
                multiplier *= 256;
                offset++;
            }

            launchId2 = ConvertData(launchId2);
            return (launchId, launchId2, recordSize + tmpSize + 1);
        }
        catch
        {
            return (null, null, null);
        }
    }
}
