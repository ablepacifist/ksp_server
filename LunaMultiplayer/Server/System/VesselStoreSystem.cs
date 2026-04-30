using LunaConfigNode;
using Server.Context;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Server.System
{
    /// <summary>
    /// Here we keep a copy of all the player vessels in <see cref="Vessel"/> format and we also save them to files at a specified rate
    /// </summary>
    public static class VesselStoreSystem
    {
        public const string VesselFileFormat = ".txt";
        public static string VesselsPath = Path.Combine(ServerContext.UniverseDirectory, "Vessels");

        public static ConcurrentDictionary<Guid, Vessel.Classes.Vessel> CurrentVessels = new ConcurrentDictionary<Guid, Vessel.Classes.Vessel>();

        /// <summary>
        /// Vessels uploaded via HTTP have their raw data written directly to disk.
        /// The backup system must not overwrite these files with the lossy Vessel.ToString() output.
        /// </summary>
        public static ConcurrentDictionary<Guid, byte> HttpUploadedVessels = new ConcurrentDictionary<Guid, byte>();

        private static readonly object BackupLock = new object();

        public static bool VesselExists(Guid vesselId) => CurrentVessels.ContainsKey(vesselId) || HttpUploadedVessels.ContainsKey(vesselId);

        /// <summary>
        /// Removes a vessel from the store
        /// </summary>
        public static void RemoveVessel(Guid vesselId)
        {
            CurrentVessels.TryRemove(vesselId, out _);
            HttpUploadedVessels.TryRemove(vesselId, out _);

            _ = Task.Run(() =>
            {
                lock (BackupLock)
                {
                    FileHandler.FileDelete(Path.Combine(VesselsPath, $"{vesselId}{VesselFileFormat}"));
                }
            });
        }

        /// <summary>
        /// Returns a vessel in the standard KSP format
        /// </summary>
        public static string GetVesselInConfigNodeFormat(Guid vesselId)
        {
            // For HTTP-uploaded vessels, read the raw file from disk to preserve all data
            if (HttpUploadedVessels.ContainsKey(vesselId))
            {
                var filePath = Path.Combine(VesselsPath, $"{vesselId}{VesselFileFormat}");
                if (File.Exists(filePath))
                    return FileHandler.ReadFileText(filePath);
            }

            return CurrentVessels.TryGetValue(vesselId, out var vessel) ?
                vessel.ToString() : null;
        }

        /// <summary>
        /// Load the stored vessels into the dictionary
        /// </summary>
        public static void LoadExistingVessels()
        {
            ChangeExistingVesselFormats();
            lock (BackupLock)
            {
                foreach (var file in Directory.GetFiles(VesselsPath).Where(f => Path.GetExtension(f) == VesselFileFormat))
                {
                    if (Guid.TryParse(Path.GetFileNameWithoutExtension(file), out var vesselId))
                    {
                        try
                        {
                            CurrentVessels.TryAdd(vesselId, new Vessel.Classes.Vessel(FileHandler.ReadFileText(file)));
                        }
                        catch
                        {
                            // Vessel file can't be parsed (e.g. debris missing sections).
                            // Mark as HTTP-uploaded so it's served raw from disk instead.
                            HttpUploadedVessels.TryAdd(vesselId, 0);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Transform OLD Xml vessels into the new format
        /// TODO: Remove this for next version
        /// </summary>
        public static void ChangeExistingVesselFormats()
        {
            lock (BackupLock)
            {
                foreach (var file in Directory.GetFiles(VesselsPath).Where(f => Path.GetExtension(f) == ".xml"))
                {
                    if (Guid.TryParse(Path.GetFileNameWithoutExtension(file), out var vesselId))
                    {
                        var vesselAsCfgNode = XmlConverter.ConvertToConfigNode(FileHandler.ReadFileText(file));
                        FileHandler.WriteToFile(file.Replace(".xml", ".txt"), vesselAsCfgNode);
                    }
                    FileHandler.FileDelete(file);
                }
            }
        }

        /// <summary>
        /// Actually performs the backup of the vessels to file
        /// </summary>
        public static void BackupVessels()
        {
            lock (BackupLock)
            {
                var vesselsInCfgNode = CurrentVessels.ToArray();
                foreach (var vessel in vesselsInCfgNode)
                {
                    // Skip vessels uploaded via HTTP - their raw data is already on disk
                    // and Vessel.ToString() would strip modules/resources/crew data
                    if (HttpUploadedVessels.ContainsKey(vessel.Key))
                        continue;

                    FileHandler.WriteToFile(Path.Combine(VesselsPath, $"{vessel.Key}{VesselFileFormat}"), vessel.Value.ToString());
                }
            }
        }
    }
}
