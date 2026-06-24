// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { PlanetaryComputerProContext as Client } from "../index.js";
import { getBinaryStreamResponse } from "#platform/static-helpers/serialization/get-binary-stream-response";
import {
  geometryUnionSerializer,
  stacSortExtensionArraySerializer,
  TileMatrixSet,
  tileMatrixSetDeserializer,
  ClassMapLegendResponse,
  classMapLegendResponseDeserializer,
  mosaicMetadataSerializer,
  TilerMosaicSearchRegistrationResponse,
  tilerMosaicSearchRegistrationResponseDeserializer,
  TileSetList,
  tileSetListDeserializer,
  TileSetMetadata,
  tileSetMetadataDeserializer,
  Feature,
  featureSerializer,
  StacItemBounds,
  stacItemBoundsDeserializer,
  TilerInfoMapResponse,
  tilerInfoMapResponseDeserializer,
  TilerInfoGeoJsonFeature,
  tilerInfoGeoJsonFeatureDeserializer,
  AssetStatisticsResponse,
  assetStatisticsResponseDeserializer,
  TilerStacItemStatistics,
  tilerStacItemStatisticsDeserializer,
  StacItemStatisticsGeoJson,
  stacItemStatisticsGeoJsonDeserializer,
  TileJsonMetadata,
  tileJsonMetadataDeserializer,
  TilerCoreModelsResponsesPoint,
  tilerCoreModelsResponsesPointDeserializer,
  TilerAssetGeoJson,
  TilerStacSearchRegistration,
  tilerStacSearchRegistrationDeserializer,
  StacItemPointAsset,
  tilerAssetGeoJsonArrayDeserializer,
  stacItemPointAssetArrayDeserializer,
  DataGetSearchAssetsForTileNoTmsResponse,
  DataGetSearchTileNoTmsByScaleAndFormatResponse,
  DataGetSearchTileNoTmsByScaleResponse,
  DataGetSearchTileNoTmsByFormatResponse,
  DataGetSearchTileNoTmsResponse,
  DataGetSearchWmtsCapabilitiesResponse,
  DataCropSearchFeatureWidthByHeightResponse,
  DataCropSearchFeatureByFormatResponse,
  DataCropSearchFeatureResponse,
  DataGetSearchBboxAssetsResponse,
  DataGetSearchBboxCropWithDimensionsResponse,
  DataGetSearchBboxCropResponse,
  DataGetSearchWmtsCapabilitiesByTmsResponse,
  DataGetSearchTileByScaleResponse,
  DataGetSearchTileByFormatResponse,
  DataGetSearchTileResponse,
  DataGetSearchTileByScaleAndFormatResponse,
  DataCropCollectionFeatureWidthByHeightResponse,
  DataCropCollectionFeatureByFormatResponse,
  DataCropCollectionFeatureResponse,
  DataGetCollectionBboxCropWithDimensionsResponse,
  DataGetCollectionBboxCropResponse,
  DataGetCollectionAssetsForBboxResponse,
  DataGetCollectionAssetsForTileNoTmsResponse,
  DataGetCollectionWmtsCapabilitiesByTmsResponse,
  DataGetCollectionWmtsCapabilitiesResponse,
  DataGetCollectionTileNoTmsByScaleResponse,
  DataGetCollectionTileNoTmsByFormatResponse,
  DataGetCollectionTileNoTmsResponse,
  DataGetCollectionTileNoTmsByScaleAndFormatResponse,
  DataGetCollectionTileByScaleResponse,
  DataGetCollectionTileByFormatResponse,
  DataGetCollectionTileResponse,
  DataGetCollectionTileByScaleAndFormatResponse,
  DataGetItemBboxCropWithDimensionsResponse,
  DataGetItemBboxCropResponse,
  DataGetItemPreviewWithFormatResponse,
  DataGetItemPreviewResponse,
  DataGetItemWmtsCapabilitiesByTmsResponse,
  DataGetItemWmtsCapabilitiesResponse,
  DataGetItemAvailableAssetsResponse,
  DataCropFeatureWidthByHeightResponse,
  DataCropFeatureByFormatResponse,
  DataCropFeatureResponse,
  DataGetTileNoTmsByScaleAndFormatResponse,
  DataGetTileNoTmsByScaleResponse,
  DataGetTileNoTmsByFormatResponse,
  DataGetTileNoTmsResponse,
  DataGetTileByScaleAndFormatResponse,
  DataGetTileByScaleResponse,
  DataGetTileByFormatResponse,
  DataGetTileResponse,
  DataGetLegendResponse,
  DataGetIntervalLegendResponse,
  DataGetTileMatricesResponse,
} from "../../models/models.js";
import { expandUrlTemplate } from "../../static-helpers/urlTemplate.js";
import {
  DataGetSearchPointWithAssetsOptionalParams,
  DataGetSearchPointOptionalParams,
  DataGetSearchAssetsForTileNoTmsOptionalParams,
  DataGetSearchTileNoTmsByScaleAndFormatOptionalParams,
  DataGetSearchTileNoTmsByScaleOptionalParams,
  DataGetSearchTileNoTmsByFormatOptionalParams,
  DataGetSearchTileNoTmsOptionalParams,
  DataGetSearchTileJsonOptionalParams,
  DataGetSearchWmtsCapabilitiesOptionalParams,
  DataCropSearchFeatureWidthByHeightOptionalParams,
  DataCropSearchFeatureByFormatOptionalParams,
  DataCropSearchFeatureOptionalParams,
  DataGetSearchBboxAssetsOptionalParams,
  DataGetSearchBboxCropWithDimensionsOptionalParams,
  DataGetSearchBboxCropOptionalParams,
  DataGetSearchInfoOptionalParams,
  DataGetSearchWmtsCapabilitiesByTmsOptionalParams,
  DataGetSearchTileJsonByTmsOptionalParams,
  DataGetSearchAssetsForTileOptionalParams,
  DataGetSearchTileByScaleOptionalParams,
  DataGetSearchTileByFormatOptionalParams,
  DataGetSearchTileOptionalParams,
  DataGetSearchTileByScaleAndFormatOptionalParams,
  DataGetSearchTilesetMetadataOptionalParams,
  DataGetSearchTilesetsOptionalParams,
  DataGetCollectionPointAssetsOptionalParams,
  DataGetCollectionPointOptionalParams,
  DataCropCollectionFeatureWidthByHeightOptionalParams,
  DataCropCollectionFeatureByFormatOptionalParams,
  DataCropCollectionFeatureOptionalParams,
  DataGetCollectionBboxCropWithDimensionsOptionalParams,
  DataGetCollectionBboxCropOptionalParams,
  DataGetCollectionInfoOptionalParams,
  DataGetCollectionAssetsForBboxOptionalParams,
  DataGetCollectionAssetsForTileNoTmsOptionalParams,
  DataGetCollectionAssetsForTileOptionalParams,
  DataGetCollectionWmtsCapabilitiesByTmsOptionalParams,
  DataGetCollectionWmtsCapabilitiesOptionalParams,
  DataGetCollectionTileJsonByTmsOptionalParams,
  DataGetCollectionTileJsonOptionalParams,
  DataGetCollectionTileNoTmsByScaleOptionalParams,
  DataGetCollectionTileNoTmsByFormatOptionalParams,
  DataGetCollectionTileNoTmsOptionalParams,
  DataGetCollectionTileNoTmsByScaleAndFormatOptionalParams,
  DataGetCollectionTileByScaleOptionalParams,
  DataGetCollectionTileByFormatOptionalParams,
  DataGetCollectionTileOptionalParams,
  DataGetCollectionTileByScaleAndFormatOptionalParams,
  DataGetCollectionTilesetMetadataOptionalParams,
  DataGetCollectionTilesetsOptionalParams,
  DataGetItemBboxCropWithDimensionsOptionalParams,
  DataGetItemBboxCropOptionalParams,
  DataGetItemPreviewWithFormatOptionalParams,
  DataGetItemPreviewOptionalParams,
  DataGetItemPointOptionalParams,
  DataGetItemWmtsCapabilitiesByTmsOptionalParams,
  DataGetItemWmtsCapabilitiesOptionalParams,
  DataGetItemTileJsonByTmsOptionalParams,
  DataGetItemTileJsonOptionalParams,
  DataGetItemFeatureStatisticsOptionalParams,
  DataGetItemStatisticsOptionalParams,
  DataGetItemAssetStatisticsOptionalParams,
  DataGetItemAvailableAssetsOptionalParams,
  DataGetItemInfoGeoJsonOptionalParams,
  DataGetItemInfoOptionalParams,
  DataGetItemBoundsOptionalParams,
  DataCropFeatureWidthByHeightOptionalParams,
  DataCropFeatureByFormatOptionalParams,
  DataCropFeatureOptionalParams,
  DataGetTileNoTmsByScaleAndFormatOptionalParams,
  DataGetTileNoTmsByScaleOptionalParams,
  DataGetTileNoTmsByFormatOptionalParams,
  DataGetTileNoTmsOptionalParams,
  DataGetTileByScaleAndFormatOptionalParams,
  DataGetTileByScaleOptionalParams,
  DataGetTileByFormatOptionalParams,
  DataGetTileOptionalParams,
  DataGetTilesetMetadataOptionalParams,
  DataGetTilesetsOptionalParams,
  DataRegisterMosaicsSearchOptionalParams,
  DataGetLegendOptionalParams,
  DataGetIntervalLegendOptionalParams,
  DataGetClassMapLegendOptionalParams,
  DataGetTileMatricesOptionalParams,
  DataGetTileMatrixDefinitionsOptionalParams,
} from "./options.js";
import {
  StreamableMethod,
  PathUncheckedResponse,
  createRestError,
  operationOptionsToRequestParameters,
} from "@azure-rest/core-client";
import { stringToUint8Array } from "@azure/core-util";


