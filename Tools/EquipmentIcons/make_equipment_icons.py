#!/usr/bin/env python3
"""
PLACEHOLDER equipment icons for the inventory HUD.

These are NOT final art. `EquipmentDefinition.Icon` is the authored slot and it wins
wherever it is filled in; these exist because all eleven of those slots are empty, and an
Image with a null sprite draws nothing at all - so a slot holding the DOTS projector looked
exactly like an empty slot, which is the one thing the inventory HUD has to be able to tell
apart.

Style matches `Resources/UI/Controls/`: 256 x 256 RGBA, pure white with the shape carried
entirely in the alpha channel, so the HUD can tint them. Drawn at 4x and downsampled, which
is where the antialiasing comes from.

Run:  python3 Tools/EquipmentIcons/make_equipment_icons.py
"""

import math
import os
import struct
import hashlib
from PIL import Image, ImageDraw

S = 256
SS = 4                      # supersample factor
N = S * SS
OUT = "Assets/CatchIfYouCan/Resources/UI/Equipment"

W = 255                     # stroke alpha


def canvas():
    im = Image.new("L", (N, N), 0)
    return im, ImageDraw.Draw(im)


def px(v):
    """A design number in 256-space, in supersampled pixels."""
    return v * SS


def stroke():
    return max(1, px(9))


# ---------------------------------------------------------------- glyphs

def flashlight(d):
    # Body down the diagonal, cone at the head.
    d.rounded_rectangle([px(96), px(58), px(160), px(150)], radius=px(14), fill=W)
    d.polygon([(px(96), px(150)), (px(160), px(150)),
               (px(196), px(216)), (px(60), px(216))], fill=W)
    d.rounded_rectangle([px(112), px(30), px(144), px(58)], radius=px(8), fill=W)


def emf_detector(d):
    d.rounded_rectangle([px(70), px(96), px(150), px(226)], radius=px(12),
                        outline=W, width=stroke())
    d.rectangle([px(86), px(116), px(134), px(150)], fill=W)
    d.line([(px(150), px(96)), (px(178), px(60))], fill=W, width=stroke())
    for i, r in enumerate((30, 52, 74)):
        d.arc([px(178 - r), px(60 - r), px(178 + r), px(60 + r)],
              start=-75, end=15, fill=W, width=stroke())


def uv_light(d):
    d.rounded_rectangle([px(104), px(28), px(152), px(104)], radius=px(10),
                        outline=W, width=stroke())
    d.polygon([(px(88), px(104)), (px(168), px(104)),
               (px(196), px(150)), (px(60), px(150))], outline=W, width=stroke())
    for x in (78, 108, 148, 178):
        d.line([(px(x), px(168)), (px(x), px(228))], fill=W, width=stroke())


def thermometer(d):
    d.rounded_rectangle([px(110), px(26), px(146), px(168)], radius=px(18),
                        outline=W, width=stroke())
    d.ellipse([px(96), px(168), px(160), px(232)], outline=W, width=stroke())
    d.ellipse([px(114), px(186), px(142), px(214)], fill=W)
    d.rectangle([px(122), px(120), px(134), px(190)], fill=W)
    for y in (54, 80, 106):
        d.line([(px(150), px(y)), (px(180), px(y))], fill=W, width=stroke())


def evp_recorder(d):
    d.rounded_rectangle([px(38), px(74), px(218), px(190)], radius=px(16),
                        outline=W, width=stroke())
    d.ellipse([px(66), px(102), px(118), px(154)], outline=W, width=stroke())
    d.ellipse([px(138), px(102), px(190), px(154)], outline=W, width=stroke())
    d.ellipse([px(86), px(122), px(98), px(134)], fill=W)
    d.ellipse([px(158), px(122), px(170), px(134)], fill=W)


def parabolic_microphone(d):
    d.arc([px(22), px(22), px(234), px(234)], start=200, end=340, fill=W, width=px(14))
    d.line([(px(128), px(86)), (px(128), px(196))], fill=W, width=stroke())
    d.ellipse([px(112), px(70), px(144), px(102)], fill=W)
    d.rounded_rectangle([px(112), px(196), px(144), px(236)], radius=px(10), fill=W)


def photo_camera(d):
    d.rounded_rectangle([px(30), px(76), px(226), px(206)], radius=px(18),
                        outline=W, width=stroke())
    d.rounded_rectangle([px(94), px(52), px(162), px(78)], radius=px(8), fill=W)
    d.ellipse([px(92), px(102), px(164), px(174)], outline=W, width=stroke())
    d.ellipse([px(112), px(122), px(144), px(154)], fill=W)
    d.ellipse([px(188), px(94), px(208), px(114)], fill=W)


