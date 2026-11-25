# 13 — Real Textures Guide (photoreal upgrade, v0.5)

The city works without these, but real photographed textures are what make it look like GTA V (your reference image 3). They're all **free and CC0**, so you can use them in the project and the report without any licence issues.

## Where they go

```
Assets/_Project/Resources/TaxiSimTextures/        <- material textures (.jpg / .png)
Assets/_Project/Resources/TaxiSimTextures/Sky/    <- ONE HDRI sky (.hdr or .exr)
```

**Keep the original file names.** The game finds files by words in their names (for example `Asphalt`, `Color`, `NormalGL`, `Roughness`). The `TaxiSimTextureImporter` script then sets up each file automatically: normal maps as normal maps, roughness as linear data, and the HDRI as a cubemap.

## What to download (about 10 minutes)

Use **ambientCG.com** (easiest) or **polyhaven.com** (also excellent). For each item: search, pick one that looks right, and download the **2K JPG** version. Unzip it and copy only these three files:

| From ambientCG | From Poly Haven |
|---|---|
| `…_Color.jpg` | `…_diff_2k.jpg` |
| `…_NormalGL.jpg` | `…_nor_gl_2k.jpg` |
| `…_Roughness.jpg` | `…_rough_2k.jpg` |

| # | Search for | Used on | Tip |
|---|---|---|---|
| 1 | **Asphalt** | All roads (lane markings are still drawn on top) | Pick a plain grey road asphalt, not a cracked or very dark one |
| 2 | **PavingStones** (ambientCG) or **paving** | Sidewalks, plazas | Small grey or red pavers look most Mumbai-like |
| 3 | **Grass** | MMRDA Grounds, lawns, campus | A short, slightly dry grass |
| 4 | **Concrete** | General ground | A light, worn concrete floor |
| 5 | **Concrete** or **plaster** (a *Facade* set is the fallback) | All building walls (window glass stays procedural) | Plain concrete or plaster works best, because our shader draws the windows itself |
| 6 | **Gravel** (optional) | Railway track bed | — |
| 7 | **Sand** (optional) | Beach and mud by the sea | — |
| 8 | **HDRI**: polyhaven.com → HDRIs → *Outdoor → Urban* or *Skies* | Daytime sky + reflections | Choose a sunny, partly cloudy one. Download **2K or 4K .hdr** into the `Sky` folder |

**Total download:** about 100–300 MB.

## Check that it worked

1. Press ▶ Play.
2. The overlay (top left) shows a line like `Textures: asphalt, paving, grass, ground, facade, HDRI sky`. The Console shows the same in the "BKC world generated…" message.
3. If an item still says it's missing, check that the file name contains the search word (e.g. `asphalt`) and that the file is directly inside `TaxiSimTextures`, not in a sub-folder (except the HDRI).

## Notes

- The HDRI sky is used **during the day**. Dusk, night and monsoon use the procedural sky with stars, clouds and city glow.
- Texture sizes in the world: asphalt repeats every 4 m, pavers every 2.5 m, facades every 3.5 m. To change them, select the material at runtime or edit `MaterialLibrary.ApplyRealTextures()`.
- For the report, list each texture's name and source in the references (CC0, ambientCG / Poly Haven).