// FIXTURE (condensed) — Source: azure-sdk-for-js
// build 6454089, planetarycomputer package. The generated operations.ts has 255
// operation functions; this faithful reduction keeps ONLY the 6 WMTS capability
// triplets (Send/Deserialize/public) that the analysis names, because their
// deserializers wrongly base64-decode XML bodies (the bug under test). The other
// ~237 operations are elided. WMTS deserializers are kept verbatim so the
// base64-decode-of-XML bug and the _getItemWmtsCapabilitiesByTmsDeserialize
// symbol remain exactly as generated.

// ... (non-WMTS data-plane operations elided for fixture brevity) ...

export function _getSearchWmtsCapabilitiesSend(
  context: Client,
  searchId: string,
  options: DataGetSearchWmtsCapabilitiesOptionalParams = { requestOptions: {} },
): StreamableMethod {
  const path = expandUrlTemplate(
    "/data/mosaic/searches/{searchId}/WMTSCapabilities.xml{?api%2Dversion,TileMatrixSetId,tile_format,tile_scale,minzoom,maxzoom,bidx*,assets*,expression,asset_bidx,asset_as_band,nodata,unscale,reproject}",
    {
      searchId: searchId,
      "api%2Dversion": context.apiVersion ?? "2026-04-15",
      TileMatrixSetId: options?.tileMatrixSetId,
      tile_format: options?.tileFormat,
      tile_scale: options?.tileScale,
      minzoom: options?.minZoom,
      maxzoom: options?.maxZoom,
      bidx: !options?.bidx
        ? options?.bidx
        : options?.bidx.map((p: any) => {
            return p;
          }),
      assets: !options?.assets
        ? options?.assets
        : options?.assets.map((p: any) => {
            return p;
          }),
      expression: options?.expression,
      asset_bidx: !options?.assetBandIndices
        ? options?.assetBandIndices
        : options?.assetBandIndices.map((p: any) => {
            return p;
          }),
      asset_as_band: options?.assetAsBand,
      nodata: options?.noData,
      unscale: options?.unscale,
      reproject: options?.reproject,
    },
    {
      allowReserved: options?.requestOptions?.skipUrlEncoding,
    },
  );
  return context.path(path).get({
    ...operationOptionsToRequestParameters(options),
    headers: { accept: "application/xml", ...options.requestOptions?.headers },
  });
}

