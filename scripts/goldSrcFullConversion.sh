#!/bin/bash

# Need these in PATH:
# Tools from this repo and:
# https://github.com/twhl-community/HalfLife.UnifiedSdk.MapDecompiler  (for commandline wad extractor)
# https://github.com/Garux/netradiant-custom/ (or other source of mbspc)

WAD2Quake3Shader '*'

cd models
MDL2Quake3OBJ_NET '*' --recursive

cd ..
cd maps
mkdir wadExtract
MapDecompilerCmdLine Tree --generate-wad-file --destination "./wadExtract" "$1.bsp"
mbspc -bsp2map220 "$1.bsp" "$1.map"

cd wadExtract 
WAD2Quake3Shader '*'

cd ..
cd ..

mkdir _converted

mkdir _converted/shaders

echo > "_converted/shaders/$1_tmp.shader"
cat "models/shaders/mdlConvertShadersQ3MAP.shader" >> "_converted/shaders/$1_tmp.shader"
echo >> "_converted/shaders/$1_tmp.shader"
cat "maps/wadExtract/shaders/wadConvertShadersQ3MAP.shader" >> "_converted/shaders/$1_tmp.shader"
echo >> "_converted/shaders/$1_tmp.shader"
cat "shaders/wadConvertShadersQ3MAP.shader" >> "_converted/shaders/$1_tmp.shader"

echo >> "_converted/shaders/$1_tmp.shader"
cat "models/shaders/mdlConvertShaders.shader" >> "_converted/shaders/$1_tmp.shader"
echo >> "_converted/shaders/$1_tmp.shader"
cat "maps/wadExtract/shaders/wadConvertShaders.shader" >> "_converted/shaders/$1_tmp.shader"
echo >> "_converted/shaders/$1_tmp.shader"
cat "shaders/wadConvertShaders.shader" >> "_converted/shaders/$1_tmp.shader"

rm "_converted/shaders/$1.shader"

cd maps
rm "$1.map"
mv "$1_decompiled.map" "$1.map"
FilterMapShaderNames "$1.map" "../_converted/shaders/"

cd ..

echo > "_converted/shaders/$1.shader"
cat "maps/shaders/$1.shader" >> "_converted/shaders/$1.shader"
echo >> "_converted/shaders/$1.shader"
cat "_converted/shaders/$1_tmp.shader" >> "_converted/shaders/$1.shader"
rm "_converted/shaders/$1_tmp.shader"

mkdir _converted/maps
mkdir _converted/models
mkdir _converted/models/mdlConvert
mkdir _converted/textures
mkdir _converted/textures/wadConvert
cp -r models/models/mdlConvert _converted/models
cp -r textures/wadConvert _converted/textures
cp -r maps/wadExtract/textures/wadConvert _converted/textures
cp -r gfx _converted
cp -r sound _converted
cp "maps/$1.map.filtered.map" "_converted/maps/$1.map"

read -n1 -r