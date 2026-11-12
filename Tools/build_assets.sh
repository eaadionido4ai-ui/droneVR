#!/usr/bin/env bash
# Rebuilds every model, texture and data file of DRONE VR from the plan of the barangay
# (Tools/world-export/src/plan.js) and the three.js builders beside it.
#
#   Tools/build_assets.sh
#
# Steps: (1) write the data for Unity and GAMA from the plan; (2) build the barangay, the operations center
#        and the props in three.js and export them (Tools/world-export); (3) lay out the palette of flat colors;
#        (4) copy the textures into the Unity project; (5) build the Blender files and the FBX models
#        (Tools/blender); (6) write the materials and Unity's .meta files.
#
# Needs Node 20+ with Playwright's Chromium, Python 3 with Pillow, and Blender 4.2 LTS or newer: either set
# BLENDER to the blender program, or set BLENDER_PYTHON to a Python that has the bpy module
# (pip install bpy==4.2.*).
set -euo pipefail
cd "$(dirname "$0")/.."
TEX=Unity/Assets/DRONEVR/Resources/DRONEVR/Textures

echo "== 1. data for Unity and GAMA"
node Tools/world-export/gen-data.mjs

echo "== 2. models from three.js"
(cd Tools/world-export && node export.mjs)

echo "== 3. palette"
python3 Tools/blender/make_palette.py Tools/world-export/out/glb Tools/blender/palette.png Tools/blender/palette.json

echo "== 4. textures"
mkdir -p "$TEX"
cp Tools/world-export/out/textures/*.png "$TEX/"
cp Tools/blender/palette.png "$TEX/palette.png"

echo "== 5. Blender files and FBX models"
if [ -n "${BLENDER:-}" ]; then
  "$BLENDER" --background --python Tools/blender/build_models.py -- "$@" 2>&1 | grep -E "pieces ->|rror|Traceback" || true
else
  "${BLENDER_PYTHON:-python3}" Tools/blender/build_models.py "$@" 2>&1 | grep -E "pieces ->|rror|Traceback" || true
fi

echo "== 6. materials and Unity .meta files"
python3 Tools/make_materials.py
python3 Tools/unity_meta.py
echo "done"
