using LmpCommon.Message.Data.Vessel;
using Server.Log;
using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace Server.System.Vessel
{
    /// <summary>
    /// When the client never sends a Proto message (common with certain client versions or tunnel setups),
    /// this creates a minimal vessel definition from position data so the vessel can be tracked and persisted.
    /// </summary>
    public partial class VesselDataUpdater
    {
        private static readonly ConcurrentDictionary<Guid, bool> VesselStubsCreated = new ConcurrentDictionary<Guid, bool>();

        /// <summary>
        /// Creates a minimal vessel in CurrentVessels from position data if the vessel doesn't already exist.
        /// This allows subsequent Position/Update/Flightstate messages to patch it and BackupSystem to persist it.
        /// </summary>
        public static void CreateVesselFromPositionIfNeeded(Guid vesselId, VesselPositionMsgData posData)
        {
            if (VesselStubsCreated.ContainsKey(vesselId)) return;
            if (VesselStoreSystem.VesselExists(vesselId)) return;
            if (VesselContext.RemovedVessels.Contains(vesselId)) return;

            // Mark immediately to prevent race condition
            if (!VesselStubsCreated.TryAdd(vesselId, true)) return;

            LunaLog.Normal($"Creating vessel stub for {vesselId} from position data (no Proto received).");

            var pidNoDashes = vesselId.ToString("N");
            var lat = posData.LatLonAlt[0].ToString(CultureInfo.InvariantCulture);
            var lon = posData.LatLonAlt[1].ToString(CultureInfo.InvariantCulture);
            var alt = posData.LatLonAlt[2].ToString(CultureInfo.InvariantCulture);
            var hgt = posData.HeightFromTerrain.ToString(CultureInfo.InvariantCulture);
            var nrm = $"{posData.NormalVector[0].ToString(CultureInfo.InvariantCulture)},{posData.NormalVector[1].ToString(CultureInfo.InvariantCulture)},{posData.NormalVector[2].ToString(CultureInfo.InvariantCulture)}";
            var rot = $"{posData.SrfRelRotation[0].ToString(CultureInfo.InvariantCulture)},{posData.SrfRelRotation[1].ToString(CultureInfo.InvariantCulture)},{posData.SrfRelRotation[2].ToString(CultureInfo.InvariantCulture)},{posData.SrfRelRotation[3].ToString(CultureInfo.InvariantCulture)}";

            var sit = posData.Landed ? "LANDED" : (posData.Splashed ? "SPLASHED" : "ORBITING");
            var body = posData.BodyName ?? "Kerbin";

            var configNode = $@"pid = {pidNoDashes}
name = Unknown Vessel
type = Ship
sit = {sit}
landed = {posData.Landed}
splashed = {posData.Splashed}
met = 0
lct = 0
lastUT = 0
root = 0
lat = {lat}
lon = {lon}
alt = {alt}
hgt = {hgt}
nrm = {nrm}
rot = {rot}
CoM = 0,0,0
stg = 0
prst = True
ref = 0
ctrl = True
distanceTraveled = 0
ORBIT
{{
	SMA = {posData.Orbit[2].ToString(CultureInfo.InvariantCulture)}
	ECC = {posData.Orbit[1].ToString(CultureInfo.InvariantCulture)}
	INC = {posData.Orbit[0].ToString(CultureInfo.InvariantCulture)}
	LPE = {posData.Orbit[4].ToString(CultureInfo.InvariantCulture)}
	LAN = {posData.Orbit[3].ToString(CultureInfo.InvariantCulture)}
	MNA = {posData.Orbit[5].ToString(CultureInfo.InvariantCulture)}
	EPH = {posData.Orbit[6].ToString(CultureInfo.InvariantCulture)}
	REF = {posData.Orbit[7].ToString(CultureInfo.InvariantCulture)}
	body = {body}
}}
PART
{{
	name = sensorBarometer
	cid = 0
	uid = 1
	mid = 0
	parent = 0
	position = 0,0,0
	rotation = 0,0,0,1
	mirror = 1,1,1
	istg = 0
	dstg = 0
	sqor = 0
	sidx = 0
	attm = 0
	mass = 0.8
	temp = 300
	expt = 0.5
	EVENTS
	{{
	}}
	ACTIONS
	{{
	}}
}}
ACTIONGROUPS
{{
	Stage = False, 0
	Gear = False, 0
	Light = False, 0
	RCS = False, 0
	SAS = False, 0
	Brakes = False, 0
	Abort = False, 0
}}
DISCOVERY
{{
	state = -1
	lastObservedTime = 0
	lifetime = Infinity
	refTime = Infinity
	size = 2
}}
FLIGHTPLAN
{{
}}
CTRLSTATE
{{
	pitch = 0
	yaw = 0
	roll = 0
	trimPitch = 0
	trimYaw = 0
	trimRoll = 0
	mainThrottle = 0
}}
VESSELMODULES
{{
}}";

            try
            {
                var vessel = new Classes.Vessel(configNode);
                lock (Semaphore.GetOrAdd(vesselId, new object()))
                {
                    VesselStoreSystem.CurrentVessels.AddOrUpdate(vesselId, vessel, (key, existingVal) => vessel);
                }
                LunaLog.Normal($"Vessel stub for {vesselId} created successfully.");
            }
            catch (Exception e)
            {
                LunaLog.Error($"Failed to create vessel stub for {vesselId}: {e}");
                VesselStubsCreated.TryRemove(vesselId, out _);
            }
        }
    }
}
