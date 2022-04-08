﻿using System;
using System.Collections;
using System.IO;
using System.Text;
using BuildBackup.Structs;

namespace BuildBackup.DataAccess
{
    public static class DownloadFileHandler
    {
        public static DownloadFile ParseDownloadFile(CDN cdn, string hash)
        {
            var download = new DownloadFile();

            byte[] content = cdn.Get(RootFolder.data, hash);

            using (BinaryReader bin = new BinaryReader(new MemoryStream(BLTE.Parse(content))))
            {
                if (Encoding.UTF8.GetString(bin.ReadBytes(2)) != "DL")
                {
                    throw new Exception("Error while parsing download file. Did BLTE header size change?");
                }
                byte version = bin.ReadBytes(1)[0];
                byte hash_size_ekey = bin.ReadBytes(1)[0];
                byte has_checksum_in_entry = bin.ReadBytes(1)[0];
                download.numEntries = bin.ReadUInt32(true);
                download.numTags = bin.ReadUInt16(true);

                int numMaskBytes = (int)((download.numEntries + 7) / 8);

                //TODO implement
                //uint32 entryExtra = 6;
                //if (has_checksum_in_entry)
                //{
                //    entryExtra += 4;
                //}
                //if (version >= 2)
                //{
                //    uint8 number_of_flag_bytes = file.read8();
                //    entryExtra += number_of_flag_bytes;
                //    if (version >= 3)
                //    {
                //        file.seek(4, SEEK_CUR);
                //    }
                //}

                // Reading the download entries
                bin.BaseStream.Seek(16, SeekOrigin.Begin);
                download.entries = new DownloadEntry[download.numEntries];
                for (int i = 0; i < download.numEntries; i++)
                {
                    download.entries[i].hash = bin.Read<MD5Hash>();
                    bin.ReadBytes(10);
                }

                // Reading the tags
                download.tags = new DownloadTag[download.numTags];
                for (int i = 0; i < download.numTags; i++)
                {
                    DownloadTag tag = new DownloadTag();
                    tag.Name = bin.ReadCString();
                    tag.Type = bin.ReadInt16BE();

                    byte[] bits = bin.ReadBytes(numMaskBytes);

                    for (int j = 0; j < numMaskBytes; j++)
                        bits[j] = (byte)((bits[j] * 0x0202020202 & 0x010884422010) % 1023);

                    tag.Bits = new BitArray(bits);

                    download.tags[i] = tag;
                }
            }

            return download;
        }
    }
}
