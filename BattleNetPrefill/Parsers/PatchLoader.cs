﻿namespace BattleNetPrefill.Parsers
{
    public class PatchLoader
    {
        private readonly CdnRequestManager _cdnRequestManager;

        public PatchLoader(CdnRequestManager cdnRequestManager)
        {
            _cdnRequestManager = cdnRequestManager;
        }

        public async Task HandlePatchesAsync(BuildConfigFile buildConfig, TactProduct targetProduct, CDNConfigFile cdnConfig)
        {
            // For whatever reason, these products do not actually make this request
            var patchConfigExclusions = new List<TactProduct>
            {
                TactProduct.CodVanguard, TactProduct.CodWarzone, TactProduct.CodBOCW, TactProduct.Hearthstone,
                TactProduct.BlizzardArcadeCollection
            };
            if (buildConfig.patchConfig != null && !patchConfigExclusions.Contains(targetProduct))
            {
                _cdnRequestManager.QueueRequest(RootFolder.config, buildConfig.patchConfig.Value);
            }

            if (buildConfig.patch != null)
            {
                await GetPatchFileAsync(buildConfig.patch.Value);
            }

            // Unused by Hearthstone
            if (cdnConfig.patchFileIndex != null && targetProduct != TactProduct.Hearthstone && targetProduct != TactProduct.BlizzardArcadeCollection)
            {
                _cdnRequestManager.QueueRequest(RootFolder.patch, cdnConfig.patchFileIndex.Value, 0, cdnConfig.patchFileIndexSize - 1, isIndex: true);
            }

            if (buildConfig.patchIndex != null)
            {
                var upperByteRange = Math.Max(4095, buildConfig.patchIndexSize[1] - 1);
                _cdnRequestManager.QueueRequest(RootFolder.data, buildConfig.patchIndex[1], 0, upperByteRange);
            }

            // Unused by Hearthstone
            if (cdnConfig.patchArchives != null && targetProduct != TactProduct.Hearthstone && targetProduct != TactProduct.BlizzardArcadeCollection
                && targetProduct != TactProduct.Overwatch2)
            {
                for (var i = 0; i < cdnConfig.patchArchives.Length; i++)
                {
                    var patchIndex = cdnConfig.patchArchives[i];
                    _cdnRequestManager.QueueRequest(RootFolder.patch, patchIndex, 0, cdnConfig.patchArchivesIndexSize[i] - 1, isIndex: true);
                }
            }
        }

        private async Task<PatchFile> GetPatchFileAsync(MD5Hash hash)
        {
            var patchFile = new PatchFile();

            byte[] content = await _cdnRequestManager.GetRequestAsBytesAsync(RootFolder.patch, hash);

            using (BinaryReader bin = new BinaryReader(new MemoryStream(content)))
            {
                if (Encoding.UTF8.GetString(bin.ReadBytes(2)) != "PA") { throw new Exception("Error while parsing patch file!"); }

                patchFile.version = bin.ReadByte();
                patchFile.fileKeySize = bin.ReadByte();
                patchFile.sizeB = bin.ReadByte();
                patchFile.patchKeySize = bin.ReadByte();
                patchFile.blockSizeBits = bin.ReadByte();
                patchFile.blockCount = bin.ReadUInt16BigEndian();
                patchFile.flags = bin.ReadByte();
                patchFile.encodingContentKey = bin.ReadBytes(16);
                patchFile.encodingEncodingKey = bin.ReadBytes(16);
                patchFile.decodedSize = bin.ReadUInt32BigEndian();
                patchFile.encodedSize = bin.ReadUInt32BigEndian();
                patchFile.especLength = bin.ReadByte();
                patchFile.encodingSpec = new string(bin.ReadChars(patchFile.especLength));

                patchFile.blocks = new PatchBlock[patchFile.blockCount];
                for (var i = 0; i < patchFile.blockCount; i++)
                {
                    patchFile.blocks[i].lastFileContentKey = bin.ReadBytes(patchFile.fileKeySize);
                    patchFile.blocks[i].blockMD5 = bin.ReadBytes(16);
                    patchFile.blocks[i].blockOffset = bin.ReadUInt32BigEndian();

                    var prevPos = bin.BaseStream.Position;

                    var files = new List<BlockFile>();

                    bin.BaseStream.Position = patchFile.blocks[i].blockOffset;
                    while (bin.BaseStream.Position <= patchFile.blocks[i].blockOffset + 0x10000)
                    {
                        var file = new BlockFile();

                        file.numPatches = bin.ReadByte();
                        if (file.numPatches == 0) break;
                        file.targetFileContentKey = bin.ReadBytes(patchFile.fileKeySize);
                        file.decodedSize = bin.ReadUInt40BigEndian();

                        var filePatches = new List<FilePatch>();

                        for (var j = 0; j < file.numPatches; j++)
                        {
                            var filePatch = new FilePatch();
                            filePatch.sourceFileEncodingKey = bin.ReadBytes(patchFile.fileKeySize);
                            filePatch.decodedSize = bin.ReadUInt40BigEndian();
                            filePatch.patchEncodingKey = bin.ReadBytes(patchFile.patchKeySize);
                            filePatch.patchSize = bin.ReadUInt32BigEndian();
                            filePatch.patchIndex = bin.ReadByte();
                            filePatches.Add(filePatch);
                        }

                        file.patches = filePatches.ToArray();

                        files.Add(file);
                    }

                    patchFile.blocks[i].files = files.ToArray();
                    bin.BaseStream.Position = prevPos;
                }
            }

            return patchFile;
        }
    }
}