export async function _getSearchWmtsCapabilitiesDeserialize(
  result: PathUncheckedResponse,
): Promise<DataGetSearchWmtsCapabilitiesResponse> {
  const expectedStatuses = ["200"];
  if (!expectedStatuses.includes(result.status)) {
    throw createRestError(result);
  }

  return {
    body: typeof result.body === "string" ? stringToUint8Array(result.body, "base64") : result.body,
  };
}

/** OGC WMTS endpoint. */
export async function getSearchWmtsCapabilities(
  context: Client,
  searchId: string,
  options: DataGetSearchWmtsCapabilitiesOptionalParams = { requestOptions: {} },
): Promise<DataGetSearchWmtsCapabilitiesResponse> {
  const result = await _getSearchWmtsCapabilitiesSend(context, searchId, options);
  return _getSearchWmtsCapabilitiesDeserialize(result);
}


// ... (intervening non-WMTS operations elided) ...

export function _getSearchWmtsCapabilitiesByTmsSend(
  context: Client,
  searchId: string,
  tileMatrixSetId: string,
  options: DataGetSearchWmtsCapabilitiesByTmsOptionalParams = { requestOptions: {} },
): StreamableMethod {
  const path = expandUrlTemplate(
    "/data/mosaic/searches/{searchId}/{tileMatrixSetId}/WMTSCapabilities.xml{?api%2Dversion,tile_format,tile_scale,minzoom,maxzoom,bidx*,assets*,expression,asset_bidx,asset_as_band,nodata,unscale,reproject}",
    {
      searchId: searchId,
      tileMatrixSetId: tileMatrixSetId,
      "api%2Dversion": context.apiVersion ?? "2026-04-15",
      tile_format: options?.tileFormat,
      tile_scale: options?.tileScale,
      minzoom: options?.minZoom,
      maxzoom: options?.maxZoom,
      bidx: !options?.bidx
        ? options?.bidx
        : options?.bidx.map((p: any) => {
            return p;
          }),
      assets: !options?.assets
        ? options?.assets
        : options?.assets.map((p: any) => {
            return p;
          }),
      expression: options?.expression,
      asset_bidx: !options?.assetBandIndices
        ? options?.assetBandIndices
        : options?.assetBandIndices.map((p: any) => {
            return p;
          }),
      asset_as_band: options?.assetAsBand,
      nodata: options?.noData,
      unscale: options?.unscale,
      reproject: options?.reproject,
    },
    {
      allowReserved: options?.requestOptions?.skipUrlEncoding,
    },
  );
  return context.path(path).get({
    ...operationOptionsToRequestParameters(options),
    headers: { accept: "application/xml", ...options.requestOptions?.headers },
  });
}

