/*
 * API contract types generated from the backend OpenAPI document.
 * Regenerate with `npm run api:types` while the API is running; never edit generated/ by hand.
 */
import type { components } from './generated/api';

export type { components, operations, paths } from './generated/api';

type Schemas = components['schemas'];

export type HealthResponse = Schemas['HealthResponse'];
export type ReadinessResponse = Schemas['ReadinessResponse'];
export type DependencyHealth = Schemas['DependencyHealth'];
export type CsrfResponse = Schemas['CsrfResponse'];
export type UserResponse = Schemas['UserResponse'];
export type RegisterRequest = Schemas['RegisterRequest'];
export type LoginRequest = Schemas['LoginRequest'];
export type EmailOnlyRequest = Schemas['EmailOnlyRequest'];
export type ConfirmTokenRequest = Schemas['ConfirmTokenRequest'];
export type ResetPasswordRequest = Schemas['ResetPasswordRequest'];
export type UpdateProfileRequest = Schemas['UpdateProfileRequest'];
export type ChangePasswordRequest = Schemas['ChangePasswordRequest'];
export type CategoryNode = Schemas['CategoryNode'];
export type CategoryResponse = Schemas['CategoryResponse'];
export type CategoryWriteRequest = Schemas['CategoryWriteRequest'];
export type CategoryUpdateRequest = Schemas['CategoryUpdateRequest'];
export type CollectionResponse = Schemas['CollectionResponse'];
export type CollectionWriteRequest = Schemas['CollectionWriteRequest'];
export type CollectionUpdateRequest = Schemas['CollectionUpdateRequest'];
export type ProductPage = Schemas['ProductPage'];
export type PublicProductDetail = Schemas['PublicProductDetail'];
export type ProductResponse = Schemas['ProductResponse'];
export type ProductWriteRequest = Schemas['ProductWriteRequest'];
export type ProductUpdateRequest = Schemas['ProductUpdateRequest'];
export type AdminProductPage = Schemas['AdminProductPage'];
export type OptionResponse = Schemas['OptionResponse'];
export type OptionWriteRequest = Schemas['OptionWriteRequest'];
export type OptionValueResponse = Schemas['OptionValueResponse'];
export type OptionValueWriteRequest = Schemas['OptionValueWriteRequest'];
export type VariantResponse = Schemas['VariantResponse'];
export type VariantWriteRequest = Schemas['VariantWriteRequest'];
export type VariantUpdateRequest = Schemas['VariantUpdateRequest'];
export type MediaResponse = Schemas['MediaResponse'];
export type MediaUpdateRequest = Schemas['MediaUpdateRequest'];
export type InventoryPage = Schemas['InventoryPage'];
export type MovementResponse = Schemas['MovementResponse'];
export type StockAdjustmentRequest = Schemas['StockAdjustmentRequest'];
