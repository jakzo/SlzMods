Resources.FindObjectsOfTypeAll<Shader>()
    .Select(shader => shader.name)
    .Aggregate((current, next) => current + "\n" + next);

/*

Hidden/Internal-MotionVectors
Sprites/Default
UI/Default
UI/Default Font
Hidden/BlitCopy
Hidden/Internal-GUITextureClip
Hidden/Internal-GUITexture
Hidden/Internal-GUIRoundedRect
Sprites/Mask
Hidden/InternalErrorShader
Hidden/InternalClear
GUI/Text Shader
Standard (Specular setup)
Standard
Legacy Shaders/VertexLit
Legacy Shaders/Diffuse
Legacy Shaders/Self-Illumin/VertexLit
Legacy Shaders/Self-Illumin/Diffuse
Legacy Shaders/Self-Illumin/Specular
Legacy Shaders/Self-Illumin/Bumped Specular
Legacy Shaders/Self-Illumin/Parallax Specular
Skybox/Cubemap
Skybox/Procedural
Legacy Shaders/Particles/Additive
Legacy Shaders/Particles/Alpha Blended
Legacy Shaders/Particles/Multiply
Legacy Shaders/Particles/VertexLit Blended
Particles/Standard Surface
UI/Unlit/Text
VolumetricLightBeam/Beam
TextMeshPro/Sprite
SLZ/ZoomLens
Valve/vr_standard
SLZ/Additive HDR
Shader Forge/reflexScope
TextMeshPro/Mobile/Distance Field
TextMeshPro/Distance Field
Valve/Internal/vr_cast_shadows
ASESampleShaders/Community/TFHC/Hologram Simple
SLZ/Lines/Colored Blended
SLZ/Highlighter
Custom/HighlightAlt
SLZ/Additive Depth
SLZ/Mod2x
SLZ/Multiply
SLZ/Laser
Hidden/PostProcessing/GrainBaker
Hidden/PostProcessing/DepthOfField
Hidden/PostProcessing/MotionBlur
Hidden/PostProcessing/Debug/Waveform
Hidden/PostProcessing/Texture2DLerp
Hidden/PostProcessing/Uber
Hidden/PostProcessing/DeferredFog
Hidden/PostProcessing/CopyStd
Hidden/PostProcessing/TemporalAntialiasing
Hidden/PostProcessing/DiscardAlpha
Hidden/PostProcessing/MultiScaleVO
Hidden/PostProcessing/Lut2DBaker
Hidden/PostProcessing/SubpixelMorphologicalAntialiasing
Hidden/PostProcessing/Debug/Vectorscope
Hidden/PostProcessing/Debug/LightMeter
Hidden/PostProcessing/Debug/Overlays
Hidden/PostProcessing/Bloom
Hidden/PostProcessing/Copy
Hidden/PostProcessing/ScalableAO
Hidden/PostProcessing/FinalPass
Hidden/PostProcessing/Debug/Histogram
Hidden/PostProcessing/ScreenSpaceReflections
SLZ/Cloud cover
SLZ/SwirlClouds
SLZ/Cloud Cover Low
SLZ/VR_Triplanar
SLZ/Icon Billboard
SLZ/DustyBalls
SLZ/Multiplicative Decal
Stylized FX/Shield
KriptoFX/RFX4/LightningMobile
SLZ/GibSkinMAS
Valve/VR/Silhouette
SLZ/ShadowOnly
SLZ/GibSkinCrablet
SLZ/Vignette
SLZ/Falloff
SLZ/Nothin
ZeroLab/VR_Dissolve
SLZ/Particle Motion Vector
SLZ/GibSkinWire
SLZ/Capsule
SLZ/Glowing Falloff
SLZ/Decal Bump Mod2x
SLZ/SmokePoof
SLZ/GibSkinMAS_Emissive
SLZ/VR ColorTint
SLZ/Void
SLZ/Holographic Projection
SLZ/VR Texture Array Selector (Buffered)
SLZ/Fill Color
Unlit/SLZ-AdditiveBillboard
SLZ/Grid Glitch
SLZ/GibSkinMAS Transparent
SLZ/Vertex Tinting
SLZ/Scanline
SLZ/Additive Hologram with Depth
SLZ/Simple Fluorescence
SLZ/Glass
SLZ/Texture Array Mod2x Decals
SLZ/Holographic Visor
SLZ/Stochastic Hologram
SLZ/ModFalloff
DefaultUI

*/