export async function _getSearchWmtsCapabilitiesByTmsDeserialize(
  result: PathUncheckedResponse,
): Promise<DataGetSearchWmtsCapabilitiesByTmsResponse> {
  const expectedStatuses = ["200"];
  if (!expectedStatuses.includes(result.status)) {
    throw createRestError(result);
  }

  return {
    body: typeof result.body === "string" ? stringToUint8Array(result.body, "base64") : result.body,
  };
}

/** OGC WMTS endpoint with TileMatrixSetId as path. */
export async function getSearchWmtsCapabilitiesByTms(
  context: Client,
  searchId: string,
  tileMatrixSetId: string,
  options: DataGetSearchWmtsCapabilitiesByTmsOptionalParams = { requestOptions: {} },
): Promise<DataGetSearchWmtsCapabilitiesByTmsResponse> {
  const result = await _getSearchWmtsCapabilitiesByTmsSend(
    context,
    searchId,
    tileMatrixSetId,
    options,
  );
  return _getSearchWmtsCapabilitiesByTmsDeserialize(result);
}


// ... (intervening non-WMTS operations elided) ...

export function _getCollectionWmtsCapabilitiesByTmsSend(
  context: Client,
  collectionId: string,
  tileMatrixSetId: string,
  options: DataGetCollectionWmtsCapabilitiesByTmsOptionalParams = { requestOptions: {} },
): StreamableMethod {
  const path = expandUrlTemplate(
    "/data/mosaic/collections/{collectionId}/{tileMatrixSetId}/WMTSCapabilities.xml{?api%2Dversion,ids,bbox,query,sortby,datetime,tile_format,tile_scale,minzoom,maxzoom,bidx*,assets*,expression,asset_bidx,asset_as_band,nodata,unscale,reproject}",
    {
      collectionId: collectionId,
      tileMatrixSetId: tileMatrixSetId,
      "api%2Dversion": context.apiVersion ?? "2026-04-15",
      ids: options?.ids,
      bbox: options?.bbox,
      query: options?.query,
      sortby: options?.sortby,
      datetime: options?.datetime,
      tile_format: options?.tileFormat,
      tile_scale: options?.tileScale,
      minzoom: options?.minZoom,
      maxzoom: options?.maxZoom,
      bidx: !options?.bidx
        ? options?.bidx
        : options?.bidx.map((p: any) => {
            return p;
          }),
      assets: !options?.assets
        ? options?.assets
        : options?.assets.map((p: any) => {
            return p;
          }),
      expression: options?.expression,
      asset_bidx: !options?.assetBandIndices
        ? options?.assetBandIndices
        : options?.assetBandIndices.map((p: any) => {
            return p;
          }),
      asset_as_band: options?.assetAsBand,
      nodata: options?.noData,
      unscale: options?.unscale,
      reproject: options?.reproject,
    },
    {
      allowReserved: options?.requestOptions?.skipUrlEncoding,
    },
  );
  return context.path(path).get({
    ...operationOptionsToRequestParameters(options),
    headers: { accept: "application/xml", ...options.requestOptions?.headers },
  });
}

