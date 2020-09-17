// O3 produces literally 1/3 the instructions of OD or O0 so let's just be kind to the driver
#pragma fxcparams(/O3 /Zi)

#define SHAPE_TYPE_NAME RasterRectangleUntextured
#define SHAPE_TYPE_NAME_TEX RasterRectangleTextured
#define SHAPE_TYPE_TECHNIQUE_NAME RasterRectangleTechnique
#define SHAPE_TYPE_TECHNIQUE_NAME_TEX TexturedRasterRectangleTechnique
#define SHAPE_TYPE_NAME_SHADOWED ShadowedRasterRectangleUntextured
#define SHAPE_TYPE_NAME_TEX_SHADOWED ShadowedRasterRectangleTextured
#define SHAPE_TYPE_TECHNIQUE_NAME_SHADOWED ShadowedRasterRectangleTechnique
#define SHAPE_TYPE_TECHNIQUE_NAME_TEX_SHADOWED ShadowedTexturedRasterRectangleTechnique

#define INCLUDE_RECTANGLE

#include "RasterShapeSkeleton.fxh"
#include "RasterShapeImpl.fxh"