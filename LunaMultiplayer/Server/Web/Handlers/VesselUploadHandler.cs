using Server.Log;
using Server.System;
using Server.System.Vessel;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uhttpsharp;

namespace Server.Web.Handlers
{
    /// <summary>
    /// HTTP handler that accepts vessel data via POST /vesselupload?vesselId=GUID
    /// Used as a workaround when the Lidgren UDP Proto messages are lost through tunnels.
    /// </summary>
    public class VesselUploadHandler : IHttpRequestHandler
    {
        public Task Handle(IHttpContext context, Func<Task> next)
        {
            var uri = context.Request.Uri;
            string path;
            try { path = uri.AbsolutePath.TrimStart('/'); }
            catch { path = uri.OriginalString.Split('?')[0].TrimStart('/'); }

            if (!path.Equals("vesselupload", StringComparison.OrdinalIgnoreCase))
                return next();

            if (context.Request.Method != HttpMethods.Post)
            {
                context.Response = new HttpResponse(HttpResponseCode.MethodNotAllowed, "POST only", false);
                return Task.CompletedTask;
            }

            try
            {
                string vesselIdStr = null;
                context.Request.QueryString?.TryGetByName("vesselId", out vesselIdStr);

                if (string.IsNullOrEmpty(vesselIdStr) || !Guid.TryParse(vesselIdStr, out var vesselId))
                {
                    context.Response = new HttpResponse(HttpResponseCode.BadRequest, "Missing or invalid vesselId parameter", false);
                    return Task.CompletedTask;
                }

                var body = context.Request.Post?.Raw;
                if (body == null || body.Length == 0)
                {
                    context.Response = new HttpResponse(HttpResponseCode.BadRequest, "Empty body", false);
                    return Task.CompletedTask;
                }

                // Try base64 first, fall back to raw text
                string vesselData;
                var bodyStr = Encoding.UTF8.GetString(body).Trim();
                try
                {
                    vesselData = Encoding.UTF8.GetString(Convert.FromBase64String(bodyStr));
                }
                catch (FormatException)
                {
                    vesselData = bodyStr;
                }

                if (!vesselData.Contains("ORBIT") || !vesselData.Contains("PART"))
                {
                    context.Response = new HttpResponse(HttpResponseCode.BadRequest, "Invalid vessel data - missing ORBIT or PART section", false);
                    return Task.CompletedTask;
                }

                // Normalize: strip \r, strip common leading whitespace from KSP save nesting
                vesselData = vesselData.Replace("\r", "");
                var lines = vesselData.Split('\n');
                if (lines.Length > 1)
                {
                    // Detect indent from first non-empty line
                    var indent = "";
                    foreach (var ln in lines)
                    {
                        if (ln.Length > 0 && (ln[0] == '\t' || ln[0] == ' '))
                        {
                            int j = 0;
                            while (j < ln.Length && (ln[j] == '\t' || ln[j] == ' ')) j++;
                            if (j > 0) indent = ln.Substring(0, j);
                            break;
                        }
                    }
                    if (indent.Length > 0)
                    {
                        for (int k = 0; k < lines.Length; k++)
                        {
                            if (lines[k].StartsWith(indent))
                                lines[k] = lines[k].Substring(indent.Length);
                        }
                        vesselData = string.Join("\n", lines);
                    }
                }

                LunaLog.Normal($"HTTP vessel upload: {vesselId} ({body.Length} bytes)");

                // Validate brace balance - reject truncated data
                int openBraces = 0, closeBraces = 0;
                foreach (var ch in vesselData) {
                    if (ch == '{') openBraces++;
                    else if (ch == '}') closeBraces++;
                }
                if (openBraces != closeBraces)
                {
                    // Save rejected data for debugging
                    var debugFile = Path.Combine(VesselStoreSystem.VesselsPath, $"REJECTED_{vesselId}.txt");
                    File.WriteAllText(debugFile, vesselData);
                    var debugLines = vesselData.Split('\n');
                    var last5 = string.Join(" | ", debugLines.Skip(Math.Max(0, debugLines.Length - 5)).Select(l => l.Trim()));
                    LunaLog.Error($"HTTP vessel upload: {vesselId} REJECTED - unbalanced braces ({openBraces} open, {closeBraces} close, {body.Length} bytes). Last: [{last5}]. Saved to {debugFile}");
                    context.Response = new HttpResponse(HttpResponseCode.BadRequest, 
                        $"Truncated vessel data - {openBraces} open braces vs {closeBraces} close braces", false);
                    return Task.CompletedTask;
                }

                // Write raw vessel data directly to disk — this always works regardless
                // of vessel type (debris, ships, etc.) since we skip the Vessel parser
                var vesselFile = Path.Combine(VesselStoreSystem.VesselsPath, $"{vesselId}.txt");
                File.WriteAllText(vesselFile, vesselData);

                // Mark as HTTP-uploaded so the backup system won't overwrite with lossy ToString()
                VesselStoreSystem.HttpUploadedVessels.TryAdd(vesselId, 0);

                // Try to also add to CurrentVessels in memory for immediate availability.
                // If parsing fails (e.g. debris missing ACTIONGROUPS), that's fine —
                // the file is on disk and will be available next server restart.
                try
                {
                    var vessel = new System.Vessel.Classes.Vessel(vesselData);
                    VesselStoreSystem.CurrentVessels.AddOrUpdate(vesselId, vessel, (key, existing) => vessel);
                }
                catch (Exception parseEx)
                {
                    LunaLog.Debug($"Vessel {vesselId} saved to disk but couldn't parse into memory: {parseEx.Message}");
                }

                context.Response = new HttpResponse(HttpResponseCode.Ok, $"Vessel {vesselId} uploaded successfully", false);
            }
            catch (Exception e)
            {
                LunaLog.Error($"HTTP vessel upload error: {e.Message}");
                context.Response = new HttpResponse(HttpResponseCode.InternalServerError, e.Message, false);
            }

            return Task.CompletedTask;
        }
    }
}
