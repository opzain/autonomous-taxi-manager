# 14 — Sound, cinematic mode and recording the demo video (v0.8)

## 1. City sound (`Scripts/World/Audio/`)

Every sound is **synthesised in code when Play starts** (`ProceduralAudio.cs`, about 0.7 s), so the project needs no audio files and no licences. You can preview all 20 sounds in `docs/audio-preview/*.wav`.

| Sound | Where you hear it |
|---|---|
| `traffic_bed` | Everywhere. Louder near the street, at rush hour (9–10 am and 7 pm) and with more traffic. It is muffled (low-passed) when the camera is high. |
| `wind_loop` | Louder the higher the camera goes, and in the monsoon. |
| `rain_loop` | Follows the monsoon intensity (M cycles the weather). |
| `thunder` | 0.4–2.8 s after each lightning flash (sound arrives after light). |
| `crickets_loop` | At night, when it isn't raining. |
| `crowd_loop` | Wherever walkers are near the camera, and at Kurla and Bandra stations (3D). |
| `horn_car`, `horn_car_double`, `horn_scooter`, `horn_auto`, `horn_truck`, `horn_bus` | From real vehicles near the camera. Drivers stuck at a red light or in a jam honk three times more often. Each vehicle type has its own horn; the truck plays a little "Horn OK Please" tune. **Our autonomous taxis never honk.** |
| `station_chime` + `pa_announcement` | At the stations, every 35–70 s: a three-tone chime, then a PA voice. The voice is speech-like murmur, not real words. |
| `train_horn` | Near the stations, at random. |
| `train_rumble` | Attached to every moving local / metro train, with wheel-joint "ta-dum" clacks. |
| `hub_hum` | 50 Hz electrical hum plus a charger whine at the AT Fleet Hub. |
| `charge_chirp` | Charging beeps at the Fleet Hub. |
| `crow` | Mumbai crows near the camera by day. |
| `dhol_loop` (v0.9) | The Ganpati procession: dhol bass and treble beats with tasha snare rolls, 4 bars at 120 BPM |
| `firework` (v0.9) | Diwali rockets: whistle, boom, then crackle, played at the launch point |
| `ped_beep` | Accessible crossing beeps at the nearest signal while pedestrians have "walk". They speed up as time runs out. |

**N** mutes and unmutes.

**Using real recordings instead:** put a file with the same name (e.g. `horn_auto.wav` or `.ogg`) in `Assets/_Project/Resources/TaxiSimAudio/`. The console line at start-up reports how many real recordings were used. For free, CC0 sources, search freesound.org with the "Creative Commons 0" filter. Keep each file short and loop-safe for the `*_loop` beds.

## 2. Pedestrians crossing at signals

- Each signalised junction ends its cycle with an **all-red pedestrian phase** of 9 s. The walk lamp blinks for the last 2.5 s.
- Real **zebra crossings and stop lines** are painted on every approach. Vehicles stop at the stop line, before the zebra.
- About 60% of walkers who reach a signalised corner **wait at the kerb** (standing, facing the road). They cross when the walk lamp is green and at least 3.2 s remain, then carry on along the opposite footpath.
- **Vehicles yield:** while anyone is on a crossing, vehicles at that junction stay at their stop line, even on green.

## 3. Cinematic mode (C)

C starts an automatic film director. It takes over the camera and hides the interface.

| Shot | What it does |
|---|---|
| Title aerial | High orbit over G-Block with the opening title card "AUTONOMOUS TAXI MANAGER" |
| Landmark orbit | Slow arc around a random landmark, captioned with its name |
| Highway flyover | Dolly along a main road at 16–28 units up, looking ahead |
| Tracking shot | Slides from behind to alongside an autonomous taxi (AT-0x) on a long lens |
| Chase cam | Behind a BEST bus, truck, auto, kaali-peeli or scooter |
| Street dolly | At eye level along the kerb: stalls, crowds, signals, shallow depth of field |
| Crane-up | From a signalised junction corner up to the skyline |
| Fleet Hub push-in | From the access road towards the depot gate |
| Signal junction | Low wide shot of a junction: watch cars stop and people cross |
| Time-lapse | Wide skyline while the clock runs fast (sunset → night) |

- **Cuts:** a new shot every 6.5–10 s, alternating hard cuts and dips to black. The same type is never repeated twice in a row.
- **Look:** 2.39:1 letterbox, lower-third captions, a lens-accurate depth of field focused on the subject, motion blur and film grain.
- **Safety:** aerial shots stay above the rooftops, using a height grid built from the building records.
- **Keys:** J skips to the next shot. C, Esc or any movement key hands the camera back exactly where it is.

## 4. Recording the demo video

1. Unity: **Window → Package Manager → Unity Registry → Recorder → Install**.
2. **Window → General → Recorder → Recorder Window → Add Recorder → Movie.** Choose MP4 (H.264), 1920×1080 (or 2560×1080 for true 2.39:1), 30 fps, **Include audio**, and set *Playback* to "Constant".
3. Press F5 (Cinematic graphics tier), set the hour with 3 (sunset), and press **C**. Then start recording. About 3 minutes gives roughly 20 shots.
4. Add your voice-over or the HUD gameplay in any editor (Clipchamp is built into Windows 11).

Without the Recorder package: **Win + Alt + R** (Xbox Game Bar) records the Game view with sound.