export async function _getCollectionWmtsCapabilitiesByTmsDeserialize(
  result: PathUncheckedResponse,
): Promise<DataGetCollectionWmtsCapabilitiesByTmsResponse> {
  const expectedStatuses = ["200"];
  if (!expectedStatuses.includes(result.status)) {
    throw createRestError(result);
  }

  return {
    body: typeof result.body === "string" ? stringToUint8Array(result.body, "base64") : result.body,
  };
}

/** OGC WMTS endpoint for a STAC collection with TileMatrixSetId as path. */
export async function getCollectionWmtsCapabilitiesByTms(
  context: Client,
  collectionId: string,
  tileMatrixSetId: string,
  options: DataGetCollectionWmtsCapabilitiesByTmsOptionalParams = { requestOptions: {} },
): Promise<DataGetCollectionWmtsCapabilitiesByTmsResponse> {
  const result = await _getCollectionWmtsCapabilitiesByTmsSend(
    context,
    collectionId,
    tileMatrixSetId,
    options,
  );
  return _getCollectionWmtsCapabilitiesByTmsDeserialize(result);
}

export function _getCollectionWmtsCapabilitiesSend(
  context: Client,
  collectionId: string,
  options: DataGetCollectionWmtsCapabilitiesOptionalParams = { requestOptions: {} },
): StreamableMethod {
  const path = expandUrlTemplate(
    "/data/mosaic/collections/{collectionId}/WMTSCapabilities.xml{?api%2Dversion,ids,bbox,query,sortby,datetime,TileMatrixSetId,tile_format,tile_scale,minzoom,maxzoom,bidx*,assets*,expression,asset_bidx,asset_as_band,nodata,unscale,reproject}",
    {
      collectionId: collectionId,
      "api%2Dversion": context.apiVersion ?? "2026-04-15",
      ids: options?.ids,
      bbox: options?.bbox,
      query: options?.query,
      sortby: options?.sortby,
      datetime: options?.datetime,
      TileMatrixSetId: options?.tileMatrixSetId,
      tile_format: options?.tileFormat,
      tile_scale: options?.tileScale,
      minzoom: options?.minZoom,
      maxzoom: options?.maxZoom,
      bidx: !options?.bidx
        ? options?.bidx
        : options?.bidx.map((p: any) => {
            return p;
          }),
      assets: !options?.assets
        ? options?.assets
        : options?.assets.map((p: any) => {
            return p;
          }),
      expression: options?.expression,
      asset_bidx: !options?.assetBandIndices
        ? options?.assetBandIndices
        : options?.assetBandIndices.map((p: any) => {
            return p;
          }),
      asset_as_band: options?.assetAsBand,
      nodata: options?.noData,
      unscale: options?.unscale,
      reproject: options?.reproject,
    },
    {
      allowReserved: options?.requestOptions?.skipUrlEncoding,
    },
  );
  return context.path(path).get({
    ...operationOptionsToRequestParameters(options),
    headers: { accept: "application/xml", ...options.requestOptions?.headers },
  });
}

export async function _getCollectionWmtsCapabilitiesDeserialize(
  result: PathUncheckedResponse,
): Promise<DataGetCollectionWmtsCapabilitiesResponse> {
  const expectedStatuses = ["200"];
  if (!expectedStatuses.includes(result.status)) {
    throw createRestError(result);
  }

  return {
    body: typeof result.body === "string" ? stringToUint8Array(result.body, "base64") : result.body,
  };
}

