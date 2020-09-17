// O3 produces literally 1/3 the instructions of OD or O0 so let's just be kind to the driver
#pragma fxcparams(/O3 /Zi)

#define SHAPE_TYPE_NAME RasterEllipseUntextured
#define SHAPE_TYPE_NAME_TEX RasterEllipseTextured
#define SHAPE_TYPE_TECHNIQUE_NAME RasterEllipseTechnique
#define SHAPE_TYPE_TECHNIQUE_NAME_TEX TexturedRasterEllipseTechnique
#define SHAPE_TYPE_NAME_SHADOWED ShadowedRasterEllipseUntextured
#define SHAPE_TYPE_NAME_TEX_SHADOWED ShadowedRasterEllipseTextured
#define SHAPE_TYPE_TECHNIQUE_NAME_SHADOWED ShadowedRasterEllipseTechnique
#define SHAPE_TYPE_TECHNIQUE_NAME_TEX_SHADOWED ShadowedTexturedRasterEllipseTechnique

#define INCLUDE_ELLIPSE

#include "RasterShapeSkeleton.fxh"
#include "RasterShapeImpl.fxh"