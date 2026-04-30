# Luna Multiplayer Server Management Guide

## Server Location
- **Install dir**: `/home/alex/Documents/ksp_server/LunaMultiplayer/`
- **Build output**: `/home/alex/Documents/ksp_server/lmp_server_out/`
- **Config dir**: `/home/alex/Documents/ksp_server/lmp_server_out/Config/`
- **Systemd service**: `/etc/systemd/system/lmp-server.service`

---

## Quick Commands

### Start the server
```bash
sudo systemctl start lmp-server
```

### Stop the server
```bash
sudo systemctl stop lmp-server
```

### Restart the server
```bash
sudo systemctl restart lmp-server
```

### Check server status
```bash
sudo systemctl status lmp-server
```

### View live logs
```bash
sudo journalctl -u lmp-server -f
```

### View last 50 lines of logs
```bash
sudo journalctl -u lmp-server -n 50
```

### Enable auto-start on reboot (already done)
```bash
sudo systemctl enable lmp-server
```

### Disable auto-start on reboot
```bash
sudo systemctl disable lmp-server
```

---

## Configuration

Server settings are stored in XML files in `/home/alex/Documents/ksp_server/lmp_server_out/Config/`:

### Main settings: `GeneralSettings.xml`
- **ServerName**: Name shown in server list
- **Description**: 200-char max description
- **Password**: Leave empty for public server (currently empty)
- **AdminPassword**: For in-game server admin commands
- **MaxPlayers**: Max concurrent players (default: 20)
- **GameMode**: Sandbox/Career/Science
- **Cheats**: Enable/disable cheats
- **AutoDekessler**: Set to `0` to avoid automatic cleanup removing debris/satellites

### Connection settings: `ConnectionSettings.xml`
- **Port**: UDP port (default: 8800) ← already configured
- **ListenAddress**: IPv6 address to bind to
- **Upnp**: Enable UPnP port forwarding (tries automatically)

### Interval settings: `IntervalSettings.xml`
- **BackupIntervalMs**: How often vessel/scenario data is flushed to disk (set to `5000`)

**To change settings**:
1. Edit the XML file
2. Restart the server: `sudo systemctl restart lmp-server`

---

## Vessel Persistence Checklist

If vessels/satellites disappear after reconnect:

1. Ensure `AutoDekessler` is `0` in `GeneralSettings.xml`
2. Ensure `BackupIntervalMs` is low enough (`5000` recommended) in `IntervalSettings.xml`
3. Wait 10-15 seconds after launching/changing vessel before leaving the game
4. Return to Space Center/Tracking Station before exiting game
5. Confirm vessel files exist: `ls /home/alex/Documents/ksp_server/lmp_server_out/Universe/Vessels`
6. Check for remove events: `journalctl -u lmp-server --no-pager | grep "Removing vessel"`

Note: If `Universe/Vessels` is empty, previously removed vessels cannot be reconstructed automatically.

---

## Troubleshooting

### Server won't start
```bash
sudo journalctl -u lmp-server -n 20
```
Check the output for errors. Common issues:
- Port 8800 already in use: `sudo ss -ulnp | grep 8800`
- Permissions: Make sure `/home/alex/Documents/ksp_server/lmp_server_out/` is readable by user `alex`

### Port not accessible
Check if it's listening:
```bash
sudo ss -ulnp | grep 8800
```

Should show something like:
```
UNCONN 0 0 *:8800 *:* users:(("dotnet",pid=XXXXX,fd=XXX))
```

### Logs are too large
Clear old logs:
```bash
sudo journalctl --vacuum=10d
```

---

## Server Details

- **Runtime**: .NET 10 
- **Framework**: Lidgren UDP networking
- **Port**: UDP 8800
- **Status**: Running as systemd service (auto-restarts on crash)
- **Auto-restart delay**: 10 seconds if it crashes
- **Playit tunnel**: Check https://playit.gg/account/tunnels for public address

---

## Building from Source (if needed)

Rebuild the server binary:
```bash
export PATH="$HOME/.dotnet:$PATH"
cd /home/alex/Documents/ksp_server/LunaMultiplayer
dotnet publish Server/Server.csproj -c Release -o /home/alex/Documents/ksp_server/lmp_server_out --self-contained false
```

Then restart systemd service:
```bash
sudo systemctl restart lmp-server
```

---

## Version Info

- **Last build date**: April 10, 2026
- **.NET version**: 10.0.201
- **Server version**: Check `LunaMultiplayer.version` file in repo

---

## Support & Documentation

- **GitHub**: https://github.com/LunaMultiplayer/LunaMultiplayer
- **Wiki**: https://github.com/LunaMultiplayer/LunaMultiplayer/wiki
- **Discord**: https://discord.gg/wKVMhWQ