/** OGC WMTS endpoint for a STAC collection. */
export async function getCollectionWmtsCapabilities(
  context: Client,
  collectionId: string,
  options: DataGetCollectionWmtsCapabilitiesOptionalParams = { requestOptions: {} },
): Promise<DataGetCollectionWmtsCapabilitiesResponse> {
  const result = await _getCollectionWmtsCapabilitiesSend(context, collectionId, options);
  return _getCollectionWmtsCapabilitiesDeserialize(result);
}


// ... (intervening non-WMTS operations elided) ...

export function _getItemWmtsCapabilitiesByTmsSend(
  context: Client,
  collectionId: string,
  itemId: string,
  tileMatrixSetId: string,
  options: DataGetItemWmtsCapabilitiesByTmsOptionalParams = { requestOptions: {} },
): StreamableMethod {
  const path = expandUrlTemplate(
    "/data/mosaic/collections/{collectionId}/items/{itemId}/{tileMatrixSetId}/WMTSCapabilities.xml{?api%2Dversion,bidx*,assets*,expression,asset_bidx,asset_as_band,nodata,unscale,reproject,algorithm,algorithm_params,tile_format,tile_scale,minzoom,maxzoom,buffer,color_formula,resampling,rescale*,colormap_name,colormap,return_mask,padding,subdataset_name,subdataset_bands,crs,datetime,sel*,sel_method}",
    {
      collectionId: collectionId,
      itemId: itemId,
      tileMatrixSetId: tileMatrixSetId,
      "api%2Dversion": context.apiVersion ?? "2026-04-15",
      bidx: !options?.bidx
        ? options?.bidx
        : options?.bidx.map((p: any) => {
            return p;
          }),
      assets: !options?.assets
        ? options?.assets
        : options?.assets.map((p: any) => {
            return p;
          }),
      expression: options?.expression,
      asset_bidx: !options?.assetBandIndices
        ? options?.assetBandIndices
        : options?.assetBandIndices.map((p: any) => {
            return p;
          }),
      asset_as_band: options?.assetAsBand,
      nodata: options?.noData,
      unscale: options?.unscale,
      reproject: options?.reproject,
      algorithm: options?.algorithm,
      algorithm_params: options?.algorithmParams,
      tile_format: options?.tileFormat,
      tile_scale: options?.tileScale,
      minzoom: options?.minZoom,
      maxzoom: options?.maxZoom,
      buffer: options?.buffer,
      color_formula: options?.colorFormula,
      resampling: options?.resampling,
      rescale: !options?.rescale
        ? options?.rescale
        : options?.rescale.map((p: any) => {
            return p;
          }),
      colormap_name: options?.colorMapName,
      colormap: options?.colorMap,
      return_mask: options?.returnMask,
      padding: options?.padding,
      subdataset_name: options?.subdatasetName,
      subdataset_bands: !options?.subdatasetBands
        ? options?.subdatasetBands
        : options?.subdatasetBands.map((p: any) => {
            return p;
          }),
      crs: options?.crs,
      datetime: options?.datetime,
      sel: !options?.sel
        ? options?.sel
        : options?.sel.map((p: any) => {
            return p;
          }),
      sel_method: options?.selMethod,
    },
    {
      allowReserved: options?.requestOptions?.skipUrlEncoding,
    },
  );
  return context.path(path).get({
    ...operationOptionsToRequestParameters(options),
    headers: { accept: "application/xml", ...options.requestOptions?.headers },
  });
}

export async function _getItemWmtsCapabilitiesByTmsDeserialize(
  result: PathUncheckedResponse,
): Promise<DataGetItemWmtsCapabilitiesByTmsResponse> {
  const expectedStatuses = ["200"];
  if (!expectedStatuses.includes(result.status)) {
    throw createRestError(result);
  }

  return {
    body: typeof result.body === "string" ? stringToUint8Array(result.body, "base64") : result.body,
  };
}

