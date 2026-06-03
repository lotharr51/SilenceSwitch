# SilenceSwitch — Manual Test Matrix

Tests that require real hardware, an active Windows session, or running audio drivers, and therefore cannot run in CI. Run before every Store submission and after any change that touches audio, tray, or startup code.

**Legend:** ✅ pass · ❌ fail (add note) · ⚠️ partial (add note) · — skipped

---

## 1. Core device switching

| # | Test | Steps | Pass criteria |
|---|------|-------|---------------|
| 1.1 | Startup switch | Launch with `SwitchOnStartup = true`. | Default output changes to preferred device within 1 s. |
| 1.2 | Silence-triggered switch | Set default to a non-preferred device. Play nothing. Wait for full timeout. | Default output switches to preferred; balloon notification shown. |
| 1.3 | Sound resets timer | Non-preferred device is default; silence has accumulated past ½ timeout (icon is yellow). Play audio briefly. | Timer resets; icon returns to green; no device switch. |
| 1.4 | Manual switch (tray menu) | Right-click → "Switch to Headphones Now". | Default output switches immediately; silence timer resets. |
| 1.5 | Already on preferred device | Preferred device is already default. Play nothing for 2× timeout. | No switch; icon stays green throughout. |

---

## 2. Tray icon colour transitions

| # | Test | Steps | Pass criteria |
|---|------|-------|---------------|
| 2.1 | Green → Yellow | Non-preferred is default; no audio for > 50 % of timeout. | Icon turns yellow. |
| 2.2 | Yellow → Green (audio) | While icon is yellow, start playing audio. | Icon returns to green within one poll interval (≤ 10 s). |
| 2.3 | Yellow → Green (switch) | Let silence run to completion. | Icon returns to green after automatic switch. |
| 2.4 | Green → Red | Unplug or disconnect preferred device while monitoring. | Icon turns red within one poll interval. |
| 2.5 | Red → Green | Reconnect preferred device after 2.4. | Icon returns to green within one poll interval. |

---

## 3. Device availability — USB

| # | Test | Steps | Pass criteria |
|---|------|-------|---------------|
| 3.1 | Unplug preferred mid-poll | Unplug preferred USB audio device while silence timer is running. | App does not crash; icon turns red; tray remains responsive. |
| 3.2 | Replug after unplug | Plug device back in after 3.1. | App recovers; icon turns green; switching resumes normally. |
| 3.3 | Unplug non-preferred device | Unplug any secondary audio device. | App does not crash; monitoring continues unaffected. |

---

## 4. Bluetooth audio

| # | Test | Steps | Pass criteria |
|---|------|-------|---------------|
| 4.1 | BT headphones as preferred | Set BT headphones as preferred. Connect them. Wait for silence switch. | Default output switches to BT headphones. |
| 4.2 | BT disconnect mid-monitoring | Disconnect BT headphones while they are the default. | Icon turns red; app remains stable. |
| 4.3 | BT reconnect | Reconnect BT headphones after 4.2. | Icon returns to green within one poll interval. |
| 4.4 | BT pairing during poll | Pair a new BT audio device while the app is monitoring. | App does not crash; new device appears in Settings dialog device list. |

---

## 5. Session-state detection (no false trips)

| # | Test | Steps | Pass criteria |
|---|------|-------|---------------|
| 5.1 | Silent passage in active movie | Play a movie; let a prolonged quiet scene (> ½ timeout, e.g. 8 min) play. | Silence timer does NOT trigger; device is NOT switched. |
| 5.2 | Paused video hold | Start playing video; pause it; wait 2× timeout. | Device IS switched (paused stream holds no active session after driver releases it). Acceptable if differs by player. |
| 5.3 | Ambient game audio | Run a game with low-volume ambient audio. Wait 2× timeout. | Device is NOT switched (active game session is detected). |

---

## 6. Settings dialog

| # | Test | Steps | Pass criteria |
|---|------|-------|---------------|
| 6.1 | Cancel preserves timer | Open Settings, wait 5 s, click Cancel. | Silence timer was NOT reset (duration continues from before dialog opened). |
| 6.2 | Save applies new timeout | Open Settings, change timeout, Save. | New timeout takes effect on the next silence episode. |
| 6.3 | Save changes device | Open Settings, select a different device, Save. | Balloon on next switch names the new device. |
| 6.4 | Double-click tray icon | Double-click the tray icon. | Settings dialog opens. |

---

## 7. Startup task

| # | Test | Steps | Pass criteria |
|---|------|-------|---------------|
| 7.1 | Enable "Run at Startup" | Right-click tray → Run at Startup (check it). | `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` contains `SilenceSwitch`. |
| 7.2 | Disable "Run at Startup" | Right-click tray → Run at Startup (uncheck it). | Registry value is removed. |
| 7.3 | Actually starts at login | Enable startup; reboot (or log off/on). | Tray icon appears automatically on next login. |

---

## 8. Single-instance guard

| # | Test | Steps | Pass criteria |
|---|------|-------|---------------|
| 8.1 | Second launch blocked | With app already running, launch a second instance. | Message box: "SilenceSwitch is already running." Second instance exits cleanly. |

---

## 9. Exit and cleanup

| # | Test | Steps | Pass criteria |
|---|------|-------|---------------|
| 9.1 | Clean exit | Right-click tray → Exit. | Tray icon disappears; process ends; no orphan processes in Task Manager. |
| 9.2 | Exit during switch | Trigger a switch (manually or via silence) then immediately click Exit. | App exits cleanly; no crash or hung process. |
