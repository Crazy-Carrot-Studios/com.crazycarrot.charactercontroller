# CCS Character Controller

**Version:** `0.1.2-preview.1` (authoritative: `package.json`)  
**Phase:** **Basic Locomotion** — third-person move, Cinemachine camera, Humanoid-oriented wizard. Not combat-ready. Not feature-complete.

Third-person **CharacterController** locomotion, **Cinemachine 3** rig, **Character Setup Wizard**, default camera profile and **CCS_Base_locomotion_controller** (root motion off).

## Unity: install from Git

Add to **`Packages/manifest.json`** inside `"dependencies"`:

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git",
"com.crazycarrot.branding": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.branding.git"
```

Optional — pin this preview build:

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git#v0.1.2-preview.1"
```

- **Unity:** 6 (`6000.0+`). **Input System** + **Cinemachine 3** versions are listed in `package.json`.
- **Wizard:** **CCS → Character Controller → Create Character**
- **Repo:** [github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller](https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller)
- **Changelog:** [CHANGELOG.md](CHANGELOG.md)

If you copy the package into **`Assets/`** (e.g. Hub), keep every **`.meta`** file so camera profile assets keep the correct script GUIDs.