def spectral_grid(d):
    # The device's own signature: a field of dots, denser toward the middle.
    d.rounded_rectangle([px(86), px(178), px(170), px(228)], radius=px(10),
                        outline=W, width=stroke())
    d.line([(px(128), px(178)), (px(128), px(156))], fill=W, width=stroke())
    r = px(7)
    for row, (y, count) in enumerate(((34, 3), (68, 5), (102, 7), (136, 5))):
        span = px(26 + row * 22)
        for i in range(count):
            t = 0.5 if count == 1 else i / (count - 1)
            x = px(128) + int((t - 0.5) * 2 * span)
            d.ellipse([x - r, px(y) - r, x + r, px(y) + r], fill=W)


def video_camera(d):
    d.rounded_rectangle([px(26), px(88), px(168), px(196)], radius=px(14),
                        outline=W, width=stroke())
    d.polygon([(px(168), px(126)), (px(228), px(96)),
               (px(228), px(188)), (px(168), px(158))], outline=W, width=stroke())
    d.rounded_rectangle([px(62), px(62), px(124), px(88)], radius=px(8), fill=W)
    d.ellipse([px(56), px(122), px(96), px(162)], outline=W, width=stroke())


def warding_relic(d):
    cx = cy = px(128)
    rad = px(96)
    d.ellipse([cx - rad, cy - rad, cx + rad, cy + rad], outline=W, width=stroke())
    pts = []
    inner = px(78)
    for i in range(5):
        a = -math.pi / 2 + i * 2 * math.pi / 5
        pts.append((cx + inner * math.cos(a), cy + inner * math.sin(a)))
    order = [0, 2, 4, 1, 3, 0]
    for i in range(5):
        d.line([pts[order[i]], pts[order[i + 1]]], fill=W, width=stroke())


def salt(d):
    d.polygon([(px(80), px(96)), (px(176), px(96)),
               (px(206), px(226)), (px(50), px(226))], outline=W, width=stroke())
    d.line([(px(80), px(96)), (px(96), px(52))], fill=W, width=stroke())
    d.line([(px(176), px(96)), (px(160), px(52))], fill=W, width=stroke())
    d.line([(px(96), px(52)), (px(160), px(52))], fill=W, width=stroke())
    for x, y in ((100, 148), (128, 132), (156, 152), (114, 184), (146, 190), (128, 168)):
        d.ellipse([px(x - 6), px(y - 6), px(x + 6), px(y + 6)], fill=W)


GLYPHS = {
    "flashlight": flashlight,
    "emf_detector": emf_detector,
    "uv_light": uv_light,
    "thermometer": thermometer,
    "evp_recorder": evp_recorder,
    "parabolic_microphone": parabolic_microphone,
    "photo_camera": photo_camera,
    "spectral_grid": spectral_grid,
    "video_camera": video_camera,
    "warding_relic": warding_relic,
    "salt": salt,
}


# ---------------------------------------------------------------- meta

META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 256
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Android
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: iOS
    maxTextureSize: 256
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: {sprite_id}
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def stable_guid(seed):
    """A fixed guid per asset name, so re-running never rewrites one (mistake: never
    regenerate a .meta guid to fix an import)."""
    return hashlib.sha256(("ciyc/equipment-icon/" + seed).encode()).hexdigest()[:32]


def stable_sprite_id(seed):
    h = hashlib.sha256(("ciyc/equipment-icon-sprite/" + seed).encode()).hexdigest()
    return h[:16] + "0800000000000000"


def write_meta(path, seed, template, **extra):
    if os.path.exists(path):
        return False                     # never touch an existing guid
    with open(path, "w", encoding="utf-8") as f:
        f.write(template.format(guid=stable_guid(seed), **extra))
    return True


def main():
    os.makedirs(OUT, exist_ok=True)
    write_meta(OUT + ".meta", "folder:UI/Equipment", FOLDER_META)

    for eid, draw_glyph in GLYPHS.items():
        mask, d = canvas()
        draw_glyph(d)
        mask = mask.resize((S, S), Image.LANCZOS)

        rgb = Image.new("RGB", (S, S), (255, 255, 255))
        icon = Image.merge("RGBA", (*rgb.split(), mask))

        name = "Icon_Equipment_" + eid
        png = os.path.join(OUT, name + ".png")
        icon.save(png, optimize=True)

        write_meta(png + ".meta", name, META, sprite_id=stable_sprite_id(name))

        alpha = list(mask.getdata())
        nonzero = sum(1 for v in alpha if v > 0) / len(alpha)
        opaque = sum(1 for v in alpha if v == 255) / len(alpha)
        print("%-24s coverage %5.1f%%  solid %5.1f%%" % (eid, nonzero * 100, opaque * 100))


if __name__ == "__main__":
    main()
