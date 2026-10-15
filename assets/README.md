# Assets

- IdleSlayer.original.ico: original icon resources extracted from the user's Steam
  installation of Idle Slayer, 9 sizes. No executable is copied.
- IdleSlayer.wordmark.png: original game wordmark from the local Steam library cache.
  Game artwork/marks belong to Pablo Leban and are excluded from the code license.
- DivineLabs.svg: own vector mark, based on the user's supplied flowing D references.
- DivineLabs.png / .ico: original game icon plus small violet DivineLabs badge.
  Regenerate with scripts/CreateBrandAssets.ps1; this also produces the Unity
  polygon data in src/Mod/BrandGeometry.cs.
- Installer.cover.png: built-in image_gen output, generated from the approved
  Cinematic/Fluent installer concept; not represented as original game artwork.

Generation prompts (built-in image_gen):
1. Compare Cinematic, Fluent and Graphite installer windows, exact product name,
   original game identity, five Russian steps, restrained cyan/violet controls.
2. Combine Cinematic portrait cover with light Fluent palette, exact casing
   DivineLabs, and a flowing D glyph matching both user references.
3. Extract only the full-height character cover from the approved combined mockup;
   preserve composition; remove all text, logos, window chrome and margins.
   Portrait about 2:5; overlay the original game wordmark in WPF.

Full concept outputs are retained under W:\DEV\.state\codex\generated_images,
and application layout renders under W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\design-0.4.1.