/** OGC WMTS endpoint for a STAC item with TileMatrixSetId as path. */
export async function getItemWmtsCapabilitiesByTms(
  context: Client,
  collectionId: string,
  itemId: string,
  tileMatrixSetId: string,
  options: DataGetItemWmtsCapabilitiesByTmsOptionalParams = { requestOptions: {} },
): Promise<DataGetItemWmtsCapabilitiesByTmsResponse> {
  const result = await _getItemWmtsCapabilitiesByTmsSend(
    context,
    collectionId,
    itemId,
    tileMatrixSetId,
    options,
  );
  return _getItemWmtsCapabilitiesByTmsDeserialize(result);
}

export function _getItemWmtsCapabilitiesSend(
  context: Client,
  collectionId: string,
  itemId: string,
  options: DataGetItemWmtsCapabilitiesOptionalParams = { requestOptions: {} },
): StreamableMethod {
  const path = expandUrlTemplate(
    "/data/mosaic/collections/{collectionId}/items/{itemId}/WMTSCapabilities.xml{?api%2Dversion,bidx*,assets*,expression,asset_bidx,asset_as_band,nodata,unscale,reproject,algorithm,algorithm_params,TileMatrixSetId,tile_format,tile_scale,minzoom,maxzoom,buffer,color_formula,resampling,rescale*,colormap_name,colormap,return_mask,padding,subdataset_name,subdataset_bands,crs,datetime,sel*,sel_method}",
    {
      collectionId: collectionId,
      itemId: itemId,
      "api%2Dversion": context.apiVersion ?? "2026-04-15",
      bidx: !options?.bidx
        ? options?.bidx
        : options?.bidx.map((p: any) => {
            return p;
          }),
      assets: !options?.assets
        ? options?.assets
        : options?.assets.map((p: any) => {
            return p;
          }),
      expression: options?.expression,
      asset_bidx: !options?.assetBandIndices
        ? options?.assetBandIndices
        : options?.assetBandIndices.map((p: any) => {
            return p;
          }),
      asset_as_band: options?.assetAsBand,
      nodata: options?.noData,
      unscale: options?.unscale,
      reproject: options?.reproject,
      algorithm: options?.algorithm,
      algorithm_params: options?.algorithmParams,
      TileMatrixSetId: options?.tileMatrixSetId,
      tile_format: options?.tileFormat,
      tile_scale: options?.tileScale,
      minzoom: options?.minZoom,
      maxzoom: options?.maxZoom,
      buffer: options?.buffer,
      color_formula: options?.colorFormula,
      resampling: options?.resampling,
      rescale: !options?.rescale
        ? options?.rescale
        : options?.rescale.map((p: any) => {
            return p;
          }),
      colormap_name: options?.colorMapName,
      colormap: options?.colorMap,
      return_mask: options?.returnMask,
      padding: options?.padding,
      subdataset_name: options?.subdatasetName,
      subdataset_bands: !options?.subdatasetBands
        ? options?.subdatasetBands
        : options?.subdatasetBands.map((p: any) => {
            return p;
          }),
      crs: options?.crs,
      datetime: options?.datetime,
      sel: !options?.sel
        ? options?.sel
        : options?.sel.map((p: any) => {
            return p;
          }),
      sel_method: options?.selMethod,
    },
    {
      allowReserved: options?.requestOptions?.skipUrlEncoding,
    },
  );
  return context.path(path).get({
    ...operationOptionsToRequestParameters(options),
    headers: { accept: "application/xml", ...options.requestOptions?.headers },
  });
}

export async function _getItemWmtsCapabilitiesDeserialize(
  result: PathUncheckedResponse,
): Promise<DataGetItemWmtsCapabilitiesResponse> {
  const expectedStatuses = ["200"];
  if (!expectedStatuses.includes(result.status)) {
    throw createRestError(result);
  }

  return {
    body: typeof result.body === "string" ? stringToUint8Array(result.body, "base64") : result.body,
  };
}

/** OGC WMTS endpoint for a STAC item. */
export async function getItemWmtsCapabilities(
  context: Client,
  collectionId: string,
  itemId: string,
  options: DataGetItemWmtsCapabilitiesOptionalParams = { requestOptions: {} },
): Promise<DataGetItemWmtsCapabilitiesResponse> {
  const result = await _getItemWmtsCapabilitiesSend(context, collectionId, itemId, options);
  return _getItemWmtsCapabilitiesDeserialize(result);
}

