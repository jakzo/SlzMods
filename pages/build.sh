#!/bin/sh
set -eux
cd "$(dirname "$0")"
mkdir ./dist

cp -r ./github-pages ./dist/github-pages
cp -r ../projects/Web/vr-input-viewer/dist ./dist/github-pages/input-viewer
cp ../projects/Web/vr-input-viewer/dist/index.html ./dist/github-pages/input-viewer.html

cp -r ./vr.jf.id.au ./dist/vr.jf.id.au
cp -r ../projects/Web/vr-input-viewer/dist/. ./dist/vr.jf.id.au/input-viewer/
cp ../projects/Web/vr-input-viewer/dist/index.html ./dist/vr.jf.id.au/input-viewer/input-viewer.html

cp ../snippets/Obs/BonelabQuestObsScene.json ./dist/vr.jf.id.au/
